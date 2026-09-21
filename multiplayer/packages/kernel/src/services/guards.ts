import {
  isHumanParticipant,
  isInProgress,
  type Match,
  type Player,
  type Turn,
} from '../domain/entities'
import { ConflictError, ForbiddenError, NotFoundError } from '../domain/errors'
import type { TurnRepository } from '../ports/storage'

/** Refuse a match that has not started or has ended. A desync pause passes. */
export function requireInProgress(match: Match): void {
  if (isInProgress(match)) return
  throw new ConflictError('The match is not in progress', { reason: 'match_not_running' })
}

/** Refuse a caller whose seat was left, kicked or handed to the computer. */
export function requireParticipant(player: Player): void {
  if (isHumanParticipant(player)) return
  throw new ForbiddenError('You are no longer part of this match', { reason: 'not_active' })
}

/** One read of the turn row, refused as `unknown_turn` when the match has no such turn. */
export async function requireTurn(
  turns: Pick<TurnRepository, 'get'>,
  matchId: string,
  number: number,
): Promise<Turn> {
  const turn = await turns.get(matchId, number)
  if (!turn) throw new NotFoundError('No such turn', { reason: 'unknown_turn' })
  return turn
}
