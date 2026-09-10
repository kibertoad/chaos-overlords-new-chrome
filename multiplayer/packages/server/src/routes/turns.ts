import {
  LIMITS,
  submitOrdersRequestSchema,
  turnReportRequestSchema,
} from '@chaos-overlords/contracts'
import { Hono } from 'hono'
import { bodyLimit } from 'hono/body-limit'
import { parseBody, requireMember, turnParam } from '../http/guards'
import type { AppEnv } from '../http/types'

/** The turn barrier: submit privately, read the sealed set, report the resulting hash. */
export function turnRoutes(): Hono<AppEnv> {
  const app = new Hono<AppEnv>()

  app.put('/:turn/orders', bodyLimit({ maxSize: LIMITS.ordersBytes }), async (c) => {
    const request = await parseBody(c, submitOrdersRequestSchema)
    const view = await c
      .get('container')
      .kernel.turns.submitOrders(requireMember(c), turnParam(c), request)
    return c.json(view)
  })

  app.get('/:turn/orders/mine', async (c) => {
    const principal = requireMember(c)
    const view = await c
      .get('container')
      .kernel.query.ownSubmission(principal.match, principal.player.id, turnParam(c))
    return c.json(view)
  })

  app.get('/:turn/orders', async (c) => {
    const principal = requireMember(c)
    const view = await c.get('container').kernel.query.sealedOrders(principal.match, turnParam(c))
    // A sealed set never changes, so clients and proxies may keep it.
    c.header('Cache-Control', 'private, max-age=31536000, immutable')
    return c.json(view)
  })

  app.post('/:turn/report', bodyLimit({ maxSize: 4096 }), async (c) => {
    const request = await parseBody(c, turnReportRequestSchema)
    await c.get('container').kernel.turns.report(requireMember(c), turnParam(c), request)
    return c.body(null, 204)
  })

  return app
}
