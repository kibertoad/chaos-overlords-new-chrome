import {
  GAME_BOUNDS,
  MULTIPLAYER_PROTOCOL_VERSION,
  type LobbyListing,
  type MatchEventBody,
  type MatchView,
  type OwnSubmissionView,
  type PlayerView,
  type SealedOrdersView,
  type SealedPlayerOrders,
  type TurnView,
} from '@chaos-overlords/contracts'
import { activePlayers, type Match, type Player, type Turn } from '../domain/entities'
import { ConflictError, NotFoundError } from '../domain/errors'
import { sha256Hex } from '../logic/crypto'
import type { MultiplayerStorage } from '../ports/storage'
import { requireTurn } from './guards'

export function toPlayerView(player: Player, hostPlayerId: string): PlayerView {
  return {
    id: player.id,
    slot: player.slot,
    displayName: player.displayName,
    portraitId: player.portraitId,
    status: player.status,
    isHost: player.id === hostPlayerId,
  }
}

/** The `match.started` announcement: the seed and the seated roster every client bootstraps from. */
export function matchStartedEvent(
  seed: number,
  seated: readonly Player[],
  hostPlayerId: string,
): MatchEventBody {
  return {
    type: 'match.started',
    payload: {
      seed,
      players: activePlayers(seated).map((player) => toPlayerView(player, hostPlayerId)),
    },
  }
}

/** How long a lobby tag may stand without a full read; see `MatchQueryService.lobbyTag`. */
export const LOBBY_TAG_WINDOW_MS = 30_000

/** Read models. Every view is assembled from list reads, never one query per player. */
export class MatchQueryService {
  constructor(private readonly storage: MultiplayerStorage) {}

  async view(match: Match): Promise<MatchView> {
    const players = await this.storage.players.listByMatch(match.id)
    const [turn, previousTurn, lastEventSeq] = await Promise.all([
      this.turnView(match.id, match.currentTurn),
      this.turnView(match.id, match.currentTurn - 1),
      this.storage.events.lastSeq(match.id),
    ])
    return {
      id: match.id,
      protocolVersion: match.protocolVersion,
      sessionVersion: match.sessionVersion,
      status: match.status,
      settings: match.settings,
      hostPlayerId: match.hostPlayerId,
      seed: match.seed,
      currentTurn: match.currentTurn,
      players: players.map((player) => toPlayerView(player, match.hostPlayerId)),
      turn,
      previousTurn,
      lastEventSeq,
      createdAt: match.createdAt.toISOString(),
    }
  }

  /**
   * The entity tag of a member's read of a lobby, or null when the match has left the lobby.
   *
   * A lobby is polled once a second by every seated player, and an unchanged poll should cost the
   * auth lookup and one `lastSeq` read rather than the roster as well. The tag is therefore built
   * from what the caller already holds and that one read, and it has to move whenever anything the
   * view shows could have moved:
   *
   * - the match row, whole (status, settings, host, seat and join counters, `updatedAt`, join code).
   *   The auth lookup read it, so it costs nothing, and it covers a settings change, which publishes
   *   no event, and a join rolled back after its seat was claimed, which publishes none either but
   *   gives the seat back;
   * - the last event sequence. Every change to a lobby's roster (a join, a leave, a kick, a new name
   *   or face, a new host) publishes an event after its write, so a read between the write and the
   *   event is tagged with the older sequence, and the next poll after the event reads again;
   * - the caller, because the detail names them (`you`);
   * - the protocol version, so a deployment that changes what the view says invalidates every tag;
   * - a thirty-second window of the server's clock. A write whose event was then lost (a crash
   *   between the two) would otherwise leave the tag where it was and every client on the old view
   *   until the next event. The window bounds that to thirty seconds at a cost of one full read per
   *   player per window.
   *
   * Only a lobby is tagged. A running match's view carries turn rows, readiness and reports, and
   * reports in particular are written without an event, so a tag that covered it would need the
   * reads it exists to save. The game polls only the lobby.
   *
   * The caller must take the tag BEFORE reading the view it answers with: a view newer than its
   * tag costs one extra full read on the next poll, a tag newer than its view would keep a client
   * on a stale view.
   */
  async lobbyTag(match: Match, playerId: string, now: Date): Promise<string | null> {
    if (match.status !== 'lobby') return null
    const lastEventSeq = await this.storage.events.lastSeq(match.id)
    // Plain `JSON.stringify` rather than `canonicalJson`: settings may hold values the canonical
    // form refuses, and a tag only has to be stable for one stored row, which it is. Two texts for
    // the same content would cost an extra read, never a stale one.
    const digest = await sha256Hex(
      JSON.stringify({
        protocol: MULTIPLAYER_PROTOCOL_VERSION,
        window: Math.floor(now.getTime() / LOBBY_TAG_WINDOW_MS),
        you: playerId,
        lastEventSeq,
        match: {
          id: match.id,
          protocolVersion: match.protocolVersion,
          sessionVersion: match.sessionVersion,
          status: match.status,
          settings: match.settings,
          hostPlayerId: match.hostPlayerId,
          joinCode: match.joinCode,
          seed: match.seed,
          currentTurn: match.currentTurn,
          seatCount: match.seatCount,
          joinCounter: match.joinCounter,
          createdAt: match.createdAt.toISOString(),
          updatedAt: match.updatedAt.toISOString(),
        },
      }),
    )
    return `"lobby-${digest.slice(0, 32)}"`
  }

