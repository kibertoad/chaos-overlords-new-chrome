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
