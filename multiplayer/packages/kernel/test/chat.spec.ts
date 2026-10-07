import { LIMITS } from '@chaos-overlords/contracts'
import { beforeEach, describe, expect, it } from 'vitest'
import { createHarness, type Harness } from './harness'

describe('lobby chat', () => {
  let h: Harness

  beforeEach(() => {
    h = createHarness()
  })

  async function lobbyOfTwo() {
    const host = await h.kernel.lobby.createMatch({
      settings: {
        name: 'Night City',
        maxPlayers: 3,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: {},
      },
      hostDisplayName: 'Host',
    })
    const guest = await h.kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Guest' })
    return { host, guest }
  }

  it('announces a message under the poster as lobby.chatMessage', async () => {
    const { guest } = await lobbyOfTwo()
    await h.kernel.lobby.postChat(await h.principalOf(guest.token), { text: 'hello' })
    const chat = h.notifier.events.filter((event) => event.type === 'lobby.chatMessage')
    expect(chat.map((event) => event.payload)).toEqual([
      { playerId: guest.player.id, text: 'hello' },
    ])
  })

  it('holds each player to their own budget per minute', async () => {
    const { host, guest } = await lobbyOfTwo()
    const guestP = await h.principalOf(guest.token)
    for (let message = 0; message < LIMITS.chatMessagesPerMinute; message += 1) {
      await h.kernel.lobby.postChat(guestP, { text: `line ${message}` })
    }
    await expect(h.kernel.lobby.postChat(guestP, { text: 'one more' })).rejects.toMatchObject({
      details: { reason: 'rate_limited' },
    })
    // The host's budget is untouched by the guest's.
    await h.kernel.lobby.postChat(await h.principalOf(host.token), { text: 'calm down' })
    h.clock.advance(60_000)
    await h.kernel.lobby.postChat(guestP, { text: 'sorry' })
  })

  /**
   * The log is chat's only store and lobby retention keeps it for days, so the log's length is what
   * bounds one lobby's cost, however many processes counted the per-player rate.
   */
  it('refuses chat once the lobby log is full', async () => {
    const { host } = await lobbyOfTwo()
    const hostP = await h.principalOf(host.token)
    while ((await h.storage.events.lastSeq(host.match.id)) < LIMITS.lobbyChatLogEvents) {
      await h.storage.events.append({
        matchId: host.match.id,
        createdAt: h.clock.now().toISOString(),
        type: 'lobby.chatMessage',
        payload: { playerId: host.player.id, text: 'filler' },
      })
    }
    await expect(h.kernel.lobby.postChat(hostP, { text: 'anyone?' })).rejects.toMatchObject({
      details: { reason: 'lobby_log_full' },
    })
  })

  it('closes once the match has started', async () => {
    const { host, guest } = await lobbyOfTwo()
    await h.kernel.lobby.start(await h.principalOf(host.token))
    await expect(
      h.kernel.lobby.postChat(await h.principalOf(guest.token), { text: 'gl hf' }),
    ).rejects.toMatchObject({ details: { reason: 'match_not_in_lobby' } })
  })
})