  private async turnView(matchId: string, number: number): Promise<TurnView | null> {
    if (number < 1) return null
    const turn = await this.storage.turns.get(matchId, number)
    if (!turn) return null
    const [orders, reports] = await Promise.all([
      this.storage.turns.listOrderSummaries(matchId, number),
      this.storage.turns.listReports(matchId, number),
    ])
    return {
      number: turn.number,
      status: turn.status,
      openedAt: turn.openedAt.toISOString(),
      deadlineAt: turn.deadlineAt?.toISOString() ?? null,
      sealedAt: turn.sealedAt?.toISOString() ?? null,
      orderSetHash: turn.orderSetHash,
      readyPlayerIds: orders.filter((row) => row.ready).map((row) => row.playerId),
      reportedPlayerIds: reports.map((report) => report.playerId),
    }
  }

  /**
   * The public lobby list.
   *
   * Two queries in total, whatever the page holds: the listing itself carries the seat count and
   * whether a snapshot exists, and the seats of every match that could still advertise one are read
   * together. It used to be two more queries for each running match on the page, on a route no
   * token guards.
   *
   * A `sessionVersion` narrows the list to the matches the caller could play; see
   * `lobbyListQuerySchema`.
   */
  async listPublicLobbies(limit: number, sessionVersion?: number): Promise<LobbyListing[]> {
    const rows = await this.storage.matches.listPublicLobbies(limit, sessionVersion)
    const lateJoinable = rows.filter(
      (row) =>
        row.status === 'running' &&
        row.settings.gameSettings.allowLateJoin === true &&
        row.hasSnapshot,
    )
    const seatsByMatch = new Map<string, number[]>()
    for (const seat of await this.storage.players.listSeats(lateJoinable.map((row) => row.id))) {
      const seats = seatsByMatch.get(seat.matchId)
      if (seats) seats.push(seat.slot)
      else seatsByMatch.set(seat.matchId, [seat.slot])
    }
    return rows.map(({ hasSnapshot: _hasSnapshot, ...listing }) => {
      const seats = seatsByMatch.get(listing.id)
      if (seats === undefined) return listing
      // `joinRunning` counts every seat a human has ever held against the host's own limit, so a
      // listing that ignored it advertised seats that every join answers `match_full` for.
      if (seats.length >= listing.settings.maxPlayers) return listing
      const reserved = new Set(seats)
      const availableSlots = Array.from(
        { length: GAME_BOUNDS.playerCount },
        (_, slot) => slot,
      ).filter((slot) => !reserved.has(slot))
      const summaries = Array.isArray(listing.settings.gameSettings.seatSummaries)
        ? listing.settings.gameSettings.seatSummaries.filter(isSeatSummary)
        : []
      return {
        ...listing,
        availableSlots,
        availableSeatSummaries: availableSlots.map(
          (slot) =>
            summaries.find((summary) => summary.slot === slot) ?? {
              slot,
              gangs: 0,
              sites: 0,
              sectors: 0,
            },
        ),
      }
    })
  }

  async ownSubmission(match: Match, playerId: string, number: number): Promise<OwnSubmissionView> {
    const row = await this.storage.turns.getOrders(match.id, number, playerId)
    if (!row) throw new NotFoundError('No such turn', { reason: 'unknown_turn' })
    return { turn: number, orders: row.orders, ready: row.ready, ordersHash: row.ordersHash }
  }

  /**
   * The full order set of a turn, only once sealed: before that, other players' plans are secret.
   *
   * The participants come from the set the seal froze, never from the roster as it stands now. That
   * is what makes the response re-hash to the `orderSetHash` it is served with: someone leaving
   * between the submission and the seal (their orders are excluded while takeover is voted on) or
   * after it (their orders stay in) changes the roster but not this set.
   */
  async sealedOrders(match: Match, number: number): Promise<SealedOrdersView> {
    const turn = await this.requireTurn(match.id, number)
    if (turn.status === 'open' || turn.orderSetHash === null || turn.sealedSlots === null) {
      throw new ConflictError('The turn has not been sealed yet', { reason: 'turn_open' })
    }
    const orders = new Map(
      (await this.storage.turns.listOrders(match.id, number)).map((row) => [row.playerId, row]),
    )
    const rows: SealedPlayerOrders[] = []
    for (const { playerId, slot } of [...turn.sealedSlots].sort((a, b) => a.slot - b.slot)) {
      const row = orders.get(playerId)
      if (!row || row.orders === null || row.ordersHash === null) {
        throw new Error(`sealed turn ${match.id}/${number} is missing orders for ${playerId}`)
      }
      rows.push({ playerId, slot, orders: row.orders, ordersHash: row.ordersHash })
    }
    return { turn: number, orderSetHash: turn.orderSetHash, players: rows }
  }

  private requireTurn(matchId: string, number: number): Promise<Turn> {
    return requireTurn(this.storage.turns, matchId, number)
  }
}

function isSeatSummary(value: unknown): value is {
  slot: number
  gangs: number
  sites: number
  sectors: number
} {
  if (value === null || typeof value !== 'object') return false
  const candidate = value as Record<string, unknown>
  return ['slot', 'gangs', 'sites', 'sectors'].every(
    (key) => Number.isSafeInteger(candidate[key]) && (candidate[key] as number) >= 0,
  )
}
