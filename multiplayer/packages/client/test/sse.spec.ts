import { describe, expect, it } from 'vitest'
import { MultiplayerClient, parseEventStream, StreamIdleError, StreamOutageError } from '../src'
import { isFatalStreamError } from '../src/client'

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
  payload: { turn: seq, deadlineAt: null },
  createdAt: '2026-09-18T12:00:00.000Z',
})
const frameOf = (seq: number, newline = '\n') =>
  `id: ${seq}${newline}event: message${newline}data: ${JSON.stringify(eventAt(seq))}${newline}${newline}`

describe('parseEventStream', () => {
  it('reassembles frames split across chunks and skips keepalives', async () => {
    const event = {
      seq: 3,
      matchId: 'm',
      type: 'turn.opened',
      payload: { turn: 1, deadlineAt: null },
      createdAt: '2026-09-18T12:00:00.000Z',
    }
    const text = `: connected\n\n: keepalive\n\nid: 3\nevent: message\ndata: ${JSON.stringify(event)}\n\n`
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

describe('parseEventStream idle deadline', () => {
  /**
   * A half-open connection delivers neither an error nor an end. Without a deadline the read
   * waits on the operating system's keepalive, tens of minutes away, while every seal is missed.
   */
  it('abandons a connection that carries nothing for the idle window', async () => {
    let cancelled = false
    const body = new ReadableStream<Uint8Array>({
      start(controller) {
        controller.enqueue(new TextEncoder().encode(frameOf(1)))
        // ...and then nothing, ever.
      },
      cancel() {
        cancelled = true
      },
    })
    const seen: number[] = []
    await expect(async () => {
      for await (const event of parseEventStream(body, { idleTimeoutMs: 30 })) {
        seen.push(event.seq)
      }
    }).rejects.toBeInstanceOf(StreamIdleError)
    expect(seen).toEqual([1])
    expect(cancelled).toBe(true)
  })

  it('counts a keepalive comment as activity', async () => {
    const encoder = new TextEncoder()
    const body = new ReadableStream<Uint8Array>({
      async start(controller) {
        for (let i = 0; i < 4; i += 1) {
          await new Promise((resolve) => setTimeout(resolve, 15))
          controller.enqueue(encoder.encode(': keepalive\n\n'))
        }
        controller.enqueue(encoder.encode(frameOf(1)))
        controller.close()
      },
    })
    const seen: number[] = []
    for await (const event of parseEventStream(body, { idleTimeoutMs: 40 })) seen.push(event.seq)
    expect(seen).toEqual([1])
  })
})

describe('MatchHandle.stream outage budget', () => {
  const sseResponse = (text: string) =>
    new Response(text, { status: 200, headers: { 'content-type': 'text/event-stream' } })

  /**
   * A server that accepts the connection and closes it at once is still an outage: the budget is
   * reset by an event arriving, never by the connection alone, so the loop cannot live forever.
   */
  it('gives up after the outage window when connections deliver nothing', async () => {
    let connections = 0
    const client = new MultiplayerClient({
      baseUrl: 'https://example.invalid',
      token: 'cop_x',
      fetch: async () => {
        connections += 1
        return sseResponse(': connected\n\n')
      },
    })
    const reconnects: number[] = []
    await expect(async () => {
      for await (const _ of client.match('m').stream({
        reconnectDelayMs: 5,
        maxReconnectDelayMs: 5,
        maxOutageMs: 60,
        onReconnect: (_error, attempt) => reconnects.push(attempt),
      })) {
        // consume
      }
    }).rejects.toBeInstanceOf(StreamOutageError)
    expect(connections).toBeGreaterThan(1)
    expect(reconnects.length).toBeGreaterThan(0)
  })

  it('resets the outage clock when an event arrives, and resumes after its seq', async () => {
    const afters: string[] = []
    let connections = 0
    const client = new MultiplayerClient({
      baseUrl: 'https://example.invalid',
      token: 'cop_x',
      fetch: async (_input, init) => {
        connections += 1
        afters.push(
          String((init?.headers as Record<string, string> | undefined)?.['Last-Event-ID']),
        )
        if (connections <= 2) return sseResponse(frameOf(connections))
        return sseResponse(': connected\n\n')
      },
    })
    const seen: number[] = []
    await expect(async () => {
      for await (const event of client.match('m').stream({
        reconnectDelayMs: 5,
        maxReconnectDelayMs: 5,
        maxOutageMs: 60,
      })) {
        seen.push(event.seq)
      }
    }).rejects.toBeInstanceOf(StreamOutageError)
    expect(seen).toEqual([1, 2])
    expect(afters.slice(0, 3)).toEqual(['0', '1', '2'])
  })

  it('resets the outage clock after sustained keepalives without match events', async () => {
    let connections = 0
    const encoder = new TextEncoder()
    const client = new MultiplayerClient({
      baseUrl: 'https://example.invalid',
      token: 'cop_x',
      fetch: async () => {
        connections += 1
        if (connections === 1) return sseResponse(': connected\n\n')
        if (connections === 2) {
          const body = new ReadableStream<Uint8Array>({
            async start(controller) {
              controller.enqueue(encoder.encode(': connected\n\n'))
              await new Promise((resolve) => setTimeout(resolve, 40))
              controller.enqueue(encoder.encode(': keepalive\n\n'))
              await new Promise((resolve) => setTimeout(resolve, 40))
              controller.close()
            },
          })
          return new Response(body, {
            status: 200,
            headers: { 'content-type': 'text/event-stream' },
          })
        }
        return sseResponse(frameOf(1))
      },
    })
    const seen: number[] = []
    for await (const event of client.match('m').stream({
      reconnectDelayMs: 1,
      maxReconnectDelayMs: 1,
      maxOutageMs: 60,
      idleTimeoutMs: 100,
    })) {
      seen.push(event.seq)
      break
    }
    expect(connections).toBe(3)
    expect(seen).toEqual([1])
  })
})

describe('isFatalStreamError', () => {
  /**
   * The reconnect loop is itself what spends the rate limit budget, so treating 429 as fatal would
   * have the recovery path destroy the stream it exists to recover.
   */
  it('retries refusals about the attempt and gives up on refusals about the membership', () => {
    for (const status of [401, 403, 404, 409, 422]) {
      expect(isFatalStreamError(status)).toBe(true)
    }
    for (const status of [408, 425, 429, 500, 502, 503]) {
      expect(isFatalStreamError(status)).toBe(false)
    }
  })
})

describe('parseEventStream framing', () => {
  const frame = (id: number, seq: number) =>
    `id: ${id}\nevent: message\ndata: ${JSON.stringify({ seq, matchId: 'm', type: 'turn.opened', payload: { turn: 1, deadlineAt: null }, createdAt: '2026-09-18T12:00:00.000Z' })}\n\n`

  const singleChunkBody = (text: string) =>
    new ReadableStream<Uint8Array>({
      start(controller) {
        controller.enqueue(new TextEncoder().encode(text))
        controller.close()
      },
    })

  it('refuses a frame whose id disagrees with its payload', async () => {
    const good = []
    for await (const event of parseEventStream(singleChunkBody(frame(4, 4)))) good.push(event)
    expect(good.map((event) => event.seq)).toEqual([4])

    // Resuming from the wrong number would skip events in silence, so the frame is refused instead.
    await expect(async () => {
      for await (const _ of parseEventStream(singleChunkBody(frame(9, 4)))) {
        // consume
      }
    }).rejects.toThrow(/disagrees/)
  })

  /**
   * The handshake has already agreed a protocol version, so a frame under another name is a mangled
   * or foreign one rather than a newer server — and reading its payload as a match event anyway
   * would move the resume cursor on something this client cannot claim to understand.
   */
  it('refuses a frame named anything but the contracted event name', async () => {
    const renamed = frameOf(5).replace('event: message', 'event: turn.opened')
    await expect(async () => {
      for await (const _ of parseEventStream(singleChunkBody(renamed))) {
        // consume
      }
    }).rejects.toThrow(/not the contracted 'message'/)
  })

  /** No name at all means `message`, as the event-stream format says. */
  it('accepts a frame that names no event', async () => {
    const unnamed = frameOf(6).replace('event: message\n', '')
    const seen = []
    for await (const parsed of parseEventStream(singleChunkBody(unnamed))) seen.push(parsed)
    expect(seen).toEqual([eventAt(6)])
  })

  it('refuses an event whose JSON does not satisfy the shared event contract', async () => {
    const malformed = `id: 4\nevent: message\ndata: ${JSON.stringify({ ...eventAt(4), payload: { turn: '4', deadlineAt: null } })}\n\n`
    await expect(async () => {
      for await (const _ of parseEventStream(singleChunkBody(malformed))) {
        // consume
      }
    }).rejects.toThrow()
  })
})
