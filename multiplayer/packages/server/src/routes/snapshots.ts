import {
  latestSnapshotContract,
  snapshotContract,
  uploadSnapshotContract,
} from '@chaos-overlords/contracts'
import type { Hono } from 'hono'
import { requireMember } from '../http/guards'
import { buildHonoRoute } from '../http/routes'
import type { AppEnv } from '../http/types'

/** Native snapshots: the host's repair for a desynced turn, and the read every client resyncs from. */
export function registerSnapshotRoutes(api: Hono<AppEnv>): void {
  buildHonoRoute(api, uploadSnapshotContract, async (c) => {
    await c
      .get('container')
      .kernel.snapshots.upload(
        requireMember(c.get('principal'), c.req.valid('param').matchId),
        c.req.valid('json'),
      )
    return c.body(null, 204)
  })

  buildHonoRoute(api, latestSnapshotContract, async (c) => {
    const principal = requireMember(c.get('principal'), c.req.valid('param').matchId)
    return c.json(await c.get('container').kernel.snapshots.latest(principal.match.id), 200)
  })

  buildHonoRoute(api, snapshotContract, async (c) => {
    const principal = requireMember(c.get('principal'), c.req.valid('param').matchId)
    const { turn } = c.req.valid('param')
    return c.json(await c.get('container').kernel.snapshots.get(principal.match.id, turn), 200)
  })
}
