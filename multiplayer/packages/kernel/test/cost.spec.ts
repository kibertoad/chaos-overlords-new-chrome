import { beforeEach, describe, expect, it } from 'vitest'
import { CountingStorage } from '../src/testing'
import { countedKernel, createHarness, HASH_A, HASH_B, ordersFor, type Harness } from './harness'

describe('the cost of the hot paths', () => {
  let h: Harness

  beforeEach(() => {
    h = createHarness()
  })

  /**
   * S3, S6, S7, S8: the cost of a hot path, pinned to a number.
   *
   * Nothing else notices when a request or a sweep pass grows a read. The behaviour is identical
   * and only the cost moves, which is how the verdict came to page an entire event log on every
   * sweep of a paused match and how the hottest write the server has came to load a quarter of a
   * megabyte of JSON to compare a boolean. These numbers are a budget, not a fact about the
   * implementation: raising one deliberately is a one-line change, and doing it by accident is what
   * this stops.
   */
  describe('the cost of the hot paths', () => {
    it('submits an order document without reading one', async () => {
      const { host, guest } = await h.startedMatch()
      await h.submit(await h.principalOf(guest.token), 1, 1, false)
      const counting = new CountingStorage(h.storage)
      const counted = countedKernel(h, counting)
      const principal = await counted.auth.authenticate(host.token)
      counting.reset()

      await counted.turns.submitOrders(principal, 1, {
        orders: ordersFor(principal, 7),
        ready: false,
      })

      // The document itself is written and never read back: the readiness decision and the
      // stale-turn acknowledgement both need the hash and the flag, which is the projection.
      expect(counting.get('turns.getOrders')).toBe(0)
      expect(counting.get('turns.getOrderSummary')).toBe(1)
      expect(counting.total()).toBeLessThanOrEqual(4)
    })

    it('re-judges a paused match without reading a snapshot body or the event log', async () => {
      const { host, guest } = await h.startedMatch()
      await h.submit(await h.principalOf(host.token), 1, 1, true)
      await h.submit(await h.principalOf(guest.token), 1, 2, true)
      await h.kernel.turns.report(await h.principalOf(host.token), 1, {
        stateHash: HASH_A,
        finished: false,
      })
      await h.kernel.turns.report(await h.principalOf(guest.token), 1, {
        stateHash: HASH_B,
        finished: false,
      })
      expect(h.storage.statusOf(host.match.id)).toBe('desynced')

      const counting = new CountingStorage(h.storage)
      const counted = countedKernel(h, counting)
      counting.reset()
      await counted.turns.reevaluate(host.match.id)

      // The verdict reads one hash, not a megabyte of base64 that happens to carry it.
      expect(counting.get('snapshots.get')).toBe(0)
      expect(counting.get('snapshots.getSummary')).toBe(1)
      // And it knows whether it already announced the divergence without paging the whole log.
      expect(counting.get('events.listAfter')).toBe(0)
    })

    it('leaves a parked desync alone on an ordinary sweep pass', async () => {
      const { host, guest } = await h.startedMatch()
      await h.submit(await h.principalOf(host.token), 1, 1, true)
      await h.submit(await h.principalOf(guest.token), 1, 2, true)
      await h.kernel.turns.report(await h.principalOf(host.token), 1, {
        stateHash: HASH_A,
        finished: false,
      })
      await h.kernel.turns.report(await h.principalOf(guest.token), 1, {
        stateHash: HASH_B,
        finished: false,
      })
      expect(h.storage.statusOf(host.match.id)).toBe('desynced')

      const counting = new CountingStorage(h.storage)
      const counted = countedKernel(h, counting)
      // The first pass of a fresh service scans everything, which is how a restart picks up work
      // left behind while it was down.
      await counted.turns.sweep()
      expect(counting.get('turns.listReports')).toBeGreaterThan(0)

      // Nothing has happened to the match since, and a match a public server keeps paused for the
      // weeks of its retention has nothing new to judge on any of them.
      h.clock.advance(10 * 60_000)
      counting.reset()
      await counted.turns.sweep()
      expect(counting.get('turns.listReports')).toBe(0)
      expect(counting.get('snapshots.getSummary')).toBe(0)

      // A report or an upload moves `updatedAt`, and the next pass picks the match back up.
      await counted.snapshots.upload(await counted.auth.authenticate(host.token), {
        turn: 1,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'AAAA',
        seatSummaries: [],
      })
      counting.reset()
      await counted.turns.sweep()
      expect(counting.get('turns.listReports')).toBeGreaterThan(0)
    })

    it('lists public lobbies without a read per listed match', async () => {
      for (let index = 0; index < 4; index += 1) {
        const created = await h.kernel.lobby.createMatch({
          settings: {
            name: `Match ${index}`,
            maxPlayers: 3,
            turnTimerSeconds: 0,
            visibility: 'public',
            gameSettings: { allowLateJoin: true },
          },
          hostDisplayName: 'Host',
        })
        await h.kernel.lobby.join({ joinCode: created.joinCode, displayName: 'Guest' })
        await h.kernel.lobby.start(await h.principalOf(created.token))
        await h.kernel.snapshots.upload(await h.principalOf(created.token), {
          turn: 0,
          formatVersion: 1,
          stateHash: HASH_A,
          body: 'AAAA',
          seatSummaries: [],
        })
      }
      const counting = new CountingStorage(h.storage)
      const counted = countedKernel(h, counting)
      counting.reset()

      const listings = await counted.query.listPublicLobbies(50)

      expect(listings.length).toBe(4)
      expect(listings.every((listing) => listing.availableSlots.length > 0)).toBe(true)
      // The listing statement and one seat read over every match on the page, whatever its size.
      expect(counting.total()).toBe(2)
      expect(counting.get('players.listByMatch')).toBe(0)
      expect(counting.get('snapshots.getLatestSummary')).toBe(0)
    })
  })
})
