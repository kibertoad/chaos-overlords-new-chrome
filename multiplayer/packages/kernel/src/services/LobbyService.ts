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
import { FIRST_TURN, type TurnService } from './TurnService'

const JOIN_CODE_LENGTH = LIMITS.joinCodeLength
const MIN_PLAYERS_TO_START = LIMITS.minPlayers
/** Attempts at an unused join code. The space is ~40 bits, so a second attempt is already rare. */
const JOIN_CODE_ATTEMPTS = 5
/** The host always holds the first position in the join sequence. */
const HOST_JOIN_ORDER = 0

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
    const passwordHash = request.password ? await hashPassword(request.password) : null
    const match = await this.createWithFreshJoinCode({
      id: matchId,
      status: 'lobby',
      settings: request.settings,
      hostPlayerId: hostId,
      joinCode: '',
      passwordHash,
      seed: null,
      currentTurn: 0,
      seatCount: 1,
      joinCounter: 1,
      createdAt: now,
      updatedAt: now,
    })
    const host: Player = {
      id: hostId,
      matchId,
      slot: -1,
      joinOrder: HOST_JOIN_ORDER,
      displayName: request.hostDisplayName,
      tokenHash: await hashToken(token),
      status: 'active',
      joinedAt: now,
    }
    // The match was inserted a statement ago and is in the lobby, so this cannot legitimately fail;
    // treating it as a conflict rather than ignoring it keeps the host's token from being handed out
    // for a row that does not exist.
    if (!(await this.deps.storage.players.create(host))) {
      throw new ConflictError('The match could not be opened; retry', { reason: 'match_not_open' })
    }
    await this.publisher.publish(matchId, {
      type: 'lobby.playerJoined',
      payload: { player: toPlayerView(host, hostId) },
    })
    return this.membership(match, host, token)
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
    const joinOrder = await this.deps.storage.matches.claimSeat(match.id)
    if (joinOrder === null) {
      throw new ConflictError('The match is full or no longer joinable', { reason: 'match_full' })
    }
    const token = generateToken()
    const player: Player = {
      id: this.newId(),
      matchId: match.id,
      slot: -1,
      joinOrder,
      displayName: request.displayName,
      tokenHash: await hashToken(token),
      status: 'active',
      joinedAt: this.deps.clock.now(),
    }
    // Everything after the seat is claimed has to give it back on failure, or capacity drifts and a
    // phantom member keeps the turn barrier waiting for a player nobody can authenticate as. The
    // insert is itself conditional on the match still being in the lobby: the host may have pressed
    // start between the claim and here, and an unseated player in a running match would wedge it.
    try {
      const seated = await this.deps.storage.players.create(player)
      if (!seated) {
        throw new ConflictError('The match started while you were joining', {
          reason: 'match_not_joinable',
        })
      }
      const membership = await this.membership(match, player, token)
      await this.publisher.publish(match.id, {
        type: 'lobby.playerJoined',
        payload: { player: toPlayerView(player, match.hostPlayerId) },
      })
      return membership
    } catch (error) {
      await this.rollbackJoin(match.id, player.id)
      throw error
    }
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
    await this.turns.openTurn({ ...match, status: 'running', seed }, FIRST_TURN)
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
    // Membership is the only thing the token ever proved, so it stops working here: a kicked player
    // keeps neither the event stream nor the sealed order sets of the turns that follow.
    await this.deps.storage.players.revokeToken(target.id)
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

  /**
   * Undo a join that could not be completed. The token was never returned, so the row is
   * unreachable: leaving it would hold a seat and keep `allActiveReady` waiting forever on a player
   * who does not exist. Deleting the row is safe precisely because nobody ever held its token.
   */
  private async rollbackJoin(matchId: string, playerId: string): Promise<void> {
    try {
      await this.deps.storage.players.delete(playerId)
      await this.deps.storage.matches.releaseSeat(matchId)
    } catch (error) {
      this.deps.logger.error('could not roll back an incomplete join', {
        matchId,
        playerId,
        error: String(error),
      })
    }
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

  /**
   * Inserts the match with a join code no other match holds. The uniqueness is the database's to
   * enforce, not ours to check first: a `getByJoinCode` probe before the insert would be a race, so
   * a refused insert is what drives the retry.
   */
  private async createWithFreshJoinCode(draft: Match): Promise<Match> {
    for (let attempt = 0; attempt < JOIN_CODE_ATTEMPTS; attempt += 1) {
      const match: Match = { ...draft, joinCode: generateJoinCode(JOIN_CODE_LENGTH) }
      if (await this.deps.storage.matches.create(match)) return match
    }
    throw new ConflictError('Could not allocate a join code; retry', {
      reason: 'join_code_exhausted',
    })
  }

  private async membership(match: Match, player: Player, token: string): Promise<MembershipView> {
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
