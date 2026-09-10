import type { LobbyListing, MatchStatus, TurnStatus } from '@chaos-overlords/contracts'
import type {
  Match,
  PersistedEvent,
  Player,
  Snapshot,
  Turn,
  TurnOrders,
  TurnReport,
} from '../domain/entities'
import type {
  EventRepository,
  MatchRepository,
  MultiplayerStorage,
  PlayerRepository,
  SnapshotRepository,
  TurnRepository,
} from '../ports/storage'

/**
 * A reference implementation of the storage ports with the same atomicity semantics as the SQL
 * ones (single-threaded JS makes every method atomic). It runs the kernel and HTTP tests
 * hermetically and doubles as the executable specification the conformance suite pins the
 * SQL implementations to.
 */
export class InMemoryStorage implements MultiplayerStorage {
  private readonly matchRows = new Map<string, Match>()
  private readonly playerRows = new Map<string, Player>()
  private readonly turnRows = new Map<string, Turn>()
  private readonly orderRows = new Map<string, TurnOrders>()
  private readonly reportRows = new Map<string, TurnReport>()
  private readonly snapshotRows = new Map<string, Snapshot>()
  private readonly eventRows = new Map<string, PersistedEvent[]>()

  readonly matches: MatchRepository = {
    create: async (match) => {
      this.matchRows.set(match.id, { ...match })
    },
    get: async (id) => clone(this.matchRows.get(id)),
    getByJoinCode: async (joinCode) =>
      clone([...this.matchRows.values()].find((match) => match.joinCode === joinCode)),
    listPublicLobbies: async (limit) => {
      const lobbies: LobbyListing[] = []
      for (const match of this.matchRows.values()) {
        if (match.status !== 'lobby' || match.settings.visibility !== 'public') continue
        const host = this.playerRows.get(match.hostPlayerId)
        lobbies.push({
          id: match.id,
          name: match.settings.name,
          hostDisplayName: host?.displayName ?? '',
          playerCount: match.seatCount,
          maxPlayers: match.settings.maxPlayers,
          passwordProtected: match.passwordHash !== null,
          createdAt: match.createdAt.toISOString(),
        })
      }
      return lobbies
        .sort((a, b) => b.createdAt.localeCompare(a.createdAt) || a.id.localeCompare(b.id))
        .slice(0, limit)
    },
    claimSeat: async (matchId) => {
      const match = this.matchRows.get(matchId)
      if (match?.status !== 'lobby' || match.seatCount >= match.settings.maxPlayers) {
        return false
      }
      match.seatCount += 1
      return true
    },
    releaseSeat: async (matchId) => {
      const match = this.matchRows.get(matchId)
      if (match && match.seatCount > 0) match.seatCount -= 1
    },
    transition: async (matchId, from, patch) => {
      const match = this.matchRows.get(matchId)
      if (!match || !from.includes(match.status)) return false
      Object.assign(match, definedOnly(patch))
      return true
    },
    allocateEventSeq: async (matchId) => {
      const match = this.matchRows.get(matchId)
      if (!match) throw new Error(`no match ${matchId}`)
      match.eventSeq += 1
      return match.eventSeq
    },
  }

  readonly players: PlayerRepository = {
    create: async (player) => {
      this.playerRows.set(player.id, { ...player })
    },
    get: async (id) => clone(this.playerRows.get(id)),
    getByTokenHash: async (tokenHash) =>
      clone([...this.playerRows.values()].find((player) => player.tokenHash === tokenHash)),
    listByMatch: async (matchId) =>
      [...this.playerRows.values()]
        .filter((player) => player.matchId === matchId)
        .sort(
          (a, b) =>
            a.slot - b.slot ||
            a.joinedAt.getTime() - b.joinedAt.getTime() ||
            a.id.localeCompare(b.id),
        )
        .map((player) => ({ ...player })),
    setStatus: async (playerId, status) => {
      const player = this.playerRows.get(playerId)
      if (player) player.status = status
    },
    assignSlots: async (assignments) => {
      for (const { playerId, slot } of assignments) {
        const player = this.playerRows.get(playerId)
        if (player) player.slot = slot
      }
    },
    delete: async (playerId) => {
      this.playerRows.delete(playerId)
    },
  }

