import type { EventRepository, PersistedEvent } from '@chaos-overlords/kernel'
import { describe, expect, it } from 'vitest'
import { LocalEventHub, MatchLog } from '../src'

/** An empty log: these tests are about the subscriptions, not about what comes down them. */
const emptyEvents: EventRepository = {
  append: async () => {
    throw new Error('not used')
  },
  listAfter: async (): Promise<PersistedEvent[]> => [],
  lastSeq: async () => 0,
}

function hubOf(limits: { perPlayer: number; perMatch: number; perProcess: number }) {
  return new LocalEventHub(emptyEvents, 60_000, limits)
}

/** Opens a stream and hands back a reader, so the response body is actually consumed. */
async function open(hub: LocalEventHub, matchId: string, playerId: string) {
  const controller = new AbortController()
  const response = await hub.open({ matchId, playerId, afterSeq: 0, signal: controller.signal })
  const reader = (response.body as ReadableStream<Uint8Array>).getReader()
  return {
    /** Drains whatever is buffered and reports whether the stream ended. */
    ended: async () => {
      for (let i = 0; i < 20; i += 1) {
        if ((await reader.read()).done) return true
      }
      return false
    },
    abort: () => controller.abort(),
  }
}

describe('LocalEventHub stream caps', () => {
  it('ends a stream when periodic membership revalidation fails', async () => {
    let authorized = true
    const hub = new LocalEventHub(emptyEvents, 5, undefined, {
      revalidate: async () => authorized,
    })
    const stream = await open(hub, 'm', 'p1')
    expect(hub.connectionCount('m')).toBe(1)
    authorized = false
    expect(await stream.ended()).toBe(true)
    expect(hub.connectionCount('m')).toBe(0)
  })

  /**
   * A stream is not a request. It lives until the client closes it and every event published to its
   * match costs it one query, so the per-minute limit on the call that opens one bounds nothing that
   * matters afterwards.
   */
  it('closes a player oldest stream rather than letting them accumulate', async () => {
    const hub = hubOf({ perPlayer: 2, perMatch: 10, perProcess: 10 })
    const first = await open(hub, 'm', 'p1')
    const second = await open(hub, 'm', 'p1')
    expect(hub.connectionCount('m')).toBe(2)

    // A reconnecting client is the case this has to allow: the new stream is the live one and the
    // oldest is the corpse, so the corpse goes rather than the reconnect being refused.
    const third = await open(hub, 'm', 'p1')
    expect(hub.connectionCount('m')).toBe(2)
    expect(await first.ended()).toBe(true)

    // Another player's streams are their own budget.
    const other = await open(hub, 'm', 'p2')
    expect(hub.connectionCount('m')).toBe(3)
    second.abort()
    third.abort()
    other.abort()
  })

  it('refuses a stream past the match ceiling, and past the process one first', async () => {
    const hub = hubOf({ perPlayer: 5, perMatch: 2, perProcess: 3 })
    const held = [await open(hub, 'm', 'p1'), await open(hub, 'm', 'p2')]
    await expect(
      hub.open({ matchId: 'm', playerId: 'p3', afterSeq: 0, signal: new AbortController().signal }),
    ).rejects.toMatchObject({ details: { reason: 'too_many_streams', scope: 'match' } })

    held.push(await open(hub, 'other', 'p1'))
    expect(hub.openStreams).toBe(3)
    // The process ceiling protects everybody else's matches, so it is tested before the match one:
    // refusing is cheaper than closing a live stream for a caller about to be turned away anyway.
    await expect(
      hub.open({
        matchId: 'other',
        playerId: 'p2',
        afterSeq: 0,
        signal: new AbortController().signal,
      }),
    ).rejects.toMatchObject({ details: { reason: 'too_many_streams', scope: 'process' } })
    for (const stream of held) stream.abort()
  })

  it('answers an already aborted request with an ended stream and holds no slot for it', async () => {
    const abandoned: string[] = []
    const hub = new LocalEventHub(
      emptyEvents,
      60_000,
      { perPlayer: 5, perMatch: 1, perProcess: 1 },
      { abandoned: (matchId, playerId) => abandoned.push(`${matchId}/${playerId}`) },
    )

    const response = await hub.open({
      matchId: 'm',
      playerId: 'gone',
      afterSeq: 0,
      signal: AbortSignal.abort(),
    })

    // Nothing to read and nothing left running: the body is already over, with no frame in it.
    expect(response.headers.get('content-type')).toContain('text/event-stream')
    expect(await response.text()).toBe('')
    expect(hub.openStreams).toBe(0)
    expect(hub.connectionCount('m')).toBe(0)
    expect(abandoned).toEqual(['m/gone'])
    // A real client can therefore claim the only slot in this match and process.
    const live = await open(hub, 'm', 'present')
    expect(hub.openStreams).toBe(1)
    live.abort()
  })

  /**
   * Making room closes the caller's oldest stream. A request nobody is waiting on must not get that
   * far: it would cost the player a live tab, or be refused over caps it was never going to use.
   */
  it('neither closes a live stream nor refuses for an already aborted request', async () => {
    const hub = hubOf({ perPlayer: 1, perMatch: 1, perProcess: 1 })
    const live = await open(hub, 'm', 'p1')

    for (const [matchId, playerId] of [
      ['m', 'p1'],
      ['m', 'p2'],
      ['other', 'p3'],
    ] as const) {
      const response = await hub.open({
        matchId,
        playerId,
        afterSeq: 0,
        signal: AbortSignal.abort(),
      })
      expect(response.status).toBe(200)
    }
    expect(hub.connectionCount('m')).toBe(1)
    expect(hub.openStreams).toBe(1)
    live.abort()
  })

  /**
   * A full match is exactly what a reconnecting player finds when every seat holds its quota. The
   * caller's own stale stream goes before the match ceiling is read, so the reconnect succeeds.
   */
  it('lets a player reconnect into a full match by replacing their own stale stream', async () => {
    const hub = hubOf({ perPlayer: 1, perMatch: 2, perProcess: 10 })
    const stale = await open(hub, 'm', 'p1')
    const other = await open(hub, 'm', 'p2')
    expect(hub.connectionCount('m')).toBe(2)

    const fresh = await open(hub, 'm', 'p1')
    expect(await stale.ended()).toBe(true)
    expect(hub.connectionCount('m')).toBe(2)
    // A genuinely new member of a full match is still refused.
    await expect(
      hub.open({ matchId: 'm', playerId: 'p3', afterSeq: 0, signal: new AbortController().signal }),
    ).rejects.toMatchObject({ details: { reason: 'too_many_streams', scope: 'match' } })
    fresh.abort()
    other.abort()
  })

  it('hangs up every stream of one membership and leaves the others alone', async () => {
    const hub = hubOf({ perPlayer: 3, perMatch: 10, perProcess: 10 })
    const kickedFirst = await open(hub, 'm', 'kicked')
    const kickedSecond = await open(hub, 'm', 'kicked')
    const survivor = await open(hub, 'm', 'staying')

    await hub.close({ matchId: 'm', playerId: 'kicked' })
    expect(hub.connectionCount('m')).toBe(1)
    expect(await kickedFirst.ended()).toBe(true)
    expect(await kickedSecond.ended()).toBe(true)
    survivor.abort()
  })
})

