import {
  createMatchRequestSchema,
  joinMatchRequestSchema,
  LIMITS,
} from '@chaos-overlords/contracts'
import { NotFoundError } from '@chaos-overlords/kernel'
import { Hono } from 'hono'
import { bodyLimit } from 'hono/body-limit'
import { parseBody, requireMember } from '../http/guards'
import { bearerAuth, memberRateLimited, rateLimited } from '../http/middleware'
import type { AppEnv } from '../http/types'

const SMALL_BODY = 16 * 1024

/** Lobby lifecycle: create, browse, join, start, leave, kick. */
export function matchRoutes(): Hono<AppEnv> {
  const app = new Hono<AppEnv>()

  app.get('/', async (c) => {
    const { config, kernel } = c.get('container')
    if (!config.publicListing) {
      throw new NotFoundError('This server does not list public matches', {
        reason: 'listing_disabled',
      })
    }
    return c.json({ matches: await kernel.query.listPublicLobbies(50) })
  })

  app.post(
    '/',
    rateLimited,
    bodyLimit({ maxSize: LIMITS.gameSettingsBytes + SMALL_BODY }),
    async (c) => {
      const request = await parseBody(c, createMatchRequestSchema)
      const membership = await c.get('container').kernel.lobby.createMatch(request)
      return c.json(membership, 201)
    },
  )

  app.post('/join', rateLimited, bodyLimit({ maxSize: SMALL_BODY }), async (c) => {
    const request = await parseBody(c, joinMatchRequestSchema)
    const membership = await c.get('container').kernel.lobby.join(request)
    return c.json(membership, 201)
  })

  // `/:matchId/*` also matches the bare `/:matchId`, so this is the only mount: adding a second one
  // for the bare path would authenticate (a token lookup plus a match read) and charge the member's
  // rate limit twice on every request to it.
  app.use('/:matchId/*', bearerAuth, memberRateLimited())

  app.get('/:matchId', async (c) => {
    const principal = requireMember(c)
    const view = await c.get('container').kernel.query.view(principal.match)
    return c.json({ match: view, joinCode: principal.match.joinCode, you: principal.player.id })
  })

  app.post('/:matchId/start', async (c) => {
    await c.get('container').kernel.lobby.start(requireMember(c))
    return c.body(null, 204)
  })

  app.post('/:matchId/leave', async (c) => {
    await c.get('container').kernel.lobby.leave(requireMember(c))
    return c.body(null, 204)
  })

  app.post('/:matchId/players/:playerId/kick', async (c) => {
    await c.get('container').kernel.lobby.kick(requireMember(c), c.req.param('playerId'))
    return c.body(null, 204)
  })

  return app
}
