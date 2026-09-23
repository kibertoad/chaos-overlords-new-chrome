import { BUG_REPORT_LIMITS, LIMITS } from '@chaos-overlords/contracts'
import { Hono } from 'hono'
import { bodyLimit } from 'hono/body-limit'
import { cors } from 'hono/cors'
import type { ServerContainer } from './container'
import { handleError } from './http/errorHandler'
import {
  bearerAuth,
  bugReportRateLimited,
  matchCreationRateLimited,
  memberRateLimited,
  rateLimited,
  requestId,
} from './http/middleware'
import { validateContractResponse } from './http/responseValidation'
import type { AppEnv } from './http/types'
import { registerBugReportRoutes } from './routes/bugReports'
import { registerEventRoutes } from './routes/events'
import { registerMemberLobbyRoutes, registerPublicLobbyRoutes } from './routes/lobby'
import { registerProtocolRoutes } from './routes/protocol'
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
  if (container.config.corsOrigins.length > 0) {
    app.use(
      '*',
      cors({
        origin: [...container.config.corsOrigins],
        allowHeaders: ['Authorization', 'Content-Type', 'Last-Event-ID'],
        exposeHeaders: ['X-Request-Id'],
        maxAge: 600,
      }),
    )
  }

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

  // Run outside every contract route, then validate the completed successful response after its
  // handler has produced it. This closes the runtime gap left by TypeScript-only
  // handler checking: storage values and JSON columns are not trustworthy merely because typed.
  api.use('*', validateContractResponse)

  // A backstop sized to the largest legitimate body on the API (a bug report's base64 state), so a
  // route added without a cap of its own is still bounded. Every route below narrows it; Hono
  // composes middleware rather than replacing it, so the tightest cap that matches a path wins.
  api.use('*', bodyLimit({ maxSize: BUG_REPORT_LIMITS.stateBase64Bytes + SMALL_BODY }))

  // The unauthenticated handshake parses a body before any player identity exists, so it needs the
  // same address budget and small-body cap as the lobby doors it protects.
  api.use('/handshake', rateLimited, bodyLimit({ maxSize: SMALL_BODY }))
  registerProtocolRoutes(api)

  // The doors a stranger can knock on, throttled per client address.
  api.use('/matches', rateLimited, bodyLimit({ maxSize: LIMITS.gameSettingsBytes + SMALL_BODY }))
  // Creating is also charged to one budget shared by every caller; browsing the same path is not.
  api.on('POST', '/matches', matchCreationRateLimited)
  api.use('/matches/join', rateLimited, bodyLimit({ maxSize: SMALL_BODY }))
  // `use(path, ...)` matches the path verbatim, so neither of the two above covers this one. It is
  // a password-gated door like `/matches/join` and needs the same throttle and the same cap on a
  // body that is buffered before anything validates it.
  api.use('/matches/join-running', rateLimited, bodyLimit({ maxSize: SMALL_BODY }))
  // The third one. It is unauthenticated like the other two and far larger than either, so it gets
  // a budget of its own rather than borrowing the lobby's — see `RateLimiters.bugReport`.
  api.use(
    '/bug-reports',
    bugReportRateLimited,
    bodyLimit({ maxSize: BUG_REPORT_LIMITS.stateBase64Bytes + SMALL_BODY }),
  )

  registerPublicLobbyRoutes(api)
  registerBugReportRoutes(api)

  // `/:matchId/*` also matches the bare `/:matchId`, so this is the only mount: a second one for
  // the bare path would authenticate (a token lookup plus a match read) and charge the member's
  // rate limit twice on every request to it.
  api.use('/matches/:matchId/*', bearerAuth, memberRateLimited())
  // The two member routes with a JSON body that had no cap of their own. A member token costs one
  // unauthenticated `POST /matches`, so without these a stranger could make the process buffer and
  // parse a body of any size, a few hundred megabytes at a time, inside the same process that is
  // sealing every other match's turns.
  api.use(
    '/matches/:matchId/settings',
    bodyLimit({ maxSize: LIMITS.gameSettingsBytes + SMALL_BODY }),
  )
  api.use('/matches/:matchId/profile', bodyLimit({ maxSize: SMALL_BODY }))
  api.use('/matches/:matchId/players/:playerId/takeover-vote', bodyLimit({ maxSize: SMALL_BODY }))
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
