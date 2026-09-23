import { beforeEach, describe, expect, it } from 'vitest'
import { DEFAULT_RETENTION, hashOrderSet } from '../src'
import { createHarness, HASH_A, HASH_B, type Harness } from './harness'

describe('absent seats, departures and the repair sweeps', () => {
  let h: Harness

  beforeEach(() => {
    h = createHarness()
  })

  it('keeps an absent human seat when a player votes to wait, then cancels on return', async () => {
    const { host, guest } = await h.startedMatch(60)
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    h.clock.advance(60_000)
    await h.kernel.turns.sweep()

    expect((await h.storage.players.get(guest.player.id))?.status).toBe('takeoverPending')
    expect(h.notifier.events.map((event) => event.type)).toContain('match.takeoverVoteRequested')
    expect((await h.storage.turns.get(host.match.id, 2))?.deadlineAt).toBeNull()
    await h.kernel.lobby.voteOnTakeover(await h.principalOf(host.token), guest.player.id, {
      decision: 'wait',
    })
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('takeoverPending')

    await h.submit(await h.principalOf(guest.token), 2, 2, false)
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('active')
    expect(h.notifier.events.map((event) => event.type)).toContain('match.takeoverVoteCancelled')
    expect((await h.storage.turns.get(host.match.id, 2))?.deadlineAt).toEqual(
      new Date(h.clock.now().getTime() + 60_000),
    )
    expect(
      h.notifier.events.some(
        (event) =>
          event.type === 'turn.deadlineExtended' &&
          event.payload.turn === 2 &&
          event.payload.deadlineAt !== null,
      ),
    ).toBe(true)
  })

  it('pauses an open turn h.clock for a departure vote and restarts it after takeover', async () => {
    const { host, guest } = await h.startedMatch(60)
    await h.kernel.lobby.leave(await h.principalOf(guest.token))

    expect((await h.storage.turns.get(host.match.id, 1))?.deadlineAt).toBeNull()
    expect(
      h.notifier.events.some(
        (event) => event.type === 'turn.deadlineExtended' && event.payload.deadlineAt === null,
      ),
    ).toBe(true)

    h.clock.advance(120_000)
    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 0 })
    await h.kernel.lobby.voteOnTakeover(await h.principalOf(host.token), guest.player.id, {
      decision: 'computer',
    })

    expect((await h.storage.turns.get(host.match.id, 1))?.deadlineAt).toEqual(
      new Date(h.clock.now().getTime() + 60_000),
    )
  })

  /**
   * Every way a vote opens stops the h.clock, not just the kick and the seal that had been wired to.
   *
   * A vote owns the screen on every remaining client, so time that passes behind it is time nobody
   * can plan in. These two paths open one without a departure to trigger it: a player voting on a
   * seat that went quiet before anyone was present to ask, and a returning player being asked about
   * the seats they find absent. Both used to leave the countdown running, which spent a turn of
   * planning time on a modal and could seal the turn while the vote was still up.
   */
  it('stops the h.clock for a vote that opens the question itself', async () => {
    const { host, guest } = await h.startedMatchOfThree(60)
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    // A prompt opened before this server kept them durably: nothing on file, seat still absent.
    await h.storage.takeovers.closePrompt(host.match.id, guest.player.id)
    await h.kernel.turns.resumeAfterTakeoverVotes(host.match.id)
    expect((await h.storage.turns.get(host.match.id, 1))?.deadlineAt).not.toBeNull()

    await h.kernel.lobby.voteOnTakeover(await h.principalOf(host.token), guest.player.id, {
      decision: 'wait',
    })

    expect(await h.storage.takeovers.listOpenPrompts(host.match.id)).toEqual([guest.player.id])
    expect((await h.storage.turns.get(host.match.id, 1))?.deadlineAt).toBeNull()
  })

  it('stops the h.clock for the seats a returning player is asked about', async () => {
    const { host, guest, third } = await h.startedMatchOfThree(60)
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    await h.kernel.lobby.leave(await h.principalOf(third.token))
    await h.storage.takeovers.closePrompt(host.match.id, guest.player.id)
    await h.storage.takeovers.closePrompt(host.match.id, third.player.id)
    await h.kernel.turns.resumeAfterTakeoverVotes(host.match.id)
    expect((await h.storage.turns.get(host.match.id, 1))?.deadlineAt).not.toBeNull()

    await h.kernel.lobby.rejoin(await h.principalOf(guest.token))

    // Asked about the seat still absent, and not about the one that just came back.
    expect(await h.storage.takeovers.listOpenPrompts(host.match.id)).toEqual([third.player.id])
    expect((await h.storage.turns.get(host.match.id, 1))?.deadlineAt).toBeNull()
  })

  it('holds turn 1 for a player who left before acting until they return and finish it', async () => {
    const { host, guest } = await h.startedMatch()
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    await h.kernel.lobby.voteOnTakeover(await h.principalOf(host.token), guest.player.id, {
      decision: 'wait',
    })
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    expect((await h.storage.turns.get(host.match.id, 1))?.status).toBe('open')
    expect((await h.principalOf(host.token)).match.currentTurn).toBe(1)

    await h.kernel.lobby.rejoin(await h.principalOf(guest.token))
    const returned = await h.principalOf(guest.token)
    expect(returned.match.currentTurn).toBe(1)
    expect((await h.kernel.query.ownSubmission(returned.match, guest.player.id, 1)).ready).toBe(
      false,
    )
    await h.submit(returned, 1, 2, true)

    const first = await h.storage.turns.get(host.match.id, 1)
    expect(first?.sealedSlots?.map((seat) => seat.playerId).sort()).toEqual(
      [host.player.id, guest.player.id].sort(),
    )
    expect((await h.principalOf(host.token)).match.currentTurn).toBe(2)
  })

  it('does not let a departure seal the turn before anyone could answer the absence vote', async () => {
    const { host, guest } = await h.startedMatch()
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    await h.kernel.lobby.leave(await h.principalOf(guest.token))

    expect((await h.storage.turns.get(host.match.id, 1))?.status).toBe('open')
    expect(await h.storage.takeovers.listOpenPrompts(host.match.id)).toEqual([guest.player.id])
  })

  it('seals a held turn once the vote hands the departed seat to the computer', async () => {
    const { host, guest } = await h.startedMatch()
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    expect((await h.principalOf(host.token)).match.currentTurn).toBe(1)

    await h.kernel.lobby.voteOnTakeover(await h.principalOf(host.token), guest.player.id, {
      decision: 'computer',
    })
    const first = await h.storage.turns.get(host.match.id, 1)
    expect(first?.sealedSlots?.map((seat) => seat.playerId)).toEqual([host.player.id])
    expect((await h.principalOf(host.token)).match.currentTurn).toBe(2)
  })

  it('counts the finished orders of a player who left after marking ready', async () => {
    const { host, guest } = await h.startedMatch()
    await h.submit(await h.principalOf(guest.token), 1, 2, true)
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    await h.submit(await h.principalOf(host.token), 1, 1, true)

    const first = await h.storage.turns.get(host.match.id, 1)
    expect(first?.sealedSlots?.map((seat) => seat.playerId).sort()).toEqual(
      [host.player.id, guest.player.id].sort(),
    )
    // Still undecided, so the next turn waits on the seat as well.
    expect(
      (await h.storage.turns.listOrderSummaries(host.match.id, 2))
        .map((row) => row.playerId)
        .sort(),
    ).toEqual([host.player.id, guest.player.id].sort())
  })

  it('seats a player who missed the turn 1 deadline into turn 2 after it sealed without them', async () => {
    const { host, guest } = await h.startedMatch(60)
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    h.clock.advance(60_000)
    await h.kernel.turns.sweep()

    // The deadline sealed turn 1 on the host's orders alone and put the quiet seat to a vote.
    const first = await h.storage.turns.get(host.match.id, 1)
    expect(first?.status).toBe('sealed')
    expect(first?.sealedSlots?.map((seat) => seat.playerId)).toEqual([host.player.id])
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('takeoverPending')
    expect((await h.principalOf(host.token)).match.currentTurn).toBe(2)
    // Still a human seat while the vote is open, so turn 2 opened with a row waiting for it.
    const waiting = await h.storage.turns.listOrders(host.match.id, 2)
    expect(waiting.find((row) => row.playerId === guest.player.id)?.ready).toBe(false)

    await h.kernel.lobby.rejoin(await h.principalOf(guest.token))
    const returned = await h.principalOf(guest.token)
    expect(returned.player.status).toBe('active')
    expect(returned.match.currentTurn).toBe(2)
    expect(await h.kernel.query.ownSubmission(returned.match, guest.player.id, 2)).toEqual({
      turn: 2,
      orders: null,
      ready: false,
      ordersHash: null,
    })

    await h.submit(await h.principalOf(host.token), 2, 1, true)
    expect((await h.principalOf(host.token)).match.currentTurn).toBe(2)
    await h.submit(await h.principalOf(guest.token), 2, 2, true)
    expect((await h.principalOf(host.token)).match.currentTurn).toBe(3)
  })

  it('seats a player who reclaims a computer seat into turn 2 after turn 1 sealed without them', async () => {
    const { host, guest } = await h.startedMatch()
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    await h.kernel.lobby.voteOnTakeover(await h.principalOf(host.token), guest.player.id, {
      decision: 'computer',
    })
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('computer')
    await h.submit(await h.principalOf(host.token), 1, 1, true)

    const first = await h.storage.turns.get(host.match.id, 1)
    expect(first?.status).toBe('sealed')
    expect(first?.sealedSlots?.map((seat) => seat.playerId)).toEqual([host.player.id])
    expect((await h.principalOf(host.token)).match.currentTurn).toBe(2)

    await h.kernel.lobby.rejoin(await h.principalOf(guest.token))
    const returned = await h.principalOf(guest.token)
    expect(returned.player.status).toBe('active')
    expect(returned.match.currentTurn).toBe(2)
    expect(await h.kernel.query.ownSubmission(returned.match, guest.player.id, 2)).toEqual({
      turn: 2,
      orders: null,
      ready: false,
      ordersHash: null,
    })

    // Back from the computer, the seat is a human one again and turn 2 waits for it.
    await h.submit(await h.principalOf(host.token), 2, 1, true)
    expect((await h.principalOf(host.token)).match.currentTurn).toBe(2)
    await h.submit(await h.principalOf(guest.token), 2, 2, true)
    expect((await h.principalOf(host.token)).match.currentTurn).toBe(3)
  })

  it('holds the turn of a player returning to an empty match until they decide the departed host', async () => {
    const { host, guest } = await h.startedMatch()
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    await h.kernel.lobby.voteOnTakeover(await h.principalOf(host.token), guest.player.id, {
      decision: 'computer',
    })
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('computer')
    await h.kernel.lobby.leave(await h.principalOf(host.token))
    expect(await h.storage.takeovers.listOpenPrompts(host.match.id)).toEqual([])

    await h.kernel.lobby.rejoin(await h.principalOf(guest.token))
    // Asked about the host who left while nobody was present, and the turn waits on that seat.
    expect(await h.storage.takeovers.listOpenPrompts(host.match.id)).toEqual([host.player.id])
    await h.submit(await h.principalOf(guest.token), 1, 2, true)
    expect((await h.storage.turns.get(host.match.id, 1))?.status).toBe('open')
    expect((await h.principalOf(guest.token)).match.currentTurn).toBe(1)

    await h.kernel.lobby.voteOnTakeover(await h.principalOf(guest.token), host.player.id, {
      decision: 'computer',
    })
    expect((await h.storage.players.get(host.player.id))?.status).toBe('computer')
    const first = await h.storage.turns.get(host.match.id, 1)
    expect(first?.sealedSlots?.map((seat) => seat.playerId)).toEqual([guest.player.id])
    expect((await h.principalOf(guest.token)).match.currentTurn).toBe(2)
  })

  it('requires every present player to approve computer control exactly once', async () => {
    const { host, guest, third } = await h.startedMatchOfThree(60)
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    await h.submit(await h.principalOf(third.token), 1, 3, true)
    h.clock.advance(60_000)
    await h.kernel.turns.sweep()

    await h.kernel.lobby.voteOnTakeover(await h.principalOf(host.token), guest.player.id, {
      decision: 'computer',
    })
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('takeoverPending')
    await h.kernel.lobby.voteOnTakeover(await h.principalOf(third.token), guest.player.id, {
      decision: 'computer',
    })
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('computer')
    expect(
      h.notifier.events.filter((event) => event.type === 'match.playerTakenOver'),
    ).toHaveLength(1)
    await h.kernel.lobby.rejoin(await h.principalOf(guest.token))
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('active')
    expect(h.notifier.events.at(-1)?.type).toBe('match.playerReturned')
    expect(h.notifier.events.at(-1)?.payload).toEqual({
      playerId: guest.player.id,
      replacedComputer: true,
    })
  })

  it('kicking the straggler completes readiness and preserves a match with nobody present', async () => {
    const { host, guest } = await h.startedMatch()
    const third = await h.kernel.lobby
      .join({ joinCode: host.joinCode, displayName: 'Third' })
      .catch(() => null)
    expect(third).toBeNull() // already started
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    await h.kernel.lobby.kick(await h.principalOf(host.token), guest.player.id)
    expect((await h.principalOf(host.token)).match.currentTurn).toBe(2)
    // The kick revokes the membership, so the token stops resolving at all: a kicked player loses
    // the event stream and the sealed order sets of the turns that follow, not just the right to act.
    await expect(h.principalOf(guest.token)).rejects.toMatchObject({
      details: { reason: 'invalid_token' },
    })
    await h.kernel.lobby.leave(await h.principalOf(host.token))
    expect(h.storage.statusOf(host.match.id)).toBe('running')
  })

  /**
   * A timed match whose last active player leaves stops its h.clock instead of sealing forever.
   *
   * Nobody is left to open a takeover prompt for, so nothing else paused the turn, and the match
   * sealed an empty turn every `turnTimerSeconds` for as long as the server ran: a turn row and two
   * events a cycle, with every cycle refreshing `updated_at` so retention never reached it. Creating
   * a match needs no account, so it was also a cheap way to grow somebody else's database.
   */
  it('stops the h.clock of a timed match whose last active player leaves, and restarts it on rejoin', async () => {
    const { host, guest } = await h.startedMatch(60)
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    // The guest's departure opens a prompt, which pauses the h.clock; the host's vote clears it.
    await h.kernel.lobby.voteOnTakeover(await h.principalOf(host.token), guest.player.id, {
      decision: 'computer',
    })
    const turn = (await h.principalOf(host.token)).match.currentTurn
    expect((await h.storage.turns.get(host.match.id, turn))?.deadlineAt).not.toBeNull()

    await h.kernel.lobby.leave(await h.principalOf(host.token))

    expect(h.storage.statusOf(host.match.id)).toBe('running')
    expect((await h.storage.turns.get(host.match.id, turn))?.deadlineAt).toBeNull()
    // Nothing for the sweep to seal, however far the h.clock is wound on.
    h.clock.advance(60 * 60 * 1000)
    expect(await h.kernel.turns.sweep()).toMatchObject({ sealed: 0 })
    expect((await h.principalOf(host.token)).match.currentTurn).toBe(turn)

    await h.kernel.lobby.rejoin(await h.principalOf(host.token))

    expect((await h.storage.turns.get(host.match.id, turn))?.deadlineAt).not.toBeNull()
  })

  /**
   * Kicking a seat the turn is waiting on re-runs the verdict.
   *
   * `humanParticipants` counts a `takeoverPending` seat, so both readiness and consensus wait on it.
   * Returning early for every non-active target left the match waiting on a seat that could never
   * answer, with the h.clock paused by its own prompt and everyone present already ready.
   */
  it('re-evaluates the turn when a seat the turn was waiting on is kicked', async () => {
    const { host, guest } = await h.startedMatch(60)
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    await h.storage.players.setStatus(guest.player.id, 'takeoverPending')
    await h.kernel.turns.openTakeoverPrompt(host.match.id, guest.player.id, 1)
    expect((await h.principalOf(host.token)).match.currentTurn).toBe(1)

    await h.kernel.lobby.kick(await h.principalOf(host.token), guest.player.id)

    expect((await h.principalOf(host.token)).match.currentTurn).toBe(2)
  })

  /** A seat already handed to the computer is not a human a kick may turn back into an absent one. */
  it('leaves a computer-controlled seat alone when the host kicks it', async () => {
    const { host, guest } = await h.startedMatch()
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    await h.kernel.lobby.voteOnTakeover(await h.principalOf(host.token), guest.player.id, {
      decision: 'computer',
    })
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('computer')

    await h.kernel.lobby.kick(await h.principalOf(host.token), guest.player.id)

    expect((await h.storage.players.get(guest.player.id))?.status).toBe('computer')
  })

  /**
   * The seat counter follows the rows, so a racing pair of removals releases one seat.
   *
   * `players.delete` used to answer nothing, and the seat was released whether or not a row went.
   * Two requests that both authenticated before either deleted therefore decremented the counter
   * twice, the lobby admitted more than `maxPlayers`, and a seventh player got slot 6 — which fails
   * `seatSchema` in every player view and makes the match a 500 for the whole roster.
   */
  it('releases one seat when two removals race for the same lobby member', async () => {
    const host = await h.kernel.lobby.createMatch({
      settings: {
        name: 'Night City',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'public',
        gameSettings: {},
      },
      hostDisplayName: 'Host',
    })
    const guest = await h.kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Guest' })
    const guestPrincipal = await h.principalOf(guest.token)

    await h.kernel.lobby.leave(guestPrincipal)
    // The same principal again: what a client retrying a lost response, or a kick racing a leave,
    // hands the server.
    await h.kernel.lobby.leave(guestPrincipal)

    // One seat back, so the lobby holds exactly one more.
    await h.kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Second' })
    await expect(
      h.kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Third' }),
    ).rejects.toMatchObject({ details: { reason: 'match_full' } })
  })

  it('lets former members rejoin and makes the first returning player host', async () => {
    const { host, guest } = await h.startedMatch()
    await h.kernel.lobby.leave(await h.principalOf(host.token))
    await h.kernel.lobby.leave(await h.principalOf(guest.token))

    expect(h.storage.statusOf(host.match.id)).toBe('running')
    await h.kernel.lobby.rejoin(await h.principalOf(guest.token))

    const detail = await h.principalOf(guest.token)
    expect(detail.player.status).toBe('active')
    expect(detail.match.hostPlayerId).toBe(guest.player.id)
    expect(h.notifier.events.map((event) => event.type)).toContain('match.playerReturned')
    expect(h.notifier.events.map((event) => event.type)).toContain('lobby.hostChanged')
  })

  it('keeps the host role with a host who only missed a deadline', async () => {
    const { host, guest } = await h.startedMatch(60)
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    // One missed timed turn and the host is `takeoverPending`: still connected, still playing.
    // Treating that as an empty seat would hand the role to any former member who called rejoin at
    // that moment, and the new host can kick the old one, which revokes their token for good.
    await h.storage.players.setStatus(host.player.id, 'takeoverPending')
    await h.kernel.lobby.rejoin(await h.principalOf(guest.token))
    expect((await h.principalOf(guest.token)).match.hostPlayerId).toBe(host.player.id)

    // A host who is actually gone is replaced, which is what the rule is for.
    await h.storage.players.setStatus(host.player.id, 'left')
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    await h.kernel.lobby.rejoin(await h.principalOf(guest.token))
    expect((await h.principalOf(guest.token)).match.hostPlayerId).toBe(guest.player.id)
  })

  it('hands off the host role when a takeover-pending host leaves', async () => {
    const { host, guest } = await h.startedMatch(60)
    await h.storage.players.setStatus(host.player.id, 'takeoverPending')
    await h.kernel.turns.openTakeoverPrompt(host.match.id, host.player.id, 1)

    await h.kernel.lobby.leave(await h.principalOf(host.token))

    expect((await h.storage.players.get(host.player.id))?.status).toBe('left')
    expect((await h.principalOf(guest.token)).match.hostPlayerId).toBe(guest.player.id)
    expect(h.notifier.events.filter((event) => event.type === 'lobby.hostChanged')).toHaveLength(1)
  })

  it('keeps the host role with a leaving pending host until somebody rejoins', async () => {
    const { host, guest } = await h.startedMatch(60)
    await h.storage.players.setStatus(host.player.id, 'takeoverPending')
    await h.kernel.turns.openTakeoverPrompt(host.match.id, host.player.id, 1)
    await h.kernel.lobby.leave(await h.principalOf(guest.token))

    await h.kernel.lobby.leave(await h.principalOf(host.token))

    expect((await h.storage.players.get(host.player.id))?.status).toBe('left')
    expect((await h.principalOf(guest.token)).match.hostPlayerId).toBe(host.player.id)
    expect(h.notifier.events.some((event) => event.type === 'lobby.hostChanged')).toBe(false)

    await h.kernel.lobby.rejoin(await h.principalOf(guest.token))
    expect((await h.principalOf(guest.token)).match.hostPlayerId).toBe(guest.player.id)
  })

  /**
   * A kick reaches a seat in any state. `remove` returned at once for anything but `active`, so
   * the host was answered 204 while the target kept a working token, an open stream, and `rejoin`,
   * which turns away nobody but the kicked.
   */
  it('kicks a player who has already left, and keeps them out', async () => {
    const { host, guest } = await h.startedMatch()
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('left')

    await h.kernel.lobby.kick(await h.principalOf(host.token), guest.player.id)

    expect((await h.storage.players.get(guest.player.id))?.status).toBe('kicked')
    expect(h.streams.closed).toEqual([{ matchId: host.match.id, playerId: guest.player.id }])
    await expect(h.principalOf(guest.token)).rejects.toMatchObject({
      details: { reason: 'invalid_token' },
    })
  })

  it('hangs up the event h.streams of a membership it revokes', async () => {
    const { host, guest } = await h.startedMatch()
    await h.kernel.lobby.kick(await h.principalOf(host.token), guest.player.id)
    // The revoke stops the next request. A stream already open is never authenticated again, so it
    // has to be closed from this side or the kicked player reads the match for as long as they like.
    expect(h.streams.closed).toEqual([{ matchId: host.match.id, playerId: guest.player.id }])
    // Leaving is not a revoke: the membership survives so the player can rejoin their seat.
    await h.kernel.lobby.leave(await h.principalOf(host.token))
    expect(h.streams.closed).toHaveLength(1)
  })

  it('refuses a report and a contradictory checkpoint after a turn is confirmed', async () => {
    const { host, guest } = await h.startedMatch()
    for (const token of [host.token, guest.token]) {
      await h.submit(await h.principalOf(token), 1, 1, true)
    }
    for (const token of [host.token, guest.token]) {
      await h.kernel.turns.report(await h.principalOf(token), 1, {
        stateHash: HASH_A,
        finished: false,
      })
    }
    // The verdict is in; a later report cannot change it or rewrite its evidence.
    await expect(
      h.kernel.turns.report(await h.principalOf(host.token), 1, {
        stateHash: HASH_B,
        finished: false,
      }),
    ).rejects.toMatchObject({ details: { reason: 'turn_confirmed' } })

    // A checkpoint of a confirmed turn can only agree with that verdict. One claiming a different
    // hash is not a checkpoint, it is a second opinion on a settled question.
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    await expect(
      h.kernel.snapshots.upload(await h.principalOf(host.token), {
        turn: 1,
        formatVersion: 1,
        stateHash: HASH_B,
        body: 'AAAA',
        seatSummaries: [],
      }),
    ).rejects.toMatchObject({ details: { reason: 'uncorroborated_state_hash' } })
  })

  /**
   * The checkpoint a reconnect replays from.
   *
   * Only the turn-0 bootstrap and desync repairs used to exist, so a client reconnecting at turn 80
   * replayed eighty turns from the beginning — eighty sequential fetches and eighty full
   * resolutions before the player saw anything. A checkpoint is not a second opinion: the turn is
   * already confirmed, and the upload is refused unless it claims exactly the hash the verdict
   * settled on.
   */
  it('stores a host checkpoint of a turn the match already confirmed', async () => {
    const { host, guest } = await h.startedMatch()
    for (const token of [host.token, guest.token]) {
      await h.submit(await h.principalOf(token), 1, 1, true)
    }
    for (const token of [host.token, guest.token]) {
      await h.kernel.turns.report(await h.principalOf(token), 1, {
        stateHash: HASH_A,
        finished: false,
      })
    }

    // A peer may not write one: the host is the client the protocol already asks for bytes.
    await expect(
      h.kernel.snapshots.upload(await h.principalOf(guest.token), {
        turn: 1,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'AAAA',
        seatSummaries: [],
      }),
    ).rejects.toMatchObject({ details: { reason: 'host_only' } })

    await h.kernel.snapshots.upload(await h.principalOf(host.token), {
      turn: 1,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })

    expect((await h.kernel.snapshots.latest(host.match.id)).turn).toBe(1)
    // Nothing is announced: a checkpoint changes nothing about the match, and the client that
    // needs it is the one that has not connected yet.
    expect(h.notifier.events.some((event) => event.type === 'snapshot.available')).toBe(false)
  })

  /**
   * A turn that has sealed but not settled is still counting its reports. `settle` judges every
   * report against a snapshot for that turn when there is one, and anything short of unanimity on
   * it answers `pending` — so a snapshot accepted here would put the turn's desync verdict out of
   * reach for good and leave it unsettled, which is what a desync pause waits on to lift.
   */
  it('refuses a full snapshot while a turn is still collecting reports', async () => {
    const { host, guest } = await h.startedMatch()
    for (const token of [host.token, guest.token]) {
      await h.submit(await h.principalOf(token), 1, 1, true)
    }
    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })

    await expect(
      h.kernel.snapshots.upload(await h.principalOf(host.token), {
        turn: 1,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'AAAA',
        seatSummaries: [],
      }),
    ).rejects.toMatchObject({ details: { reason: 'turn_not_desynced' } })

    // The disagreement is still free to surface.
    await h.kernel.turns.report(await h.principalOf(guest.token), 1, {
      stateHash: HASH_B,
      finished: false,
    })
    expect(h.notifier.events.some((event) => event.type === 'turn.desynced')).toBe(true)
    // And the repair the desync asks for is accepted.
    await h.kernel.snapshots.upload(await h.principalOf(host.token), {
      turn: 1,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })
  })

  /**
   * `availableSlots` listed every slot no player row held and ignored the host's own limit, while
   * `joinRunning` counts every such row against it: every join against those seats was refused.
   */
  it('advertises no late-join seats once the roster is at the host limit', async () => {
    const host = await h.kernel.lobby.createMatch({
      settings: {
        name: 'Two Up',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'public',
        gameSettings: { allowLateJoin: true },
      },
      hostDisplayName: 'Host',
    })
    await h.kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Guest' })
    await h.kernel.lobby.start(await h.principalOf(host.token))
    await h.kernel.snapshots.upload(await h.principalOf(host.token), {
      turn: 0,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })

    const listing = (await h.kernel.query.listPublicLobbies(10)).find(
      (candidate) => candidate.id === host.match.id,
    )

    expect(listing?.availableSlots).toEqual([])
    expect(listing?.availableSeatSummaries).toEqual([])
    await expect(
      h.kernel.lobby.joinRunning({ match: host.match.id, displayName: 'Late', slot: 4 }),
    ).rejects.toMatchObject({ details: { reason: 'match_full' } })
  })

  it('refuses seat summaries that would push the settings blob past its cap', async () => {
    const host = await h.kernel.lobby.createMatch({
      settings: {
        name: 'Fat Settings',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: { filler: 'x'.repeat(8 * 1024 - 64) },
      },
      hostDisplayName: 'Host',
    })
    await h.kernel.lobby.start(await h.principalOf(host.token))
    // The merge writes through `json_set`, which skips the checks `createMatch` applies — and the
    // blob is served on every match read and in every public listing.
    await expect(
      h.kernel.snapshots.upload(await h.principalOf(host.token), {
        turn: 0,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'AAAA',
        seatSummaries: [{ slot: 1, gangs: 1, sites: 1, sectors: 1 }],
      }),
    ).rejects.toMatchObject({ details: { reason: 'game_settings_too_large' } })
  })

  it('collects a running match nobody came back to, long after the ordinary window', async () => {
    const { host, guest } = await h.startedMatch()
    await h.kernel.lobby.leave(await h.principalOf(host.token))
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    expect(h.storage.statusOf(host.match.id)).toBe('running')

    // Past the terminated-match window it is left alone: the match is kept running precisely so
    // somebody can rejoin it.
    h.clock.advance(DEFAULT_RETENTION.finishedMaxAgeMs + 1)
    expect(await h.kernel.retention.collect()).toBe(0)
    expect(h.storage.statusOf(host.match.id)).toBe('running')

    h.clock.advance(DEFAULT_RETENTION.abandonedLiveMaxAgeMs - DEFAULT_RETENTION.finishedMaxAgeMs)
    expect(await h.kernel.retention.collect()).toBe(1)
    expect(h.storage.statusOf(host.match.id)).toBeUndefined()
  })

  it('serves a sealed set that re-hashes to the digest it was announced with', async () => {
    const { host, guest } = await h.startedMatch()
    // The guest plans, is then kicked, and the turn seals without them. The vote has not approved
    // computer control yet, so their orders must be in neither the set nor its digest.
    await h.submit(await h.principalOf(guest.token), 1, 2, false)
    await h.kernel.lobby.kick(await h.principalOf(host.token), guest.player.id)
    await h.submit(await h.principalOf(host.token), 1, 1, true)

    const hostP = await h.principalOf(host.token)
    const sealed = await h.kernel.query.sealedOrders(hostP.match, 1)
    expect(sealed.players.map((row) => row.playerId)).toEqual([host.player.id])
    expect(
      await hashOrderSet(
        sealed.players.map((row) => ({ slot: row.slot, ordersHash: row.ordersHash })),
      ),
    ).toBe(sealed.orderSetHash)
  })

  it('keeps a departed player in the set of a turn that sealed before they left', async () => {
    const { host, guest } = await h.startedMatch()
    for (const token of [host.token, guest.token]) {
      await h.submit(await h.principalOf(token), 1, 1, true)
    }
    await h.kernel.lobby.kick(await h.principalOf(host.token), guest.player.id)
    const hostP = await h.principalOf(host.token)
    const sealed = await h.kernel.query.sealedOrders(hostP.match, 1)
    expect(sealed.players.map((row) => row.slot)).toEqual([0, 1])
    expect(
      await hashOrderSet(
        sealed.players.map((row) => ({ slot: row.slot, ordersHash: row.ordersHash })),
      ),
    ).toBe(sealed.orderSetHash)
  })

  it('numbers the event log without gaps when two players act at the same time', async () => {
    const { host, guest } = await h.startedMatch()
    const [hostP, guestP] = await Promise.all([
      h.principalOf(host.token),
      h.principalOf(guest.token),
    ])
    await Promise.all([h.submit(hostP, 1, 1, true), h.submit(guestP, 1, 2, true)])
    const log = await h.storage.events.listAfter(hostP.match.id, 0, 100)
    // Contiguous from 1: a hole would be a sequence number a stream cursor has already passed.
    expect(log.map((event) => event.seq)).toEqual(log.map((_, index) => index + 1))
    expect(await h.storage.events.lastSeq(hostP.match.id)).toBe(log.length)
    const types = log.map((event) => event.type)
    expect(types.filter((type) => type === 'turn.readiness')).toHaveLength(2)
    expect(types.filter((type) => type === 'turn.sealed')).toHaveLength(1)
    expect(h.notifier.events.map((event) => event.seq)).toEqual(log.map((event) => event.seq))
  })

  it('finishes a seal that was interrupted before the next turn opened', async () => {
    const { host, guest } = await h.startedMatch()
    for (const token of [host.token, guest.token]) {
      await h.submit(await h.principalOf(token), 1, 1, false)
    }
    // What a process dying mid-seal leaves behind: turn 1 sealed, no digest, no turn 2.
    expect(
      await h.storage.turns.transition(host.match.id, 1, ['open'], {
        status: 'sealed',
        sealedAt: h.clock.now(),
      }),
    ).toBe(true)
    expect((await h.principalOf(host.token)).match.currentTurn).toBe(1)

    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 1 })

    const hostP = await h.principalOf(host.token)
    expect(hostP.match.currentTurn).toBe(2)
    const sealed = await h.kernel.query.sealedOrders(hostP.match, 1)
    expect(sealed.players).toHaveLength(2)
    expect(h.notifier.events.map((event) => event.type)).toContain('turn.sealed')
    // And a second sweep has nothing left to do.
    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 0 })
  })

  it('restarts the open turn h.clock when a desync pause lifts', async () => {
    const { host, guest } = await h.startedMatch(60)
    for (const token of [host.token, guest.token]) {
      await h.submit(await h.principalOf(token), 1, 1, true)
    }
    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    await h.kernel.turns.report(await h.principalOf(guest.token), 1, {
      stateHash: HASH_B,
      finished: false,
    })
    expect(h.storage.statusOf(host.match.id)).toBe('desynced')

    // The pause outlasts the turn timer; orders are refused throughout, so the turn must not seal
    // empty the moment the match resumes.
    h.clock.advance(120_000)
    await h.kernel.snapshots.upload(await h.principalOf(host.token), {
      turn: 1,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })
    await h.kernel.turns.report(await h.principalOf(guest.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    expect(h.storage.statusOf(host.match.id)).toBe('running')

    const open = await h.storage.turns.get(host.match.id, 2)
    expect(open?.deadlineAt).toEqual(new Date(h.clock.now().getTime() + 60_000))
    expect(h.notifier.events.map((event) => event.type)).toContain('turn.deadlineExtended')
    expect(h.scheduler.scheduled.at(-1)).toEqual({
      matchId: host.match.id,
      turn: 2,
      dueAt: open?.deadlineAt,
    })
    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 0 })
  })

  it('collects matches past their retention age, and never a live one', async () => {
    const { host } = await h.startedMatch()
    const abandoned = await h.kernel.lobby.createMatch({
      settings: {
        name: 'empty',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: {},
      },
      hostDisplayName: 'Nobody',
    })
    await h.kernel.lobby.leave(await h.principalOf(abandoned.token))
    expect(h.storage.statusOf(abandoned.match.id)).toBe('abandoned')

    expect(await h.kernel.retention.collect()).toBe(0)
    h.clock.advance(DEFAULT_RETENTION.finishedMaxAgeMs + 1)
    expect(await h.kernel.retention.collect()).toBe(1)
    expect(h.storage.statusOf(abandoned.match.id)).toBeUndefined()
    expect(h.storage.statusOf(host.match.id)).toBe('running')
  })

  it('gives the seat back when the player row cannot be written', async () => {
    const created = await h.kernel.lobby.createMatch({
      settings: {
        name: 'x',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'public',
        gameSettings: {},
      },
      hostDisplayName: 'Host',
    })
    // The seat is claimed before the row exists, so a failure in between would otherwise shrink the
    // lobby's capacity for good.
    const create = h.storage.players.create
    h.storage.players.create = async () => {
      throw new Error('disk on fire')
    }
    await expect(
      h.kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G' }),
    ).rejects.toThrow('disk on fire')
    h.storage.players.create = create
    expect((await h.kernel.query.listPublicLobbies(10))[0]?.playerCount).toBe(1)
    const recovered = await h.kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G' })
    expect(recovered.player.displayName).toBe('G')
  })

  it('accepts a confirmed turn checkpoint only at the hash the verdict settled on', async () => {
    const { host, guest } = await h.startedMatch()
    for (const token of [host.token, guest.token]) {
      await h.submit(await h.principalOf(token), 1, 1, true)
    }
    for (const token of [host.token, guest.token]) {
      await h.kernel.turns.report(await h.principalOf(token), 1, {
        stateHash: HASH_A,
        finished: false,
      })
    }
    const upload = async (stateHash: string) =>
      h.kernel.snapshots.upload(await h.principalOf(host.token), {
        turn: 1,
        formatVersion: 1,
        stateHash,
        body: 'AAAA',
        seatSummaries: [],
      })
    // A checkpoint may only agree with the verdict: the matching hash is stored, and the
    // contradictory one is refused, which is the whole difference between a checkpoint and a
    // second opinion on a settled turn.
    await upload(HASH_A)
    expect((await h.kernel.snapshots.latest(host.match.id)).turn).toBe(1)
    await expect(upload(HASH_B)).rejects.toMatchObject({
      details: { reason: 'uncorroborated_state_hash' },
    })
  })

  it('a host leaving the lobby abandons it; a guest leaving frees the seat', async () => {
    const created = await h.kernel.lobby.createMatch({
      settings: {
        name: 'x',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'public',
        gameSettings: {},
      },
      hostDisplayName: 'Host',
    })
    const guest = await h.kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G' })
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    expect((await h.kernel.query.listPublicLobbies(10))[0]?.playerCount).toBe(1)
    await h.kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G2' })
    await h.kernel.lobby.leave(await h.principalOf(created.token))
    expect(h.storage.statusOf(created.match.id)).toBe('abandoned')
    expect(await h.kernel.query.listPublicLobbies(10)).toEqual([])
  })

  /**
   * The submitted slot is the one every client applies the orders under, so an op naming another
   * player would make the two attributions disagree. The server is the only party that knows the
   * submitter's slot for certain, so it is the only one that can refuse this.
   */
  it('refuses orders that act for another slot', async () => {
    const { host } = await h.startedMatch()
    const hostP = await h.principalOf(host.token)
    expect(hostP.player.slot).toBe(0)
    await expect(
      h.kernel.turns.submitOrders(hostP, 1, {
        orders: { schemaVersion: 1, ops: [{ op: 'cancelCommand', player: 1, gang: 4 }] },
        ready: true,
      }),
    ).rejects.toMatchObject({ details: { reason: 'foreign_slot_ops' } })
    // The refusal leaves nothing behind: no orders, and no readiness that could seal the turn.
    expect((await h.kernel.query.ownSubmission(hostP.match, hostP.player.id, 1)).orders).toBeNull()
    expect(h.storage.statusOf(hostP.match.id)).toBe('running')
  })
})
