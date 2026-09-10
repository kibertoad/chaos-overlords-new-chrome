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
 * A reference implementation of the storage ports with the same semantics as the SQL ones:
 * single-threaded JS makes every method atomic, and the uniqueness a database index would enforce
 * (match id, join code, token hash, turn and event keys) is enforced here by hand so a service can
 * be tested against the same refusals. The storage conformance suite runs against it, which is what
 * keeps the claim honest.
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
      const taken = [...this.matchRows.values()].some((row) => row.joinCode === match.joinCode)
      if (taken || this.matchRows.has(match.id)) return false
      this.matchRows.set(match.id, { ...match })
      return true
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
        return null
      }
      match.seatCount += 1
      match.joinCounter += 1
      return match.joinCounter - 1
    },
    releaseSeat: async (matchId) => {
      const match = this.matchRows.get(matchId)
      if (match && match.seatCount > 0) match.seatCount -= 1
    },
    deleteInactive: async (statuses, before, limit) => {
      const doomed = [...this.matchRows.values()]
        .filter((match) => statuses.includes(match.status) && match.updatedAt < before)
        .sort((a, b) => a.updatedAt.getTime() - b.updatedAt.getTime())
        .slice(0, limit)
      for (const match of doomed) this.deleteMatch(match.id)
      return doomed.length
    },
    transition: async (matchId, from, patch) => {
      const match = this.matchRows.get(matchId)
      if (!match || !from.includes(match.status)) return false
      Object.assign(match, definedOnly(patch))
      return true
    },
  }

  readonly players: PlayerRepository = {
    create: async (player) => {
      const clash = [...this.playerRows.values()].some(
        (row) => player.tokenHash !== null && row.tokenHash === player.tokenHash,
      )
      if (clash || this.playerRows.has(player.id)) {
        throw new Error(`player ${player.id} or its token already exists`)
      }
      // Conditional on the lobby, like the conditional insert the real repositories use.
      if (this.matchRows.get(player.matchId)?.status !== 'lobby') return false
      this.playerRows.set(player.id, { ...player })
      return true
    },
    get: async (id) => clone(this.playerRows.get(id)),
    getByTokenHash: async (tokenHash) =>
      clone(
        [...this.playerRows.values()].find(
          (player) => player.tokenHash !== null && player.tokenHash === tokenHash,
        ),
      ),
    listByMatch: async (matchId) =>
      [...this.playerRows.values()]
        .filter((player) => player.matchId === matchId)
        .sort((a, b) => a.slot - b.slot || a.joinOrder - b.joinOrder || a.id.localeCompare(b.id))
        .map((player) => ({ ...player })),
    setStatus: async (playerId, status) => {
      const player = this.playerRows.get(playerId)
      if (player) player.status = status
    },
    revokeToken: async (playerId) => {
      const player = this.playerRows.get(playerId)
      if (player) player.tokenHash = null
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
      const created = !this.turnRows.has(turnKey(turn.matchId, turn.number))
      if (created) this.turnRows.set(turnKey(turn.matchId, turn.number), { ...turn })
      for (const playerId of playerIds) {
        const key = orderKey(turn.matchId, turn.number, playerId)
        if (this.orderRows.has(key)) continue
        this.orderRows.set(key, {
          matchId: turn.matchId,
          turn: turn.number,
          playerId,
          orders: null,
          ordersHash: null,
          ready: false,
          submittedAt: null,
        })
      }
      return created
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
    rescheduleDeadline: async (matchId, number, deadlineAt) => {
      const turn = this.turnRows.get(turnKey(matchId, number))
      if (turn?.status !== 'open') return false
      turn.deadlineAt = deadlineAt
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
    // Driven from the matches, so a current turn with no row at all counts as stalled too.
    listStalledSeals: async (limit) =>
      [...this.matchRows.values()]
        .filter((match) => match.status === 'running' || match.status === 'desynced')
        .filter(
          (match) => this.turnRows.get(turnKey(match.id, match.currentTurn))?.status !== 'open',
        )
        .sort((a, b) => a.id.localeCompare(b.id))
        .slice(0, limit)
        .map((match) => ({ matchId: match.id, number: match.currentTurn })),
  }

  readonly snapshots: SnapshotRepository = {
    put: async (snapshot) => {
      this.snapshotRows.set(turnKey(snapshot.matchId, snapshot.turn), { ...snapshot })
    },
    get: async (matchId, turn) => clone(this.snapshotRows.get(turnKey(matchId, turn))),
    prune: async (matchId, keep) => {
      const turns = [...this.snapshotRows.values()]
        .filter((snapshot) => snapshot.matchId === matchId)
        .map((snapshot) => snapshot.turn)
        .sort((a, b) => b - a)
      if (turns.length <= keep) return 0
      const dropped = turns.slice(keep)
      for (const turn of dropped) this.snapshotRows.delete(turnKey(matchId, turn))
      return dropped.length
    },
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
      const persisted = { ...event, seq: (log.at(-1)?.seq ?? 0) + 1 } as PersistedEvent
      log.push(persisted)
      this.eventRows.set(event.matchId, log)
      return { ...persisted }
    },
    listAfter: async (matchId, afterSeq, limit) =>
      (this.eventRows.get(matchId) ?? [])
        .filter((event) => event.seq > afterSeq)
        .slice(0, limit)
        .map((event) => ({ ...event })),
    lastSeq: async (matchId) => this.eventRows.get(matchId)?.at(-1)?.seq ?? 0,
  }

  /** What a cascading delete does in SQL: the match row and everything keyed by it. */
  private deleteMatch(matchId: string): void {
    this.matchRows.delete(matchId)
    this.eventRows.delete(matchId)
    for (const [id, player] of this.playerRows) {
      if (player.matchId === matchId) this.playerRows.delete(id)
    }
    for (const [key, turn] of this.turnRows) {
      if (turn.matchId === matchId) this.turnRows.delete(key)
    }
    for (const [key, row] of this.orderRows) {
      if (row.matchId === matchId) this.orderRows.delete(key)
    }
    for (const [key, row] of this.reportRows) {
      if (row.matchId === matchId) this.reportRows.delete(key)
    }
    for (const [key, row] of this.snapshotRows) {
      if (row.matchId === matchId) this.snapshotRows.delete(key)
    }
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
  return value === undefined ? null : structuredClone(value)
}

function definedOnly<T extends object>(patch: T): Partial<T> {
  const out: Partial<T> = {}
  for (const [key, value] of Object.entries(patch)) {
    if (value !== undefined) (out as Record<string, unknown>)[key] = value
  }
  return out
}
