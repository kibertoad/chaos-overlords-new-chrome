import {
  isConcluded,
  isHumanParticipant,
  isInProgress,
  type Match,
  type Player,
  type Turn,
} from '../domain/entities'
import { ConflictError, ForbiddenError, NotFoundError } from '../domain/errors'
import { FIRST_TURN } from '../logic/turn-logic'
import type { TurnRepository } from '../ports/storage'

/** Refuse a match that has not started or has ended. A desync pause passes. */
export function requireInProgress(match: Match): void {
  if (isInProgress(match)) return
  throw new ConflictError('The match is not in progress', { reason: 'match_not_running' })
}

/**
 * Refuse a read of what a match played from views hides until it ends: another seat's orders, the
 * whole state. Every match played otherwise publishes them, because every client resolves it.
 */
export function requireReleased(match: Match, what: string): void {
  if (!match.seatViews || isConcluded(match)) return
  throw new ConflictError(`${what} is withheld until the match ends`, {
    reason: 'withheld_until_end',
  })
}

const LOCKSTEP_ONLY = {
  reports_not_taken: 'The server alone resolves the turns of a match played from views',
  snapshot_not_required: 'The server writes the snapshots of a match played from views',
} as const

/**
 * Refuse in a match played from views what only a client that resolves the match sends: hash
 * reports and snapshot uploads. Its clients hold no state of their own.
 */
export function requireLockstep(match: Match, reason: keyof typeof LOCKSTEP_ONLY): void {
  if (match.seatViews) throw new ConflictError(LOCKSTEP_ONLY[reason], { reason })
}

/**
 * Whether the turn before the match's open turn is confirmed: always in a lockstep match, whose
 * next turn opens at the seal, and in a match played from views only once the server has resolved
 * it, since nobody can plan the open turn before then. True for the first turn.
 */
export async function previousTurnConfirmed(
  turns: TurnRepository,
  match: Match,
  number: number = match.currentTurn,
): Promise<boolean> {
  if (!match.seatViews || number <= FIRST_TURN) return true
  return (await turns.get(match.id, number - 1))?.status === 'confirmed'
}

/** Refuse a match that is not running, naming a desync pause apart. */
export function requireRunning(match: Match): void {
  if (match.status === 'desynced') {
    throw new ConflictError('The match is paused until the host uploads a snapshot', {
      reason: 'match_desynced',
    })
  }
  if (match.status !== 'running') {
    throw new ConflictError('The match is not in progress', { reason: 'match_not_running' })
  }
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
