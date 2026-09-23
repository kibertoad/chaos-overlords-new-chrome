import {
  MATCH_EVENT_SSE_NAME,
  matchEventSchema,
  SSE_HEARTBEAT_COMMENT,
} from '@chaos-overlords/contracts'
import type { PersistedEvent } from '@chaos-overlords/kernel'
import { validateSync } from '@toad-contracts/core'
import type { EventFrame } from './MatchLog'

export interface EventStreamSource {
  /**
   * Formatted frames after `afterSeq`, ascending, at most one page.
   *
   * Frames rather than rows because the validation and the serialisation of one event are the same
   * work for every reader of it; see `MatchLog`.
   */
  page(afterSeq: number, force?: boolean): Promise<EventFrame[]>
  /**
   * Whether a stream at `lastSeq` is known to have everything.
   *
   * Only ever used to skip a periodic catch-up read that would have found nothing, so an
   * implementation that cannot tell must answer false.
   */
  caughtUp(lastSeq: number): boolean
  /**
   * Register a wake-up for new events; returns the unsubscribe.
   *
   * `close` ends this stream from the other side, which is how a revoked membership loses a stream
   * it already holds and how a player's stale stream is dropped to make room for their reconnect.
   * It is safe to call at any time and does nothing once the stream is closed.
   */
  subscribe(wake: () => void, close: (reason: HubCloseReason) => void): () => void
}

/** Why the hub ended a stream from its side; see `EventStreamSource.subscribe`. */
export type HubCloseReason =
  /** The player's membership was revoked, so the streams it held were hung up. */
  | 'revoked'
  /** The player opened another stream and this, their oldest, made room for it. */
  | 'replaced'
  /** The process is shutting down, or the object holding the stream is. */
  | 'shutdown'

/**
 * Why a stream ended.
 *
 * The client sees every one of these the same way — the body simply ends — so this is the only
 * record of which it was. A stream the server dropped while the player was still reading it shows
 * up on their screen as a lost connection, and without the reason in the log a membership lookup
 * that failed once on a database hiccup is indistinguishable from a deploy.
 */
export type SseCloseReason =
  | HubCloseReason
  /** The client went away: the request was aborted, or the consumer cancelled the body. */
  | 'client_gone'
  /** The periodic membership check answered that this player is no longer a member. */
  | 'membership_revoked'
  /** The periodic membership check failed or hung too many times in a row to trust the stream. */
  | 'membership_unverified'
  /** The consumer stopped reading for long enough that the connection is presumed dead. */
  | 'stalled'
  /** Reading the log failed, and the stream was errored rather than closed. */
  | 'read_failed'

/**
 * Whether a close is one a healthy client should never see: the server dropped a stream that was
 * still being read, for a reason of its own.
 *
 * `client_gone`, `replaced` and `shutdown` are routine, and a revoked membership is the stream
 * doing its job. The rest are worth an operator's attention.
 */
export function isUnexpectedClose(reason: SseCloseReason): boolean {
  return reason === 'membership_unverified' || reason === 'stalled' || reason === 'read_failed'
}

export interface SseOptions {
  afterSeq: number
  heartbeatMs: number
  signal: AbortSignal
  /** Told the sequence of a stored event this build cannot validate, so it can be logged. */
  onUnreadable?: (seq: number) => void
  /** Confirm that the subscription still belongs to an active member. */
  revalidate?: () => Promise<boolean>
  /** Told once, when the stream ends, why it ended. */
  onClose?: (reason: SseCloseReason) => void
}

/**
 * Heartbeats between two catch-up reads a caught-up stream cannot talk its way out of.
 *
 * `caughtUp` answers from what THIS process has been told, which is everything on a single Node
 * process and inside a Durable Object, and not necessarily everything when several processes share
 * one database. This bounds how long such a stream can be behind without knowing it, at the cost of
 * one query per stream every hundred seconds or so instead of one every twenty.
 */
const CATCH_UP_EVERY_HEARTBEATS = 5

