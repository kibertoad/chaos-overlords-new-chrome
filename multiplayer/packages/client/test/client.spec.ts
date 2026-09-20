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

  /** And the caller's own cancellation still reaches the body once the stream is open. */
  it('keeps the caller signal attached after the headers arrive', async () => {
    const controller = new AbortController()
    let bodySignal: AbortSignal | undefined
    const client = new MultiplayerClient({
      baseUrl: 'https://example.invalid',
      token: 'token',
      requestTimeoutMs: 10_000,
      fetch: async (_input, init) => {
        bodySignal = init?.signal
        return new Response(': connected\n\n', {
          status: 200,
          headers: { 'content-type': 'text/event-stream' },
        })
      },
    })

    const events = client.match('m').streamOnce({ signal: controller.signal })
    await events.next()
    expect(bodySignal?.aborted).toBe(false)
    controller.abort()
    expect(bodySignal?.aborted).toBe(true)
    await events.return(undefined)
  })
})
