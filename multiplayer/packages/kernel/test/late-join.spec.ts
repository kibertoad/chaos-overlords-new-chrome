import { beforeEach, describe, expect, it } from 'vitest'
import { createHarness, HASH_A, type Harness } from './harness'

/**
 * Late join into a seat a human held until the players present voted it to the computer. See the
 * 2026-10-06 late-join decision.
 */
describe('late join into a seat the vote handed to the computer', () => {
  let h: Harness

  beforeEach(() => {
    h = createHarness()
  })

  /** Host and guest seated in slots 0 and 1, the bootstrap uploaded, the guest's face 7. */
  async function startedPair(maxPlayers = 6) {
    const host = await h.kernel.lobby.createMatch({
      settings: {
        name: 'Open Door',
        maxPlayers,
        turnTimerSeconds: 0,
        visibility: 'public',
        gameSettings: { allowLateJoin: true },
      },
      hostDisplayName: 'Host',
    })
    const guest = await h.kernel.lobby.join({
      joinCode: host.joinCode,
      displayName: 'Guest',
      portraitId: 7,
    })
    await h.kernel.lobby.start(await h.principalOf(host.token))
    await h.kernel.snapshots.upload(await h.principalOf(host.token), {
      turn: 0,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })
    const guestSlot = (await h.storage.players.get(guest.player.id))?.slot ?? -1
    return { host, guest, guestSlot }
  }

  /** The guest leaves and the host votes their seat to the computer. */
  async function voteOut(hostToken: string, guestToken: string, guestId: string) {
    await h.kernel.lobby.leave(await h.principalOf(guestToken))
    await h.kernel.lobby.voteOnTakeover(await h.principalOf(hostToken), guestId, {
      decision: 'computer',
    })
    expect((await h.storage.players.get(guestId))?.status).toBe('computer')
  }

  async function listedSlots(matchId: string) {
    return (await h.kernel.query.listPublicLobbies(10)).find((listing) => listing.id === matchId)
      ?.availableSlots
  }

  it('keeps an absent human seat reserved until the vote hands it to the computer', async () => {
    const { host, guest, guestSlot } = await startedPair()
    await h.kernel.lobby.leave(await h.principalOf(guest.token))

    expect(await listedSlots(host.match.id)).not.toContain(guestSlot)
    await expect(
      h.kernel.lobby.joinRunning({ match: host.match.id, displayName: 'Late', slot: guestSlot }),
    ).rejects.toMatchObject({ details: { reason: 'seat_reserved' } })
  })

  it('opens the seat after the vote, and a claim revokes the token of the former player', async () => {
    const { host, guest, guestSlot } = await startedPair()
    await voteOut(host.token, guest.token, guest.player.id)
    expect(await listedSlots(host.match.id)).toContain(guestSlot)

    const late = await h.kernel.lobby.joinRunning({
      match: host.match.id,
      displayName: 'Late',
      slot: guestSlot,
      portraitId: 2,
    })

    expect(late.player.slot).toBe(guestSlot)
    expect(late.player.id).not.toBe(guest.player.id)
    // The face the seat was generated with, which the match state never changed.
    expect(late.player.portraitId).toBe(7)
    expect(await listedSlots(host.match.id)).not.toContain(guestSlot)
    expect((await h.storage.players.get(guest.player.id))?.tokenHash).toBeNull()
    await expect(h.principalOf(guest.token)).rejects.toBeDefined()
    expect(h.streams.closed).toContainEqual({ matchId: host.match.id, playerId: guest.player.id })
    expect(h.notifier.events.at(-1)).toMatchObject({
      type: 'match.latePlayerJoined',
      payload: { playerId: late.player.id, slot: guestSlot },
    })
  })

  it('lets the former player take the seat back while nobody has claimed it', async () => {
    const { host, guest, guestSlot } = await startedPair()
    await voteOut(host.token, guest.token, guest.player.id)

    await h.kernel.lobby.rejoin(await h.principalOf(guest.token))

    expect((await h.storage.players.get(guest.player.id))?.status).toBe('active')
    await expect(
      h.kernel.lobby.joinRunning({ match: host.match.id, displayName: 'Late', slot: guestSlot }),
    ).rejects.toMatchObject({ details: { reason: 'seat_reserved' } })
  })

  it('refuses a return that authenticated before a late joiner claimed the seat', async () => {
    const { host, guest, guestSlot } = await startedPair()
    await voteOut(host.token, guest.token, guest.player.id)
    const returning = await h.principalOf(guest.token)

    const late = await h.kernel.lobby.joinRunning({
      match: host.match.id,
      displayName: 'Late',
      slot: guestSlot,
    })

    await expect(h.kernel.lobby.rejoin(returning)).rejects.toMatchObject({
      details: { reason: 'seat_taken' },
    })
    const holders = (await h.storage.players.listByMatch(host.match.id)).filter(
      (player) => player.slot === guestSlot,
    )
    expect(holders.map((player) => [player.id, player.status])).toEqual([
      [guest.player.id, 'computer'],
      [late.player.id, 'active'],
    ])
  })

  it('opens the seat again when its late joiner is voted out in turn', async () => {
    const { host, guest, guestSlot } = await startedPair()
    await voteOut(host.token, guest.token, guest.player.id)
    const first = await h.kernel.lobby.joinRunning({
      match: host.match.id,
      displayName: 'First',
      slot: guestSlot,
    })
    await voteOut(host.token, first.token, first.player.id)

    const second = await h.kernel.lobby.joinRunning({
      match: host.match.id,
      displayName: 'Second',
      slot: guestSlot,
    })

    expect(new Set([guest.player.id, first.player.id, second.player.id]).size).toBe(3)
    expect(second.player.portraitId).toBe(7)
    expect((await h.storage.players.get(first.player.id))?.tokenHash).toBeNull()
  })

  it('counts a former player against the limit only until their seat is claimed', async () => {
    const { host, guest, guestSlot } = await startedPair(2)
    await voteOut(host.token, guest.token, guest.player.id)
    const neverHuman = guestSlot === 2 ? 3 : 2

    // The guest can still come back, so a third seat would put three humans in a match of two.
    await expect(
      h.kernel.lobby.joinRunning({ match: host.match.id, displayName: 'Late', slot: neverHuman }),
    ).rejects.toMatchObject({ details: { reason: 'match_full' } })
    expect(await listedSlots(host.match.id)).toEqual([guestSlot])

    const late = await h.kernel.lobby.joinRunning({
      match: host.match.id,
      displayName: 'Late',
      slot: guestSlot,
    })
    expect(late.player.status).toBe('active')
  })

  it('lets two late joiners racing for one released seat seat only one', async () => {
    const { host, guest, guestSlot } = await startedPair()
    await voteOut(host.token, guest.token, guest.player.id)

    const results = await Promise.allSettled(
      ['Ann', 'Bob'].map((displayName) =>
        h.kernel.lobby.joinRunning({ match: host.match.id, displayName, slot: guestSlot }),
      ),
    )

    expect(results.filter((result) => result.status === 'fulfilled')).toHaveLength(1)
    const holders = (await h.storage.players.listByMatch(host.match.id)).filter(
      (player) => player.slot === guestSlot && player.status !== 'computer',
    )
    expect(holders).toHaveLength(1)
  })
})
