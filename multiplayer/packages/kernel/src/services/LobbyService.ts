import {
  type CreateMatchRequest,
  type JoinMatchRequest,
  LIMITS,
  type MembershipView,
} from '@chaos-overlords/contracts'
import { activePlayers, type Match, type Player } from '../domain/entities'
import { ConflictError, ForbiddenError, NotFoundError, UnauthorizedError } from '../domain/errors'
import { generateJoinCode, generateSeed, generateToken, hashToken } from '../logic/crypto'
import { hashPassword, verifyPassword } from '../logic/password'
import { assignSlots } from '../logic/turn-logic'
import type { Principal } from './AuthService'
import type { KernelDeps } from './deps'
import type { EventPublisher } from './EventPublisher'
import { MatchQueryService, toPlayerView } from './MatchQueryService'
import type { TurnService } from './TurnService'

const JOIN_CODE_LENGTH = LIMITS.joinCodeLength
const MIN_PLAYERS_TO_START = LIMITS.minPlayers
const JOIN_CODE_ATTEMPTS = 5

export interface LobbyServiceOptions {
  /** Generates the player/match ids; defaults to `crypto.randomUUID`. */
  newId?: () => string
}

/** Match creation, joining, leaving, kicking and the start transition. */
export class LobbyService {
  private readonly query: MatchQueryService
  private readonly newId: () => string

  constructor(
    private readonly deps: KernelDeps,
    private readonly publisher: EventPublisher,
    private readonly turns: TurnService,
    options: LobbyServiceOptions = {},
  ) {
    this.query = new MatchQueryService(deps.storage)
    this.newId = options.newId ?? (() => crypto.randomUUID())
  }

  async createMatch(request: CreateMatchRequest): Promise<MembershipView> {
    const now = this.deps.clock.now()
    const matchId = this.newId()
    const hostId = this.newId()
    const token = generateToken()
    const match: Match = {
      id: matchId,
      status: 'lobby',
      settings: request.settings,
      hostPlayerId: hostId,
      joinCode: await this.freshJoinCode(),
      passwordHash: request.password ? await hashPassword(request.password) : null,
      seed: null,
      currentTurn: 0,
      seatCount: 1,
      eventSeq: 0,
      createdAt: now,
      updatedAt: now,
    }
    const host: Player = {
      id: hostId,
      matchId,
      slot: -1,
      displayName: request.hostDisplayName,
      tokenHash: await hashToken(token),
      status: 'active',
      joinedAt: now,
    }
    await this.deps.storage.matches.create(match)
    await this.deps.storage.players.create(host)
    await this.publisher.publish(matchId, {
      type: 'lobby.playerJoined',
      payload: { player: toPlayerView(host, hostId) },
    })
    return this.membership(matchId, host, token)
  }

  async join(request: JoinMatchRequest): Promise<MembershipView> {
    const match = await this.deps.storage.matches.getByJoinCode(request.joinCode)
    if (!match)
      throw new NotFoundError('No match with that join code', { reason: 'unknown_join_code' })
    if (match.status !== 'lobby') {
      throw new ConflictError('The match has already started', { reason: 'match_not_joinable' })
    }
    if (match.passwordHash !== null) {
      if (!request.password) {
        throw new UnauthorizedError('This match needs a password', { reason: 'password_required' })
      }
      if (!(await verifyPassword(request.password, match.passwordHash))) {
        throw new UnauthorizedError('Wrong password', { reason: 'wrong_password' })
      }
    }
    if (!(await this.deps.storage.matches.claimSeat(match.id))) {
      throw new ConflictError('The match is full or no longer joinable', { reason: 'match_full' })
    }
    const token = generateToken()
    const player: Player = {
      id: this.newId(),
      matchId: match.id,
      slot: -1,
      displayName: request.displayName,
      tokenHash: await hashToken(token),
      status: 'active',
      joinedAt: this.deps.clock.now(),
    }
    await this.deps.storage.players.create(player)
    await this.publisher.publish(match.id, {
      type: 'lobby.playerJoined',
      payload: { player: toPlayerView(player, match.hostPlayerId) },
    })
    return this.membership(match.id, player, token)
  }

  async leave(principal: Principal): Promise<void> {
    await this.remove(principal.match, principal.player, 'left')
  }

  async kick(principal: Principal, targetPlayerId: string): Promise<void> {
    const { match, player } = principal
    requireHost(principal)
    if (targetPlayerId === player.id) {
      throw new ConflictError('Leave the match instead of kicking yourself', {
        reason: 'self_kick',
      })
    }
    const target = await this.deps.storage.players.get(targetPlayerId)
    if (!target || target.matchId !== match.id) {
      throw new NotFoundError('No such player in this match', { reason: 'unknown_player' })
    }
    await this.remove(match, target, 'kicked')
  }

