import { type FetchLike, MultiplayerApiError, MultiplayerClient } from '@chaos-overlords/client'
import {
  LIMITS,
  type MatchEvent,
  type MatchEventType,
  type OrderDocument,
  SSE_HEARTBEAT_COMMENT,
} from '@chaos-overlords/contracts'
import { describe, expect, it } from 'vitest'

export interface HttpConformanceHarness {
  /** Sends a request to the facade under test (an in-process app or a live server). */
  fetch: FetchLike
  /** Whether the facade was configured with public lobby listing. */
  publicListing: boolean
  /** Make every open deadline due and run the sweep, when the harness controls time. */
  expireDeadlines?: () => Promise<void>
  /**
   * The interval between event stream keepalive frames, when the harness has shortened it enough
   * for a test to wait one out. Left out, the keepalive case is skipped, and the stream-cap case
   * leaves its streams unread.
   */
  keepaliveMs?: number
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
 * Reads a raw response body as text until `until` holds or `timeoutMs` passes, then cancels it.
 *
 * The client SDK parses frames and drops comments, so the frames the SDK hides (the keepalive) and
 * the ids it consumes (the resume point) are read here byte for byte.
 */
async function readText(
  response: Response,
  until: (text: string) => boolean,
  timeoutMs = 5000,
): Promise<string> {
  const body = response.body
  if (!body) throw new Error('the response has no body')
  const reader = body.getReader()
  const decoder = new TextDecoder()
  let text = ''
  const timer = setTimeout(() => {
    void reader.cancel().catch(() => undefined)
  }, timeoutMs)
  try {
    for (;;) {
      const { value, done } = await reader.read()
      if (done) break
      text += decoder.decode(value, { stream: true })
      if (until(text)) break
    }
  } finally {
    clearTimeout(timer)
    await reader.cancel().catch(() => undefined)
  }
  return text
}

/** The sequence numbers of the event frames in a raw stream, in order. */
const frameIds = (text: string): number[] =>
  [...text.matchAll(/^id: ?(\d+)$/gm)].map((match) => Number(match[1]))

/** Every response carries a correlation id, refusals and the event stream included. */
function expectRequestId(response: Response): void {
  expect(response.headers.get('x-request-id')).toMatch(/^[A-Za-z0-9_-]+$/)
}

/** A whole number of seconds no shorter than one, as every 429 must carry. */
function expectRetryAfter(response: Response): void {
  const value = response.headers.get('retry-after')
  expect(value).toMatch(/^\d+$/)
  expect(Number(value)).toBeGreaterThanOrEqual(1)
}

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

