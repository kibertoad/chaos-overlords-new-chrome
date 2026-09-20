import { beforeEach, describe, expect, it } from 'vitest'
import { createHarness, HASH_A, HASH_B, type Harness } from './harness'

describe('desync verdicts, snapshots and recovery', () => {
  let h: Harness

  beforeEach(() => {
    h = createHarness()
  })

  /**
   * A recovery snapshot becomes the hash everyone converges on, so a host who desynced deliberately
   * must not be able to name a state of its own invention.
   */
  it('refuses a recovery snapshot no majority of players reported', async () => {
    const { host, guest, third } = await h.startedMatchOfThree()
    for (const token of [host.token, guest.token, third.token]) {
      await h.submit(await h.principalOf(token), 1, 1, true)
    }
    // Two honest clients agree; the host is the odd one out.
    await h.kernel.turns.report(await h.principalOf(guest.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    await h.kernel.turns.report(await h.principalOf(third.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_B,
      finished: false,
    })
    expect(h.storage.statusOf(host.match.id)).toBe('desynced')
    const desync = h.notifier.events.find((event) => event.type === 'turn.desynced')
    expect(desync?.payload).toMatchObject({ candidateStateHashes: [HASH_A] })

    const upload = async (stateHash: string) =>
      h.kernel.snapshots.upload(await h.principalOf(host.token), {
        turn: 1,
        formatVersion: 1,
        stateHash,
        body: 'AAAA',
        seatSummaries: [],
      })
    await expect(upload(HASH_B)).rejects.toMatchObject({
      details: { reason: 'uncorroborated_state_hash', candidateStateHashes: [HASH_A] },
    })
    // The majority's hash is allowed, and settles the turn once the host re-reports against it.
    await expect(upload(HASH_A)).resolves.toBeUndefined()
    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    expect(h.storage.statusOf(host.match.id)).toBe('running')
  })

  it('lets the host break a genuine tie, which is all a two-player desync can be', async () => {
    const { host, guest } = await h.startedMatch()
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
    await expect(
      h.kernel.snapshots.upload(await h.principalOf(host.token), {
        turn: 1,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'AAAA',
        seatSummaries: [],
      }),
    ).resolves.toBeUndefined()
  })

  /**
   * Retention only collects matches that are over, so a long match that desyncs repeatedly would
   * otherwise hold a megabyte of base64 per turn with nothing to stop it.
   */
  it('keeps only the recent snapshots of a live match', async () => {
    const { host, guest } = await h.startedMatch()
    for (const token of [host.token, guest.token]) {
      await h.submit(await h.principalOf(token), 1, 1, true)
    }
    const hostP = await h.principalOf(host.token)
    // Turn 1 sealed, so turns 0..1 may be snapshotted; drive currentTurn up to make room for more.
    for (let turn = 0; turn <= 7; turn += 1) {
      await h.storage.snapshots.put({
        matchId: hostP.match.id,
        turn,
        formatVersion: 1,
        protocolVersion: 1,
        sessionVersion: 1,
        stateHash: HASH_A,
        uploadedByPlayerId: hostP.player.id,
        uploadedAt: h.clock.now(),
        body: 'AAAA',
      })
    }
    expect(await h.storage.snapshots.prune(hostP.match.id, 5)).toBe(3)
    const remaining = []
    for (let turn = 0; turn <= 7; turn += 1) {
      if (await h.storage.snapshots.get(hostP.match.id, turn)) remaining.push(turn)
    }
    expect(remaining).toEqual([3, 4, 5, 6, 7])
    // The newest is what a reconnecting client bootstraps from, so it must survive.
    expect((await h.storage.snapshots.getLatest(hostP.match.id))?.turn).toBe(7)
  })

  it('stores a confirmed-turn checkpoint, and refuses one that rewrites the verdict', async () => {
    const { host, guest } = await h.startedMatch()
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    await h.submit(await h.principalOf(guest.token), 1, 2, true)
    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    await h.kernel.turns.report(await h.principalOf(guest.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })

    // The checkpoint a reconnect replays from: accepted at the hash the verdict settled on...
    await h.kernel.snapshots.upload(await h.principalOf(host.token), {
      turn: 1,
      formatVersion: 23,
      stateHash: HASH_A,
      body: 'AUTOSAVE',
      seatSummaries: [],
    })
    expect(await h.kernel.snapshots.latest(host.match.id)).toMatchObject({
      turn: 1,
      body: 'AUTOSAVE',
    })

    // ...and refused at any other, because a settled turn's state is not the host's to restate.
    await expect(
      h.kernel.snapshots.upload(await h.principalOf(host.token), {
        turn: 1,
        formatVersion: 23,
        stateHash: HASH_B,
        body: 'DOCTORED',
        seatSummaries: [],
      }),
    ).rejects.toMatchObject({ details: { reason: 'uncorroborated_state_hash' } })
  })

  /**
   * The seal opens the successor before anyone has reported, so the turn that ends the match is
   * always followed by an open one. Leaving its deadline armed would have the sweeper chasing a
   * finished match forever, and a client counting down after the game ended.
   */
  it('disarms the open turn left behind when a match finishes', async () => {
    const { host, guest } = await h.startedMatch(60)
    for (const token of [host.token, guest.token]) {
      await h.submit(await h.principalOf(token), 1, 1, true)
    }
    expect((await h.storage.turns.get(host.match.id, 2))?.deadlineAt).toBeInstanceOf(Date)
    for (const token of [host.token, guest.token]) {
      await h.kernel.turns.report(await h.principalOf(token), 1, {
        stateHash: HASH_A,
        finished: true,
      })
    }
    expect(h.storage.statusOf(host.match.id)).toBe('finished')
    expect((await h.storage.turns.get(host.match.id, 2))?.deadlineAt).toBeNull()
    h.clock.advance(120_000)
    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 0 })
  })

  /**
   * The other way a match can be left with nothing to play: the status changed and the process died
   * before turn 1 existed. Same repair, driven from the match rather than from a turn row.
   */
  it('lets a returning player decide the seats that went quiet while nobody was present', async () => {
    const { host, guest } = await h.startedMatch()
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    // Nobody is present when the host goes, so no vote was ever opened for that seat.
    await h.kernel.lobby.leave(await h.principalOf(host.token))
    expect(await h.storage.takeovers.listOpenPrompts(host.match.id)).toEqual([guest.player.id])

    await h.kernel.lobby.rejoin(await h.principalOf(guest.token))
    // The returning player is asked about the absent host, and nothing about themselves.
    expect(await h.storage.takeovers.listOpenPrompts(host.match.id)).toEqual([host.player.id])
    expect(
      h.notifier.events
        .filter((event) => event.type === 'match.takeoverVoteRequested')
        .map((event) => event.payload.playerId),
    ).toEqual([guest.player.id, host.player.id])
    await h.kernel.lobby.voteOnTakeover(await h.principalOf(guest.token), host.player.id, {
      decision: 'computer',
    })
    expect((await h.storage.players.get(host.player.id))?.status).toBe('computer')
    expect(await h.storage.takeovers.hasOpenPrompts(host.match.id)).toBe(false)
  })

  it('opens the vote a seat never had when somebody votes on it', async () => {
    const { host, guest, third } = await h.startedMatchOfThree()
    await h.kernel.lobby.leave(await h.principalOf(guest.token))
    // A prompt opened before this server kept them durably: nothing on file, seat still absent.
    await h.storage.takeovers.closePrompt(host.match.id, guest.player.id)
    await h.kernel.lobby.voteOnTakeover(await h.principalOf(host.token), guest.player.id, {
      decision: 'computer',
    })
    expect(await h.storage.takeovers.listOpenPrompts(host.match.id)).toEqual([guest.player.id])
    await h.kernel.lobby.voteOnTakeover(await h.principalOf(third.token), guest.player.id, {
      decision: 'computer',
    })
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('computer')
    // An active seat is never put to a vote, with or without a prompt.
    await expect(
      h.kernel.lobby.voteOnTakeover(await h.principalOf(host.token), third.player.id, {
        decision: 'computer',
      }),
    ).rejects.toMatchObject({ details: { reason: 'takeover_not_pending' } })
  })

  it('waits for a seat that merely missed a deadline before confirming its turn', async () => {
    const { host, guest } = await h.startedMatch(60)
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    h.clock.advance(60_000)
    await h.kernel.turns.sweep()
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('takeoverPending')

    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    // The absent seat's client still applies the sealed turn; its report is part of the verdict.
    expect((await h.storage.turns.get(host.match.id, 1))?.status).toBe('sealed')
    await h.kernel.turns.report(await h.principalOf(guest.token), 1, {
      stateHash: HASH_B,
      finished: false,
    })
    expect((await h.storage.turns.get(host.match.id, 1))?.status).toBe('desynced')
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('active')
  })

  it('stores nothing when a snapshot is refused for its seat summaries', async () => {
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
    await expect(
      h.kernel.snapshots.upload(await h.principalOf(host.token), {
        turn: 0,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'AAAA',
        seatSummaries: [{ slot: 1, gangs: 1, sites: 1, sectors: 1 }],
      }),
    ).rejects.toMatchObject({ details: { reason: 'game_settings_too_large' } })
    // A refused upload must not leave a snapshot behind that the caller was told was rejected.
    expect(await h.storage.snapshots.getLatest(host.match.id)).toBeNull()
  })

  it('seats and announces a match whose start died before either', async () => {
    const created = await h.kernel.lobby.createMatch({
      settings: {
        name: 'x',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: {},
      },
      hostDisplayName: 'Host',
    })
    const guest = await h.kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G' })
    await h.storage.matches.transition(created.match.id, ['lobby'], {
      status: 'running',
      seed: 7,
      currentTurn: 0,
      updatedAt: h.clock.now(),
    })
    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 1 })
    const roster = await h.storage.players.listByMatch(created.match.id)
    expect(roster.map((player) => [player.id, player.slot])).toEqual([
      [created.player.id, 0],
      [guest.player.id, 1],
    ])
    const started = h.notifier.events.filter((event) => event.type === 'match.started')
    expect(started).toHaveLength(1)
    expect(started[0]?.payload).toMatchObject({ seed: 7 })
    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 0 })
    expect(h.notifier.events.filter((event) => event.type === 'match.started')).toHaveLength(1)
  })

  it('opens turn 1 for a match whose start was interrupted', async () => {
    const created = await h.kernel.lobby.createMatch({
      settings: {
        name: 'x',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: {},
      },
      hostDisplayName: 'Host',
    })
    await h.kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G' })
    // What `start` leaves behind if it dies after the transition: running, but no turn at all.
    expect(
      await h.storage.matches.transition(created.match.id, ['lobby'], {
        status: 'running',
        seed: 1,
        currentTurn: 0,
        updatedAt: h.clock.now(),
      }),
    ).toBe(true)
    expect(await h.storage.turns.get(created.match.id, 1)).toBeNull()

    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 1 })
    expect((await h.storage.turns.get(created.match.id, 1))?.status).toBe('open')
    expect((await h.kernel.auth.authenticate(created.token)).match.currentTurn).toBe(1)
    // Idempotent: a second sweep has nothing left to repair.
    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 0 })
  })

  /**
   * A seat claimed a moment before the host pressed start must not become an unseated player in a
   * running match: they would hold a seat, count towards readiness, and have no slot to play.
   */
  it('refuses a join that lands after the match has started', async () => {
    const created = await h.kernel.lobby.createMatch({
      settings: {
        name: 'x',
        maxPlayers: 4,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: {},
      },
      hostDisplayName: 'Host',
    })
    await h.kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G' })
    await h.kernel.lobby.start(await h.principalOf(created.token))
    // The same refusal an unknown code gets, so a scan of the code space cannot tell a code that
    // exists from one that does not.
    await expect(
      h.kernel.lobby.join({ joinCode: created.joinCode, displayName: 'Late' }),
    ).rejects.toMatchObject({ details: { reason: 'unknown_join_code' } })
    await expect(
      h.kernel.lobby.join({ joinCode: 'ZZZZZZZZ', displayName: 'Late' }),
    ).rejects.toMatchObject({ details: { reason: 'unknown_join_code' } })

    // And when the race is lost inside the window: the seat is claimed while the lobby is open, the
    // match starts, and only then does the row get written. Capacity and the roster stay honest.
    const running = (await h.principalOf(created.token)).match
    expect(await h.storage.matches.claimSeat(running.id)).toBeNull()
    const roster = await h.storage.players.listByMatch(running.id)
    expect(roster.every((player) => player.slot >= 0)).toBe(true)
    expect(roster).toHaveLength(2)
  })

  /**
   * S1: two `completeSeal` calls in flight at once, with the roster changing between them.
   *
   * The sweep deliberately runs `completeSeal` against a seal the request path is still finishing,
   * so this interleaving is a designed one rather than a corner. The freeze used to be conditional
   * on the STATUS, which does not change across the window, so both callers computed a set, both
   * wrote one, and both announced it — and with a departure in between, the two sets differed. A
   * client that fetched the set after the second write verified it against the first announced
   * digest and ended its session on "the sealed-set digest for turn N does not match the event
   * log", which is terminal.
   *
   * The interleaving is forced rather than raced for: the first caller is held on the roster read
   * the digest is computed from, so it goes on to hash a roster that has since changed. That is
   * precisely the state a second caller must not be allowed to overwrite.
   */
  it('freezes a sealed turn once, even with a departure between two concurrent completions', async () => {
    const { host, guest, third } = await h.startedMatchOfThree()
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    await h.submit(await h.principalOf(guest.token), 1, 2, true)
    await h.submit(await h.principalOf(third.token), 1, 3, false)
    const matchId = host.match.id
    // The seal's own compare-and-swap without the completion that follows it, so both callers
    // below enter the freeze branch on the same unfrozen turn.
    expect(
      await h.storage.turns.transition(matchId, 1, ['open'], {
        status: 'sealed',
        sealedAt: h.clock.now(),
      }),
    ).toBe(true)

    // Hold the second roster read — the one `completeSeal` derives the sealed set from — so the
    // first caller carries a roster of three past the departure below.
    let release = (): void => {}
    const held = new Promise<void>((resolve) => {
      release = resolve
    })
    const realListByMatch = h.storage.players.listByMatch.bind(h.storage.players)
    let reads = 0
    h.storage.players.listByMatch = async (id: string) => {
      const roster = await realListByMatch(id)
      reads += 1
      if (reads === 2) await held
      return roster
    }

    const held3 = h.kernel.turns.sweep()
    await Promise.resolve()
    await h.kernel.lobby.kick(await h.principalOf(host.token), third.player.id)
    // The second caller runs to completion on the roster of two while the first is still held.
    await h.kernel.turns.sweep()
    release()
    await held3
    h.storage.players.listByMatch = realListByMatch

    const sealed = h.notifier.events.filter((event) => event.type === 'turn.sealed')
    expect(sealed).toHaveLength(1)
    const turn = await h.storage.turns.get(matchId, 1)
    const announced = sealed[0]?.payload as { orderSetHash: string } | undefined
    expect(turn?.orderSetHash).toBe(announced?.orderSetHash)
  })

  /**
   * R1, server half: a repair must name the turn that actually diverged.
   *
   * The successor a seal opens carries no reports, so `authoritativeCandidates` had nothing to
   * count for it and let the host store any hash it liked. `evaluateConsensus` then judged every
   * later report against that hash and could only confirm or wait, never desync — the host
   * arbitrating a disagreement it is a party to, through the one turn the corroboration rule could
   * not see.
   */
  it('refuses a repair snapshot for a turn that did not diverge', async () => {
    const { host, guest } = await h.startedMatch()
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    await h.submit(await h.principalOf(guest.token), 1, 2, true)
    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    await h.kernel.turns.report(await h.principalOf(guest.token), 1, {
      stateHash: HASH_B,
      finished: false,
    })
    expect(h.storage.statusOf(host.match.id)).toBe('desynced')

    // Turn 2 is `open`, turn 1 is the one that diverged. Neither the open successor nor a turn
    // already confirmed may be repaired.
    await expect(
      h.kernel.snapshots.upload(await h.principalOf(host.token), {
        turn: 2,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'AAAA',
        seatSummaries: [],
      }),
    ).rejects.toMatchObject({ details: { reason: 'turn_open' } })

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
    // Confirmed now, so a snapshot for it can only be a checkpoint, and a checkpoint may only
    // agree with the verdict.
    await expect(
      h.kernel.snapshots.upload(await h.principalOf(host.token), {
        turn: 1,
        formatVersion: 1,
        stateHash: HASH_B,
        body: 'BBBB',
        seatSummaries: [],
      }),
    ).rejects.toMatchObject({ details: { reason: 'uncorroborated_state_hash' } })
  })

  /**
   * R1, the case that had no fix: the host is the odd one out.
   *
   * Its own hash can never be the majority's, so a host-only repair rule meant no repair existed
   * and the match stayed paused until retention collected it. The hash is still the hard rule —
   * whoever posts the repair must name the one the players reported most often — so letting a
   * majority holder post it cannot be used to impose a state nobody computed.
   */
  it('lets a majority holder repair a desync the host is the outlier of, and refuses the outlier', async () => {
    const { host, guest, third } = await h.startedMatchOfThree()
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    await h.submit(await h.principalOf(guest.token), 1, 2, true)
    await h.submit(await h.principalOf(third.token), 1, 3, true)
    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_B,
      finished: false,
    })
    await h.kernel.turns.report(await h.principalOf(guest.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    await h.kernel.turns.report(await h.principalOf(third.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    expect(h.storage.statusOf(host.match.id)).toBe('desynced')

    // The host's own state is not what the others computed, so it may not be imposed on them.
    await expect(
      h.kernel.snapshots.upload(await h.principalOf(host.token), {
        turn: 1,
        formatVersion: 1,
        stateHash: HASH_B,
        body: 'BBBB',
        seatSummaries: [],
      }),
    ).rejects.toMatchObject({ details: { reason: 'uncorroborated_state_hash' } })

    await h.kernel.snapshots.upload(await h.principalOf(guest.token), {
      turn: 1,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })
    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    expect(h.storage.statusOf(host.match.id)).toBe('running')
  })

  /**
   * The other half of the same rule: a tie has no majority, so the host still breaks it and a peer
   * may not. Without this, whoever uploaded first would decide a two-player match.
   */
  it('lets only the host break a tie between two reported states', async () => {
    const { host, guest } = await h.startedMatch()
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    await h.submit(await h.principalOf(guest.token), 1, 2, true)
    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    await h.kernel.turns.report(await h.principalOf(guest.token), 1, {
      stateHash: HASH_B,
      finished: false,
    })

    await expect(
      h.kernel.snapshots.upload(await h.principalOf(guest.token), {
        turn: 1,
        formatVersion: 1,
        stateHash: HASH_B,
        body: 'BBBB',
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
    expect((await h.kernel.snapshots.latest(host.match.id)).body).toBe('AAAA')
  })

  /**
   * S4: readiness is a change notification, not a receipt.
   *
   * A member may submit at the member rate limit, and the client sends a whole-document
   * replacement per command the player queues. Publishing on every `ready: true` submission let one
   * client grow a match's durable log by hundreds of thousands of rows a day, and wake every
   * subscriber of the match for each of them.
   */
  it('publishes readiness only when it changes', async () => {
    const { host, guest } = await h.startedMatch()
    const readiness = () => h.notifier.events.filter((event) => event.type === 'turn.readiness')
    await h.submit(await h.principalOf(host.token), 1, 1, false)
    expect(readiness()).toHaveLength(0)
    await h.submit(await h.principalOf(host.token), 1, 2, true)
    expect(readiness()).toHaveLength(1)
    for (let repeat = 0; repeat < 5; repeat += 1) {
      await h.submit(await h.principalOf(host.token), 1, 3 + repeat, true)
    }
    expect(readiness()).toHaveLength(1)
    await h.submit(await h.principalOf(host.token), 1, 9, false)
    expect(readiness()).toHaveLength(2)
    await h.submit(await h.principalOf(guest.token), 1, 10, true)
    expect(readiness()).toHaveLength(3)
  })

  /**
   * S6: evidence a verdict was taken on is immutable.
   *
   * `report` reads the turn status and writes in two statements, so a report that lost the race
   * with the `settle` that confirmed the turn used to land behind the verdict it could not have
   * changed. The write is conditional on the turn still awaiting one now.
   */
  it('refuses a report that lands after the turn was confirmed', async () => {
    const { host, guest } = await h.startedMatch()
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    await h.submit(await h.principalOf(guest.token), 1, 2, true)
    const principal = await h.principalOf(guest.token)
    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    await h.kernel.turns.report(principal, 1, { stateHash: HASH_A, finished: false })
    expect((await h.storage.turns.get(host.match.id, 1))?.status).toBe('confirmed')
    expect(
      await h.storage.turns.upsertReport({
        matchId: host.match.id,
        turn: 1,
        playerId: principal.player.id,
        stateHash: HASH_B,
        finished: false,
        reportedAt: h.clock.now(),
      }),
    ).toBe(false)
    const reports = await h.storage.turns.listReports(host.match.id, 1)
    expect(reports.every((report) => report.stateHash === HASH_A)).toBe(true)
  })
})
