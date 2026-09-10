import { LIMITS, uploadSnapshotRequestSchema } from '@chaos-overlords/contracts'
import { Hono } from 'hono'
import { bodyLimit } from 'hono/body-limit'
import { parseBody, requireMember, turnParam } from '../http/guards'
import type { AppEnv } from '../http/types'

export function snapshotRoutes(): Hono<AppEnv> {
  const app = new Hono<AppEnv>()

  app.post('/', bodyLimit({ maxSize: LIMITS.snapshotBase64Bytes + 4096 }), async (c) => {
    const request = await parseBody(c, uploadSnapshotRequestSchema)
    await c.get('container').kernel.snapshots.upload(requireMember(c), request)
    return c.body(null, 204)
  })

  app.get('/latest', async (c) => {
    const principal = requireMember(c)
    return c.json(await c.get('container').kernel.snapshots.latest(principal.match.id))
  })

  app.get('/:turn', async (c) => {
    const principal = requireMember(c)
    return c.json(await c.get('container').kernel.snapshots.get(principal.match.id, turnParam(c)))
  })

  return app
}