/**
 * Heartbeats a consumer may leave the drain parked on backpressure before the stream is dropped.
 *
 * `enqueue` never refuses, so a connection that has stopped being read — a suspended phone, a
 * half-open TCP connection a NAT forgot — is invisible from this side: the drain parks correctly,
 * but the slot, its heartbeat timer and its catch-up reads persist until the OS retransmit timer
 * gives up, which on Linux is fifteen to thirty minutes. Dropping it costs the client nothing it
 * cannot recover: it reconnects and resumes from its `Last-Event-ID`.
 */
const STALLED_HEARTBEATS_BEFORE_DROP = 3

/**
 * Consecutive membership checks that may fail or hang before the stream is dropped.
 *
 * A check that FAILS has learned nothing about the membership, only about the database, and D1 in
 * particular answers the odd query with a transient error. Dropping the stream on the first one
 * put a "connection lost" dialog in front of a player whose membership, server and connection were
 * all fine. A kick does not wait on this check — `LocalEventHub.close` hangs the stream up at once —
 * so tolerating one failure only delays the backstop by a catch-up cycle, and a check that answers
 * "not a member" still ends the stream immediately.
 */
const MEMBERSHIP_CHECK_FAILURES_BEFORE_DROP = 2

const encoder = new TextEncoder()

/**
 * Frames buffered for a consumer that is not keeping up, before the drain parks.
 *
 * The default of one would park after every single event, costing a round-trip per frame on a
 * healthy stream. A small window batches the common case and still bounds what one stalled
 * connection can hold.
 */
const STREAM_HIGH_WATER_MARK = 32

/**
 * A server-sent event stream over a persisted, sequence-numbered log.
 *
 * The log is the truth and the wake-up is only a hint: every wake drains the log from the last
 * delivered sequence, while every heartbeat also performs a catch-up drain. A notification lost
 * between persist and fan-out therefore costs at most one heartbeat rather than waiting for some
 * unrelated later event, and `Last-Event-ID` resumes exactly. One drain runs at a time per
 * connection.
 *
 * The drain respects backpressure. `enqueue` on a stream nobody is reading never refuses, it just
 * buffers, so a consumer that has stopped reading (a suspended phone, a half-open TCP connection)
 * would otherwise pull the whole event log into this process's memory, once per such connection.
 * Instead the drain parks on `pull` when the queue is full and resumes when the consumer reads
 * again; the log keeps the events in the meantime, which is the whole point of it being the truth.
 */
