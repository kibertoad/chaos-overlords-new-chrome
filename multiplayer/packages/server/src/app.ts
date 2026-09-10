import { LIMITS } from '@chaos-overlords/contracts'
import { Hono } from 'hono'
import { bodyLimit } from 'hono/body-limit'
import type { ServerContainer } from './container'
import { handleError } from './http/errorHandler'
import { bearerAuth, memberRateLimited, rateLimited, requestId } from './http/middleware'
import type { AppEnv } from './http/types'
import { registerEventRoutes } from './routes/events'
import { registerMemberLobbyRoutes, registerPublicLobbyRoutes } from './routes/lobby'
import { registerSnapshotRoutes } from './routes/snapshots'
import { registerTurnRoutes } from './routes/turns'

export const API_PREFIX = '/api/v1'

const SMALL_BODY = 16 * 1024

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
  app.route(API_PREFIX, apiRoutes())
  return app
}

/**
 * The API, mounted from the contracts.
 *
 * **Registration order is load-bearing.** Hono composes every matching middleware and the first
 * matching handler in the order they were registered, and a handler ends the chain. The
 * unauthenticated doors are therefore registered before the `/matches/:matchId/*` auth mount, which
 * would otherwise also match `/matches/join` and demand a token for it. Each body limit is
 * registered before the route it guards, so it runs before the contract validator reads the body —
 * a limit checked after the body has been parsed guards nothing.
 */
function apiRoutes(): Hono<AppEnv> {
  const api = new Hono<AppEnv>()

  // The two doors a stranger can knock on, throttled per client address.
  api.use('/matches', rateLimited, bodyLimit({ maxSize: LIMITS.gameSettingsBytes + SMALL_BODY }))
  api.use('/matches/join', rateLimited, bodyLimit({ maxSize: SMALL_BODY }))

  registerPublicLobbyRoutes(api)

  // `/:matchId/*` also matches the bare `/:matchId`, so this is the only mount: a second one for
  // the bare path would authenticate (a token lookup plus a match read) and charge the member's
  // rate limit twice on every request to it.
  api.use('/matches/:matchId/*', bearerAuth, memberRateLimited())
  api.use('/matches/:matchId/turns/:turn/orders', bodyLimit({ maxSize: LIMITS.ordersBytes }))
  api.use('/matches/:matchId/turns/:turn/report', bodyLimit({ maxSize: SMALL_BODY }))
  // A snapshot is a megabyte, so uploads carry their own tighter budget on top of the member one.
  api.use(
    '/matches/:matchId/snapshots',
    memberRateLimited('upload'),
    bodyLimit({ maxSize: LIMITS.snapshotBase64Bytes + SMALL_BODY }),
  )

  registerMemberLobbyRoutes(api)
  registerTurnRoutes(api)
  registerSnapshotRoutes(api)
  registerEventRoutes(api)
  return api
}
