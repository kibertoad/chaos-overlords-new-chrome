import {
  createMatchContract,
  getMatchContract,
  joinMatchContract,
  kickPlayerContract,
  leaveMatchContract,
  listLobbiesContract,
  startMatchContract,
} from '@chaos-overlords/contracts'
import { NotFoundError } from '@chaos-overlords/kernel'
import type { Hono } from 'hono'
import { requireMember } from '../http/guards'
import { buildHonoRoute } from '../http/routes'
import type { AppEnv } from '../http/types'

/**
 * Browse, create and join: the three routes that answer without a token.
 *
 * They are registered before the app's `/matches/:matchId/*` auth mount, whose pattern also matches
 * `/matches/join`. Hono ends the chain at the first matching handler, so being registered first is
 * what keeps joining a lobby from demanding the token a player is joining to get.
 */
export function registerPublicLobbyRoutes(api: Hono<AppEnv>): void {
  buildHonoRoute(api, listLobbiesContract, async (c) => {
    const { config, kernel } = c.get('container')
    if (!config.publicListing) {
      throw new NotFoundError('This server does not list public matches', {
        reason: 'listing_disabled',
      })
    }
    return c.json({ matches: await kernel.query.listPublicLobbies(50) }, 200)
  })

  buildHonoRoute(api, createMatchContract, async (c) => {
    const membership = await c.get('container').kernel.lobby.createMatch(c.req.valid('json'))
    return c.json(membership, 201)
  })

  buildHonoRoute(api, joinMatchContract, async (c) => {
    const membership = await c.get('container').kernel.lobby.join(c.req.valid('json'))
    return c.json(membership, 201)
  })
}

/** The rest of the lobby lifecycle, for a seated member. */
export function registerMemberLobbyRoutes(api: Hono<AppEnv>): void {
  buildHonoRoute(api, getMatchContract, async (c) => {
    const principal = requireMember(c.get('principal'), c.req.valid('param').matchId)
    const view = await c.get('container').kernel.query.view(principal.match)
    return c.json(
      { match: view, joinCode: principal.match.joinCode, you: principal.player.id },
      200,
    )
  })

  buildHonoRoute(api, startMatchContract, async (c) => {
    await c
      .get('container')
      .kernel.lobby.start(requireMember(c.get('principal'), c.req.valid('param').matchId))
    return c.body(null, 204)
  })

  buildHonoRoute(api, leaveMatchContract, async (c) => {
    await c
      .get('container')
      .kernel.lobby.leave(requireMember(c.get('principal'), c.req.valid('param').matchId))
    return c.body(null, 204)
  })

  buildHonoRoute(api, kickPlayerContract, async (c) => {
    const { matchId, playerId } = c.req.valid('param')
    await c.get('container').kernel.lobby.kick(requireMember(c.get('principal'), matchId), playerId)
    return c.body(null, 204)
  })
}
