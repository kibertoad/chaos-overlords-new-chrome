import { LIMITS } from '@chaos-overlords/contracts'
import { beforeEach, describe, expect, it } from 'vitest'
import { releasedTurn } from '../src/logic/spectating'
import { spectatorStartTurn } from '../src/services/spectatorRelease'
import { createHarness, type Harness } from './harness'

const settings = {
  name: 'Watched City',
  maxPlayers: 3,
  turnTimerSeconds: 0,
  visibility: 'private' as const,
  gameSettings: { seatSummaries: [{ slot: 2, gangs: 1, sites: 1, sectors: 1 }], scenario: 3 },
  spectatorDelayTurns: 2,
}

describe('releasedTurn', () => {
  const match = (status: string, currentTurn: number) =>
    ({ status, currentTurn, settings: { ...settings } }) as Parameters<typeof releasedTurn>[0]

  it('releases nothing in the lobby and holds a running match back by the delay', () => {
    expect(releasedTurn(match('lobby', 0), null)).toBe(0)
    expect(releasedTurn(match('running', 1), null)).toBe(0)
    expect(releasedTurn(match('running', 3), null)).toBe(0)
    expect(releasedTurn(match('running', 4), null)).toBe(1)
    expect(releasedTurn(match('desynced', 10), null)).toBe(7)
  })

  it('releases every sealed turn once the match is over', () => {
    expect(releasedTurn(match('finished', 10), { status: 'open', orderSetHash: null })).toBe(9)
    expect(releasedTurn(match('finished', 10), { status: 'confirmed', orderSetHash: 'h' })).toBe(10)
    expect(releasedTurn(match('abandoned', 6), null)).toBe(5)
  })
})

describe('spectators', () => {
  let h: Harness

  beforeEach(() => {
    h = createHarness()
  })

  async function watchedLobby(delay: number | null = 2) {
    const host = await h.kernel.lobby.createMatch({
      settings: { ...settings, spectatorDelayTurns: delay },
      hostDisplayName: 'Host',
    })
    return host
  }

  it('leaves the live seat summaries out of what a spectator reads', async () => {
    const host = await watchedLobby()
    const watcher = await h.kernel.spectators.join({ joinCode: host.joinCode, displayName: 'W' })
    expect(watcher.match.settings.gameSettings).toEqual({ scenario: 3 })
    expect(watcher.match.settings.spectatorDelayTurns).toBe(2)
  })

  it('admits a fixed number of spectators over the life of a match', async () => {
    const host = await watchedLobby()
    for (let n = 0; n < LIMITS.spectatorsPerMatch; n += 1) {
      const watcher = await h.kernel.spectators.join({
        joinCode: host.joinCode,
        displayName: `W${n}`,
      })
      if (n === 0)
        await h.kernel.spectators.leave(await h.kernel.spectators.authenticate(watcher.token))
    }
    await expect(
      h.kernel.spectators.join({ joinCode: host.joinCode, displayName: 'One too many' }),
    ).rejects.toMatchObject({ details: { reason: 'spectators_full' } })
  })

  it('announces a departure once, and says whether the host removed them', async () => {
    const host = await watchedLobby()
    const first = await h.kernel.spectators.join({ joinCode: host.joinCode, displayName: 'A' })
    const second = await h.kernel.spectators.join({ joinCode: host.joinCode, displayName: 'B' })
    const hostP = await h.principalOf(host.token)
    const firstP = await h.kernel.spectators.authenticate(first.token)
    await h.kernel.spectators.leave(firstP)
    await h.kernel.spectators.leave(firstP)
    await h.kernel.spectators.remove(hostP, second.spectator.id)
    await expect(h.kernel.spectators.remove(hostP, second.spectator.id)).rejects.toMatchObject({
      details: { reason: 'unknown_spectator' },
    })
    expect(
      h.notifier.events
        .filter((event) => event.type === 'spectator.left')
        .map((event) => event.payload),
    ).toEqual([
      { spectatorId: first.spectator.id, removed: false },
      { spectatorId: second.spectator.id, removed: true },
    ])
    await expect(h.kernel.spectators.authenticate(second.token)).rejects.toMatchObject({
      details: { reason: 'invalid_token' },
    })
  })

  it('refuses every read once the host turns spectating off in the lobby', async () => {
    const host = await watchedLobby()
    const watcher = await h.kernel.spectators.join({ joinCode: host.joinCode, displayName: 'W' })
    await h.kernel.lobby.updateSettings(await h.principalOf(host.token), {
      ...settings,
      spectatorDelayTurns: null,
    })
    await expect(
      h.kernel.spectators.view(await h.kernel.spectators.authenticate(watcher.token)),
    ).rejects.toMatchObject({ details: { reason: 'spectating_disabled' } })
  })

  it('never takes a player token, nor a player door a spectator token', async () => {
    const host = await watchedLobby()
    const watcher = await h.kernel.spectators.join({ joinCode: host.joinCode, displayName: 'W' })
    await expect(h.kernel.spectators.authenticate(host.token)).rejects.toMatchObject({
      details: { reason: 'invalid_token' },
    })
    await expect(h.kernel.auth.authenticate(watcher.token)).rejects.toMatchObject({
      details: { reason: 'invalid_token' },
    })
  })

  it('keeps the snapshot a spectator starts from out of the live pruning', async () => {
    const host = await watchedLobby()
    const guest = await h.kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Guest' })
    await h.kernel.lobby.start(await h.principalOf(host.token))
    const match = await h.storage.matches.get(host.match.id)
    if (!match) throw new Error('match missing')
    const base = {
      matchId: match.id,
      formatVersion: 1,
      protocolVersion: 1,
      sessionVersion: 1,
      stateHash: 'c'.repeat(32),
      uploadedByPlayerId: guest.player.id,
      uploadedAt: new Date(),
      body: 'QUJD',
    }
    for (const turn of [0, 3, 6, 7, 8, 9, 10, 11]) await h.storage.snapshots.put({ ...base, turn })
    // Turn 12 open, 11 sealed: two behind is turn 9, so a spectator starts from that snapshot.
    expect(await spectatorStartTurn(h.storage, { ...match, currentTurn: 12 })).toBe(9)
    // Turn 6 open: three is the newest snapshot a spectator may read.
    expect(await spectatorStartTurn(h.storage, { ...match, currentTurn: 6 })).toBe(3)
    expect(
      await spectatorStartTurn(h.storage, {
        ...match,
        settings: { ...match.settings, spectatorDelayTurns: null },
      }),
    ).toBeUndefined()
  })
})
