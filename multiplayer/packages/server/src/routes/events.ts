import { listEventsContract, streamEventsContract } from '@chaos-overlords/contracts'
import type { Hono } from 'hono'
import { requireMember } from '../http/guards'
import { buildHonoRoute } from '../http/routes'
import type { AppEnv } from '../http/types'

/** The event log: a paged REST read (the fallback) and the SSE stream over the same log. */
export function registerEventRoutes(api: Hono<AppEnv>): void {
  buildHonoRoute(api, listEventsContract, async (c) => {
    const principal = requireMember(c.get('principal'), c.req.valid('param').matchId)
    const { after, limit } = c.req.valid('query')
    const events = await c
      .get('container')
      .kernel.deps.storage.events.listAfter(principal.match.id, after, limit)
    return c.json({ events }, 200)
  })

  buildHonoRoute(api, streamEventsContract, async (c) => {
    const principal = requireMember(c.get('principal'), c.req.valid('param').matchId)
    const afterSeq = resumePoint(c.req.header('last-event-id'), c.req.query('after'))
    return c.get('container').eventStream.open({
      matchId: principal.match.id,
      afterSeq,
      signal: c.req.raw.signal,
    })
  })
}

/**
 * `Last-Event-ID` (what an EventSource resends) wins over `?after=`; both default to 0.
 *
 * The stream reads these itself rather than through a query schema because a browser's own
 * `EventSource` sets the header and nothing else, and a resume point that failed validation should
 * restart the stream from the beginning rather than refuse the connection.
 */
function resumePoint(lastEventId: string | undefined, after: string | undefined): number {
  for (const candidate of [lastEventId, after]) {
    const parsed = Number(candidate)
    if (candidate !== undefined && Number.isInteger(parsed) && parsed >= 0) return parsed
  }
  return 0
}
