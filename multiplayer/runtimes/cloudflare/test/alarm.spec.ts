import { env, runInDurableObject, SELF } from 'cloudflare:test'
import { MultiplayerClient } from '@chaos-overlords/client'
import { EARLY_DEADLINE_RETRY_MS } from '@chaos-overlords/kernel'
import { expect, it } from 'vitest'
import { hubFor } from '../src/kernel'
import type { MatchHub } from '../src/MatchHub'

it('arms the match hub alarm at the turn deadline when a timed match starts', async () => {
  const client = new MultiplayerClient({
    baseUrl: 'http://worker',
    fetch: (input, init) => SELF.fetch(input, init),
  })
  const host = await client.createMatch({
    settings: {
      name: 'Timed',
      maxPlayers: 2,
      turnTimerSeconds: 120,
      visibility: 'private',
      gameSettings: {},
    },
    hostDisplayName: 'Ada',
  })
  await client.join({ joinCode: host.joinCode, displayName: 'Grace' })
  const api = client.withToken(host.token).match(host.match.id)
  await api.start()
  const deadline = (await api.get()).match.turn?.deadlineAt
  expect(deadline).toEqual(expect.any(String))

  const stub = hubFor(env, host.match.id)
  const alarm = await runInDurableObject(stub as never, async (_instance: MatchHub, state) =>
    state.storage.getAlarm(),
  )
  expect(alarm).toBe(new Date(deadline as string).getTime())
})

it('seals the turn when the alarm fires, and forgets a deadline nothing is due for', async () => {
  const client = new MultiplayerClient({
    baseUrl: 'http://worker',
    fetch: (input, init) => SELF.fetch(input, init),
  })
  const host = await client.createMatch({
    settings: {
      name: 'Timed',
      maxPlayers: 2,
      turnTimerSeconds: 30,
      visibility: 'private',
      gameSettings: {},
    },
    hostDisplayName: 'Ada',
  })
  const guest = await client.join({ joinCode: host.joinCode, displayName: 'Grace' })
  const hostApi = client.withToken(host.token).match(host.match.id)
  const guestApi = client.withToken(guest.token).match(host.match.id)
  await hostApi.start()
  // Both submit a draft without pressing ready, so the deadline is what seals the turn and no
  // seat is absent (an absence vote would open turn 2 with no deadline at all).
  await hostApi.submitOrders(1, {
    orders: { schemaVersion: 1, ops: [{ op: 'cancelCommand', player: 0, gang: 1 }] },
    ready: false,
  })
  await guestApi.submitOrders(1, {
    orders: { schemaVersion: 1, ops: [{ op: 'cancelCommand', player: 1, gang: 1 }] },
    ready: false,
  })
  const stub = hubFor(env, host.match.id)
  // A deadline the wall clock has not reached is not sealed on: the alarm re-reads it and finds it
  // due only later. Move the stored deadline into the past so the fire seals now.
  await runInDurableObject(stub as never, async (instance: MatchHub, state) => {
    await env.DB.prepare('update turns set deadline_at = ? where match_id = ? and number = 1')
      .bind(Date.now() - 1000, host.match.id)
      .run()
    await instance.alarm()
    // The seal opened turn 2 with its own deadline, which replaced the fired one.
    const pending = await state.storage.get<{ turn: number }>('deadline')
    expect(pending?.turn).toBe(2)
    expect(await state.storage.getAlarm()).toEqual(expect.any(Number))
  })
  expect((await hostApi.sealedOrders(1)).players.map((p) => p.slot)).toEqual([0, 1])
  expect((await hostApi.get()).match.currentTurn).toBe(2)

  // Both players ready seals turn 2 on readiness; the alarm then fires for a turn already sealed
  // whose successor was armed in the same call. Once the match is over, nothing is due at all.
  await hostApi.submitOrders(2, {
    orders: { schemaVersion: 1, ops: [{ op: 'cancelCommand', player: 0, gang: 1 }] },
    ready: true,
  })
  await guestApi.submitOrders(2, {
    orders: { schemaVersion: 1, ops: [{ op: 'cancelCommand', player: 1, gang: 1 }] },
    ready: true,
  })
  await hostApi.report(1, { stateHash: 'a'.repeat(32), finished: false })
  await guestApi.report(1, { stateHash: 'a'.repeat(32), finished: false })
  await hostApi.report(2, { stateHash: 'b'.repeat(32), finished: true })
  await guestApi.report(2, { stateHash: 'b'.repeat(32), finished: true })
  expect((await hostApi.get()).match.status).toBe('finished')
  await runInDurableObject(stub as never, async (instance: MatchHub, state) => {
    await instance.alarm()
    expect(await state.storage.get('deadline')).toBeUndefined()
    expect(await state.storage.getAlarm()).toBeNull()
  })
})

it('re-arms an alarm that fires before the deadline, so the turn still seals on time', async () => {
  const client = new MultiplayerClient({
    baseUrl: 'http://worker',
    fetch: (input, init) => SELF.fetch(input, init),
  })
  const host = await client.createMatch({
    settings: {
      name: 'Timed',
      maxPlayers: 2,
      turnTimerSeconds: 30,
      visibility: 'private',
      gameSettings: {},
    },
    hostDisplayName: 'Ada',
  })
  await client.join({ joinCode: host.joinCode, displayName: 'Grace' })
  const api = client.withToken(host.token).match(host.match.id)
  await api.start()
  const deadline = new Date((await api.get()).match.turn?.deadlineAt as string).getTime()

  const stub = hubFor(env, host.match.id)
  await runInDurableObject(stub as never, async (instance: MatchHub, state) => {
    // What the runtime does as an alarm fires: the alarm is spent before the handler runs. This
    // one fires early, as one does when the object's clock is behind the isolate that set the
    // deadline. It used to leave the turn with no alarm at all until the cron swept it.
    await state.storage.deleteAlarm()
    await instance.alarm()
    // Firing proves the scheduler reached the deadline, so re-arming at the deadline itself would
    // fire again at once for as long as the object's clock lags. The retry goes past it instead.
    const retryAt = deadline + EARLY_DEADLINE_RETRY_MS
    expect(await state.storage.getAlarm()).toBe(retryAt)
    expect(await state.storage.get('deadline')).toEqual({
      matchId: host.match.id,
      turn: 1,
      dueAtMs: retryAt,
    })

    // Still early on the retry: each fire moves the next one on by the interval, not zero.
    await state.storage.deleteAlarm()
    await instance.alarm()
    expect(await state.storage.getAlarm()).toBe(retryAt + EARLY_DEADLINE_RETRY_MS)
  })
  expect((await api.get()).match.currentTurn).toBe(1)
})
