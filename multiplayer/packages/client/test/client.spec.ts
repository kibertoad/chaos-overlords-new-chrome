import { handshakeContract } from '@chaos-overlords/contracts'
import { describe, expect, it } from 'vitest'
import { MultiplayerClient } from '../src'

describe('MultiplayerClient contract responses', () => {
  it('refuses successful JSON that does not satisfy the shared response schema', async () => {
    const client = new MultiplayerClient({
      baseUrl: 'https://example.invalid',
      fetch: async () =>
        new Response(JSON.stringify({ protocolVersion: '3' }), {
          status: 200,
          headers: { 'content-type': 'application/json' },
        }),
    })

    await expect(
      client.call(handshakeContract, handshakeContract.pathResolver(), {
        protocolVersion: 3,
      }),
    ).rejects.toThrow()
  })
})

describe('MultiplayerClient stream connect phase', () => {
  /**
   * A stream is exempt from the request timeout because it is meant to stay open. That exemption
   * used to cover getting it open too: against a host that drops SYNs rather than refusing them,
   * each attempt cost the operating system's own connect timeout — often two minutes — so a
   * five-minute outage budget bought two attempts instead of the dozens it is sized for.
   */
  it('gives up on a connection that never produces headers', async () => {
    const client = new MultiplayerClient({
      baseUrl: 'https://example.invalid',
      token: 'token',
      requestTimeoutMs: 40,
      // A host that accepts and then says nothing at all: the promise settles only when aborted.
      fetch: (_input, init) =>
        new Promise((_resolve, reject) => {
          init?.signal?.addEventListener('abort', () => reject(init.signal?.reason), { once: true })
        }),
    })

    const started = Date.now()
    await expect(client.match('m').streamOnce().next()).rejects.toThrow(/did not open within/)
    expect(Date.now() - started).toBeLessThan(2_000)
  })

  /** `0` disables the deadline, the same way it does for a plain request. */
  it('does not arm the connect deadline when the timeout is disabled', async () => {
    const caller = new AbortController()
    let connectSignal: AbortSignal | undefined
    const client = new MultiplayerClient({
      baseUrl: 'https://example.invalid',
      token: 'token',
      requestTimeoutMs: 0,
      fetch: async (_input, init) => {
        connectSignal = init?.signal ?? undefined
        return openStreamResponse(init?.signal)
      },
    })

    const events = client.match('m').streamOnce({ signal: caller.signal })
    const first = events.next()
    // An armed timer fires on the next macrotask at `0`, which would abort the stream before it
    // could ever carry anything.
    await new Promise((resolve) => setTimeout(resolve, 5))
    expect(connectSignal?.aborted).toBe(false)
    caller.abort()
    await expect(first).rejects.toThrow()
  })

  /** And the caller's own cancellation still reaches the body of a stream that is open. */
  it('keeps the caller signal attached after the headers arrive', async () => {
    const caller = new AbortController()
    let bodySignal: AbortSignal | undefined
    const client = new MultiplayerClient({
      baseUrl: 'https://example.invalid',
      token: 'token',
      requestTimeoutMs: 10_000,
      fetch: async (_input, init) => {
        bodySignal = init?.signal ?? undefined
        return openStreamResponse(init?.signal)
      },
    })

    const events = client.match('m').streamOnce({ signal: caller.signal })
    const first = events.next()
    await new Promise((resolve) => setTimeout(resolve, 5))
    expect(bodySignal?.aborted).toBe(false)
    caller.abort()
    expect(bodySignal?.aborted).toBe(true)
    await expect(first).rejects.toThrow()
  })

  /**
   * `stream` opens one connection per reconnect against a signal the caller owns for the life of
   * the stream, so a connection that forwards the caller's abort has to take the listener back off
   * when it ends. Left attached, a long outage accumulated one listener and one `AbortController`
   * per attempt, and Node starts warning about a leak at ten.
   */
  it('detaches the caller signal when a connection ends', async () => {
    const controller = new AbortController()
    let attached = 0
    const signal = controller.signal
    const add = signal.addEventListener.bind(signal)
    const remove = signal.removeEventListener.bind(signal)
    signal.addEventListener = ((...args: Parameters<typeof add>) => {
      attached += 1
      return add(...args)
    }) as typeof add
    signal.removeEventListener = ((...args: Parameters<typeof remove>) => {
      attached -= 1
      return remove(...args)
    }) as typeof remove

    const client = new MultiplayerClient({
      baseUrl: 'https://example.invalid',
      token: 'token',
      requestTimeoutMs: 10_000,
      fetch: async () =>
        new Response(': connected\n\n', {
          status: 200,
          headers: { 'content-type': 'text/event-stream' },
        }),
    })

    for (let attempt = 0; attempt < 12; attempt += 1) {
      // The server closes at once, which is exactly what the reconnect loop drives through.
      for await (const _event of client.match('m').streamOnce({ signal })) {
        // The body carries only a keepalive comment, so there is nothing to consume.
      }
    }
    expect(attached).toBe(0)
  })
})

describe('MultiplayerClient stream reconnects', () => {
  /** The attempt numbers `stream` reports across `rounds` connections that each deliver `body`. */
  async function attemptsAgainst(body: string, rounds: number): Promise<number[]> {
    const controller = new AbortController()
    const attempts: number[] = []
    const client = new MultiplayerClient({
      baseUrl: 'https://example.invalid',
      token: 'token',
      fetch: async () =>
        new Response(body, { status: 200, headers: { 'content-type': 'text/event-stream' } }),
    })
    const events = client.match('m').stream({
      signal: controller.signal,
      reconnectDelayMs: 1,
      maxReconnectDelayMs: 1,
      maxOutageMs: 0,
      onReconnect: (_error, attempt) => {
        attempts.push(attempt)
        if (attempts.length >= rounds) controller.abort()
      },
    })
    for await (const _event of events) {
      // Neither body carries an event.
    }
    return attempts
  }

  /** A proxy that passes the opening comment and drops is an outage, and is backed off from. */
  it('keeps counting attempts against connections that end after the opening comment', async () => {
    expect(await attemptsAgainst(': connected\n\n', 3)).toEqual([1, 2, 3])
  })

  /**
   * The first keepalive after the opening comment proves the connection, however soon it arrives.
   * The server's heartbeat timer starts before the client has the headers, so a timed threshold
   * missed that keepalive whenever the headers were slow.
   */
  it('resets the attempt counter once a keepalive follows the opening comment', async () => {
    expect(await attemptsAgainst(': connected\n\n: keepalive\n\n', 3)).toEqual([1, 1, 1])
  })
})

/**
 * A stream response that stays open until its request signal aborts, the way a real one does.
 *
 * `fetch` wires the two together; a hand-built `Response` does not, and a body nothing can end
 * leaves the reader waiting out its idle timeout at the end of a test.
 */
function openStreamResponse(signal: AbortSignal | null | undefined): Response {
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      controller.enqueue(new TextEncoder().encode(': connected\n\n'))
      signal?.addEventListener('abort', () => controller.error(signal.reason), { once: true })
    },
  })
  return new Response(body, { status: 200, headers: { 'content-type': 'text/event-stream' } })
}
