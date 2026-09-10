import { SSE_HEARTBEAT_COMMENT } from '@chaos-overlords/contracts'
import type { PersistedEvent } from '@chaos-overlords/kernel'

export interface EventStreamSource {
  /** Persisted events after `afterSeq`, ascending, at most one page. */
  listAfter(afterSeq: number): Promise<PersistedEvent[]>
  /** Register a wake-up for new events; returns the unsubscribe. */
  subscribe(wake: () => void): () => void
}

export interface SseOptions {
  afterSeq: number
  heartbeatMs: number
  signal: AbortSignal
}

const encoder = new TextEncoder()

/**
 * A server-sent event stream over a persisted, sequence-numbered log.
 *
 * The log is the truth and the wake-up is only a hint: every wake drains the log from the last
 * delivered sequence, so a notification lost between persist and fan-out costs latency, never an
 * event, and `Last-Event-ID` resumes exactly. One drain runs at a time per connection.
 */
export function createSseResponse(source: EventStreamSource, options: SseOptions): Response {
  let lastSeq = options.afterSeq
  let draining: Promise<void> | null = null
  let wakeAgain = false
  let closed = false

  let shutdown: () => void = () => {
    closed = true
  }

  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      const send = (text: string) => {
        if (!closed) controller.enqueue(encoder.encode(text))
      }
      const drain = async (): Promise<void> => {
        while (!closed) {
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
            if (!closed) controller.error(error)
          })
          .finally(() => {
            draining = null
          })
      }

      const unsubscribe = source.subscribe(wake)
      const heartbeat = setInterval(
        () => send(`: ${SSE_HEARTBEAT_COMMENT}\n\n`),
        options.heartbeatMs,
      )
      shutdown = () => {
        if (closed) return
        closed = true
        clearInterval(heartbeat)
        unsubscribe()
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
    // The consumer went away (a dropped TCP connection surfaces here, not as an abort).
    cancel() {
      shutdown()
    },
  })

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
  return `id: ${event.seq}\nevent: ${event.type}\ndata: ${JSON.stringify(event)}\n\n`
}
