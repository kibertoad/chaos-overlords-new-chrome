import type { AddressInfo } from 'node:net'
import { defineHttpConformance } from '@chaos-overlords/conformance'
import { type ServerType, serve } from '@hono/node-server'
import { afterAll, beforeAll, describe } from 'vitest'
import { buildNodeRuntime, loadConfig, type NodeRuntime } from '../src'

/**
 * The facade over a real HTTP listener: the client SDK's fetch goes over TCP, so the streaming
 * headers, the abort path and the JSON round-trip are the production ones, not app.request().
 */
function defineFacadeSuite(name: string, databaseUrl: string | undefined): void {
  describe.skipIf(!databaseUrl)(name, () => {
    let runtime: NodeRuntime
    let server: ServerType
    let baseUrl = ''

    beforeAll(async () => {
      runtime = await buildNodeRuntime(
        loadConfig({
          DATABASE_URL: databaseUrl,
          PUBLIC_LISTING: 'true',
          LOG_LEVEL: 'error',
          RATE_LIMIT_PER_MINUTE: '10000',
        }),
      )
      server = serve({ fetch: runtime.app.fetch, hostname: '127.0.0.1', port: 0 })
      await new Promise<void>((resolve) => server.once('listening', () => resolve()))
      baseUrl = `http://127.0.0.1:${(server.address() as AddressInfo).port}`
    })

    afterAll(async () => {
      await new Promise<void>((resolve) => server.close(() => resolve()))
      await runtime.close()
    })

    defineHttpConformance({
      fetch: (input, init) => fetch(input.replace('http://conformance', baseUrl), init),
      publicListing: true,
    })
  })
}

defineFacadeSuite('node runtime over sqlite', 'sqlite::memory:')
defineFacadeSuite('node runtime over postgres', process.env.TEST_DATABASE_URL)
