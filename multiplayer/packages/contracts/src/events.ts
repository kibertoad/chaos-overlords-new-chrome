import type { MatchStatus, PlayerView } from './views'

interface EventBase<TType extends string, TPayload> {
  type: TType
  payload: TPayload
}

export type MatchEventBody =
  | EventBase<'lobby.playerJoined', { player: PlayerView }>
  | EventBase<'lobby.playerLeft', { playerId: string; reason: 'left' | 'kicked' }>
  | EventBase<'lobby.hostChanged', { hostPlayerId: string }>
  | EventBase<'match.started', { seed: number; players: PlayerView[] }>
  | EventBase<'turn.opened', { turn: number; deadlineAt: string | null }>
  | EventBase<'turn.deadlineExtended', { turn: number; deadlineAt: string | null }>
  | EventBase<'turn.readiness', { turn: number; playerId: string; ready: boolean }>
  | EventBase<'turn.sealed', { turn: number; orderSetHash: string }>
  | EventBase<'turn.confirmed', { turn: number; stateHash: string }>
  | EventBase<
      'turn.desynced',
      { turn: number; reports: Array<{ playerId: string; stateHash: string }> }
    >
  | EventBase<
      'snapshot.available',
      { turn: number; formatVersion: number; stateHash: string; uploadedByPlayerId: string }
    >
  | EventBase<'match.statusChanged', { status: MatchStatus }>

export type MatchEventType = MatchEventBody['type']

/**
 * An event as persisted and delivered: the body plus its position in the match's log.
 *
 * Sequence numbers are gapless and allocated by the insert itself, so a client that has seen `seq`
 * has seen every event before it. Delivery is at least once: a redelivery after a resume or a
 * repaired seal repeats a fact a client may already hold, so every handler must be idempotent.
 */
export type MatchEvent = MatchEventBody & {
  seq: number
  matchId: string
  createdAt: string
}

/** The SSE stream also emits a keepalive comment; this is the one non-event frame. */
export const SSE_HEARTBEAT_COMMENT = 'keepalive'
