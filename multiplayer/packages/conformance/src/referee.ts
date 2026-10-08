import { type FetchLike, MultiplayerClient } from '@chaos-overlords/client'
import type { OrderDocument } from '@chaos-overlords/contracts'
import { fakeSealedHash, fakeStartHash } from '@chaos-overlords/kernel/testing'
import { describe, expect, it } from 'vitest'

export interface RefereeConformanceHarness {
  /** Sends a request to a facade whose kernel referees with `FakeTurnResolver`. */
  fetch: FetchLike
}

const DOCTORED = 'b'.repeat(32)

const orders = (slot: number, n: number): OrderDocument => ({
  schemaVersion: 1,
  ops: [{ op: 'cancelCommand', player: slot, gang: n }],
})

/**
 * The server refereeing turns, through the public client against a facade: the turn is confirmed
 * on the server's state as it seals, a seat that reports anything else is told it diverged and
 * finds the server's snapshot of the turn stored, and the host's start is checked against the
 * state the server built. Each runtime runs it over its own storage and wiring.
 */
export function defineRefereeConformance(harness: RefereeConformanceHarness): void {
  const client = () =>
    new MultiplayerClient({ baseUrl: 'http://conformance', fetch: harness.fetch })

  describe('referee conformance', () => {
    it('decides a turn on the server state and tells only the seat that differs', async () => {
      const anonymous = client()
      const host = await anonymous.createMatch({
        settings: {
          name: 'Refereed city',
          maxPlayers: 2,
          turnTimerSeconds: 0,
          visibility: 'private',
          gameSettings: {},
        },
        hostDisplayName: 'Ada',
      })
      const guest = await anonymous.join({ joinCode: host.joinCode, displayName: 'Grace' })
      const hostApi = anonymous.withToken(host.token).match(host.match.id)
      const guestApi = anonymous.withToken(guest.token).match(host.match.id)
      await hostApi.start()

      const started = (await hostApi.get()).match
      expect(started.refereed).toBe(true)
      const start = fakeStartHash(started.seed ?? -1)
      await expect(
        hostApi.uploadSnapshot({
          turn: 0,
          formatVersion: 1,
          stateHash: DOCTORED,
          body: 'AAAA',
          seatSummaries: [],
        }),
      ).rejects.toMatchObject({ status: 409, reason: 'uncorroborated_state_hash' })
      await hostApi.uploadSnapshot({
        turn: 0,
        formatVersion: 1,
        stateHash: start,
        body: 'AAAA',
        seatSummaries: [],
      })
      expect(await hostApi.snapshot(0)).toMatchObject({
        stateHash: start,
        uploadedByPlayerId: 'server',
      })

      await hostApi.submitOrders(1, { orders: orders(0, 1), ready: true })
      await guestApi.submitOrders(1, { orders: orders(1, 2), ready: true })
      const decided = fakeSealedHash(start, (await guestApi.sealedOrders(1)).orderSetHash)
      const confirmed = (await guestApi.events(0)).events.find(
        (event) => event.type === 'turn.confirmed',
      )
      expect(confirmed?.payload).toEqual({ turn: 1, stateHash: decided })

      await guestApi.report(1, { stateHash: DOCTORED, finished: false })
      await hostApi.report(1, { stateHash: decided, finished: false })
      const diverged = (await hostApi.events(0)).events.filter(
        (event) => event.type === 'turn.diverged',
      )
      expect(diverged.map((event) => event.payload)).toEqual([
        { turn: 1, playerId: guest.player.id, stateHash: decided, reportedStateHash: DOCTORED },
      ])
      expect(await guestApi.snapshot(1)).toMatchObject({
        stateHash: decided,
        uploadedByPlayerId: 'server',
      })
      const after = (await hostApi.get()).match
      expect(after.status).toBe('running')
      expect(after.previousTurn?.status).toBe('confirmed')
    })
  })
}
