import { handshakeContract } from '@chaos-overlords/contracts'
import { NotFoundError } from '@chaos-overlords/kernel'
import { Hono } from 'hono'
import { describe, expect, it } from 'vitest'
import { handleError } from '../src/http/errorHandler'
import { validateContractResponse } from '../src/http/responseValidation'
import type { AppEnv } from '../src/http/types'

describe('contract response validation', () => {
  it('does not cache a validation failure after the handler set an immutable header', async () => {
    const app = new Hono<AppEnv>()
    app.onError(handleError)
    app.use('*', validateContractResponse)
    app.get('/', (c) => {
      c.set('requestId', 'req-test')
      c.set('apiContract', handshakeContract)
      c.header('Cache-Control', 'private, max-age=31536000, immutable')
      return c.json({ protocolVersion: 'invalid' })
    })

    const response = await app.request('/')

    expect(response.status).toBe(500)
    expect(response.headers.get('cache-control')).toBe('no-store')
    expect(await response.json()).toMatchObject({
      error: { code: 'internal', requestId: 'req-test' },
    })
  })

  it('does not cache a refusal thrown after the handler set an immutable header', async () => {
    const app = new Hono<AppEnv>()
    app.onError(handleError)
    app.get('/', (c) => {
      c.set('requestId', 'req-test')
      c.header('Cache-Control', 'private, max-age=31536000, immutable')
      throw new NotFoundError('Turn not sealed', { reason: 'turn_not_sealed' })
    })

    const response = await app.request('/')

    expect(response.status).toBe(404)
    expect(response.headers.get('cache-control')).toBe('no-store')
  })

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
