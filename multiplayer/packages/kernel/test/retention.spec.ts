import { beforeEach, describe, expect, it } from 'vitest'
import {
  DEFAULT_RETENTION,
  DEFAULT_RETENTION_DAYS,
  type RetentionPolicy,
  retentionPolicyFromDays,
  RetentionService,
} from '../src'
import { createHarness, type Harness } from './harness'

const DAY_MS = 24 * 60 * 60 * 1000

describe('retention windows', () => {
  let h: Harness

  beforeEach(() => {
    h = createHarness()
  })

  const retentionWith = (policy: Partial<RetentionPolicy>) =>
    new RetentionService(h, { ...DEFAULT_RETENTION, ...policy })

  const openLobby = () =>
    h.kernel.lobby.createMatch({
      settings: {
        name: 'Waiting room',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'public',
        gameSettings: {},
      },
      hostDisplayName: 'Host',
    })

  it('collects a lobby nobody started on its own, shorter window', async () => {
    const lobby = await openLobby()

    h.clock.advance(DEFAULT_RETENTION.lobbyMaxAgeMs)
    expect(await h.kernel.retention.collect()).toBe(0)
    expect(h.storage.statusOf(lobby.match.id)).toBe('lobby')

    h.clock.advance(1)
    expect(await h.kernel.retention.collect()).toBe(1)
    expect(h.storage.statusOf(lobby.match.id)).toBeUndefined()
  })

  it('keeps each kind of match for its own window, and 0 keeps that kind forever', async () => {
    const lobby = await openLobby()
    const finished = await openLobby()
    await h.kernel.lobby.leave(await h.principalOf(finished.token))
    expect(h.storage.statusOf(finished.match.id)).toBe('abandoned')

    const keepLobbies = retentionWith({ lobbyMaxAgeMs: 0, finishedMaxAgeMs: DAY_MS })
    h.clock.advance(365 * DAY_MS)
    expect(await keepLobbies.collect()).toBe(1)
    expect(h.storage.statusOf(lobby.match.id)).toBe('lobby')
    expect(h.storage.statusOf(finished.match.id)).toBeUndefined()
  })

  it('collects a silent running match whose players never left', async () => {
    const { host } = await h.startedMatch()

    h.clock.advance(DEFAULT_RETENTION.abandonedLiveMaxAgeMs + 1)
    expect(await h.kernel.retention.collect()).toBe(0)
    expect(h.storage.statusOf(host.match.id)).toBe('running')

    h.clock.advance(DEFAULT_RETENTION.silentLiveMaxAgeMs - DEFAULT_RETENTION.abandonedLiveMaxAgeMs)
    expect(await h.kernel.retention.collect()).toBe(1)
    expect(h.storage.statusOf(host.match.id)).toBeUndefined()
  })

  it('deletes at most one batch per window per pass', async () => {
    for (let i = 0; i < 3; i++) await openLobby()
    const retention = retentionWith({ batchSize: 2 })
    h.clock.advance(DEFAULT_RETENTION.lobbyMaxAgeMs + 1)
    expect(await retention.collect()).toBe(2)
    expect(await retention.collect()).toBe(1)
  })
})

describe('retentionPolicyFromDays', () => {
  it('is what the defaults are built from', () => {
    expect(retentionPolicyFromDays(DEFAULT_RETENTION_DAYS)).toEqual(DEFAULT_RETENTION)
  })

  it('keeps the silent window three times the abandoned one unless it is stated', () => {
    const derived = retentionPolicyFromDays({ finished: 7, lobby: 1, abandonedLive: 10 })
    expect(derived.silentLiveMaxAgeMs).toBe(30 * DAY_MS)
    // Switching the abandoned window off keeps running matches forever, as it always has.
    expect(
      retentionPolicyFromDays({ finished: 7, lobby: 1, abandonedLive: 0 }).silentLiveMaxAgeMs,
    ).toBe(0)
    expect(
      retentionPolicyFromDays({ finished: 7, lobby: 1, abandonedLive: 10, silentLive: 12 })
        .silentLiveMaxAgeMs,
    ).toBe(12 * DAY_MS)
  })
})
