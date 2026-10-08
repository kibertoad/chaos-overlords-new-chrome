import { foreignOps, type SubmitOrdersRequest } from '@chaos-overlords/contracts'
import {
  isHumanParticipant,
  isInProgress,
  type Match,
  type Player,
  type Turn,
} from '../domain/entities'
import { ConflictError, ForbiddenError, NotFoundError, ValidationError } from '../domain/errors'
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

/**
 * Refuse a document whose ops act for a slot other than the submitter's.
 *
 * The sealed set attributes every op to the slot it was submitted from, and that attribution is
 * the one clients apply, so an op naming a different player can only be a client bug or an attempt
 * to act as somebody else. Catching it here keeps the two attributions from ever disagreeing, and
 * it is the one piece of order semantics the server can judge without knowing the rules.
 */
export function assertOwnOps(request: SubmitOrdersRequest, slot: number): void {
  const foreign = foreignOps(request.orders, slot)
  if (foreign.length === 0) return
  throw new ValidationError('Orders may only act for your own slot', {
    reason: 'foreign_slot_ops',
    slot,
    ops: foreign.slice(0, 8).map((op) => ({ op: op.op, player: op.player })),
  })
}

/** Refuse a match that is paused for a desync or not running. */
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
