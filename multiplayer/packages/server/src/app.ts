import { Hono } from 'hono'
import type { ServerContainer } from './container'
import { handleError } from './http/errorHandler'
import { requestId } from './http/middleware'
import type { AppEnv } from './http/types'
import { eventRoutes } from './routes/events'
import { matchRoutes } from './routes/matches'
import { snapshotRoutes } from './routes/snapshots'
import { turnRoutes } from './routes/turns'

export const API_PREFIX = '/api/v1'

/** The application both runtime facades serve; only the container differs between them. */
export function createApp(container: ServerContainer): Hono<AppEnv> {
  const app = new Hono<AppEnv>()
  app.onError(handleError)
  app.use('*', async (c, next) => {
    c.set('container', container)
    await next()
  })
  app.use('*', requestId)

  app.get('/health', (c) => c.json({ ok: true }))

  const matches = matchRoutes()
  matches.route('/:matchId/turns', turnRoutes())
  matches.route('/:matchId/snapshots', snapshotRoutes())
  matches.route('/:matchId', eventRoutes())
  app.route(`${API_PREFIX}/matches`, matches)
  return app
}
