import { type FetchLike, MultiplayerApiError, MultiplayerClient } from '@chaos-overlords/client'
import {
  LIMITS,
  type MatchEvent,
  type MatchEventType,
  type OrderDocument,
} from '@chaos-overlords/contracts'
import { describe, expect, it } from 'vitest'

export interface HttpConformanceHarness {
  /** Sends a request to the facade under test (an in-process app or a live server). */
  fetch: FetchLike
  /** Whether the facade was configured with public lobby listing. */
  publicListing: boolean
  /** Make every open deadline due and run the sweep, when the harness controls time. */
  expireDeadlines?: () => Promise<void>
}

const HASH_A = 'a'.repeat(32)
const HASH_B = 'b'.repeat(32)
/**
 * A one-op document for the player seated in `slot`. Ops name their own slot because the server
 * refuses any that do not, so the fixture has to know which seat it is submitting for.
 */
const orders = (slot: number, n: number): OrderDocument => ({
  schemaVersion: 1,
  ops: [{ op: 'cancelCommand', player: slot, gang: n }],
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
        reason: 'invalid_request',
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

      await host.api.submitOrders(1, { orders: orders(0, 1), ready: true })
      await expect(guest.api.sealedOrders(1)).rejects.toMatchObject({
        status: 409,
        reason: 'turn_open',
      })
      expect((await host.api.mySubmission(1)).ready).toBe(true)
      await guest.api.submitOrders(1, { orders: orders(1, 2), ready: true })

      const sealed = await guest.api.sealedOrders(1)
      expect(sealed.players.map((p) => [p.slot, p.orders])).toEqual([
        [0, orders(0, 1)],
        [1, orders(1, 2)],
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
      await host.api.submitOrders(1, { orders: orders(0, 1), ready: true })
      const events = await pending
      expect(events.map((event) => event.seq)).toEqual([lastSeq, lastSeq + 1])
      expect(events[1]?.type).toBe('turn.readiness')
    })

    it('flags a desync, pauses the match, and recovers when reports match the host snapshot', async () => {
      const { matchId, host, guest } = await lobbyOfTwo()
      await host.api.start()
      await host.api.submitOrders(1, { orders: orders(0, 1), ready: true })
      await guest.api.submitOrders(1, { orders: orders(1, 2), ready: true })
      await host.api.report(1, { stateHash: HASH_A, finished: false })
      await guest.api.report(1, { stateHash: HASH_B, finished: false })
      expect((await host.api.get()).match.status).toBe('desynced')
      await expect(
        guest.api.submitOrders(2, { orders: orders(1, 3), ready: true }),
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
        seatSummaries: [],
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

    it('lets a lobby member change their own name and face until the match starts', async () => {
      const { host, guest } = await lobbyOfTwo()
      await guest.api.updateProfile({ displayName: 'Hopper', portraitId: 6 })
      const renamed = (await host.api.get()).match.players.find((p) => p.id === guest.player.id)
      expect(renamed).toMatchObject({ displayName: 'Hopper', portraitId: 6 })
      await expect(
        guest.api.updateProfile({ displayName: 'ada', portraitId: 6 }),
      ).rejects.toMatchObject({ status: 409, reason: 'display_name_taken' })
      await host.api.start()
      await expect(
        guest.api.updateProfile({ displayName: 'Grace', portraitId: 2 }),
      ).rejects.toMatchObject({ status: 409, reason: 'match_not_in_lobby' })
      const started = (await host.api.get()).match.players.find((p) => p.id === guest.player.id)
      expect(started).toMatchObject({ displayName: 'Hopper', portraitId: 6 })
    })

    it('kicking opens a takeover vote and unblocks readiness; leaving as host passes the crown', async () => {
      const { host, guest } = await lobbyOfTwo()
      const third = await client().join({ joinCode: host.joinCode, displayName: 'Linus' })
      const thirdApi = client().withToken(third.token).match(host.match.id)
      await host.api.start()
      await host.api.submitOrders(1, { orders: orders(0, 1), ready: true })
      await thirdApi.submitOrders(1, { orders: orders(2, 3), ready: true })
      await host.api.kick(guest.player.id)
      expect((await host.api.get()).match.currentTurn).toBe(2)
      // The kick revokes the token, so the kicked player loses reads as well as writes: no orders,
      // no sealed sets of the turns that follow, no stream.
      await expect(
        guest.api.submitOrders(2, { orders: orders(1, 1), ready: true }),
      ).rejects.toMatchObject({ status: 401, reason: 'invalid_token' })
      await expect(guest.api.get()).rejects.toMatchObject({ status: 401 })
      await expect(guest.api.sealedOrders(1)).rejects.toMatchObject({ status: 401 })
      await host.api.leave()
      const detail = await thirdApi.get()
      expect(detail.match.hostPlayerId).toBe(third.player.id)
      expect(detail.match.status).toBe('running')
    })

    it('lets a former member rejoin and puts the seats that went quiet to them', async () => {
      const { host, guest } = await lobbyOfTwo()
      await host.api.start()
      await guest.api.leave()
      // Nobody is present when the host goes, so no vote could be opened for that seat then.
      await host.api.leave()
      await guest.api.rejoin()
      const detail = await guest.api.get()
      expect(detail.match.hostPlayerId).toBe(guest.player.id)
      expect(detail.match.players.find((p) => p.id === guest.player.id)?.status).toBe('active')
      const { events } = await guest.api.events(0)
      expect(
        events
          .filter((event) => event.type === 'match.takeoverVoteRequested')
          .map((event) => (event.payload as { playerId: string }).playerId),
      ).toEqual([guest.player.id, host.player.id])
      // One present player is the whole electorate: their vote makes the seat computer controlled.
      await guest.api.voteOnTakeover(host.player.id, { decision: 'computer' })
      expect(
        (await guest.api.get()).match.players.find((p) => p.id === host.player.id)?.status,
      ).toBe('computer')
      await expect(
        guest.api.voteOnTakeover(host.player.id, { decision: 'computer' }),
      ).rejects.toMatchObject({ status: 409, reason: 'takeover_not_pending' })
    })

    it('seats a late joiner in a never-human slot once the bootstrap snapshot exists', async () => {
      const anonymous = client()
      const host = await anonymous.createMatch({
        settings: { ...settings, gameSettings: { allowLateJoin: true } },
        hostDisplayName: 'Ada',
      })
      const hostApi = anonymous.withToken(host.token).match(host.match.id)
      await hostApi.start()
      await expect(
        anonymous.joinRunning({ match: host.match.id, slot: 1, displayName: 'Late' }),
      ).rejects.toMatchObject({ status: 409, reason: 'late_join_not_ready' })
      await hostApi.uploadSnapshot({
        turn: 0,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'c2F2ZQ==',
        seatSummaries: [{ slot: 1, gangs: 2, sites: 3, sectors: 4 }],
      })
      if (harness.publicListing) {
        const listed = (await anonymous.listLobbies()).matches.find((l) => l.id === host.match.id)
        expect(listed?.availableSlots).toContain(1)
        expect(listed?.availableSeatSummaries).toContainEqual({
          slot: 1,
          gangs: 2,
          sites: 3,
          sectors: 4,
        })
      }
      await expect(
        anonymous.joinRunning({ match: host.match.id, slot: 0, displayName: 'Late' }),
      ).rejects.toMatchObject({ status: 409, reason: 'seat_reserved' })
      const late = await anonymous.joinRunning({
        match: host.match.id,
        slot: 1,
        displayName: 'Late',
      })
      expect(late.player.slot).toBe(1)
      const lateApi = anonymous.withToken(late.token).match(host.match.id)
      // Seated into the open turn: the newcomer can submit to it straight away.
      expect((await lateApi.submitOrders(1, { orders: orders(1, 1), ready: false })).ready).toBe(
        false,
      )
      await expect(
        anonymous.joinRunning({ match: host.match.id, slot: 1, displayName: 'Later' }),
      ).rejects.toMatchObject({ status: 409, reason: 'seat_reserved' })
    })

    it('names the refused field without echoing what was sent, and caps every body', async () => {
      const response = await harness.fetch('http://conformance/api/v1/matches/join', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ joinCode: 'ABCDEFGH', displayName: '', password: 'hunter2' }),
      })
      expect(response.status).toBe(422)
      const body = await response.json()
      expect(body.error.details.reason).toBe('invalid_request')
      const issues = body.error.details.issues as Array<{ message: string; path: string[] }>
      expect(issues.some((issue) => issue.path.join('.') === 'displayName')).toBe(true)
      expect(JSON.stringify(body)).not.toContain('hunter2')

      const oversized = await harness.fetch('http://conformance/api/v1/matches/join', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ joinCode: 'ABCDEFGH', displayName: 'x'.repeat(20 * 1024) }),
      })
      expect(oversized.status).toBe(413)
      expect((await oversized.json()).error.code).toBe('payload_too_large')
    })

    it.skipIf(!harness.expireDeadlines)('seals a turn when its timer expires', async () => {
      const { host, guest } = await lobbyOfTwo(60)
      await host.api.start()
      expect((await host.api.get()).match.turn?.deadlineAt).toEqual(expect.any(String))
      await guest.api.submitOrders(1, { orders: orders(1, 2), ready: false })
      await harness.expireDeadlines?.()
      const sealed = await host.api.sealedOrders(1)
      expect(sealed.players.map((p) => p.slot)).toEqual([1])
      expect((await host.api.get()).match.currentTurn).toBe(2)
    })
  })

  describe('http conformance: when to ask again', () => {
    /** A member's raw read of the match, so the status and the tag headers can be read. */
    const readMatch = (matchId: string, token: string, ifNoneMatch?: string) =>
      harness.fetch(`http://conformance/api/v1/matches/${matchId}`, {
        headers: {
          authorization: `Bearer ${token}`,
          accept: 'application/json',
          ...(ifNoneMatch === undefined ? {} : { 'if-none-match': ifNoneMatch }),
        },
      })

    /** A whole number of seconds no shorter than one, as every 429 must carry. */
    const expectRetryAfter = (response: Response) => {
      const value = response.headers.get('retry-after')
      expect(value).toMatch(/^\d+$/)
      expect(Number(value)).toBeGreaterThanOrEqual(1)
    }

    it('answers an unchanged lobby read with 304 and a changed one with a new tag', async () => {
      const { matchId, host, guest } = await lobbyOfTwo()
      const first = await readMatch(matchId, host.token)
      expect(first.status).toBe(200)
      const tag = first.headers.get('etag') ?? ''
      expect(tag).toMatch(/^"[^"]+"$/)
      expect(first.headers.get('cache-control')).toMatch(/private/)
      await first.json()

      const unchanged = await readMatch(matchId, host.token, tag)
      expect(unchanged.status).toBe(304)
      expect(unchanged.headers.get('etag')).toBe(tag)
      expect(await unchanged.text()).toBe('')
      // A proxy may weaken the tag on the way out; the comparison is the weak one.
      expect((await readMatch(matchId, host.token, `"other", W/${tag}`)).status).toBe(304)

      // The detail names its reader, so one member's tag is never another's.
      const asGuest = await readMatch(matchId, guest.token, tag)
      expect(asGuest.status).toBe(200)
      expect(asGuest.headers.get('etag')).not.toBe(tag)
      await asGuest.json()

      // A roster change publishes an event, a settings change moves the match row: both re-tag.
      await guest.api.updateProfile({ displayName: 'Hopper', portraitId: 3 })
      const renamed = await readMatch(matchId, host.token, tag)
      expect(renamed.status).toBe(200)
      const renamedTag = renamed.headers.get('etag') ?? ''
      expect(renamedTag).not.toBe(tag)
      const detail = (await renamed.json()) as { match: { players: { displayName: string }[] } }
      expect(detail.match.players.map((p) => p.displayName)).toEqual(['Ada', 'Hopper'])

      await host.api.updateSettings({ ...settings, name: 'Renamed city' })
      const resettled = await readMatch(matchId, host.token, renamedTag)
      expect(resettled.status).toBe(200)
      const settled = (await resettled.json()) as { match: { settings: { name: string } } }
      expect(settled.match.settings.name).toBe('Renamed city')
    })

    it('never answers a running match from a tag', async () => {
      const { matchId, host } = await lobbyOfTwo()
      const lobby = await readMatch(matchId, host.token)
      const tag = lobby.headers.get('etag') ?? ''
      await lobby.json()
      await host.api.start()
      const running = await readMatch(matchId, host.token, tag)
      expect(running.status).toBe(200)
      expect(running.headers.get('etag')).toBeNull()
      expect(((await running.json()) as { match: { status: string } }).match.status).toBe('running')
      // `*` names any current copy, and a running match has none a client may keep.
      const starred = await readMatch(matchId, host.token, '*')
      expect(starred.status).toBe(200)
      await starred.json()
    })

    it('says when to come back after too many password attempts', async () => {
      const host = await client().createMatch({
        settings: { ...settings, name: 'Gated city' },
        hostDisplayName: 'Ada',
        password: 'opensesame',
      })
      let refused: Response | undefined
      for (let attempt = 0; attempt < 20 && refused === undefined; attempt += 1) {
        const response = await harness.fetch('http://conformance/api/v1/matches/join', {
          method: 'POST',
          headers: { 'content-type': 'application/json' },
          body: JSON.stringify({
            joinCode: host.joinCode,
            displayName: 'Mallory',
            password: `guess-${attempt}-wrong`,
          }),
        })
        if (response.status === 429) refused = response
        else {
          expect(response.status).toBe(401)
          await response.json()
        }
      }
      if (!refused) throw new Error('the password-attempt budget never ran out')
      expectRetryAfter(refused)
      const body = (await refused.json()) as {
        error: { code: string; details: { retryAfterSeconds?: number } }
      }
      expect(body.error.code).toBe('rate_limited')
      expect(String(body.error.details.retryAfterSeconds)).toBe(refused.headers.get('retry-after'))
    })

    it('says when to come back after refusing a stream past the per-match cap', async () => {
      // A player's own stale streams make room for their next one, so the cap a client cannot talk
      // its way past is the match's: every seat holding its three, plus a stream that outlived the
      // membership it was opened under, which a lobby leaver's is until the hub notices.
      const anonymous = client()
      const host = await anonymous.createMatch({
        settings: { ...settings, maxPlayers: LIMITS.maxPlayers },
        hostDisplayName: 'Ada',
      })
      const members = [host]
      for (let n = 1; n < LIMITS.maxPlayers; n += 1) {
        members.push(await anonymous.join({ joinCode: host.joinCode, displayName: `Seat ${n}` }))
      }
      const stream = (token: string, signal: AbortSignal) =>
        harness.fetch(`http://conformance/api/v1/matches/${host.match.id}/stream`, {
          headers: { authorization: `Bearer ${token}`, accept: 'text/event-stream' },
          signal,
        })
      const controller = new AbortController()
      try {
        for (const member of members) {
          for (let n = 0; n < 3; n += 1) {
            expect((await stream(member.token, controller.signal)).status).toBe(200)
          }
        }
        const leaver = members.at(-1)
        if (!leaver) throw new Error('no members')
        await anonymous.withToken(leaver.token).match(host.match.id).leave()
        const late = await anonymous.join({ joinCode: host.joinCode, displayName: 'Late' })
        const refused = await stream(late.token, controller.signal)
        expect(refused.status).toBe(429)
        expectRetryAfter(refused)
        expect((await refused.json()).error).toMatchObject({
          code: 'rate_limited',
          details: { reason: 'too_many_streams', scope: 'match' },
        })
      } finally {
        controller.abort()
      }
    })
  })
}