  async start(principal: Principal): Promise<void> {
    const { match } = principal
    requireHost(principal)
    if (match.status !== 'lobby') {
      throw new ConflictError('The match has already started', { reason: 'match_not_in_lobby' })
    }
    const roster = activePlayers(await this.deps.storage.players.listByMatch(match.id))
    if (roster.length < MIN_PLAYERS_TO_START) {
      throw new ConflictError(`At least ${MIN_PLAYERS_TO_START} players are needed`, {
        reason: 'not_enough_players',
      })
    }
    const seed = generateSeed()
    const started = await this.deps.storage.matches.transition(match.id, ['lobby'], {
      status: 'running',
      seed,
      currentTurn: 0,
      updatedAt: this.deps.clock.now(),
    })
    if (!started) {
      throw new ConflictError('The match has already started', { reason: 'match_not_in_lobby' })
    }
    // Seats can no longer be claimed, so the roster is final from here.
    const finalRoster = await this.deps.storage.players.listByMatch(match.id)
    await this.deps.storage.players.assignSlots(assignSlots(finalRoster, match.hostPlayerId))
    const seated = await this.deps.storage.players.listByMatch(match.id)
    await this.publisher.publish(match.id, {
      type: 'match.started',
      payload: {
        seed,
        players: activePlayers(seated).map((player) => toPlayerView(player, match.hostPlayerId)),
      },
    })
    await this.turns.openTurn({ ...match, status: 'running', seed }, 1)
  }

  private async remove(match: Match, target: Player, reason: 'left' | 'kicked'): Promise<void> {
    if (target.status !== 'active') return
    const now = this.deps.clock.now()
    if (match.status === 'lobby') {
      if (target.id === match.hostPlayerId) {
        await this.abandon(match, now)
        return
      }
      await this.deps.storage.players.delete(target.id)
      await this.deps.storage.matches.releaseSeat(match.id)
      await this.publisher.publish(match.id, {
        type: 'lobby.playerLeft',
        payload: { playerId: target.id, reason },
      })
      return
    }
    await this.deps.storage.players.setStatus(target.id, reason)
    await this.publisher.publish(match.id, {
      type: 'lobby.playerLeft',
      payload: { playerId: target.id, reason },
    })
    const remaining = activePlayers(await this.deps.storage.players.listByMatch(match.id))
    if (remaining.length === 0) {
      await this.abandon(match, now)
      return
    }
    if (target.id === match.hostPlayerId) {
      const successor = remaining[0] as Player
      await this.deps.storage.matches.transition(match.id, ['running', 'desynced'], {
        hostPlayerId: successor.id,
        updatedAt: now,
      })
      await this.publisher.publish(match.id, {
        type: 'lobby.hostChanged',
        payload: { hostPlayerId: successor.id },
      })
    }
    // A departure can complete readiness or a consensus that was waiting on the leaver.
    await this.turns.reevaluate(match.id)
  }

  private async abandon(match: Match, now: Date): Promise<void> {
    const changed = await this.deps.storage.matches.transition(
      match.id,
      ['lobby', 'running', 'desynced'],
      { status: 'abandoned', updatedAt: now },
    )
    if (changed) {
      await this.publisher.publish(match.id, {
        type: 'match.statusChanged',
        payload: { status: 'abandoned' },
      })
    }
  }

  private async freshJoinCode(): Promise<string> {
    for (let attempt = 0; attempt < JOIN_CODE_ATTEMPTS; attempt += 1) {
      const code = generateJoinCode(JOIN_CODE_LENGTH)
      if (!(await this.deps.storage.matches.getByJoinCode(code))) return code
    }
    throw new ConflictError('Could not allocate a join code; retry', {
      reason: 'join_code_exhausted',
    })
  }

  private async membership(
    matchId: string,
    player: Player,
    token: string,
  ): Promise<MembershipView> {
    const match = await this.deps.storage.matches.get(matchId)
    if (!match) throw new NotFoundError('Match vanished during join', { reason: 'unknown_match' })
    return {
      match: await this.query.view(match),
      player: toPlayerView(player, match.hostPlayerId),
      token,
      joinCode: match.joinCode,
    }
  }
}

function requireHost(principal: Principal): void {
  if (principal.player.id !== principal.match.hostPlayerId) {
    throw new ForbiddenError('Only the host can do that', { reason: 'host_only' })
  }
}
