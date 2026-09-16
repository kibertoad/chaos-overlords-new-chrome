import type { EventRepository, PersistedEvent } from '@chaos-overlords/kernel'
import { describe, expect, it } from 'vitest'
import { LocalEventHub } from '../src'

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
