import type { MatchEventBody } from '@chaos-overlords/contracts'
import { beforeEach, describe, expect, it } from 'vitest'
import type { Match } from '../src/domain/entities'
import { createHarness, HASH_A, HASH_B, type Harness } from './harness'

/**
 * The announcements that follow a compare-and-swap. Each used to be published by the swap's winner
 * alone, so an append that threw after the swap lost the event for good and every client waited on
 * it forever. These pin that a failed append is always finished by a later pass, and that a repeat
 * never logs the same fact twice.
 */
describe('announcements that survive a failed publish', () => {
  let h: Harness

  beforeEach(() => {
    h = createHarness()
  })

  /** Make the next keyed append of `type` throw, as a database blip after the swap would. */
  function failNextAppendOf(type: MatchEventBody['type']): void {
    const real = h.storage.events.appendOnce
    let failed = false
    h.storage.events.appendOnce = async (event, key) => {
      if (!failed && event.type === type) {
        failed = true
        throw new Error(`append of ${type} failed`)
      }
      return real(event, key)
    }
  }

  async function logOf(matchId: string) {
    return h.storage.events.listAfter(matchId, 0, 1000)
  }

  async function eventsOf(matchId: string, type: MatchEventBody['type']) {
    return (await logOf(matchId)).filter((event) => event.type === type)
  }

  it('announces a seal whose announcement failed, from the bounded sweep, exactly once', async () => {
    const { host, guest } = await h.startedMatch()
    const matchId = host.match.id
    // Spend the first pass, which is an unbounded scan, so the repair below has to come from the
    // bounded one. Then let the match row go stale: planning took longer than the sweep window,
    // and sealing writes only the turn row.
    await h.kernel.turns.sweep()
    h.clock.advance(10 * 60_000)

    failNextAppendOf('turn.sealed')
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    await expect(h.submit(await h.principalOf(guest.token), 1, 2, true)).rejects.toThrow(
      'append of turn.sealed failed',
    )
    const stalled = await h.storage.turns.get(matchId, 1)
    expect(stalled?.status).toBe('sealed')
    expect(stalled?.orderSetHash).not.toBeNull()
    expect(await eventsOf(matchId, 'turn.sealed')).toEqual([])
    expect(await h.storage.turns.get(matchId, 2)).toBeNull()

    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 1 })
    const sealed = await eventsOf(matchId, 'turn.sealed')
    expect(sealed.map((event) => event.payload)).toEqual([
      { turn: 1, orderSetHash: stalled?.orderSetHash },
    ])
    expect((await h.storage.turns.get(matchId, 2))?.status).toBe('open')
    // The seal is announced before its successor opens.
    const log = await logOf(matchId)
    const sealedAt = log.findIndex((event) => event.type === 'turn.sealed')
    const openedAt = log.findIndex(
      (event) => event.type === 'turn.opened' && event.payload.turn === 2,
    )
    expect(sealedAt).toBeLessThan(openedAt)

    // Nothing is left to finish, and a repeat logs nothing.
    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 0 })
    expect(await eventsOf(matchId, 'turn.sealed')).toHaveLength(1)
  })

  it('announces a divergence whose announcement failed, exactly once', async () => {
    const { host, guest } = await h.startedMatch()
    const matchId = host.match.id
    for (const token of [host.token, guest.token]) {
      await h.submit(await h.principalOf(token), 1, 1, true)
    }
    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    failNextAppendOf('turn.desynced')
    await expect(
      h.kernel.turns.report(await h.principalOf(guest.token), 1, {
        stateHash: HASH_B,
        finished: false,
      }),
    ).rejects.toThrow('append of turn.desynced failed')
    // Paused before the announcement, so the sweep's desync pass is what finds it.
    expect(h.storage.statusOf(matchId)).toBe('desynced')
    expect((await h.storage.turns.get(matchId, 1))?.desyncedAt).toBeNull()
    expect(await eventsOf(matchId, 'turn.desynced')).toEqual([])

    await h.kernel.turns.sweep()
    expect(await eventsOf(matchId, 'turn.desynced')).toHaveLength(1)
    expect((await eventsOf(matchId, 'match.statusChanged')).map((event) => event.payload)).toEqual([
      { status: 'desynced' },
    ])
    expect((await h.storage.turns.get(matchId, 1))?.desyncedAt).toBeInstanceOf(Date)

    await h.kernel.turns.sweep()
    expect(await eventsOf(matchId, 'turn.desynced')).toHaveLength(1)
    expect(await eventsOf(matchId, 'match.statusChanged')).toHaveLength(1)
  })

  it('finishes a match whose final confirmation failed to publish', async () => {
    const { host, guest } = await h.startedMatch(60)
    const matchId = host.match.id
    for (const token of [host.token, guest.token]) {
      await h.submit(await h.principalOf(token), 1, 1, true)
    }
    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: true,
    })
    failNextAppendOf('turn.confirmed')
    await expect(
      h.kernel.turns.report(await h.principalOf(guest.token), 1, {
        stateHash: HASH_A,
        finished: true,
      }),
    ).rejects.toThrow('append of turn.confirmed failed')
    // Confirmed and nothing after it: the state no request path revisits.
    expect((await h.storage.turns.get(matchId, 1))?.status).toBe('confirmed')
    expect((await h.storage.turns.get(matchId, 1))?.settledAt).toBeNull()
    expect(h.storage.statusOf(matchId)).toBe('running')
    // The client treats a report on a confirmed turn as done, so its retry does not help.
    await expect(
      h.kernel.turns.report(await h.principalOf(guest.token), 1, {
        stateHash: HASH_A,
        finished: true,
      }),
    ).rejects.toMatchObject({ details: { reason: 'turn_confirmed' } })

    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 1 })
    expect(h.storage.statusOf(matchId)).toBe('finished')
    expect((await eventsOf(matchId, 'turn.confirmed')).map((event) => event.payload)).toEqual([
      { turn: 1, stateHash: HASH_A },
    ])
    expect((await eventsOf(matchId, 'match.statusChanged')).map((event) => event.payload)).toEqual([
      { status: 'finished' },
    ])
    expect((await h.storage.turns.get(matchId, 2))?.deadlineAt).toBeNull()
    expect((await h.storage.turns.get(matchId, 1))?.settledAt).toBeInstanceOf(Date)

    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 0 })
    expect(await eventsOf(matchId, 'turn.confirmed')).toHaveLength(1)
  })

  it('finishes the announcement of a match whose finish was not announced', async () => {
    const { host, guest } = await h.startedMatch()
    const matchId = host.match.id
    for (const token of [host.token, guest.token]) {
      await h.submit(await h.principalOf(token), 1, 1, true)
    }
    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: true,
    })
    failNextAppendOf('match.statusChanged')
    await expect(
      h.kernel.turns.report(await h.principalOf(guest.token), 1, {
        stateHash: HASH_A,
        finished: true,
      }),
    ).rejects.toThrow('append of match.statusChanged failed')
    expect(h.storage.statusOf(matchId)).toBe('finished')

    await h.kernel.turns.sweep()
    expect((await eventsOf(matchId, 'match.statusChanged')).map((event) => event.payload)).toEqual([
      { status: 'finished' },
    ])
    expect(await eventsOf(matchId, 'turn.confirmed')).toHaveLength(1)
    expect((await h.storage.turns.get(matchId, 1))?.settledAt).toBeInstanceOf(Date)
  })

  it('puts the question about a seat that missed a deadline, when asking it first failed', async () => {
    const { host, guest } = await h.startedMatch(60)
    const matchId = host.match.id
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    h.clock.advance(61_000)

    failNextAppendOf('match.takeoverVoteRequested')
    // The deadline seal wins its swap and then throws in the prompt, after the seat was marked
    // pending and its prompt stored. The same pass's stalled-seal repair then finds the seat
    // pending, which it used to skip as not active, and the prompt unannounced.
    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 1 })
    expect((await h.storage.players.get(guest.player.id))?.status).toBe('takeoverPending')
    expect(await h.storage.takeovers.listOpenPrompts(matchId)).toEqual([guest.player.id])
    expect(
      (await eventsOf(matchId, 'match.takeoverVoteRequested')).map((event) => event.payload),
    ).toEqual([{ playerId: guest.player.id, turn: 1 }])
    expect(await eventsOf(matchId, 'turn.sealed')).toHaveLength(1)
    expect((await h.storage.turns.get(matchId, 2))?.status).toBe('open')

    // A rejoin re-asks about every absent seat; an announced prompt is left as it is.
    await h.kernel.turns.openTakeoverPrompt(matchId, guest.player.id, 2)
    expect(await eventsOf(matchId, 'match.takeoverVoteRequested')).toHaveLength(1)
  })

  /** Seal turns 1 and 2 of a two-seat match with nothing reported on either. */
  async function twoSealedTurns(turnTimerSeconds = 0) {
    const seats = await h.startedMatch(turnTimerSeconds)
    for (const turn of [1, 2]) {
      for (const token of [seats.host.token, seats.guest.token]) {
        await h.submit(await h.principalOf(token), turn, turn, true)
      }
    }
    return seats
  }

  async function reportTurn(token: string, turn: number, stateHash: string): Promise<void> {
    await h.kernel.turns.report(await h.principalOf(token), turn, { stateHash, finished: false })
  }

  async function statusesOf(matchId: string) {
    return (await eventsOf(matchId, 'match.statusChanged')).map((event) => event.payload)
  }

  it('announces one pause however many turns diverge behind it', async () => {
    const { host, guest } = await twoSealedTurns()
    const matchId = host.match.id
    await reportTurn(host.token, 1, HASH_A)
    await reportTurn(guest.token, 1, HASH_B)
    expect(await statusesOf(matchId)).toEqual([{ status: 'desynced' }])

    // Turn 2 diverges too, and its verdict is cut short before its receipt: the sweep's retry finds
    // the match paused, and the pause already announced by turn 1.
    await reportTurn(host.token, 2, HASH_A)
    failNextAppendOf('turn.desynced')
    await expect(reportTurn(guest.token, 2, HASH_B)).rejects.toThrow('append of turn.desynced')
    await h.kernel.turns.sweep()
    expect(await eventsOf(matchId, 'turn.desynced')).toHaveLength(2)
    expect(await statusesOf(matchId)).toEqual([{ status: 'desynced' }])
  })

  it('announces the pause of a turn an older build stamped before pausing the match', async () => {
    const { host, guest } = await h.startedMatch()
    const matchId = host.match.id
    for (const token of [host.token, guest.token]) {
      await h.submit(await h.principalOf(token), 1, 1, true)
    }
    await reportTurn(host.token, 1, HASH_A)
    // The old order: the receipt was claimed first, and the process died before the pause.
    await h.storage.turns.transition(matchId, 1, ['sealed'], { status: 'desynced' })
    await h.storage.turns.claimDesyncAnnouncement(matchId, 1, h.clock.now())
    expect(h.storage.statusOf(matchId)).toBe('running')

    await reportTurn(guest.token, 1, HASH_B)
    expect(h.storage.statusOf(matchId)).toBe('desynced')
    expect(await statusesOf(matchId)).toEqual([{ status: 'desynced' }])
  })

  it('finishes a desync lift whose announcement failed: the clock restarts and it is told once', async () => {
    const { host, guest } = await h.startedMatch(60)
    const matchId = host.match.id
    for (const token of [host.token, guest.token]) {
      await h.submit(await h.principalOf(token), 1, 1, true)
    }
    await reportTurn(host.token, 1, HASH_A)
    await reportTurn(guest.token, 1, HASH_B)
    h.clock.advance(120_000)
    await h.kernel.snapshots.upload(await h.principalOf(host.token), {
      turn: 1,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })
    failNextAppendOf('match.statusChanged')
    await expect(reportTurn(guest.token, 1, HASH_A)).rejects.toThrow(
      'append of match.statusChanged failed',
    )
    expect(h.storage.statusOf(matchId)).toBe('running')
    expect(await statusesOf(matchId)).toEqual([{ status: 'desynced' }])

    // A later pass restarts the clock from its own moment, so the turn does not seal on arrival.
    h.clock.advance(30_000)
    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 1 })
    expect(await statusesOf(matchId)).toEqual([{ status: 'desynced' }, { status: 'running' }])
    expect((await h.storage.turns.get(matchId, 2))?.deadlineAt).toEqual(
      new Date(h.clock.now().getTime() + 60_000),
    )
    expect((await h.storage.turns.get(matchId, 1))?.settledAt).toBeInstanceOf(Date)

    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 0 })
    expect(await statusesOf(matchId)).toHaveLength(2)
  })

  it('reports no repair for a verdict whose follow-ups had all completed', async () => {
    const { host, guest } = await h.startedMatch()
    const matchId = host.match.id
    for (const token of [host.token, guest.token]) {
      await h.submit(await h.principalOf(token), 1, 1, true)
    }
    await reportTurn(host.token, 1, HASH_A)
    // Everything is announced; only the stamp is lost, as it is for a verdict still in flight.
    const real = h.storage.turns.markSettled
    let failed = false
    h.storage.turns.markSettled = async (...args) => {
      if (!failed) {
        failed = true
        throw new Error('markSettled failed')
      }
      return real(...args)
    }
    await expect(reportTurn(guest.token, 1, HASH_A)).rejects.toThrow('markSettled failed')
    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 0 })
    expect((await h.storage.turns.get(matchId, 1))?.settledAt).toBeInstanceOf(Date)
    expect(await eventsOf(matchId, 'turn.confirmed')).toHaveLength(1)
  })

  it('withdraws a question about a seat that came back while it was being asked', async () => {
    const { host, guest } = await h.startedMatch(60)
    const matchId = host.match.id
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    h.clock.advance(61_000)
    // The seat returns between the prompt being read and its request being logged.
    const real = h.storage.events.appendOnce
    h.storage.events.appendOnce = async (event, key) => {
      if (event.type === 'match.takeoverVoteRequested') {
        await h.storage.takeovers.closePrompt(matchId, guest.player.id)
      }
      return real(event, key)
    }
    await h.kernel.turns.sweep()
    const questions = (await logOf(matchId)).filter(
      (event) =>
        event.type === 'match.takeoverVoteRequested' ||
        event.type === 'match.takeoverVoteCancelled',
    )
    expect(questions.map((event) => event.type)).toEqual([
      'match.takeoverVoteRequested',
      'match.takeoverVoteCancelled',
    ])
  })

  it('lets an announcement whose insert failed be logged by its retry', async () => {
    const { host } = await h.startedMatch()
    const matchId = host.match.id
    const body = {
      matchId,
      type: 'turn.sealed' as const,
      payload: { turn: 9, orderSetHash: HASH_A },
      createdAt: h.clock.now().toISOString(),
    }
    const real = h.storage.events.append
    h.storage.events.append = async () => {
      h.storage.events.append = real
      throw new Error('append failed')
    }
    await expect(h.storage.events.appendOnce(body, 'turn.sealed:9')).rejects.toThrow(
      'append failed',
    )
    expect(await h.storage.events.appendOnce(body, 'turn.sealed:9')).not.toBeNull()
    expect(await h.storage.events.appendOnce(body, 'turn.sealed:9')).toBeNull()
  })

  /**
   * The report merged the host's summaries into the settings its principal was authenticated with,
   * then replaced the whole blob, putting back whatever that copy held over anything written since.
   */
  it('writes seat summaries into the stored settings, not the copy read at authentication', async () => {
    const { host, guest } = await h.startedMatch()
    const matchId = host.match.id
    for (const token of [host.token, guest.token]) {
      await h.submit(await h.principalOf(token), 1, 1, true)
    }
    const stale = await h.principalOf(host.token)
    // Something else lands in the stored blob after the host's request was authenticated.
    const rows = (h.storage as unknown as { matchRows: Map<string, Match> }).matchRows
    const row = rows.get(matchId)
    if (!row) throw new Error('the match disappeared')
    row.settings = {
      ...row.settings,
      gameSettings: { ...row.settings.gameSettings, landedMeanwhile: true },
    }

    const seatSummaries = [{ slot: 2, gangs: 3, sites: 4, sectors: 5 }]
    await h.kernel.turns.report(stale, 1, { stateHash: HASH_A, finished: false, seatSummaries })
    expect((await h.storage.matches.get(matchId))?.settings.gameSettings).toEqual({
      scenario: 3,
      landedMeanwhile: true,
      seatSummaries,
    })
  })
})
