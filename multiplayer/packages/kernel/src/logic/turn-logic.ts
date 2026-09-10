import type { Player, TurnOrders, TurnReport } from '../domain/entities'

/** Every active player has marked ready. An empty roster is never "ready". */
export function allActiveReady(players: readonly Player[], orders: readonly TurnOrders[]): boolean {
  const active = players.filter((player) => player.status === 'active')
  if (active.length === 0) return false
  const readyIds = new Set(orders.filter((row) => row.ready).map((row) => row.playerId))
  return active.every((player) => readyIds.has(player.id))
}

export type Consensus =
  | { kind: 'pending' }
  | { kind: 'confirmed'; stateHash: string; finished: boolean }
  | { kind: 'desynced'; reports: Array<{ playerId: string; stateHash: string }> }

/**
 * Compare the post-turn state hashes the active players reported.
 *
 * Without an authoritative hash: once everyone has reported, unanimous agreement confirms the
 * turn and any disagreement flags a desync. With one (the host uploaded a snapshot for this
 * turn), a report is counted only when it matches it, so stragglers reloading the snapshot can
 * converge without tripping a second desync.
 */
export function evaluateConsensus(
  players: readonly Player[],
  reports: readonly TurnReport[],
  authoritativeHash: string | null,
): Consensus {
  const active = players.filter((player) => player.status === 'active')
  if (active.length === 0) return { kind: 'pending' }
  const byPlayer = new Map(reports.map((report) => [report.playerId, report]))
  const activeReports = active.map((player) => byPlayer.get(player.id))
  if (activeReports.some((report) => report === undefined)) return { kind: 'pending' }
  const present = activeReports as TurnReport[]

  if (authoritativeHash !== null) {
    if (present.every((report) => report.stateHash === authoritativeHash)) {
      return {
        kind: 'confirmed',
        stateHash: authoritativeHash,
        finished: present.every((report) => report.finished),
      }
    }
    return { kind: 'pending' }
  }

  const first = present[0]
  if (first && present.every((report) => report.stateHash === first.stateHash)) {
    return {
      kind: 'confirmed',
      stateHash: first.stateHash,
      finished: present.every((report) => report.finished),
    }
  }
  return {
    kind: 'desynced',
    reports: present.map((report) => ({ playerId: report.playerId, stateHash: report.stateHash })),
  }
}

/**
 * Host takes slot 0; everyone else follows the match's monotonic join sequence. The sequence, not
 * the join timestamp, is the key: two players seated in the same millisecond would otherwise be
 * ordered by their random ids, and the slot order decides resolution order on every client.
 */
export function assignSlots(
  players: readonly Player[],
  hostPlayerId: string,
): Array<{ playerId: string; slot: number }> {
  const ordered = [...players]
    .filter((player) => player.status === 'active')
    .sort((a, b) => {
      if (a.id === b.id) return 0
      if (a.id === hostPlayerId) return -1
      if (b.id === hostPlayerId) return 1
      return a.joinOrder - b.joinOrder || a.id.localeCompare(b.id)
    })
  return ordered.map((player, slot) => ({ playerId: player.id, slot }))
}

export function turnDeadline(openedAt: Date, turnTimerSeconds: number): Date | null {
  return turnTimerSeconds > 0 ? new Date(openedAt.getTime() + turnTimerSeconds * 1000) : null
}
