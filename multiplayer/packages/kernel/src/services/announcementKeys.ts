import type { MatchEventBody } from '@chaos-overlords/contracts'

/*
 * Dedupe keys of the announcements that follow a compare-and-swap (`EventPublisher.publishOnce`).
 * Each names the one fact it announces, so a repeat of the announcement is recognised as the same
 * fact and never logged twice. They are server-side only and never reach a client.
 */
export const sealAnnouncementKey = (turn: number): string => `turn.sealed:${turn}`
export const confirmationKey = (turn: number): string => `turn.confirmed:${turn}`

/** A `turn.desynced` payload as this build writes it: the tie-breaker is always named. */
export type DesyncPayload = Extract<MatchEventBody, { type: 'turn.desynced' }>['payload'] & {
  tieBreakerPlayerId: string | null
}

/**
 * The dedupe key of a desync announcement: the turn and everything a client acts on, so a verdict
 * announced again with the same content is logged once. The reports themselves are left out,
 * because a report that changes without changing the candidates or the tie-breaker asks nothing
 * new of anybody.
 */
export const desyncAnnouncementKey = (payload: DesyncPayload): string =>
  `turn.desynced:${payload.turn}:${payload.candidateStateHashes.join(',')}:${payload.tieBreakerPlayerId ?? ''}`

/**
 * A pause or its lift, named by the status event it follows: the same change announced after the
 * same last word is the same fact, and the next pause of the match follows a different event.
 */
export const statusAnnouncementKey = (status: string, afterSeq: number): string =>
  `match.statusChanged:${status}:after:${afterSeq}`
/** A match finishes once. */
export const FINISH_ANNOUNCEMENT_KEY = 'match.statusChanged:finished'
/** A prompt is one opening of the question about a seat, which its `openedAt` identifies. */
export const promptAnnouncementKey = (playerId: string, openedAt: Date): string =>
  `match.takeoverVoteRequested:${playerId}:${openedAt.getTime()}`
/** The withdrawal of one prompt's request that was logged after the prompt had closed. */
export const promptWithdrawalKey = (playerId: string, openedAt: Date): string =>
  `match.takeoverVoteCancelled:${playerId}:${openedAt.getTime()}`
