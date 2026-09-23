import type { PersistedEvent } from '../domain/entities'

export interface Clock {
  now(): Date
}

export interface Logger {
  debug(msg: string, fields?: Record<string, unknown>): void
  info(msg: string, fields?: Record<string, unknown>): void
  warn(msg: string, fields?: Record<string, unknown>): void
  error(msg: string, fields?: Record<string, unknown>): void
}

export const noopLogger: Logger = {
  debug() {},
  info() {},
  warn() {},
  error() {},
}

/** Fan-out of a freshly persisted event to whoever is streaming that match. */
export interface EventNotifier {
  notify(event: PersistedEvent): Promise<void>
}

/**
 * Wakes the turn service at a turn's deadline. Sealing is idempotent, so a duplicate or late
 * wake is harmless; a MISSED wake is what the periodic `TurnService.sweep` safety net covers.
 */
export interface DeadlineScheduler {
  schedule(input: { matchId: string; turn: number; dueAt: Date }): Promise<void>
}

/**
 * Opens the server-sent event stream for one match. Each runtime owns the response because the
 * fan-out lives in a different place (an in-process hub on Node, a Durable Object on Cloudflare).
 *
 * The player is named as well as the match because the fan-out has to be able to bound and end a
 * membership's streams: see {@link StreamCloser}.
 */
export interface EventStreamOpener {
  open(input: {
    matchId: string
    playerId: string
    afterSeq: number
    signal: AbortSignal
    /** Lobby streams use a smaller share of a Node process's total stream capacity. */
    lobby?: boolean
  }): Promise<Response>
}

/**
 * Ends every event stream a membership is holding.
 *
 * Revoking a token stops the next REQUEST. A stream that is already open is never authenticated
 * again, so a kicked player would otherwise keep receiving every sealed set, every `turn.desynced`
 * (which carries each player's state hash) and every host change for as long as they cared to hold
 * the connection. Called by the kick path, straight after the revoke.
 */
export interface StreamCloser {
  close(input: { matchId: string; playerId: string }): Promise<void>
}
