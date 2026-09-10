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
 * wake is harmless; a MISSED wake is what the periodic `sweepExpiredTurns` safety net covers.
 */
export interface DeadlineScheduler {
  schedule(input: { matchId: string; turn: number; dueAt: Date }): Promise<void>
}

/**
 * Opens the server-sent event stream for one match. Each runtime owns the response because the
 * fan-out lives in a different place (an in-process hub on Node, a Durable Object on Cloudflare).
 */
export interface EventStreamOpener {
  open(input: { matchId: string; afterSeq: number; signal: AbortSignal }): Promise<Response>
}
