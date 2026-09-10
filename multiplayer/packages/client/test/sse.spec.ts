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
})
