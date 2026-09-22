import { MATCH_EVENT_SSE_NAME } from '@chaos-overlords/contracts'
import type { PersistedEvent } from '@chaos-overlords/kernel'
import { describe, expect, it, vi } from 'vitest'
import { createSseResponse, type EventStreamSource, formatEvent } from '../src'

const event = (seq: number): PersistedEvent =>
  ({
    seq,
    matchId: 'm',
    type: 'turn.opened',
    payload: { turn: seq, deadlineAt: null },
    createdAt: '2026-01-01T00:00:00.000Z',
  }) as PersistedEvent

/** Let queued microtasks and the stream's own pull scheduling run. */
const settle = () => new Promise((resolve) => setTimeout(resolve, 50))

const frames = (events: PersistedEvent[]) =>
  events.map((item) => ({ seq: item.seq, text: formatEvent(item) }))

/** A log of `total` events, which records how far a reader has actually pulled it. */
function logOf(total: number) {
  const reads: number[] = []
  const source: EventStreamSource = {
    page: async (afterSeq) => {
      reads.push(afterSeq)
      return frames(
        Array.from({ length: Math.min(50, total - afterSeq) }, (_, i) => event(afterSeq + i + 1)),
      )
    },
    caughtUp: () => false,
    subscribe: () => () => {},
  }
  return { source, reads }
}

describe('createSseResponse lifecycle', () => {
  /** A source that records what the response asked of it. */
  function recordingSource() {
    const calls = { pages: 0, subscribed: 0, unsubscribed: 0 }
    const source: EventStreamSource = {
      page: async () => {
        calls.pages += 1
        return []
      },
      caughtUp: () => true,
      subscribe: () => {
        calls.subscribed += 1
        return () => {
          calls.unsubscribed += 1
        }
      },
    }
    return { source, calls }
  }

  /**
   * An AbortSignal only dispatches for a future transition, and the route can do its
   * authentication after the client has gone. Such a request must build nothing rather than build a
   * stream and tear it down again.
   */
  it('builds nothing for a request whose signal has already aborted', async () => {
    const { source, calls } = recordingSource()
    const signal = AbortSignal.abort()
    const added = vi.spyOn(signal, 'addEventListener')

    const response = createSseResponse(source, { afterSeq: 0, heartbeatMs: 10, signal })

    expect(response.status).toBe(200)
    expect(response.headers.get('content-type')).toContain('text/event-stream')
    // No `: connected` frame: the stream is over before it began.
    expect(await response.text()).toBe('')
    await settle()
    expect(calls).toEqual({ pages: 0, subscribed: 0, unsubscribed: 0 })
    expect(added).not.toHaveBeenCalled()
  })

  it('takes its abort listener off the signal when the stream ends another way', async () => {
    const { source, calls } = recordingSource()
    const controller = new AbortController()
    const removed = vi.spyOn(controller.signal, 'removeEventListener')
    const response = createSseResponse(source, {
      afterSeq: 0,
      heartbeatMs: 60_000,
      signal: controller.signal,
    })
    const reader = (response.body as ReadableStream<Uint8Array>).getReader()
    await reader.read()

    // The consumer going away, not the request signal, is what ends this one.
    await reader.cancel()

    expect(calls.unsubscribed).toBe(1)
    expect(removed).toHaveBeenCalledWith('abort', expect.any(Function))
  })
})

