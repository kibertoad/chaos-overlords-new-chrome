import {
  ownSubmissionContract,
  reportTurnContract,
  seatViewContract,
  sealedOrdersContract,
  submitOrdersContract,
} from '@chaos-overlords/contracts'
import type { Hono } from 'hono'
import { answering } from '../http/contractJson'
import { requireMember } from '../http/guards'
import { buildHonoRoute } from '../http/routes'
import type { AppEnv } from '../http/types'

/**
 * The turn barrier: submit privately, read the sealed set, report the resulting hash, or, in a
 * match played from views, read the seat's own view of the open turn.
 */
export function registerTurnRoutes(api: Hono<AppEnv>): void {
  buildHonoRoute(api, submitOrdersContract, async (c) => {
    const { matchId, turn } = c.req.valid('param')
    const view = await c
      .get('container')
      .kernel.turns.submitOrders(
        requireMember(c.get('principal'), matchId),
        turn,
        c.req.valid('json'),
      )
    return c.json(answering(c, view), 200)
  })

  buildHonoRoute(api, ownSubmissionContract, async (c) => {
    const { matchId, turn } = c.req.valid('param')
    const principal = requireMember(c.get('principal'), matchId)
    const view = await c
      .get('container')
      .kernel.query.ownSubmission(principal.match, principal.player.id, turn)
    return c.json(answering(c, view), 200)
  })

  buildHonoRoute(api, sealedOrdersContract, async (c) => {
    const { matchId, turn } = c.req.valid('param')
    const principal = requireMember(c.get('principal'), matchId)
    const view = await c.get('container').kernel.query.memberSealedOrders(principal.match, turn)
    // A sealed set never changes, so clients and proxies may keep it.
    c.header('Cache-Control', 'private, max-age=31536000, immutable')
    return c.json(answering(c, view), 200)
  })

  buildHonoRoute(api, seatViewContract, async (c) => {
    const principal = requireMember(c.get('principal'), c.req.valid('param').matchId)
    const view = await c.get('container').kernel.views.seatView(principal)
    // A view is the caller's alone and is replaced every turn.
    c.header('Cache-Control', 'private, no-store')
    return c.json(answering(c, view), 200)
  })

  buildHonoRoute(api, reportTurnContract, async (c) => {
    const { matchId, turn } = c.req.valid('param')
    await c
      .get('container')
      .kernel.turns.report(requireMember(c.get('principal'), matchId), turn, c.req.valid('json'))
    return c.body(null, 204)
  })
}