  /** A raw JSON POST, for the refusals the typed client never sends. */
  const post = (path: string, body: string, token?: string) =>
    harness.fetch(`http://conformance/api/v1${path}`, {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        ...(token ? { authorization: `Bearer ${token}` } : {}),
      },
      body,
    })

  /** A raw event stream request, so the status, the headers and every frame can be read. */
  const openStream = (
    matchId: string,
    token: string,
    options: { query?: string; lastEventId?: number; signal: AbortSignal },
  ) =>
    harness.fetch(`http://conformance/api/v1/matches/${matchId}/stream${options.query ?? ''}`, {
      headers: {
        authorization: `Bearer ${token}`,
        accept: 'text/event-stream',
        ...(options.lastEventId === undefined
          ? {}
          : { 'last-event-id': String(options.lastEventId) }),
      },
      signal: options.signal,
    })

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

    it('relays Comlink ops in the sealed set and refuses one for another seat or untypeable text', async () => {
      const { host, guest } = await lobbyOfTwo()
      await host.api.start()
      const send: OrderDocument = {
        schemaVersion: 1,
        ops: [{ op: 'sendComlinkMessage', player: 0, recipients: [1], text: 'MEET AT "DAWN".' }],
      }
      const read: OrderDocument = {
        schemaVersion: 1,
        ops: [{ op: 'markComlinkRead', player: 1, sequence: 0 }],
      }

      await expect(
        guest.api.submitOrders(1, {
          orders: { schemaVersion: 1, ops: [{ ...send.ops[0], player: 0 }] } as OrderDocument,
          ready: false,
        }),
      ).rejects.toMatchObject({ status: 422, reason: 'foreign_slot_ops' })
      await expect(
        host.api.submitOrders(1, {
          orders: {
            schemaVersion: 1,
            ops: [{ op: 'sendComlinkMessage', player: 0, recipients: [1], text: 'lower case' }],
          },
          ready: false,
        }),
      ).rejects.toMatchObject({ status: 422 })

      await host.api.submitOrders(1, { orders: send, ready: true })
      await guest.api.submitOrders(1, { orders: read, ready: true })

      const sealed = await guest.api.sealedOrders(1)
      expect(sealed.players.map((p) => [p.slot, p.orders])).toEqual([
        [0, send],
        [1, read],
      ])
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

    it('relays lobby chat through the event log until the match starts', async () => {
      const { host, guest } = await lobbyOfTwo()
      await guest.api.postChat('  helló there  ')
      await host.api.postChat('ready when you are')
      const { events } = await host.api.events(0)
      const chat = events.filter((event) => event.type === 'lobby.chatMessage')
      expect(chat.map((event) => event.payload)).toEqual([
        { playerId: guest.player.id, text: 'helló there' },
        { playerId: host.player.id, text: 'ready when you are' },
      ])
      await expect(guest.api.postChat('‮evil')).rejects.toMatchObject({
        status: 422,
      })
      await expect(guest.api.postChat('x'.repeat(161))).rejects.toMatchObject({ status: 422 })
      await host.api.start()
      await expect(guest.api.postChat('too late')).rejects.toMatchObject({
        status: 409,
        reason: 'match_not_in_lobby',
      })
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

    it('seats a late joiner in a seat the vote handed to the computer, ending its old token', async () => {
      const anonymous = client()
      const host = await anonymous.createMatch({
        settings: { ...settings, gameSettings: { allowLateJoin: true } },
        hostDisplayName: 'Ada',
      })
      const guest = await anonymous.join({ joinCode: host.joinCode, displayName: 'Grace' })
      const hostApi = anonymous.withToken(host.token).match(host.match.id)
      const guestApi = anonymous.withToken(guest.token).match(host.match.id)
      await hostApi.start()
      await hostApi.uploadSnapshot({
        turn: 0,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'c2F2ZQ==',
        seatSummaries: [],
      })
      const guestSlot =
        (await hostApi.get()).match.players.find((p) => p.id === guest.player.id)?.slot ?? -1
      await guestApi.leave()
      await expect(
        anonymous.joinRunning({ match: host.match.id, slot: guestSlot, displayName: 'Late' }),
      ).rejects.toMatchObject({ status: 409, reason: 'seat_reserved' })

      await hostApi.voteOnTakeover(guest.player.id, { decision: 'computer' })
      if (harness.publicListing) {
        const listed = (await anonymous.listLobbies()).matches.find((l) => l.id === host.match.id)
        expect(listed?.availableSlots).toContain(guestSlot)
      }
      const late = await anonymous.joinRunning({
        match: host.match.id,
        slot: guestSlot,
        displayName: 'Late',
      })

      expect(late.player.slot).toBe(guestSlot)
      expect(late.player.id).not.toBe(guest.player.id)
      await expect(guestApi.rejoin()).rejects.toMatchObject({ status: 401 })
      const roster = (await hostApi.get()).match.players.filter((p) => p.slot === guestSlot)
      expect(roster.map((p) => [p.id, p.status])).toEqual([
        [guest.player.id, 'computer'],
        [late.player.id, 'active'],
      ])
    })

    it('names the refused field without echoing what was sent, and caps every body', async () => {
      const response = await post(
        '/matches/join',
        JSON.stringify({ joinCode: 'ABCDEFGH', displayName: '', password: 'hunter2' }),
      )
      expect(response.status).toBe(422)
      const body = await response.json()
      expect(body.error.details.reason).toBe('invalid_request')
      const issues = body.error.details.issues as Array<{ message: string; path: string[] }>
      expect(issues.some((issue) => issue.path.join('.') === 'displayName')).toBe(true)
      expect(JSON.stringify(body)).not.toContain('hunter2')

      const oversized = await post(
        '/matches/join',
        JSON.stringify({ joinCode: 'ABCDEFGH', displayName: 'x'.repeat(20 * 1024) }),
      )
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

    it('admits a joiner to a password-protected match only with the password', async () => {
      const anonymous = client()
      const host = await anonymous.createMatch({
        settings: { ...settings, name: 'Gated city' },
        hostDisplayName: 'Ada',
        password: 'opensesame',
      })
      if (harness.publicListing) {
        const listed = (await anonymous.listLobbies()).matches.find((l) => l.id === host.match.id)
        expect(listed?.passwordProtected).toBe(true)
      }
      await expect(
        anonymous.join({ joinCode: host.joinCode, displayName: 'Grace' }),
      ).rejects.toMatchObject({ status: 401, reason: 'password_required' })
      await expect(
        anonymous.join({ joinCode: host.joinCode, displayName: 'Grace', password: 'opensesamf' }),
      ).rejects.toMatchObject({ status: 401, reason: 'wrong_password' })
      const guest = await anonymous.join({
        joinCode: host.joinCode,
        displayName: 'Grace',
        password: 'opensesame',
      })
      const detail = await anonymous.withToken(guest.token).match(host.match.id).get()
      expect(detail.match.players.map((p) => p.displayName)).toEqual(['Ada', 'Grace'])
      // A password the contract refuses is refused before anything is stored.
      await expect(
        anonymous.createMatch({ settings, hostDisplayName: 'Ada', password: 'short' }),
      ).rejects.toMatchObject({ status: 422, reason: 'invalid_request' })
    })

    it('does not echo a secret of the wrong type', async () => {
      // A number fails on its type rather than its length, and the validator's default message for
      // a type refusal quotes the value it received.
      const secret = 902_137_465
      for (const [path, body] of [
        ['/matches/join', { joinCode: 'ABCDEFGH', displayName: 'Grace', password: secret }],
        ['/matches', { settings, hostDisplayName: 'Ada', password: secret }],
      ] as const) {
        const response = await post(path, JSON.stringify(body))
        expect(response.status).toBe(422)
        expectRequestId(response)
        const text = await response.text()
        expect(text).not.toContain(String(secret))
        const issues = JSON.parse(text).error.details.issues as Array<{ path: string[] }>
        expect(issues.some((issue) => issue.path.join('.') === 'password')).toBe(true)
      }
    })

    it('answers malformed JSON with 422', async () => {
      const response = await post('/matches/join', '{"joinCode": "ABCDEFGH", "displayName":')
      expect(response.status).toBe(422)
      expectRequestId(response)
      expect((await response.json()).error).toMatchObject({
        code: 'validation_failed',
        requestId: response.headers.get('x-request-id'),
      })
    })

    it('puts a request id on every response, the event stream included', async () => {
      const health = await harness.fetch('http://conformance/health')
      expectRequestId(health)
      const created = await post('/matches', JSON.stringify({ settings, hostDisplayName: 'Ada' }))
      expect(created.status).toBe(201)
      expectRequestId(created)
      const { match, token } = (await created.json()) as { match: { id: string }; token: string }
      const refused = await harness.fetch(`http://conformance/api/v1/matches/${match.id}`)
      expect(refused.status).toBe(401)
      expectRequestId(refused)
      const controller = new AbortController()
      try {
        const stream = await openStream(match.id, token, { signal: controller.signal })
        expect(stream.status).toBe(200)
        expect(stream.headers.get('content-type')).toMatch(/^text\/event-stream/)
        expectRequestId(stream)
        // A caller's own correlation id is adopted rather than replaced.
        const tagged = await harness.fetch('http://conformance/health', {
          headers: { 'x-request-id': 'conformance-tag-1' },
        })
        expect(tagged.headers.get('x-request-id')).toBe('conformance-tag-1')
      } finally {
        controller.abort()
      }
    })

    it('resumes from Last-Event-ID when ?after= says otherwise', async () => {
      const { matchId, host, guest } = await lobbyOfTwo()
      await host.api.start()
      const lastSeq = (await guest.api.events(0)).events.at(-1)?.seq ?? 0
      expect(lastSeq).toBeGreaterThan(1)
      const controller = new AbortController()
      try {
        const response = await openStream(matchId, guest.token, {
          query: '?after=0',
          lastEventId: lastSeq - 1,
          signal: controller.signal,
        })
        expect(response.status).toBe(200)
        const text = await readText(response, (seen) => frameIds(seen).includes(lastSeq))
        expect(frameIds(text)).toEqual([lastSeq])
      } finally {
        controller.abort()
      }
    })

    it('refuses a stream past the per-match cap with 429', async () => {
      // A player's own stale streams make room for their next one, so the cap a client cannot talk
      // its way past is the match's: every seat holding its three, plus a stream that outlived the
      // membership it was opened under. A lobby leaver's stream is one of those until the hub's
      // periodic membership check catches up with it.
      const anonymous = client()
      const host = await anonymous.createMatch({
        settings: { ...settings, maxPlayers: LIMITS.maxPlayers },
        hostDisplayName: 'Ada',
      })
      const members = [host]
      for (let n = 1; n < LIMITS.maxPlayers; n += 1) {
        members.push(await anonymous.join({ joinCode: host.joinCode, displayName: `Seat ${n}` }))
      }
      const controller = new AbortController()
      try {
        for (const member of members) {
          for (let n = 0; n < 3; n += 1) {
            const response = await openStream(host.match.id, member.token, {
              signal: controller.signal,
            })
            expect(response.status).toBe(200)
            // Read and thrown away, so the hub does not drop the stream as stalled: unread, an
            // in-process stream fills its queue within a couple of seconds of 50 ms keepalives, and
            // one dropped stream leaves room for the stream this case expects refused. A harness
            // on the default keepalive takes minutes to fill the queue, and is left unread: on
            // workerd, aborting a stream that is being read leaves rejections unhandled inside the
            // runtime, which Vitest counts as errors of the run.
            if (harness.keepaliveMs !== undefined) {
              void response.body?.pipeTo(new WritableStream()).catch(() => undefined)
            }
          }
        }
        const leaver = members.at(-1)
        if (!leaver) throw new Error('no members')
        await anonymous.withToken(leaver.token).match(host.match.id).leave()
        const late = await anonymous.join({ joinCode: host.joinCode, displayName: 'Late' })
        const refused = await openStream(host.match.id, late.token, { signal: controller.signal })
        expect(refused.status).toBe(429)
        expectRequestId(refused)
        expectRetryAfter(refused)
        expect((await refused.json()).error).toMatchObject({
          code: 'rate_limited',
          details: { reason: 'too_many_streams', scope: 'match' },
        })
      } finally {
        controller.abort()
      }
    })

    it.skipIf(harness.keepaliveMs === undefined)(
      'sends a keepalive frame on an idle stream',
      async () => {
        const { matchId, host } = await lobbyOfTwo()
        const controller = new AbortController()
        try {
          const response = await openStream(matchId, host.token, { signal: controller.signal })
          expect(response.status).toBe(200)
          const text = await readText(
            response,
            (seen) => seen.includes(`: ${SSE_HEARTBEAT_COMMENT}\n\n`),
            (harness.keepaliveMs ?? 0) * 3 + 2000,
          )
          expect(text).toContain(`: ${SSE_HEARTBEAT_COMMENT}\n\n`)
        } finally {
          controller.abort()
        }
      },
    )
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
  })

  describe('http conformance: oversized bodies', () => {
    // Last on purpose. The refusal goes out before the body is read, and the Node listener destroys
    // a connection whose unread body has not drained within half a second, which fails whatever
    // request the client queued on that connection next; docs/MULTIPLAYER-REVIEW.md tracks it.
    it('answers an oversized snapshot with 413 before reading it', async () => {
      const { matchId, host } = await lobbyOfTwo()
      // The cap is the snapshot limit plus an allowance for the rest of the envelope (16 KB in
      // packages/server/src/app.ts). Going over it by 32 KB keeps the case on the 413 path; an
      // allowance of 32 KB or more would let the body through to the schema and answer 422.
      const response = await post(
        `/matches/${matchId}/snapshots`,
        JSON.stringify({
          turn: 0,
          formatVersion: 1,
          stateHash: HASH_A,
          body: 'A'.repeat(LIMITS.snapshotBase64Bytes + 32 * 1024),
          seatSummaries: [],
        }),
        host.token,
      )
      expect(response.status).toBe(413)
      expectRequestId(response)
      expect((await response.json()).error.code).toBe('payload_too_large')
    })
  })
}
