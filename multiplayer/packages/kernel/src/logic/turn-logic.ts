import {
  activePlayers,
  humanParticipants,
  isHumanParticipant,
  type Player,
  type Turn,
  type TurnOrders,
  type TurnReport,
} from '../domain/entities'

/**
 * The seats a turn waits on: every human participant, and a seat that left while the absence vote
 * on it is still open.
 *
 * Leaving does not decide a seat; the vote does. A departed player's turn used to seal the moment
 * everyone present was ready, before anybody had answered the prompt — or after they had voted to
 * `wait` — so the player came back a turn later than the one they left on, having given no orders
 * for it. Until the vote hands the seat to the computer, the turn is theirs too. A kicked seat is
 * never waited on: its token is revoked, so its player cannot come back to finish the turn.
 */
export function awaitedSeats(
  players: readonly Player[],
  openPrompts: ReadonlySet<string>,
): Player[] {
  return players.filter(
    (player) =>
      isHumanParticipant(player) || (player.status === 'left' && openPrompts.has(player.id)),
  )
}

/** Every awaited seat that was asked for orders has marked ready. An empty roster is never "ready". */
export function allAwaitedReady(
  awaited: readonly Player[],
  orders: ReadonlyArray<Pick<TurnOrders, 'playerId' | 'ready'>>,
): boolean {
  const asked = new Set(orders.map((row) => row.playerId))
  const seats = awaited.filter((player) => asked.has(player.id))
  if (seats.length === 0) return false
  const readyIds = new Set(orders.filter((row) => row.ready).map((row) => row.playerId))
  return seats.every((player) => readyIds.has(player.id))
}

/**
 * Whether the turn was sealed by its clock running out rather than by everyone being ready. Read
 * from the stored row alone, so a sweep finishing an interrupted seal reaches the same answer as the
 * call that sealed it: a deadline seal is refused until the deadline has passed, so its `sealedAt`
 * is never earlier. A turn without a deadline (timer off, or paused by an absence vote) can only
 * seal on readiness.
 */
export function sealedByDeadline(turn: Pick<Turn, 'deadlineAt' | 'sealedAt'>): boolean {
  if (turn.deadlineAt === null || turn.sealedAt === null) return false
  return turn.sealedAt.getTime() >= turn.deadlineAt.getTime()
}

export type Consensus =
  | { kind: 'pending' }
  | { kind: 'confirmed'; stateHash: string; finished: boolean }
  | {
      kind: 'desynced'
      reports: Array<{ playerId: string; stateHash: string }>
      /** The hashes tied for the most reports; what a recovery snapshot may claim. */
      candidateStateHashes: string[]
    }

/**
 * The state hashes that the most active players reported, tied if more than one.
 *
 * This is the corroboration a recovery snapshot is held to. Letting the host name any hash at all
 * would make the host's client authoritative over a disagreement it is itself a party to: upload a
 * doctored snapshot after a deliberate desync and every other client is told to adopt it. A hash
 * that more players than any other already computed independently cannot be minted by one of them.
 *
 * A genuine tie (most of all, the 1-1 split of a two-player match) leaves nothing to count, and
 * there is no third party to ask, so the host breaks it. Lockstep of three or more is where this
 * bites, and that is the case worth defending: consistency is what matters, so converging on the
 * majority's state is right even when the host's own client happens to be the correct one.
 */
export function authoritativeCandidates(
  players: readonly Player[],
  reports: readonly TurnReport[],
): string[] {
  const active = new Set(humanParticipants(players).map((p) => p.id))
  const counts = new Map<string, number>()
  for (const report of reports) {
    if (!active.has(report.playerId)) continue
    counts.set(report.stateHash, (counts.get(report.stateHash) ?? 0) + 1)
  }
  const best = Math.max(0, ...counts.values())
  if (best === 0) return []
  return [...counts.entries()]
    .filter(([, count]) => count === best)
    .map(([stateHash]) => stateHash)
    .sort()
}

/**
 * Compare the post-turn state hashes the human seats reported.
 *
 * Without an authoritative hash: once everyone has reported, unanimous agreement confirms the
 * turn and any disagreement flags a desync. With one (the host uploaded a snapshot for this
 * turn), a report is counted only when it matches it, so stragglers reloading the snapshot can
 * converge without tripping a second desync.
 *
 * A seat that merely missed one timed deadline (`takeoverPending`) is still a human seat whose
 * client applies every sealed turn, so its report is waited for like any other: confirming without
 * it would refuse its later, possibly disagreeing, report as `turn_confirmed` and leave a genuine
 * divergence undetected. The wait costs nothing the match was not already paying — an open
 * absence vote pauses the turn clock — and the vote that makes the seat computer controlled
 * re-runs the verdict without it.
 */
export function evaluateConsensus(
  players: readonly Player[],
  reports: readonly TurnReport[],
  authoritativeHash: string | null,
): Consensus {
  const active = humanParticipants(players)
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
    candidateStateHashes: authoritativeCandidates(players, reports),
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
  const ordered = activePlayers(players).sort((a, b) => {
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