export function createSseResponse(source: EventStreamSource, options: SseOptions): Response {
  // An AbortSignal only dispatches an event for a future transition, and the caller can have done
  // authentication and resume work after its client went away. Such a request is answered before
  // anything is built: no subscription, no heartbeat, no abort listener that could never fire.
  if (options.signal.aborted) return abandonedSseResponse()
  const { onUnreadable } = options
  let lastSeq = options.afterSeq
  let draining: Promise<void> | null = null
  let wakeAgain = false
  let closed = false

  let shutdown: (reason: SseCloseReason) => void = () => {
    closed = true
  }

  /** Resolved by `pull` when the consumer has read enough to make room for more. */
  let demand: (() => void) | null = null

  const stream = new ReadableStream<Uint8Array>(
    {
      start(controller) {
        const send = (text: string) => {
          if (!closed) controller.enqueue(encoder.encode(text))
        }
        /** Wait until the consumer wants more, or the stream closes. */
        const awaitDemand = () =>
          new Promise<void>((resolve) => {
            if (closed || (controller.desiredSize ?? 1) > 0) return resolve()
            demand = resolve
          })
        const drain = async (force: boolean): Promise<void> => {
          let first = force
          while (!closed) {
            await awaitDemand()
            if (closed) return
            // Only the first page of a forced drain has to reach the log — and it does, because
            // `force` bypasses the source's memo rather than only its "nothing further" answer.
            // Once it has, the cursor is beyond whatever this process did not know about.
            const frames = await source.page(lastSeq, first)
            first = false
            if (frames.length === 0) return
            for (const frame of frames) {
              // A stored row this build cannot validate is skipped, not fatal. Failing the drain
              // errored the stream, and the client reconnected with the same `Last-Event-ID`, read
              // the same row and failed again, for good — and a payload reshaped by a server
              // upgrade is a protocol change, which AGENTS.md says stored matches survive.
              if (frame.text === null) {
                onUnreadable?.(frame.seq)
              } else {
                send(frame.text)
              }
              lastSeq = Math.max(lastSeq, frame.seq)
            }
          }
        }
        /** True when a drain must reach the log rather than trust what this process was told. */
        let forceNext = false
        const wake = (force = false) => {
          if (closed) return
          forceNext ||= force
          if (draining) {
            wakeAgain = true
            return
          }
          draining = (async () => {
            do {
              wakeAgain = false
              const forced = forceNext
              forceNext = false
              await drain(forced)
            } while (wakeAgain && !closed)
          })()
            .catch((error) => {
              // A failed read (a database blip) ends this stream as a whole. Erroring the
              // controller alone left the heartbeat enqueueing into a dead stream and the
              // subscription counted against every cap until the socket happened to close.
              if (closed) return
              try {
                controller.error(error)
              } catch {
                // Already closed by the consumer.
              }
              shutdown('read_failed')
            })
            .finally(() => {
              draining = null
              // `wakeAgain` is only read by the loop above, and this runs microtasks after it
              // stopped: a wake that landed in between set the flag with nobody left to act on it,
              // and its event would wait for the next one in the match to carry it out. Re-enter
              // instead, which is a no-op when nothing arrived.
              if (wakeAgain && !closed) wake()
            })
        }

        // A thunk, not `shutdown` itself: the real one is assigned a few lines below and the
        // placeholder above it only flips `closed`, so passing the reference here would hand the
        // hub a close that leaves the heartbeat running and the subscription in place.
        const unsubscribe = source.subscribe(
          () => wake(),
          (reason) => {
            shutdown(reason)
          },
        )
        let beats = 0
        let stalledBeats = 0
        let validating = false
        /** The pending check already counted as failed by an overdue beat; see below. */
        let pendingCounted = false
        let failedChecks = 0
        /** A check that learned nothing; enough of them in a row and the stream is not trusted. */
        const checkFailed = () => {
          failedChecks += 1
          if (failedChecks >= MEMBERSHIP_CHECK_FAILURES_BEFORE_DROP) {
            shutdown('membership_unverified')
          }
        }
        const heartbeat = setInterval(() => {
          // A consumer that is not reading is not a consumer. The queue is full, so this frame
          // would only buffer; after a few beats of that the connection is gone in every way that
          // matters and the stream is dropped rather than held; see the constant above.
          if ((controller.desiredSize ?? 1) <= 0) {
            stalledBeats += 1
            if (stalledBeats >= STALLED_HEARTBEATS_BEFORE_DROP) shutdown('stalled')
            return
          }
          stalledBeats = 0
          send(`: ${SSE_HEARTBEAT_COMMENT}\n\n`)
          // Fan-out is deliberately best-effort. Re-read the durable log even when the socket is
          // healthy so a failed or process-local notification cannot strand this client forever —
          // but only when this process is not already certain there is nothing to read, because an
          // idle server was otherwise issuing one query per stream per heartbeat to find nothing.
          beats += 1
          const overdue = beats % CATCH_UP_EVERY_HEARTBEATS === 0
          if (overdue && options.revalidate) {
            // A lookup still pending a whole catch-up cycle later is a failed one. Waiting on it
            // without counting it left the stream never checked again for as long as that query
            // hung; it is left to settle rather than raced by a second one.
            if (validating) {
              pendingCounted = true
              checkFailed()
              if (closed) return
            } else {
              validating = true
              pendingCounted = false
              void Promise.resolve()
                .then(options.revalidate)
                .then(
                  (valid) => {
                    validating = false
                    if (!valid) shutdown('membership_revoked')
                    else failedChecks = 0
                  },
                  () => {
                    validating = false
                    // One lookup is one failure: a check that hung past a catch-up cycle was
                    // counted then, and its eventual rejection is the same failure, not a second.
                    if (!pendingCounted) checkFailed()
                  },
                )
            }
          }
          // The catch-up does not wait on the membership check: notifications keep delivering
          // while it is in flight anyway, so holding this back only delayed the read.
          if (overdue || !source.caughtUp(lastSeq)) wake(overdue)
        }, options.heartbeatMs)
        const aborted = () => shutdown('client_gone')
        shutdown = (reason) => {
          if (closed) return
          closed = true
          // A stream ended by `cancel`, a stall or its hub would otherwise leave this listener, and
          // everything its closure holds, on the request signal for as long as that lives.
          options.signal.removeEventListener('abort', aborted)
          clearInterval(heartbeat)
          unsubscribe()
          // Release a drain parked on backpressure, so it observes `closed` and stops.
          demand?.()
          demand = null
          try {
            controller.close()
          } catch {
            // Already errored, or cancelled by the consumer.
          }
          try {
            options.onClose?.(reason)
          } catch {
            // An observer that throws must not escape into a timer or abort a hub's close loop.
          }
        }
        options.signal.addEventListener('abort', aborted, { once: true })
        send(': connected\n\n')
        // The opening catch-up always reaches the log: a fresh stream has been told nothing.
        wake(true)
      },
      // The consumer read enough to want more: let a drain parked on backpressure continue.
      pull() {
        const resume = demand
        demand = null
        resume?.()
      },
      // The consumer went away (a dropped TCP connection surfaces here, not as an abort).
      cancel() {
        shutdown('client_gone')
      },
    },
    new CountQueuingStrategy({ highWaterMark: STREAM_HIGH_WATER_MARK }),
  )

  return new Response(stream, { headers: SSE_HEADERS })
}

