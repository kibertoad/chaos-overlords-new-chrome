import type { Match } from '../domain/entities'
import { releasedTurn, releaseReadsCurrentTurn, spectatorDelay } from '../logic/spectating'
import type { MultiplayerStorage } from '../ports/storage'

/** The newest turn a spectator of this match may read; see `releasedTurn`. */
export async function releasedTurnOf(storage: MultiplayerStorage, match: Match): Promise<number> {
  const row = releaseReadsCurrentTurn(match)
    ? await storage.turns.get(match.id, match.currentTurn)
    : null
  return releasedTurn(match, row)
}

/**
 * The snapshot turn a spectator of this match starts from, or undefined when the match cannot be
 * watched or has no snapshot old enough: the newest one at or below the released turn.
 */
export async function spectatorStartTurn(
  storage: MultiplayerStorage,
  match: Match,
): Promise<number | undefined> {
  if (spectatorDelay(match) === null) return undefined
  const released = await releasedTurnOf(storage, match)
  return (await storage.snapshots.getLatestSummaryAtOrBelow(match.id, released))?.turn
}
