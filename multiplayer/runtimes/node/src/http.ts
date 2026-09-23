import type { Server } from 'node:http'
import type { AddressInfo } from 'node:net'
import { serve } from '@hono/node-server'
import type { NodeConfig } from './config.js'

type HttpLimits = Pick<
  NodeConfig,
  'host' | 'port' | 'maxConnections' | 'headersTimeoutMs' | 'requestTimeoutMs'
>

/**
 * The HTTP/1 server with its connection-level limits in place.
 *
 * The application's own limits (rate limits, body caps, stream caps) all run once a request has
 * arrived; these are the ones that bound a client which never finishes sending one. The deadlines
 * go to `createServer` rather than being assigned afterwards because that is where Node validates
 * them against each other.
 *
 * `ServerType` widens to HTTP/2, which has none of these knobs; this server is always the HTTP/1 one
 * `serve` creates without a `createServer` override.
 */
export function startHttpServer(
  fetch: (request: Request) => Response | Promise<Response>,
  limits: HttpLimits,
  onListening?: (info: AddressInfo) => void,
): Server {
  const server = serve(
    {
      fetch,
      hostname: limits.host,
      port: limits.port,
      serverOptions: {
        headersTimeout: limits.headersTimeoutMs,
        requestTimeout: limits.requestTimeoutMs,
      },
    },
    onListening,
  ) as Server
  server.maxConnections = limits.maxConnections
  return server
}