const SSE_HEADERS = {
  'Content-Type': 'text/event-stream; charset=utf-8',
  'Cache-Control': 'no-cache, no-transform',
  Connection: 'keep-alive',
  'X-Accel-Buffering': 'no',
} as const

/**
 * The answer to a stream request whose client had gone before the stream could be built.
 *
 * Still the contract's 200 `text/event-stream`, since response validation refuses any status the
 * contract does not declare, but with no body: the stream ends before its first frame. Nobody is
 * reading it in the ordinary case; a caller that wants a request which only *looked* aborted to be
 * visible (a runtime passing a signal through wrongly would otherwise loop reconnects silently)
 * logs the fact where it decided to answer this, as `LocalEventHub` does.
 */
export function abandonedSseResponse(): Response {
  return new Response(null, { headers: SSE_HEADERS })
}

/**
 * One frame: the sequence number as its `id`, the contract's single event name, and the validated
 * event as JSON.
 *
 * The name is the one the contract declares rather than the event's own `type`. A frame named after
 * its type is a *named* event, which a stock `EventSource` delivers only to a listener registered
 * for that exact name and never to `onmessage` — so the browser client the contract describes would
 * hold an open stream and see nothing. Both of this repo's clients branch on the `type` inside the
 * payload, which is unaffected.
 */
export function formatEvent(event: PersistedEvent): string {
  const validated = validateSync(matchEventSchema, event)
  const data = JSON.stringify(validated)
  return `id: ${validated.seq}\nevent: ${MATCH_EVENT_SSE_NAME}\ndata: ${data}\n\n`
}

/** The same frame, or null when the stored row does not fit this build's schema. */
export function tryFormatEvent(event: PersistedEvent): string | null {
  try {
    return formatEvent(event)
  } catch {
    return null
  }
}

/**
 * Whether a stored row fits this build's schema, without building a frame from it.
 *
 * The REST read of the log uses this to leave an unreadable row out of its page. Asking
 * `tryFormatEvent` there meant serialising every event of the page once to throw the string away
 * and once more inside `c.json`.
 */
export function isReadableEvent(event: PersistedEvent): boolean {
  try {
    validateSync(matchEventSchema, event)
    return true
  } catch {
    return false
  }
}
