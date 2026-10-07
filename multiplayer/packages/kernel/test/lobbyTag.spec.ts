import { beforeEach, describe, expect, it } from 'vitest'
import { LOBBY_TAG_WINDOW_MS } from '../src'
import { createHarness, type Harness } from './harness'

describe('the lobby tag', () => {
  let h: Harness

  beforeEach(() => {
    h = createHarness()
  })

  const lobby = async () => {
    const host = await h.kernel.lobby.createMatch({
      settings: {
        name: 'Tagged',
        maxPlayers: 3,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: {},
      },
      hostDisplayName: 'Ada',
    })
    const guest = await h.kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Grace' })
    return { host, guest }
  }

  const tagOf = async (token: string) => {
    const principal = await h.principalOf(token)
    return h.kernel.query.lobbyTag(principal.match, principal.player.id, h.clock.now())
  }

  it('stays put while nothing changes and differs between members', async () => {
    const { host, guest } = await lobby()
    const tag = await tagOf(host.token)
    expect(tag).toMatch(/^"lobby-[0-9a-f]{32}"$/)
    expect(await tagOf(host.token)).toBe(tag)
    expect(await tagOf(guest.token)).not.toBe(tag)
  })

  it('moves with a settings change, which publishes no event', async () => {
    const { host } = await lobby()
    const tag = await tagOf(host.token)
    const principal = await h.principalOf(host.token)
    await h.kernel.lobby.updateSettings(principal, { ...principal.match.settings, name: 'Renamed' })
    expect(await tagOf(host.token)).not.toBe(tag)
  })

  it('moves with a roster change', async () => {
    const { host, guest } = await lobby()
    const tag = await tagOf(host.token)
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    expect(await tagOf(host.token)).not.toBe(tag)
  })

  it('moves at the end of its window, so a change whose event was lost is read within it', async () => {
    const { host } = await lobby()
    // Align to the start of a window, counted from the match's creation, so the steps below land
    // on either side of its end.
    const { match } = await h.principalOf(host.token)
    const elapsed = h.clock.now().getTime() - match.createdAt.getTime()
    h.clock.advance(LOBBY_TAG_WINDOW_MS - (elapsed % LOBBY_TAG_WINDOW_MS))
    const tag = await tagOf(host.token)
    h.clock.advance(LOBBY_TAG_WINDOW_MS - 1)
    expect(await tagOf(host.token)).toBe(tag)
    h.clock.advance(1)
    expect(await tagOf(host.token)).not.toBe(tag)
  })

  it('is not given once the match has left the lobby', async () => {
    const { host } = await lobby()
    await h.kernel.lobby.start(await h.principalOf(host.token))
    expect(await tagOf(host.token)).toBeNull()
  })
})
