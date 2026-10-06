import {
  listSpectatorsContract,
  removeSpectatorContract,
  spectateContract,
  spectatorEventsContract,
  spectatorMatchContract,
  spectatorSealedOrdersContract,
  spectatorSnapshotContract,
  stopSpectatingContract,
} from '@chaos-overlords/contracts'
import { NotFoundError, type SpectatorPrincipal } from '@chaos-overlords/kernel'
import type { Hono } from 'hono'
import { answering } from '../http/contractJson'
import { requireMember } from '../http/guards'
import { buildHonoRoute } from '../http/routes'
import type { AppEnv } from '../http/types'
import { isReadableEvent } from '../sse/createSseResponse'

/**
 * The spectator, checked against the `:matchId` the request named. A token for another match
 * answers 404, as the player guard does.
 */
function requireSpectator(principal: SpectatorPrincipal, matchId: string): SpectatorPrincipal {
  if (principal.match.id !== matchId) {
    throw new NotFoundError('No such match', { reason: 'unknown_match' })
  }
  return principal
}

/** The one spectator door that answers without a token. Registered before the token mount. */
export function registerPublicSpectatorRoutes(api: Hono<AppEnv>): void {
  buildHonoRoute(api, spectateContract, async (c) => {
    const membership = await c
      .get('container')
      .kernel.spectators.join(c.req.valid('json'), c.get('caller'))
    return c.json(answering(c, membership), 201)
  })
}

/** What a spectator token reads, all of it held behind the match's delay by the kernel. */
export function registerSpectatorRoutes(api: Hono<AppEnv>): void {
  buildHonoRoute(api, spectatorMatchContract, async (c) => {
    const principal = requireSpectator(c.get('spectator'), c.req.valid('param').matchId)
    return c.json(answering(c, await c.get('container').kernel.spectators.view(principal)), 200)
  })

  buildHonoRoute(api, spectatorEventsContract, async (c) => {
    const principal = requireSpectator(c.get('spectator'), c.req.valid('param').matchId)
    const { after, limit } = c.req.valid('query')
    const page = await c.get('container').kernel.spectators.events(principal, after, limit)
    // A stored row this build's schema refuses is left out, as the player read leaves it out,
    // rather than failing the page; the cursor still moves past it.
    return c.json(answering(c, { ...page, events: page.events.filter(isReadableEvent) }), 200)
  })

  buildHonoRoute(api, spectatorSealedOrdersContract, async (c) => {
    const { matchId, turn } = c.req.valid('param')
    const principal = requireSpectator(c.get('spectator'), matchId)
    const view = await c.get('container').kernel.spectators.sealedOrders(principal, turn)
    c.header('Cache-Control', 'private, max-age=31536000, immutable')
    return c.json(answering(c, view), 200)
  })

  buildHonoRoute(api, spectatorSnapshotContract, async (c) => {
    const principal = requireSpectator(c.get('spectator'), c.req.valid('param').matchId)
    return c.json(
      answering(c, await c.get('container').kernel.spectators.latestSnapshot(principal)),
      200,
    )
  })

  buildHonoRoute(api, stopSpectatingContract, async (c) => {
    const principal = requireSpectator(c.get('spectator'), c.req.valid('param').matchId)
    await c.get('container').kernel.spectators.leave(principal)
    return c.body(null, 204)
  })
}

/** What the players of a match do about the people watching it. */
export function registerMemberSpectatorRoutes(api: Hono<AppEnv>): void {
  buildHonoRoute(api, listSpectatorsContract, async (c) => {
    const principal = requireMember(c.get('principal'), c.req.valid('param').matchId)
    return c.json(answering(c, await c.get('container').kernel.spectators.list(principal)), 200)
  })

  buildHonoRoute(api, removeSpectatorContract, async (c) => {
    const { matchId, spectatorId } = c.req.valid('param')
    await c
      .get('container')
      .kernel.spectators.remove(requireMember(c.get('principal'), matchId), spectatorId)
    return c.body(null, 204)
  })
}
