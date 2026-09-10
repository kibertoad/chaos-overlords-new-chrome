import { eventsQuerySchema } from '@chaos-overlords/contracts'
import { Hono } from 'hono'
import { requireMember } from '../http/guards'
import type { AppEnv } from '../http/types'

/** The event log: a paged REST read (the fallback) and the SSE stream over the same log. */
export function eventRoutes(): Hono<AppEnv> {
  const app = new Hono<AppEnv>()

  app.get('/events', async (c) => {
    const principal = requireMember(c)
    const query = eventsQuerySchema.parse(c.req.query())
    const events = await c
      .get('container')
      .kernel.deps.storage.events.listAfter(principal.match.id, query.after, query.limit)
    return c.json({ events })
  })

  app.get('/stream', async (c) => {
    const principal = requireMember(c)
    const afterSeq = resumePoint(c.req.header('last-event-id'), c.req.query('after'))
    return c.get('container').eventStream.open({
      matchId: principal.match.id,
      afterSeq,
      signal: c.req.raw.signal,
    })
  })

  return app
}

/** `Last-Event-ID` (what an EventSource resends) wins over `?after=`; both default to 0. */
function resumePoint(lastEventId: string | undefined, after: string | undefined): number {
  for (const candidate of [lastEventId, after]) {
    const parsed = Number(candidate)
    if (candidate !== undefined && Number.isInteger(parsed) && parsed >= 0) return parsed
  }
  return 0
}