  readonly turns: TurnRepository = {
    open: async (turn, playerIds) => {
      this.turnRows.set(turnKey(turn.matchId, turn.number), { ...turn })
      for (const playerId of playerIds) {
        this.orderRows.set(orderKey(turn.matchId, turn.number, playerId), {
          matchId: turn.matchId,
          turn: turn.number,
          playerId,
          orders: null,
          ordersHash: null,
          ready: false,
          submittedAt: null,
        })
      }
    },
    get: async (matchId, number) => clone(this.turnRows.get(turnKey(matchId, number))),
    submitOrders: async (matchId, number, playerId, submission) => {
      const turn = this.turnRows.get(turnKey(matchId, number))
      const row = this.orderRows.get(orderKey(matchId, number, playerId))
      if (turn?.status !== 'open' || !row) return false
      Object.assign(row, submission)
      return true
    },
    getOrders: async (matchId, number, playerId) =>
      clone(this.orderRows.get(orderKey(matchId, number, playerId))),
    listOrders: async (matchId, number) =>
      [...this.orderRows.values()]
        .filter((row) => row.matchId === matchId && row.turn === number)
        .sort((a, b) => a.playerId.localeCompare(b.playerId))
        .map((row) => ({ ...row })),
    transition: async (matchId, number, from, patch) => {
      const turn = this.turnRows.get(turnKey(matchId, number))
      if (!turn || !from.includes(turn.status)) return false
      Object.assign(turn, definedOnly(patch))
      return true
    },
    upsertReport: async (report) => {
      this.reportRows.set(orderKey(report.matchId, report.turn, report.playerId), { ...report })
    },
    listReports: async (matchId, number) =>
      [...this.reportRows.values()]
        .filter((row) => row.matchId === matchId && row.turn === number)
        .sort((a, b) => a.playerId.localeCompare(b.playerId))
        .map((row) => ({ ...row })),
    listUnsettled: async (matchId) =>
      [...this.turnRows.values()]
        .filter(
          (turn) =>
            turn.matchId === matchId &&
            (['sealed', 'desynced'] as TurnStatus[]).includes(turn.status),
        )
        .sort((a, b) => a.number - b.number)
        .map((turn) => ({ ...turn })),
    listExpiredOpen: async (now, limit) =>
      [...this.turnRows.values()]
        .filter(
          (turn) => turn.status === 'open' && turn.deadlineAt !== null && turn.deadlineAt <= now,
        )
        .sort((a, b) => (a.deadlineAt?.getTime() ?? 0) - (b.deadlineAt?.getTime() ?? 0))
        .slice(0, limit)
        .map((turn) => ({ matchId: turn.matchId, number: turn.number })),
  }

  readonly snapshots: SnapshotRepository = {
    put: async (snapshot) => {
      this.snapshotRows.set(turnKey(snapshot.matchId, snapshot.turn), { ...snapshot })
    },
    get: async (matchId, turn) => clone(this.snapshotRows.get(turnKey(matchId, turn))),
    getLatest: async (matchId) =>
      clone(
        [...this.snapshotRows.values()]
          .filter((snapshot) => snapshot.matchId === matchId)
          .sort((a, b) => b.turn - a.turn)[0],
      ),
  }

  readonly events: EventRepository = {
    append: async (event) => {
      const log = this.eventRows.get(event.matchId) ?? []
      if (log.some((existing) => existing.seq === event.seq)) {
        throw new Error(`duplicate event seq ${event.seq} for ${event.matchId}`)
      }
      log.push({ ...event })
      log.sort((a, b) => a.seq - b.seq)
      this.eventRows.set(event.matchId, log)
    },
    listAfter: async (matchId, afterSeq, limit) =>
      (this.eventRows.get(matchId) ?? [])
        .filter((event) => event.seq > afterSeq)
        .slice(0, limit)
        .map((event) => ({ ...event })),
  }

  /** Test hook: the match statuses on file, for assertions that bypass the services. */
  statusOf(matchId: string): MatchStatus | undefined {
    return this.matchRows.get(matchId)?.status
  }
}

function turnKey(matchId: string, number: number): string {
  return `${matchId}:${number}`
}

function orderKey(matchId: string, number: number, playerId: string): string {
  return `${matchId}:${number}:${playerId}`
}

function clone<T>(value: T | undefined): T | null {
  return value === undefined ? null : { ...value }
}

function definedOnly<T extends object>(patch: T): Partial<T> {
  const out: Partial<T> = {}
  for (const [key, value] of Object.entries(patch)) {
    if (value !== undefined) (out as Record<string, unknown>)[key] = value
  }
  return out
}
