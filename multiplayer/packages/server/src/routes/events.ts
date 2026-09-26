import { INT32_MAX, listEventsContract, streamEventsContract } from '@chaos-overlords/contracts'
import { ConflictError, type PersistedEvent, UnauthorizedError } from '@chaos-overlords/kernel'
import type { Hono } from 'hono'
import { answering } from '../http/contractJson'
import { requireMember } from '../http/guards'
import { buildHonoRoute } from '../http/routes'
import type { AppEnv } from '../http/types'
import { isReadableEvent } from '../sse/createSseResponse'
import { isActiveMember } from '../sse/membership'

/**
 * The events a client's history replay applies to the match itself — seats, seals, verdicts and
 * pauses — as opposed to vote tallies, readiness and deadlines, which only feed the interface.
 */
const STATE_BEARING_EVENTS: ReadonlySet<string> = new Set([
  'match.started',
  'match.statusChanged',
  'match.playerTakenOver',
  'match.playerReturned',
  'match.latePlayerJoined',
  'turn.sealed',
  'turn.confirmed',
  'turn.desynced',
])

/** The event log: a paged REST read (the fallback) and the SSE stream over the same log. */
export function registerEventRoutes(api: Hono<AppEnv>): void {
  buildHonoRoute(api, listEventsContract, async (c) => {
    const principal = requireMember(c.get('principal'), c.req.valid('param').matchId)
    const { after, limit } = c.req.valid('query')
    const container = c.get('container')
    // A row this build's schema refuses is withheld rather than answered as a 500. One reshaped
    // payload — which a protocol-only upgrade may leave behind, and AGENTS.md says stored matches
    // survive those — otherwise made every page holding it unreadable for the life of the match.
    //
    // The contract with the client: the stored log is gapless, so a jump in the sequence numbers of
    // a page (or of the stream, which withholds the same rows) is a withheld row and nothing else.
    // The C# client's history replay steps over one (`MultiplayerMatchSession.Restore`); a
    // placeholder event would have been a new event type, which the wire's union does not have.
    //
    // Only a row the replay can do without is withheld. The client cannot tell what a gap held,
    // and its turn checks catch a missing seal but not a missing handover or pause: stepping over
    // one of those plans every later turn from seats that differ from its peers'. Such a row is
    // refused instead, so the loss is reported where it happens rather than as a desync later.
    //
    // The test is a validation, not a format: building the whole SSE frame here only to throw the
    // string away meant serialising every event of the page an extra time, and `c.json` then
    // serialised all of them again.
    const readable: PersistedEvent[] = []
    let cursor = after
    for (;;) {
      const events = await container.kernel.deps.storage.events.listAfter(
        principal.match.id,
        cursor,
        limit,
      )
      for (const event of events) {
        if (isReadableEvent(event)) {
          readable.push(event)
          continue
        }
        if (STATE_BEARING_EVENTS.has(event.type)) {
          container.kernel.deps.logger.error('an unreadable stored event changes the match', {
            matchId: principal.match.id,
            seq: event.seq,
            type: event.type,
          })
          throw new ConflictError('A stored event this server cannot read changes the match', {
            reason: 'unreadable_event',
            seq: event.seq,
          })
        }
        container.kernel.deps.logger.warn('skipped an unreadable stored event', {
          matchId: principal.match.id,
          seq: event.seq,
        })
      }
      // An empty page is the end of the log to a client. A full page that was ALL withheld is
      // not, so the read carries on past it rather than answer one: a client would otherwise stop
      // there, short of every readable event behind it.
      if (readable.length > 0 || events.length < limit) break
      cursor = events.at(-1)?.seq ?? cursor
    }
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
      lobby: principal.match.status === 'lobby',
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
