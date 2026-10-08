import {
  LIMITS,
  type MatchEventType,
  type MatchSettings,
  type PlayerView,
  type SealedOrdersView,
  type SnapshotView,
  type SpectateRequest,
  type SpectatorEventPage,
  type SpectatorList,
  type SpectatorMatchView,
  type SpectatorMembership,
  type SpectatorView,
} from '@chaos-overlords/contracts'
import type { Match, PersistedEvent, Spectator } from '../domain/entities'
import { ConflictError, ForbiddenError, NotFoundError, UnauthorizedError } from '../domain/errors'
import { generateSpectatorToken, hashToken, SPECTATOR_TOKEN_PREFIX } from '../logic/crypto'
import { spectatorDelay, startReleased } from '../logic/spectating'
import { sealAnnouncementKey } from '../logic/turn-logic'
import type { Principal } from './AuthService'
import type { KernelDeps } from './deps'
import type { EventPublisher } from './EventPublisher'
import type { LobbyService } from './LobbyService'
import { MatchQueryService, toPlayerView } from './MatchQueryService'
import { toSnapshotView } from './SnapshotService'
import { releasedTurnOf } from './spectatorRelease'

/** A spectator token resolved: who is watching, and what. */
export interface SpectatorPrincipal {
  spectator: Spectator
  match: Match
}

/**
 * The events a spectator reads: the ones a rebuild from sealed sets needs to know who controlled
 * each seat on each turn. Everything else in the log (readiness, deadlines, reports, desync
 * verdicts, votes, chat, spectators) is the players' and stays with them.
 */
const SPECTATOR_EVENT_TYPES: ReadonlySet<MatchEventType> = new Set<MatchEventType>([
  'match.started',
  'match.playerTakenOver',
  'match.playerReturned',
  'match.latePlayerJoined',
  'turn.opened',
  'turn.sealed',
])

/** Events one spectator read examines at most, whatever it returns; the rest wait for the next. */
const SPECTATOR_EVENT_SCAN = 1000

/**
 * Watching a match from behind its delay.
 *
 * Every read goes through `released`, which is the server's answer to how far a spectator may see.
 * Nothing here writes to a match except the two announcements players are owed (who arrived, who
 * left), and nothing here is reachable with a player token or reaches a player route with a
 * spectator token: the doors are kept apart in `authenticate` and in the HTTP mount.
 */
export class SpectatorService {
  private readonly query: MatchQueryService
  private readonly newId: () => string

  constructor(
    private readonly deps: KernelDeps,
    private readonly publisher: EventPublisher,
    private readonly lobby: LobbyService,
    options: { newId?: () => string } = {},
  ) {
    this.query = new MatchQueryService(deps.storage)
    this.newId = options.newId ?? (() => crypto.randomUUID())
  }

  /** Resolves a spectator token. Never reveals which half failed, like the player door. */
  async authenticate(token: string): Promise<SpectatorPrincipal> {
    if (!token.startsWith(SPECTATOR_TOKEN_PREFIX)) throw invalidToken()
    const spectator = await this.deps.storage.spectators.getByTokenHash(await hashToken(token))
    if (!spectator) throw invalidToken()
    const match = await this.deps.storage.matches.get(spectator.matchId)
    if (!match) throw invalidToken()
    return { spectator, match }
  }

  /**
   * Start watching a match by its join code.
   *
   * The code is the same gate the seat doors use, and so is the password with its budgets. A code
   * naming no match and a code naming a match that cannot be watched answer differently on
   * purpose: the second is a fact the public listing already shows, and a player told to watch
   * a match that does not allow it deserves to be told so.
   */
  async join(request: SpectateRequest, caller?: string): Promise<SpectatorMembership> {
    const match = await this.deps.storage.matches.getByJoinCode(request.joinCode)
    if (!match) {
      throw new NotFoundError('No match with that join code', { reason: 'unknown_join_code' })
    }
    requireSpectating(match)
    await this.lobby.verifyMatchPassword(match, request.password, caller)
    const token = generateSpectatorToken()
    const spectator: Spectator = {
      id: this.newId(),
      matchId: match.id,
      displayName: request.displayName,
      tokenHash: await hashToken(token),
      joinedAt: this.deps.clock.now(),
      leftAt: null,
    }
    if (!(await this.deps.storage.spectators.create(spectator, LIMITS.spectatorsPerMatch))) {
      throw new ConflictError('This match has admitted as many spectators as it can', {
        reason: 'spectators_full',
      })
    }
    await this.publisher.publish(match.id, {
      type: 'spectator.joined',
      payload: { spectator: toSpectatorView(spectator) },
    })
    return {
      match: await this.view({ spectator, match }),
      spectator: toSpectatorView(spectator),
      token,
    }
  }

