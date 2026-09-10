import { serve } from '@hono/node-server'
import { loadConfig } from './config'
import { buildNodeRuntime } from './container'

const config = loadConfig()
const runtime = await buildNodeRuntime(config)
const server = serve(
  { fetch: runtime.app.fetch, hostname: config.host, port: config.port },
  (info) => {
    console.log(JSON.stringify({ msg: 'listening', host: info.address, port: info.port }))
  },
)

const shutdown = () => {
  server.close(() => {
    runtime.close().finally(() => process.exit(0))
  })
}
process.once('SIGINT', shutdown)
process.once('SIGTERM', shutdown)