/** A log that records every read, so a test can assert what fan-out actually costs. */
function countingLog(events: PersistedEvent[]) {
  const reads: number[] = []
  const repository: EventRepository = {
    append: async () => {
      throw new Error('not used')
    },
    listAfter: async (_matchId, afterSeq, limit) => {
      reads.push(afterSeq)
      return events.filter((event) => event.seq > afterSeq).slice(0, limit)
    },
    lastSeq: async () => events.at(-1)?.seq ?? 0,
  }
  return { repository, reads }
}

const eventAt = (seq: number): PersistedEvent =>
  ({
    seq,
    matchId: 'm',
    type: 'turn.opened',
    payload: { turn: seq, deadlineAt: null },
    createdAt: '2026-01-01T00:00:00.000Z',
  }) as PersistedEvent

/** Let the stream's own scheduling run. */
const settle = () => new Promise((resolve) => setTimeout(resolve, 30))

describe('LocalEventHub fan-out cost', () => {
  /**
   * The work of one event is per EVENT, not per reader.
   *
   * Every subscriber used to drain the log with its own `listAfter`, validate the same rows with
   * its own schema pass and serialise the same payload with its own `JSON.stringify`. A seal emits
   * three to five events in a burst and wakes up to eighteen streams per match, and the notifier
   * was holding the durable row the whole time.
   */
  it('costs no reads at all to fan one published event out to many streams', async () => {
    const log = countingLog([])
    const hub = new LocalEventHub(log.repository, 60_000)
    const readers = await Promise.all(
      ['p1', 'p2', 'p3', 'p4'].map((playerId) => open(hub, 'm', playerId)),
    )
    await settle()
    // Every one of them has to reach the log, because a fresh stream has been told nothing — but
    // they are all at the same cursor, so they share one statement rather than issuing four.
    expect(log.reads).toEqual([0])
    log.reads.length = 0

    await hub.notify(eventAt(1))
    await hub.notify(eventAt(2))
    await settle()

    expect(log.reads).toEqual([])
    for (const reader of readers) reader.abort()
  })

  /**
   * The other half: a heartbeat on a stream that has everything asks nothing. At the default cap of
   * 512 streams the unconditional re-read was about twenty-five queries a second finding nothing,
   * on the event loop that also seals turns.
   */
  it('stops re-reading the log on the heartbeat of a stream that is caught up', async () => {
    const log = countingLog([])
    const hub = new LocalEventHub(log.repository, 10)
    const reader = await open(hub, 'm', 'p1')
    await hub.notify(eventAt(1))
    await settle()
    log.reads.length = 0

    // Many heartbeats' worth of time, with nothing published.
    await new Promise((resolve) => setTimeout(resolve, 120))

    // Only the periodic catch-up, which exists for an append this process was never told about.
    expect(log.reads.length).toBeLessThan(4)
    reader.abort()
  })

  /**
   * A hub woken without the event body cannot know it has everything, so it must keep reading. The
   * Durable Object used to be notified by match id alone, and a `caughtUp` that answered from reads
   * rather than from notifications would have silenced every stream it held.
   */
  it('keeps reading the log for a hub that is woken without the event', async () => {
    const durable: PersistedEvent[] = []
    const log = countingLog(durable)
    const hub = new LocalEventHub(log.repository, 60_000)
    const controller = new AbortController()
    const response = await hub.open({
      matchId: 'm',
      playerId: 'p1',
      afterSeq: 0,
      signal: controller.signal,
    })
    const reader = (response.body as ReadableStream<Uint8Array>).getReader()
    const decoder = new TextDecoder()
    await reader.read()
    await settle()

    durable.push(eventAt(1))
    hub.wake('m')
    let text = ''
    for (let i = 0; i < 10 && !text.includes('id: 1'); i += 1) {
      const frame = await reader.read()
      if (frame.value) text += decoder.decode(frame.value)
    }
    expect(text).toContain('id: 1')
    controller.abort()
    await reader.cancel().catch(() => {})
  })

  /**
   * A reconnecting player whose predecessor is still open must not be told the server is full: the
   * corpse it is replacing is occupying one of the slots being counted.
   */
  it('closes the caller own stale stream before reading the process cap', async () => {
    const log = countingLog([])
    const hub = new LocalEventHub(log.repository, 60_000, {
      perPlayer: 1,
      perMatch: 4,
      perProcess: 1,
    })
    const stale = await open(hub, 'm', 'p1')
    expect(hub.openStreams).toBe(1)
    const fresh = await open(hub, 'm', 'p1')
    expect(await stale.ended()).toBe(true)
    expect(hub.openStreams).toBe(1)
    fresh.abort()
  })
})

describe('MatchLog catch-up', () => {
  /**
   * The periodic catch-up exists for an append another process made against the same database, and
   * the drain spends its force on the FIRST page of the pass. `force` therefore has to bypass the
   * memo rather than only its "nothing further" answer: a page served out of the tail — the common
   * case on a woken stream — used to spend the force without a read reaching the log, and the
   * stream waited another whole catch-up period before it tried again.
   */
  it('reaches the log for a forced page even when the tail answers it', async () => {
    // Only the first event was published through this process; the second is another process's.
    const log = countingLog([eventAt(1), eventAt(2)])
    const matchLog = new MatchLog('m', log.repository)
    matchLog.record(eventAt(1))

    expect((await matchLog.page(0)).map((frame) => frame.seq)).toEqual([1])
    expect(log.reads).toEqual([])

    expect((await matchLog.page(0, true)).map((frame) => frame.seq)).toEqual([1, 2])
    expect(log.reads).toEqual([0])
  })
})
