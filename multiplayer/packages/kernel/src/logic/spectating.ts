import type { Match, Turn } from '../domain/entities'

/**
 * How far behind the players a spectator of this match watches, or null when it cannot be watched.
 */
export function spectatorDelay(match: Pick<Match, 'settings'>): number | null {
  const delay = match.settings.spectatorDelayTurns
  return typeof delay === 'number' ? delay : null
}

/**
 * The newest turn a spectator may read, or 0 when none is released.
 *
 * While the match runs, the newest sealed turn is taken to be the one before the open turn, which
 * is never ahead of the truth: between a seal and the next turn opening it is one behind, and that
 * only delays a release. Once the match is over everything sealed is released, with the open turn
 * counted when the row says it was sealed after all. `currentTurnRow` is that row, and is only read
 * for a match that is over.
 */
export function releasedTurn(
  match: Pick<Match, 'status' | 'currentTurn' | 'settings'>,
  currentTurnRow: Pick<Turn, 'status' | 'orderSetHash'> | null,
): number {
  if (match.currentTurn < 1) return 0
  const delay = spectatorDelay(match) ?? 0
  if (match.status === 'finished' || match.status === 'abandoned') {
    const currentSealed =
      currentTurnRow !== null &&
      currentTurnRow.status !== 'open' &&
      currentTurnRow.orderSetHash !== null
    return currentSealed ? match.currentTurn : match.currentTurn - 1
  }
  if (match.status !== 'running' && match.status !== 'desynced') return 0
  return Math.max(0, match.currentTurn - 1 - delay)
}

/** Whether releasing a turn needs the open turn's row; see `releasedTurn`. */
export function releaseReadsCurrentTurn(match: Pick<Match, 'status' | 'currentTurn'>): boolean {
  return match.currentTurn >= 1 && (match.status === 'finished' || match.status === 'abandoned')
}