  /** The match as a spectator may see it. */
  async view(principal: SpectatorPrincipal): Promise<SpectatorMatchView> {
    const { match } = principal
    const delay = requireSpectating(match)
    const [released, players] = await Promise.all([this.released(match), this.roster(match)])
    return {
      id: match.id,
      protocolVersion: match.protocolVersion,
      sessionVersion: match.sessionVersion,
      // A desync is the players' verdict about a turn the delay has not released.
      status: match.status === 'desynced' ? 'running' : match.status,
      settings: withoutLiveSummaries(match.settings),
      players,
      currentTurn: match.currentTurn,
      delayTurns: delay,
      releasedTurn: released,
      seed: released >= 1 ? match.seed : null,
      createdAt: match.createdAt.toISOString(),
    }
  }

  /**
   * The released events that decide who controls each seat, from `after`.
   *
   * While the match runs the log is read up to and including the seal of the released turn (the
   * start announcement while no turn is released), and nothing after it. Whatever a player's
   * client meets after that seal it applies to the next turn, so a handover logged there, even
   * before that turn's `turn.opened`, is about a turn a spectator may not see yet. Once the match
   * is over the whole log is released. The cursor the answer carries moves past the events left
   * out, so a quiet stretch of readiness and chat is examined once rather than on every poll.
   */
  async events(
    principal: SpectatorPrincipal,
    after: number,
    limit: number,
  ): Promise<SpectatorEventPage> {
    const { match } = principal
    requireSpectating(match)
    const lastReleasedSeq = await this.lastReleasedSeq(match)
    const events: PersistedEvent[] = []
    let cursor = after
    let scanned = 0
    while (events.length < limit && scanned < SPECTATOR_EVENT_SCAN && cursor < lastReleasedSeq) {
      // The sequence is gapless, so a page never needs to reach past the cut.
      const page = await this.deps.storage.events.listAfter(
        match.id,
        cursor,
        Math.min(LIMITS.eventsPageSize, SPECTATOR_EVENT_SCAN - scanned, lastReleasedSeq - cursor),
      )
      if (page.length === 0) break
      for (const event of page) {
        if (event.seq > lastReleasedSeq) break
        scanned++
        cursor = event.seq
        if (SPECTATOR_EVENT_TYPES.has(event.type)) {
          events.push(event)
          if (events.length >= limit) break
        }
      }
      if (page.length < LIMITS.eventsPageSize) break
    }
    return { events, cursor }
  }

  /**
   * The roster a spectator is shown: the one the match started with while it runs, and the live
   * one in the lobby and once it is over.
   *
   * A running match's live roster says who left, who the computer stands in for and who joined
   * late as of the open turn, which is newer than anything released. The released events carry
   * those changes to a spectator on the turn they reach. The start announcement is the roster as
   * every client bootstrapped from it; a start that has not announced itself yet has changed
   * nothing since, so the live roster stands in for it.
   */
  private async roster(match: Match): Promise<PlayerView[]> {
    if (match.status === 'running' || match.status === 'desynced') {
      const started = await this.deps.storage.events.latestOfType(match.id, 'match.started')
      if (started?.type === 'match.started') return started.payload.players
    }
    const players = await this.deps.storage.players.listByMatch(match.id)
    return players.map((player) => toPlayerView(player, match.hostPlayerId))
  }

  /** A released turn's sealed set. */
  async sealedOrders(principal: SpectatorPrincipal, turn: number): Promise<SealedOrdersView> {
    const { match } = principal
    requireSpectating(match)
    await this.requireReleased(match, turn)
    return this.query.sealedOrders(match, turn)
  }

