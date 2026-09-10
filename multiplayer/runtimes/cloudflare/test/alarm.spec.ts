import { env, runInDurableObject, SELF } from 'cloudflare:test'
import { MultiplayerClient } from '@chaos-overlords/client'
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
