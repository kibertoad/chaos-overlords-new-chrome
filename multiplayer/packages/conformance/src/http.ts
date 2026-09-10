import { type FetchLike, MultiplayerApiError, MultiplayerClient } from '@chaos-overlords/client'
import type { MatchEvent, MatchEventType, OrderDocument } from '@chaos-overlords/contracts'
import { describe, expect, it } from 'vitest'

export interface HttpConformanceHarness {
  /** Sends a request to the facade under test (an in-process app or a live server). */
  fetch: FetchLike
  /** Whether the facade was configured with public lobby listing. */
  publicListing: boolean
  /** Make every open deadline due and run the sweep, when the harness controls time. */
  expireDeadlines?: () => Promise<void>
}

const HASH_A = 'a'.repeat(64)
const HASH_B = 'b'.repeat(64)
const orders = (n: number): OrderDocument => ({
  schemaVersion: 1,
  ops: [{ op: 'move', args: { gang: n } }],
})

async function collect(
  iterator: AsyncGenerator<MatchEvent>,
  controller: AbortController,
  until: (events: MatchEvent[]) => boolean,
  timeoutMs = 5000,
): Promise<MatchEvent[]> {
  const events: MatchEvent[] = []
  const timer = setTimeout(() => controller.abort(), timeoutMs)
  try {
    for await (const event of iterator) {
      events.push(event)
      if (until(events)) break
    }
  } finally {
    clearTimeout(timer)
    controller.abort()
  }
  return events
}

const types = (events: MatchEvent[]): MatchEventType[] => events.map((event) => event.type)

/**
 * Drives the whole protocol through the public client against a facade: every runtime runs it,
 * so a route only one facade mounts, or a container wired differently, fails the same test.
 */
