import { beforeEach, describe, expect, it } from 'vitest'
import { createHarness, type Harness } from './harness'

/**
 * Removing a seat by a unanimous vote of the other active players (docs/DECISIONS.md, "Let the other
 * players remove a seat by unanimous vote"). The remedy for a host who never readies an untimed turn
 * and for a host who will not kick a player who desyncs every turn, since only the host can kick.
 */
describe('removal votes', () => {
  let h: Harness

  beforeEach(() => {
    h = createHarness()
  })

  const closedEvents = () =>
    h.notifier.events.filter((event) => event.type === 'match.removalVoteClosed')

  it('removes a host who never readies an untimed turn once every other player approves', async () => {
    const { host, guest, third } = await h.startedMatchOfThree()
    await h.submit(await h.principalOf(guest.token), 1, 2, true)
    await h.submit(await h.principalOf(third.token), 1, 3, true)

    await h.kernel.lobby.voteOnRemoval(await h.principalOf(guest.token), host.player.id, {
      decision: 'remove',
    })
    expect((await h.storage.players.get(host.player.id))?.status).toBe('active')
    expect((await h.principalOf(guest.token)).match.currentTurn).toBe(1)

    await h.kernel.lobby.voteOnRemoval(await h.principalOf(third.token), host.player.id, {
      decision: 'remove',
    })
    expect((await h.storage.players.get(host.player.id))?.status).toBe('kicked')
    // The removed seat is no longer waited on, so the turn the host was holding seals.
    const after = await h.principalOf(guest.token)
    expect(after.match.currentTurn).toBe(2)
    // The role moves to the lowest active slot, as it does for a host who leaves.
    expect(after.match.hostPlayerId).toBe(guest.player.id)
    // A removal is a kick: the token is revoked and the streams hung up.
    await expect(h.principalOf(host.token)).rejects.toMatchObject({
      details: { reason: 'invalid_token' },
    })
    expect(h.streams.closed).toContainEqual({ matchId: host.match.id, playerId: host.player.id })
    expect(closedEvents().map((event) => event.payload)).toEqual([
      { playerId: host.player.id, removed: true },
    ])
    expect(await h.storage.removals.listTargets(host.match.id)).toEqual([])
    // The seat is put to the takeover vote like any kicked seat.
    expect(
      h.notifier.events.some(
        (event) =>
          event.type === 'match.takeoverVoteRequested' && event.payload.playerId === host.player.id,
      ),
    ).toBe(true)
  })

  it('lets the only other player of a match of two remove the host', async () => {
    const { host, guest } = await h.startedMatch()
    await h.submit(await h.principalOf(guest.token), 1, 2, true)
    await h.kernel.lobby.voteOnRemoval(await h.principalOf(guest.token), host.player.id, {
      decision: 'remove',
    })
    expect((await h.storage.players.get(host.player.id))?.status).toBe('kicked')
    expect((await h.principalOf(guest.token)).match.currentTurn).toBe(2)
  })

  it('counts the host as one voter among the others', async () => {
    const { host, guest, third } = await h.startedMatchOfThree()
    // The host is a voter like any other: removing the guest takes the host and the third.
    await h.kernel.lobby.voteOnRemoval(await h.principalOf(third.token), guest.player.id, {
      decision: 'remove',
    })
    await h.kernel.lobby.voteOnRemoval(await h.principalOf(host.token), guest.player.id, {
      decision: 'keep',
    })
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('active')
    await h.kernel.lobby.voteOnRemoval(await h.principalOf(host.token), guest.player.id, {
      decision: 'remove',
    })
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('kicked')
  })

  it('keeps the vote open while one player holds keep and closes it when nobody holds remove', async () => {
    const { host, guest, third } = await h.startedMatchOfThree()
    await h.kernel.lobby.voteOnRemoval(await h.principalOf(guest.token), host.player.id, {
      decision: 'remove',
    })
    await h.kernel.lobby.voteOnRemoval(await h.principalOf(third.token), host.player.id, {
      decision: 'keep',
    })
    expect(await h.storage.removals.listTargets(host.match.id)).toEqual([host.player.id])
    expect(closedEvents()).toHaveLength(0)

    await h.kernel.lobby.voteOnRemoval(await h.principalOf(guest.token), host.player.id, {
      decision: 'keep',
    })
    expect((await h.storage.players.get(host.player.id))?.status).toBe('active')
    expect(await h.storage.removals.listTargets(host.match.id)).toEqual([])
    expect(closedEvents().map((event) => event.payload)).toEqual([
      { playerId: host.player.id, removed: false },
    ])
  })

  it('answers keep on a seat nobody proposes removing without logging anything', async () => {
    const { host, guest } = await h.startedMatchOfThree()
    const before = h.notifier.events.length
    await h.kernel.lobby.voteOnRemoval(await h.principalOf(guest.token), host.player.id, {
      decision: 'keep',
    })
    expect(h.notifier.events).toHaveLength(before)
    expect(await h.storage.removals.listTargets(host.match.id)).toEqual([])
  })

  it('completes the vote when the one player who had not approved leaves', async () => {
    const { host, guest, third } = await h.startedMatchOfThree()
    await h.kernel.lobby.voteOnRemoval(await h.principalOf(guest.token), host.player.id, {
      decision: 'remove',
    })
    await h.kernel.lobby.leave(await h.principalOf(third.token))
    expect((await h.storage.players.get(host.player.id))?.status).toBe('kicked')
    expect(closedEvents().map((event) => event.payload)).toEqual([
      { playerId: host.player.id, removed: true },
    ])
  })

  it('completes the vote when the one player who held keep is handed to the computer', async () => {
    const { host, guest, third } = await h.startedMatchOfThree()
    await h.kernel.lobby.voteOnRemoval(await h.principalOf(host.token), third.player.id, {
      decision: 'remove',
    })
    await h.kernel.lobby.voteOnRemoval(await h.principalOf(guest.token), third.player.id, {
      decision: 'keep',
    })
    // The guest misses a timed deadline, and the others hand the seat to the computer.
    await h.storage.players.transitionStatus(guest.player.id, ['active'], 'takeoverPending')
    for (const voter of [host, third]) {
      await h.kernel.lobby.voteOnTakeover(await h.principalOf(voter.token), guest.player.id, {
        decision: 'computer',
      })
    }
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('computer')
    expect((await h.storage.players.get(third.player.id))?.status).toBe('kicked')
    // The close follows the seat's own departure, so clients read it against the updated roster.
    const types = h.notifier.events.map((event) => event.type)
    expect(types.lastIndexOf('match.removalVoteClosed')).toBeGreaterThan(
      types.lastIndexOf('lobby.playerLeft'),
    )
    expect(closedEvents().map((event) => event.payload)).toEqual([
      { playerId: third.player.id, removed: true },
    ])
  })

  it('closes an open vote when the host kicks the same seat', async () => {
    const { host, guest, third } = await h.startedMatchOfThree()
    await h.kernel.lobby.voteOnRemoval(await h.principalOf(third.token), guest.player.id, {
      decision: 'remove',
    })
    await h.kernel.lobby.kick(await h.principalOf(host.token), guest.player.id)
    expect(closedEvents().map((event) => event.payload)).toEqual([
      { playerId: guest.player.id, removed: true },
    ])
    expect(await h.storage.removals.listTargets(host.match.id)).toEqual([])
  })

  it('refuses a vote on oneself, on a removed seat, from an absent player and before the start', async () => {
    const lobby = await h.kernel.lobby.createMatch({
      settings: {
        name: 'Lobby',
        maxPlayers: 3,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: {},
      },
      hostDisplayName: 'Host',
    })
    const waiting = await h.kernel.lobby.join({ joinCode: lobby.joinCode, displayName: 'Guest' })
    await expect(
      h.kernel.lobby.voteOnRemoval(await h.principalOf(waiting.token), lobby.player.id, {
        decision: 'remove',
      }),
    ).rejects.toMatchObject({ details: { reason: 'match_not_running' } })

    const { host, guest, third } = await h.startedMatchOfThree()
    await expect(
      h.kernel.lobby.voteOnRemoval(await h.principalOf(guest.token), guest.player.id, {
        decision: 'remove',
      }),
    ).rejects.toMatchObject({ details: { reason: 'self_removal' } })

    await h.kernel.lobby.kick(await h.principalOf(host.token), third.player.id)
    await expect(
      h.kernel.lobby.voteOnRemoval(await h.principalOf(guest.token), third.player.id, {
        decision: 'remove',
      }),
    ).rejects.toMatchObject({ details: { reason: 'already_removed' } })

    // A kicked computer seat keeps its `computer` status; its revoked token marks it removed.
    const { host: otherHost, guest: otherGuest, third: quiet } = await h.startedMatchOfThree()
    await h.kernel.lobby.leave(await h.principalOf(quiet.token))
    for (const voter of [otherHost, otherGuest]) {
      await h.kernel.lobby.voteOnTakeover(await h.principalOf(voter.token), quiet.player.id, {
        decision: 'computer',
      })
    }
    await h.kernel.lobby.kick(await h.principalOf(otherHost.token), quiet.player.id)
    expect((await h.storage.players.get(quiet.player.id))?.status).toBe('computer')
    await expect(
      h.kernel.lobby.voteOnRemoval(await h.principalOf(otherGuest.token), quiet.player.id, {
        decision: 'remove',
      }),
    ).rejects.toMatchObject({ details: { reason: 'already_removed' } })

    const leaving = await h.principalOf(guest.token)
    await h.kernel.lobby.leave(leaving)
    await expect(
      h.kernel.lobby.voteOnRemoval(await h.principalOf(guest.token), host.player.id, {
        decision: 'remove',
      }),
    ).rejects.toMatchObject({ details: { reason: 'not_active' } })
  })
})
