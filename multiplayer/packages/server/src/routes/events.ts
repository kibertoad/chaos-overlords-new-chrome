import { INT32_MAX, listEventsContract, streamEventsContract } from '@chaos-overlords/contracts'
import { UnauthorizedError } from '@chaos-overlords/kernel'
import type { Hono } from 'hono'
import { answering } from '../http/contractJson'
import { requireMember } from '../http/guards'
import { buildHonoRoute } from '../http/routes'
import type { AppEnv } from '../http/types'
import { isReadableEvent } from '../sse/createSseResponse'
import { isActiveMember } from '../sse/membership'

/** The event log: a paged REST read (the fallback) and the SSE stream over the same log. */
export function registerEventRoutes(api: Hono<AppEnv>): void {
  buildHonoRoute(api, listEventsContract, async (c) => {
    const principal = requireMember(c.get('principal'), c.req.valid('param').matchId)
    const { after, limit } = c.req.valid('query')
    const container = c.get('container')
    const events = await container.kernel.deps.storage.events.listAfter(
      principal.match.id,
      after,
      limit,
    )
    // A row this build's schema refuses is left out rather than answered as a 500. One reshaped
    // payload — which a protocol-only upgrade may leave behind, and AGENTS.md says stored matches
    // survive those — otherwise made every page holding it unreadable for the life of the match.
    //
    // The test is a validation, not a format: building the whole SSE frame here only to throw the
    // string away meant serialising every event of the page an extra time, and `c.json` then
    // serialised all of them again.
    const readable = events.filter((event) => {
      if (isReadableEvent(event)) return true
      container.kernel.deps.logger.warn('skipped an unreadable stored event', {
        matchId: principal.match.id,
        seq: event.seq,
      })
      return false
    })
    return c.json(answering(c, { events: readable }), 200)
  })

  buildHonoRoute(api, streamEventsContract, async (c) => {
    const principal = requireMember(c.get('principal'), c.req.valid('param').matchId)
    const afterSeq = resumePoint(c.req.header('last-event-id'), c.req.query('after'))
    const stream = await c.get('container').eventStream.open({
      matchId: principal.match.id,
      playerId: principal.player.id,
      afterSeq,
      signal: c.req.raw.signal,
    })
    // The kick may have revoked the token and run hangUp after bearerAuth but before the hub
    // subscribed. That hangUp saw no listener. Re-read the row after open and cancel the new stream
    // before returning any of its buffered frames when the membership is already gone.
    let stillMember = false
    try {
      stillMember = await isActiveMember(
        c.get('container').kernel.deps.storage.players,
        principal.match.id,
        principal.player.id,
      )
    } finally {
      if (!stillMember) await stream.body?.cancel()
    }
    if (!stillMember) {
      throw new UnauthorizedError('Invalid or expired player token', { reason: 'invalid_token' })
    }
    // The stream is a raw `Response`, and Hono does not merge the headers the middleware prepared
    // into one of those — so the one response an operator most wants to correlate, a stream that
    // misbehaved for an hour, was the only one without an `X-Request-Id`.
    return c.newResponse(stream.body, stream)
  })
}

/**
 * `Last-Event-ID` (what an EventSource resends) wins over `?after=`; both default to 0.
 *
 * The stream reads these itself rather than through a query schema because a browser's own
 * `EventSource` sets the header and nothing else, and a resume point that failed validation should
 * restart the stream from the beginning rather than refuse the connection. The bound is the one
 * the REST read applies: a value past it is not a sequence number, and on Postgres it would fail
 * inside the drain after the 200 went out, which a client reads as a retryable stream error and
 * repeats with the same value forever.
 */
function resumePoint(lastEventId: string | undefined, after: string | undefined): number {
  for (const candidate of [lastEventId, after]) {
    const parsed = Number(candidate)
    if (candidate !== undefined && Number.isInteger(parsed) && parsed >= 0 && parsed <= INT32_MAX) {
      return parsed
    }
  }
  return 0
}
