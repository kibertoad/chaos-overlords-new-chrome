import type { PersistedEvent } from '@chaos-overlords/kernel'
import { describe, expect, it } from 'vitest'
import { createSseResponse, type EventStreamSource } from '../src'

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

/** A log of `total` events, which records how far a reader has actually pulled it. */
function logOf(total: number) {
  const reads: number[] = []
  const source: EventStreamSource = {
    listAfter: async (afterSeq) => {
      reads.push(afterSeq)
      return Array.from({ length: Math.min(50, total - afterSeq) }, (_, i) =>
        event(afterSeq + i + 1),
      )
    },
    subscribe: () => () => {},
  }
  return { source, reads }
}

describe('createSseResponse backpressure', () => {
  it('recovers a durable event whose fan-out wake was lost', async () => {
    const durable: PersistedEvent[] = []
    const source: EventStreamSource = {
      listAfter: async (afterSeq) => durable.filter((item) => item.seq > afterSeq),
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
      listAfter: async () => {
        throw new Error('database unavailable')
      },
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