describe('createSseResponse backpressure', () => {
  it('recovers a durable event whose fan-out wake was lost', async () => {
    const durable: PersistedEvent[] = []
    const source: EventStreamSource = {
      page: async (afterSeq) => frames(durable.filter((item) => item.seq > afterSeq)),
      // Unsure, as a source that cannot see every writer must answer: the periodic catch-up is
      // the whole point of this test.
      caughtUp: () => false,
      // Deliberately discard the wake callback, as a failed cross-process notification would.
      subscribe: () => () => {},
    }
    const controller = new AbortController()
    const response = createSseResponse(source, {
      afterSeq: 0,
      heartbeatMs: 10,
      signal: controller.signal,
    })
    const reader = (response.body as ReadableStream<Uint8Array>).getReader()
    const decoder = new TextDecoder()
    await reader.read() // connected comment and initial empty catch-up
    durable.push(event(1))

    let text = ''
    for (let i = 0; i < 10 && !text.includes('id: 1'); i += 1) {
      const frame = await reader.read()
      if (frame.value) text += decoder.decode(frame.value)
    }
    expect(text).toContain('id: 1')
    controller.abort()
    await reader.cancel().catch(() => {})
  })

  /**
   * `enqueue` never refuses; it buffers. A consumer that has stopped reading — a suspended phone, a
   * half-open TCP connection — would otherwise pull an entire event log into this process's memory,
   * once per such connection. The drain has to park instead and let the log hold the events.
   */
  it('stops draining for a consumer that is not reading', async () => {
    const { source, reads } = logOf(10_000)
    const controller = new AbortController()
    const response = createSseResponse(source, {
      afterSeq: 0,
      heartbeatMs: 60_000,
      signal: controller.signal,
    })
    const body = response.body as ReadableStream<Uint8Array>
    const reader = body.getReader()

    // Read one chunk and then stop, as a stalled consumer would.
    await reader.read()
    await settle()
    const parked = reads.length
    await settle()
    expect(reads.length).toBe(parked)
    // The drain read one page and stopped, nowhere near the whole log.
    expect(parked).toBe(1)

    // Reading the buffered frames back below the high-water mark releases the drain: nothing was
    // lost, only deferred, and the log still holds it.
    for (let i = 0; i < 40; i += 1) await reader.read()
    await settle()
    expect(reads.length).toBeGreaterThan(parked)

    controller.abort()
    await reader.cancel().catch(() => {})
  })

  it('delivers every event in order to a consumer that keeps up', async () => {
    const { source } = logOf(120)
    const controller = new AbortController()
    const response = createSseResponse(source, {
      afterSeq: 0,
      heartbeatMs: 60_000,
      signal: controller.signal,
    })
    const reader = (response.body as ReadableStream<Uint8Array>).getReader()
    const decoder = new TextDecoder()
    const seqs: number[] = []
    while (seqs.length < 120) {
      const { value, done } = await reader.read()
      if (done) break
      for (const line of decoder.decode(value).split('\n')) {
        if (line.startsWith('id: ')) seqs.push(Number(line.slice(4)))
      }
    }
    expect(seqs).toEqual(Array.from({ length: 120 }, (_, i) => i + 1))
    controller.abort()
    await reader.cancel().catch(() => {})
  })

  /**
   * A read that fails (a database blip) must end the whole stream: heartbeat stopped, subscription
   * released, so the caps see the slot free and nothing keeps enqueueing into a dead controller.
   */
  it('ends the stream and releases its subscription when a read fails', async () => {
    let unsubscribed = 0
    const source: EventStreamSource = {
      page: async () => {
        throw new Error('database unavailable')
      },
      caughtUp: () => false,
      subscribe: () => () => {
        unsubscribed += 1
      },
    }
    const response = createSseResponse(source, {
      afterSeq: 0,
      heartbeatMs: 10,
      signal: new AbortController().signal,
    })
    const reader = (response.body as ReadableStream<Uint8Array>).getReader()
    await expect(
      (async () => {
        for (let i = 0; i < 20; i += 1) if ((await reader.read()).done) return
      })(),
    ).rejects.toThrow(/database unavailable/)
    await settle()
    expect(unsubscribed).toBe(1)
    // Heartbeats after the failure must not throw inside their timer; give a few a chance to run.
    await settle()
  })

  /** A drain parked on backpressure must observe the shutdown, not sit on the log forever. */
  it('releases a parked drain when the stream closes', async () => {
    const { source, reads } = logOf(10_000)
    const controller = new AbortController()
    const response = createSseResponse(source, {
      afterSeq: 0,
      heartbeatMs: 60_000,
      signal: controller.signal,
    })
    const reader = (response.body as ReadableStream<Uint8Array>).getReader()
    await reader.read()
    await settle()
    controller.abort()
    await settle()
    const afterAbort = reads.length

    // The stream ends rather than hanging, and the released drain reads nothing more.
    let done = false
    for (let i = 0; i < 200 && !done; i += 1) done = (await reader.read()).done
    expect(done).toBe(true)
    expect(reads.length).toBe(afterAbort)
  })
})

describe('formatEvent', () => {
  /**
   * A frame named after the event's own `type` is a *named* SSE event, which a stock `EventSource`
   * delivers only to a listener registered for that exact name — never to `onmessage`. The contract
   * declares one name for the whole stream, and this is the frame that has to carry it.
   */
  it('frames every event under the one name the contract declares', () => {
    const frame = formatEvent(event(7))
    expect(frame).toContain(`event: ${MATCH_EVENT_SSE_NAME}`)
    expect(frame).not.toContain('event: turn.opened')
    expect(frame.startsWith('id: 7\n')).toBe(true)
    expect(frame.endsWith('\n\n')).toBe(true)
  })

  it('carries the validated event as the frame data', () => {
    const data = formatEvent(event(2))
      .split('\n')
      .find((line) => line.startsWith('data: '))
    expect(JSON.parse(data?.slice(6) ?? '')).toEqual(event(2))
  })
})
