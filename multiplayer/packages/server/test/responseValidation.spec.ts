import { handshakeContract } from '@chaos-overlords/contracts'
import { Hono } from 'hono'
import { describe, expect, it } from 'vitest'
import { validateContractResponse } from '../src/http/responseValidation'
import type { AppEnv } from '../src/http/types'

describe('contract response validation', () => {
  it('prevents a typed-but-invalid producer value from reaching the wire', async () => {
    const app = new Hono<AppEnv>()
    app.onError((error, c) => c.json({ error: error.name }, 500))
    app.use('*', validateContractResponse)
    app.get('/', (c) => {
      c.set('apiContract', handshakeContract)
      return c.json({ protocolVersion: '3' })
    })

    const response = await app.request('/')

    expect(response.status).toBe(500)
    expect(await response.json()).toEqual({ error: 'ContractResponseError' })
  })
})
