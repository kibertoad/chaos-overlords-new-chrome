import type {
  LobbyListing,
  MatchView,
  OwnSubmissionView,
  PlayerView,
  SealedOrdersView,
  SealedPlayerOrders,
  TurnView,
} from '@chaos-overlords/contracts'
import type { Match, Player, Turn } from '../domain/entities'
import { ConflictError, NotFoundError } from '../domain/errors'
import type { MultiplayerStorage } from '../ports/storage'

export function toPlayerView(player: Player, hostPlayerId: string): PlayerView {
  return {
    id: player.id,
    slot: player.slot,
    displayName: player.displayName,
    status: player.status,
    isHost: player.id === hostPlayerId,
  }
}

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

  private async turnView(matchId: string, number: number): Promise<TurnView | null> {
    if (number < 1) return null
    const turn = await this.storage.turns.get(matchId, number)
    if (!turn) return null
    const [orders, reports] = await Promise.all([
      this.storage.turns.listOrders(matchId, number),
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

  listPublicLobbies(limit: number): Promise<LobbyListing[]> {
    return this.storage.matches.listPublicLobbies(limit)
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
   * between the submission and the seal (their orders are excluded, their slot becomes a computer
   * player) or after it (their orders stay in) changes the roster but not this set.
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

  private async requireTurn(matchId: string, number: number): Promise<Turn> {
    const turn = await this.storage.turns.get(matchId, number)
    if (!turn) throw new NotFoundError('No such turn', { reason: 'unknown_turn' })
    return turn
  }
}