export function defineHttpConformance(harness: HttpConformanceHarness): void {
  const client = () =>
    new MultiplayerClient({ baseUrl: 'http://conformance', fetch: harness.fetch })

  const settings = {
    name: 'Conformance city',
    maxPlayers: 3,
    turnTimerSeconds: 0,
    visibility: 'public' as const,
    gameSettings: { scenario: 'smg-islands' },
  }

  async function lobbyOfTwo(turnTimerSeconds = 0) {
    const anonymous = client()
    const host = await anonymous.createMatch({
      settings: { ...settings, turnTimerSeconds },
      hostDisplayName: 'Ada',
    })
    const guest = await anonymous.join({ joinCode: host.joinCode, displayName: 'Grace' })
    return {
      matchId: host.match.id,
      host: { ...host, api: anonymous.withToken(host.token).match(host.match.id) },
      guest: { ...guest, api: anonymous.withToken(guest.token).match(host.match.id) },
    }
  }

  describe('http conformance', () => {
    it('serves health', async () => {
      const response = await harness.fetch('http://conformance/health')
      expect(response.status).toBe(200)
      expect(await response.json()).toEqual({ ok: true })
    })

    it('creates a match and returns a one-time token the caller then authenticates with', async () => {
      const membership = await client().createMatch({ settings, hostDisplayName: 'Ada' })
      expect(membership.token).toMatch(/^cop_[A-Za-z0-9_-]{43}$/)
      expect(membership.joinCode).toMatch(/^[A-Z0-9]{8}$/)
      expect(membership.player.isHost).toBe(true)
      expect(membership.match.status).toBe('lobby')
      const detail = await client().withToken(membership.token).match(membership.match.id).get()
      expect(detail.you).toBe(membership.player.id)
      expect(detail.joinCode).toBe(membership.joinCode)
    })

    it('answers the error envelope with a status class, a reason and a request id', async () => {
      await expect(client().match('nope').get()).rejects.toMatchObject({
        status: 401,
        reason: 'missing_token',
      })
      const membership = await client().createMatch({ settings, hostDisplayName: 'Ada' })
      const other = await client().createMatch({ settings, hostDisplayName: 'Bob' })
      const asAda = client().withToken(membership.token)
      await expect(asAda.match(other.match.id).get()).rejects.toMatchObject({
        status: 404,
        reason: 'unknown_match',
      })
      await expect(
        asAda.withToken('cop_bogus').match(membership.match.id).get(),
      ).rejects.toMatchObject({ status: 401, reason: 'invalid_token' })
      const invalid = await client()
        .createMatch({ settings: { ...settings, maxPlayers: 99 }, hostDisplayName: 'Ada' })
        .catch((e: unknown) => e)
      expect(invalid).toBeInstanceOf(MultiplayerApiError)
      expect(invalid).toMatchObject({
        status: 422,
        code: 'validation_failed',
        reason: 'invalid_body',
      })
      expect((invalid as MultiplayerApiError).requestId).toBeTruthy()
    })

    it('lists public lobbies only when the facade enables it', async () => {
      await client().createMatch({ settings, hostDisplayName: 'Ada' })
      if (harness.publicListing) {
        const { matches } = await client().listLobbies()
        expect(
          matches.some((entry) => entry.name === settings.name && entry.hostDisplayName === 'Ada'),
        ).toBe(true)
      } else {
        await expect(client().listLobbies()).rejects.toMatchObject({
          status: 404,
          reason: 'listing_disabled',
        })
      }
    })

    it('runs a turn end to end: private orders, seal on readiness, sealed set, confirmation', async () => {
      const { host, guest } = await lobbyOfTwo()
      await expect(guest.api.start()).rejects.toMatchObject({ status: 403, reason: 'host_only' })
      await host.api.start()

      const detail = await guest.api.get()
      expect(detail.match.status).toBe('running')
      expect(detail.match.currentTurn).toBe(1)
      expect(detail.match.seed).toEqual(expect.any(Number))
      expect(detail.match.players.map((p) => [p.displayName, p.slot])).toEqual([
        ['Ada', 0],
        ['Grace', 1],
      ])

      const controller = new AbortController()
      const streamed = collect(
        guest.api.stream({ after: detail.match.lastEventSeq, signal: controller.signal }),
        controller,
        (events) => types(events).includes('turn.confirmed'),
      )

      await host.api.submitOrders(1, { orders: orders(1), ready: true })
      await expect(guest.api.sealedOrders(1)).rejects.toMatchObject({
        status: 409,
        reason: 'turn_open',
      })
      expect((await host.api.mySubmission(1)).ready).toBe(true)
      await guest.api.submitOrders(1, { orders: orders(2), ready: true })

      const sealed = await guest.api.sealedOrders(1)
      expect(sealed.players.map((p) => [p.slot, p.orders])).toEqual([
        [0, orders(1)],
        [1, orders(2)],
      ])
      expect((await host.api.get()).match.currentTurn).toBe(2)

      await host.api.report(1, { stateHash: HASH_A, finished: false })
      await guest.api.report(1, { stateHash: HASH_A, finished: false })
      expect((await host.api.get()).match.previousTurn?.status).toBe('confirmed')

      const events = await streamed
      expect(types(events)).toEqual([
        'turn.readiness',
        'turn.readiness',
        'turn.sealed',
        'turn.opened',
        'turn.confirmed',
      ])
      expect(
        events.every((event, index) => index === 0 || event.seq > (events[index - 1]?.seq ?? 0)),
      ).toBe(true)
      const { events: paged } = await guest.api.events(detail.match.lastEventSeq)
      expect(paged).toEqual(events)
    })

    it('resumes the stream from Last-Event-ID without replaying delivered events', async () => {
      const { host, guest } = await lobbyOfTwo()
      await host.api.start()
      const all = (await guest.api.events(0)).events
      const lastSeq = all.at(-1)?.seq ?? 0
      const controller = new AbortController()
      const pending = collect(
        guest.api.streamOnce({ after: lastSeq - 1, signal: controller.signal }),
        controller,
        (events) => events.length >= 2,
      )
      await host.api.submitOrders(1, { orders: orders(1), ready: true })
      const events = await pending
      expect(events.map((event) => event.seq)).toEqual([lastSeq, lastSeq + 1])
      expect(events[1]?.type).toBe('turn.readiness')
    })

    it('flags a desync, pauses the match, and recovers when reports match the host snapshot', async () => {
      const { matchId, host, guest } = await lobbyOfTwo()
      await host.api.start()
      await host.api.submitOrders(1, { orders: orders(1), ready: true })
      await guest.api.submitOrders(1, { orders: orders(2), ready: true })
      await host.api.report(1, { stateHash: HASH_A, finished: false })
      await guest.api.report(1, { stateHash: HASH_B, finished: false })
      expect((await host.api.get()).match.status).toBe('desynced')
      await expect(
        guest.api.submitOrders(2, { orders: orders(3), ready: true }),
      ).rejects.toMatchObject({ reason: 'match_desynced' })
      await expect(guest.api.latestSnapshot()).rejects.toMatchObject({
        status: 404,
        reason: 'no_snapshot',
      })
      await host.api.uploadSnapshot({
        turn: 1,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'c2F2ZQ==',
      })
      expect((await guest.api.latestSnapshot()).body).toBe('c2F2ZQ==')
      await guest.api.report(1, { stateHash: HASH_A, finished: false })
      const detail = await guest.api.get()
      expect(detail.match.status).toBe('running')
      expect(detail.match.previousTurn?.status).toBe('confirmed')
      const { events } = await client().withToken(host.token).match(matchId).events(0)
      expect(types(events)).toContain('turn.desynced')
      expect(types(events)).toContain('snapshot.available')
    })

    it('kicking hands the slot to the AI and unblocks readiness; leaving as host passes the crown', async () => {
      const { host, guest } = await lobbyOfTwo()
      const third = await client().join({ joinCode: host.joinCode, displayName: 'Linus' })
      const thirdApi = client().withToken(third.token).match(host.match.id)
      await host.api.start()
      await host.api.submitOrders(1, { orders: orders(1), ready: true })
      await thirdApi.submitOrders(1, { orders: orders(3), ready: true })
      await host.api.kick(guest.player.id)
      expect((await host.api.get()).match.currentTurn).toBe(2)
      await expect(
        guest.api.submitOrders(2, { orders: orders(1), ready: true }),
      ).rejects.toMatchObject({ status: 403, reason: 'not_active' })
      await host.api.leave()
      const detail = await thirdApi.get()
      expect(detail.match.hostPlayerId).toBe(third.player.id)
      expect(detail.match.status).toBe('running')
    })

    it.skipIf(!harness.expireDeadlines)('seals a turn when its timer expires', async () => {
      const { host, guest } = await lobbyOfTwo(60)
      await host.api.start()
      expect((await host.api.get()).match.turn?.deadlineAt).toEqual(expect.any(String))
      await guest.api.submitOrders(1, { orders: orders(2), ready: false })
      await harness.expireDeadlines?.()
      const sealed = await host.api.sealedOrders(1)
      expect(sealed.players.map((p) => p.slot)).toEqual([1])
      expect((await host.api.get()).match.currentTurn).toBe(2)
    })
  })
}
