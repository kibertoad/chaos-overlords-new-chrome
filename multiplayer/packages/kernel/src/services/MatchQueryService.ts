import type {
  LobbyListing,
  MatchView,
  OwnSubmissionView,
  PlayerView,
  SealedOrdersView,
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
    const [turn, previousTurn] = await Promise.all([
      this.turnView(match.id, match.currentTurn),
      this.turnView(match.id, match.currentTurn - 1),
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
      lastEventSeq: match.eventSeq,
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

  /** The full order set of a turn, only once sealed: before that, other players' plans are secret. */
  async sealedOrders(match: Match, number: number): Promise<SealedOrdersView> {
    const turn = await this.requireTurn(match.id, number)
    if (turn.status === 'open' || turn.orderSetHash === null) {
      throw new ConflictError('The turn has not been sealed yet', { reason: 'turn_open' })
    }
    const [orders, players] = await Promise.all([
      this.storage.turns.listOrders(match.id, number),
      this.storage.players.listByMatch(match.id),
    ])
    const slotOf = new Map(players.map((player) => [player.id, player.slot]))
    const rows = orders
      .filter((row) => row.orders !== null && row.ordersHash !== null)
      .map((row) => ({
        playerId: row.playerId,
        slot: slotOf.get(row.playerId) ?? -1,
        orders: row.orders as NonNullable<typeof row.orders>,
        ordersHash: row.ordersHash as string,
      }))
      .sort((a, b) => a.slot - b.slot)
    return { turn: number, orderSetHash: turn.orderSetHash, players: rows }
  }

  private async requireTurn(matchId: string, number: number): Promise<Turn> {
    const turn = await this.storage.turns.get(matchId, number)
    if (!turn) throw new NotFoundError('No such turn', { reason: 'unknown_turn' })
    return turn
  }
}
