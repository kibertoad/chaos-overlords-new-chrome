import { describe, expect, it } from 'vitest'
import { parseEventStream } from '../src'

function bodyOf(chunks: string[]): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder()
  return new ReadableStream({
    start(controller) {
      for (const chunk of chunks) controller.enqueue(encoder.encode(chunk))
      controller.close()
    },
  })
}

const eventAt = (seq: number) => ({
  seq,
  matchId: 'm',
  type: 'turn.opened',
  payload: { turn: seq },
  createdAt: 'x',
})
const frameOf = (seq: number, newline = '\n') =>
  `id: ${seq}${newline}event: turn.opened${newline}data: ${JSON.stringify(eventAt(seq))}${newline}${newline}`

describe('parseEventStream', () => {
  it('reassembles frames split across chunks and skips keepalives', async () => {
    const event = {
      seq: 3,
      matchId: 'm',
      type: 'turn.opened',
      payload: { turn: 1 },
      createdAt: 'x',
    }
    const text = `: connected\n\n: keepalive\n\nid: 3\nevent: turn.opened\ndata: ${JSON.stringify(event)}\n\n`
    const chunks = [text.slice(0, 20), text.slice(20, 60), text.slice(60)]
    const seen = []
    for await (const parsed of parseEventStream(bodyOf(chunks))) seen.push(parsed)
    expect(seen).toEqual([event])
  })

  it('accepts CRLF frames, which the event-stream format allows', async () => {
    const seen = []
    for await (const parsed of parseEventStream(bodyOf([frameOf(1, '\r\n')]))) seen.push(parsed)
    expect(seen).toEqual([eventAt(1)])
  })

  it('cancels the body when the consumer stops reading early', async () => {
    let cancelled = false
    const body = new ReadableStream<Uint8Array>({
      start(controller) {
        controller.enqueue(new TextEncoder().encode(frameOf(1) + frameOf(2)))
      },
      cancel() {
        cancelled = true
      },
    })
    for await (const parsed of parseEventStream(body)) {
      expect(parsed.seq).toBe(1)
      break
    }
    // Releasing the lock alone would leave the connection open until a collector noticed.
    expect(cancelled).toBe(true)
  })
})
