#!/usr/bin/env node

import type { Server } from 'node:http'
import { serve } from '@hono/node-server'
import { loadConfig } from './config.js'
import { buildNodeRuntime } from './container.js'
import { createLogger } from './logger.js'

const config = loadConfig()
const logger = createLogger(config.logLevel)
const runtime = await buildNodeRuntime(config)
const server = serve(
  { fetch: runtime.app.fetch, hostname: config.host, port: config.port },
  (info) => logger.info('listening', { host: info.address, port: info.port }),
)

server.on('error', (error) => {
  logger.error('server error', { error: String(error) })
  process.exitCode = 1
  server.close()
})
process.on('unhandledRejection', (reason) => {
  logger.error('unhandled rejection', { error: String(reason) })
})
process.on('uncaughtException', (error) => {
  logger.error('uncaught exception', { error: error.stack ?? String(error) })
  process.exitCode = 1
  shutdown()
})

/**
 * Event streams are open connections that never end on their own, so waiting for the server to go
 * quiet would wait forever. Idle connections are closed at once, and the streams still running are
 * given the grace period before they are cut; `close` then returns and the process can exit.
 *
 * `ServerType` widens to HTTP/2, which has no connection-closing methods; this server is always the
 * HTTP/1 one `serve` creates without a `createServer` override.
 */
const connections = server as Server

let shuttingDown = false
let exiting = false
function shutdown(): void {
  if (shuttingDown) return
  shuttingDown = true
  const exit = () => {
    // Both the deadline and `server.close` can reach this, and `runtime.close()` is not idempotent:
    // running it twice made the second `pool.end()` reject with "Called end on pool more than
    // once", which surfaced as an unhandled rejection during an otherwise clean shutdown.
    if (exiting) return
    exiting = true
    void runtime.close().finally(() => process.exit(process.exitCode ?? 0))
  }
  const deadline = setTimeout(() => {
    logger.warn('forcing open connections closed', { graceMs: config.shutdownGraceMs })
    connections.closeAllConnections()
    exit()
  }, config.shutdownGraceMs)
  deadline.unref()
  // End the event streams first. They never close on their own, so without this every SIGTERM on a
  // server with one connected player waited the whole grace period and then cut the sockets, which
  // clients see as a connection reset rather than as a stream to reconnect with their
  // `Last-Event-ID`. Closing them lets `server.close` return at once.
  runtime.closeStreams()
  connections.closeIdleConnections()
  server.close(() => {
    clearTimeout(deadline)
    exit()
  })
}

process.once('SIGINT', shutdown)
process.once('SIGTERM', shutdown)
