import { matchEventSchema, SSE_HEARTBEAT_COMMENT } from '@chaos-overlords/contracts'
import type { PersistedEvent } from '@chaos-overlords/kernel'
import { validateSync } from '@toad-contracts/core'

export interface EventStreamSource {
  /** Persisted events after `afterSeq`, ascending, at most one page. */
  listAfter(afterSeq: number): Promise<PersistedEvent[]>
  /**
   * Register a wake-up for new events; returns the unsubscribe.
   *
   * `close` ends this stream from the other side, which is how a revoked membership loses a stream
   * it already holds and how a player's stale stream is dropped to make room for their reconnect.
   * It is safe to call at any time and does nothing once the stream is closed.
   */
  subscribe(wake: () => void, close: () => void): () => void
}

export interface SseOptions {
  afterSeq: number
  heartbeatMs: number
  signal: AbortSignal
}

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
  let lastSeq = options.afterSeq
  let draining: Promise<void> | null = null
  let wakeAgain = false
  let closed = false

  let shutdown: () => void = () => {
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
        const awaitDemand = (controller: ReadableStreamDefaultController<Uint8Array>) =>
          new Promise<void>((resolve) => {
            if (closed || (controller.desiredSize ?? 1) > 0) return resolve()
            demand = resolve
          })
        const drain = async (): Promise<void> => {
          while (!closed) {
            await awaitDemand(controller)
            if (closed) return
            const events = await source.listAfter(lastSeq)
            if (events.length === 0) return
            for (const event of events) {
              send(formatEvent(event))
              lastSeq = Math.max(lastSeq, event.seq)
            }
          }
        }
        const wake = () => {
          if (closed) return
          if (draining) {
            wakeAgain = true
            return
          }
          draining = (async () => {
            do {
              wakeAgain = false
              await drain()
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
              shutdown()
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
        const unsubscribe = source.subscribe(wake, () => {
          shutdown()
        })
        const heartbeat = setInterval(() => {
          send(`: ${SSE_HEARTBEAT_COMMENT}\n\n`)
          // Fan-out is deliberately best-effort. Re-read the durable log even when the socket is
          // healthy so a failed or process-local notification cannot strand this client forever.
          wake()
        }, options.heartbeatMs)
        shutdown = () => {
          if (closed) return
          closed = true
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
        }
        options.signal.addEventListener('abort', shutdown, { once: true })
        send(': connected\n\n')
        wake()
      },
      // The consumer read enough to want more: let a drain parked on backpressure continue.
      pull() {
        const resume = demand
        demand = null
        resume?.()
      },
      // The consumer went away (a dropped TCP connection surfaces here, not as an abort).
      cancel() {
        shutdown()
      },
    },
    new CountQueuingStrategy({ highWaterMark: STREAM_HIGH_WATER_MARK }),
  )

  return new Response(stream, {
    headers: {
      'Content-Type': 'text/event-stream; charset=utf-8',
      'Cache-Control': 'no-cache, no-transform',
      Connection: 'keep-alive',
      'X-Accel-Buffering': 'no',
    },
  })
}

export function formatEvent(event: PersistedEvent): string {
  const validated = validateSync(matchEventSchema, event)
  return `id: ${validated.seq}\nevent: ${validated.type}\ndata: ${JSON.stringify(validated)}\n\n`
}