  /** The newest snapshot at or below the released turn: where watching starts. */
  async latestSnapshot(principal: SpectatorPrincipal): Promise<SnapshotView> {
    const { match } = principal
    requireSpectating(match)
    // `released` is 0 both before the start is released and once it is; only the second may read
    // the bootstrap, which until then is the board the players are planning on.
    const summary = startReleased(match)
      ? await this.deps.storage.snapshots.getLatestSummaryAtOrBelow(
          match.id,
          await this.released(match),
        )
      : null
    const snapshot = summary && (await this.deps.storage.snapshots.get(match.id, summary.turn))
    if (!snapshot) {
      throw new NotFoundError('No snapshot has been released yet', { reason: 'no_snapshot' })
    }
    return toSnapshotView(snapshot)
  }

  /** Stop watching. The token stops working; a second call finds nothing to revoke. */
  async leave(principal: SpectatorPrincipal): Promise<void> {
    await this.end(principal.match.id, principal.spectator.id, false)
  }

  /** The spectators watching, for a player of the match. */
  async list(principal: Principal): Promise<SpectatorList> {
    const spectators = await this.deps.storage.spectators.listActive(principal.match.id)
    return { spectators: spectators.map(toSpectatorView) }
  }

  /** The host stops a spectator watching, at any point of the match. */
  async remove(principal: Principal, spectatorId: string): Promise<void> {
    if (principal.player.id !== principal.match.hostPlayerId) {
      throw new ForbiddenError('Only the host can do that', { reason: 'host_only' })
    }
    const spectator = await this.deps.storage.spectators.get(spectatorId)
    if (!spectator || spectator.matchId !== principal.match.id || spectator.leftAt !== null) {
      throw new NotFoundError('No such spectator', { reason: 'unknown_spectator' })
    }
    await this.end(principal.match.id, spectatorId, true)
  }

  private async end(matchId: string, spectatorId: string, removed: boolean): Promise<void> {
    if (!(await this.deps.storage.spectators.revoke(spectatorId, this.deps.clock.now()))) return
    await this.publisher.publish(matchId, {
      type: 'spectator.left',
      payload: { spectatorId, removed },
    })
  }

  /**
   * The sequence number of the last event a spectator may read: the seal of the released turn,
   * or `match.started` while none is. 0 when that announcement is not in the log yet, which
   * releases nothing; unbounded once the match is over.
   */
  private async lastReleasedSeq(match: Match): Promise<number> {
    if (match.status === 'finished' || match.status === 'abandoned') {
      return Number.POSITIVE_INFINITY
    }
    // Before the start is released, even the start announcement is news about the open turn 1.
    if (!startReleased(match)) return 0
    const released = await this.released(match)
    const seq =
      released >= 1
        ? await this.deps.storage.events.seqOfKey(match.id, sealAnnouncementKey(released))
        : ((await this.deps.storage.events.latestOfType(match.id, 'match.started'))?.seq ?? null)
    return seq ?? 0
  }

  private released(match: Match): Promise<number> {
    return releasedTurnOf(this.deps.storage, match)
  }

  private async requireReleased(match: Match, turn: number): Promise<void> {
    const released = await this.released(match)
    if (turn > released) {
      throw new ConflictError('That turn has not been released to spectators', {
        reason: 'turn_not_released',
        releasedTurn: released,
      })
    }
  }
}

/** The delay, or the refusal a match that cannot be watched answers with. */
function requireSpectating(match: Match): number {
  const delay = spectatorDelay(match)
  if (delay === null) {
    throw new ForbiddenError('This match cannot be watched', { reason: 'spectating_disabled' })
  }
  return delay
}

/** The settings without `seatSummaries`, which describe the computer seats as they are now. */
function withoutLiveSummaries(settings: MatchSettings): MatchSettings {
  const { seatSummaries: _seatSummaries, ...gameSettings } = settings.gameSettings
  return { ...settings, gameSettings }
}

export function toSpectatorView(spectator: Spectator): SpectatorView {
  return {
    id: spectator.id,
    displayName: spectator.displayName,
    joinedAt: spectator.joinedAt.toISOString(),
  }
}

function invalidToken(): UnauthorizedError {
  return new UnauthorizedError('Invalid or expired spectator token', { reason: 'invalid_token' })
}
