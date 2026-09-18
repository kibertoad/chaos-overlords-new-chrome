import {
  foreignOps,
  type OwnSubmissionView,
  type SubmitOrdersRequest,
  type TurnReportRequest,
} from '@chaos-overlords/contracts'
import { activePlayers, humanParticipants, type Match, type SealedSlot } from '../domain/entities'
import { ConflictError, ForbiddenError, NotFoundError, ValidationError } from '../domain/errors'
import { hashOrderDocument, hashOrderSet } from '../logic/crypto'
import { allActiveReady, assignSlots, evaluateConsensus, turnDeadline } from '../logic/turn-logic'
import type { Principal } from './AuthService'
import type { KernelDeps } from './deps'
import type { EventPublisher } from './EventPublisher'
import { toPlayerView } from './MatchQueryService'
import { mergeSeatSummaries } from './SnapshotService'

export type SealTrigger = 'ready' | 'deadline'

/** Turns are numbered from 1; 0 is the lobby's `currentTurn`, before any turn exists. */
export const FIRST_TURN = 1

/**
 * The simultaneous-turn barrier. Players submit orders privately; the turn seals when every
 * active player is ready or the deadline passes; the sealed set becomes readable and the next
 * turn opens at once. Clients resolve the turn locally and report the resulting state hash, and
 * the server confirms the turn on agreement or flags a desync.
 *
 * Every state change here is a compare-and-swap, so concurrent callers (the last player's ready
 * racing the timer, two players' reports landing together) produce exactly one seal and one
 * verdict. Sealing is a CAS followed by `completeSeal`, which is idempotent from any point: that is
 * what lets `sweep` finish a seal whose process died halfway through instead of stranding the match.
 */
export class TurnService {
  constructor(
    private readonly deps: KernelDeps,
    private readonly publisher: EventPublisher,
  ) {}

  async submitOrders(
    principal: Principal,
    number: number,
    request: SubmitOrdersRequest,
  ): Promise<OwnSubmissionView> {
    const { match, player } = principal
    requireRunning(match)
    if (player.status !== 'active' && player.status !== 'takeoverPending') {
      throw new ForbiddenError('You are no longer part of this match', { reason: 'not_active' })
    }
    assertOwnOps(request, player.slot)
    const ordersHash = await hashOrderDocument(request.orders)
    if (number !== match.currentTurn) {
      // The document may have been committed and sealed while its success response was lost. The
      // endpoint is a replacement, not an append, so an identical retry is an acknowledgement of
      // that durable row even after currentTurn advances. A different document remains a stale
      // write and is refused.
      const persisted = await this.deps.storage.turns.getOrders(match.id, number, player.id)
      if (persisted?.ordersHash === ordersHash && persisted.ready === request.ready) {
        return { turn: number, orders: request.orders, ready: request.ready, ordersHash }
      }
      throw new ConflictError(`Turn ${match.currentTurn} is the open turn`, {
        reason: 'not_current_turn',
        currentTurn: match.currentTurn,
      })
    }
    await this.restorePendingPlayer(player.id, match.id)
    const previous = await this.deps.storage.turns.getOrders(match.id, number, player.id)
    const accepted = await this.deps.storage.turns.submitOrders(match.id, number, player.id, {
      orders: request.orders,
      ordersHash,
      ready: request.ready,
      submittedAt: this.deps.clock.now(),
    })
    if (!accepted) {
      throw new ConflictError('The turn is no longer accepting orders', { reason: 'turn_not_open' })
    }
    if (previous?.ready !== request.ready || (request.ready && previous?.ready === true)) {
      // Re-publishing the same ready value is deliberate. If a process stopped after storing the
      // row but before publishing its first event, the idempotent HTTP retry repairs the event too.
      // Consumers treat readiness as a set, so the duplicate is harmless when the first did land.
      await this.publisher.publish(match.id, {
        type: 'turn.readiness',
        payload: { turn: number, playerId: player.id, ready: request.ready },
      })
    }
    if (request.ready) await this.trySeal(match.id, number, 'ready')
    return { turn: number, orders: request.orders, ready: request.ready, ordersHash }
  }

  /** Seal `number` if its trigger condition holds. Returns whether THIS call sealed it. */
  async trySeal(matchId: string, number: number, trigger: SealTrigger): Promise<boolean> {
    const match = await this.deps.storage.matches.get(matchId)
    if (match?.status !== 'running') return false
    const turn = await this.deps.storage.turns.get(matchId, number)
    if (turn?.status !== 'open') return false
    if (trigger === 'deadline') {
      if (turn.deadlineAt === null || turn.deadlineAt.getTime() > this.deps.clock.now().getTime()) {
        return false
      }
    } else {
      const [players, orders] = await Promise.all([
        this.deps.storage.players.listByMatch(matchId),
        this.deps.storage.turns.listOrderSummaries(matchId, number),
      ])
      if (!allActiveReady(players, orders)) return false
    }
    const won = await this.deps.storage.turns.transition(matchId, number, ['open'], {
      status: 'sealed',
      sealedAt: this.deps.clock.now(),
    })
    if (!won) return false
    this.deps.logger.info('turn sealed', { matchId, turn: number, trigger })
    await this.completeSeal(match, number)
    return true
  }

  /**
   * Everything that follows the seal's compare-and-swap: freeze the participant set and its digest,
   * announce it, and open the next turn. Each step is conditional on its own predecessor, so a call
   * on a turn that is already complete changes nothing and a call on one that stopped halfway
   * finishes it. Orders are immutable from the CAS onwards, so the digest it derives is stable.
   */
  private async completeSeal(match: Match, number: number): Promise<boolean> {
    const turn = await this.deps.storage.turns.get(match.id, number)
    if (turn?.status === 'open') return false
    if (!turn) {
      // No row at all: the match is live and pointed at a turn that was never created, which is
      // what `start` leaves behind when it dies between the status change and opening turn 1.
      // There is no seal to finish, only the missing turn to open — `currentTurn` names it, except
      // at 0, which is the lobby's value and means turn 1 was never reached.
      if (number <= FIRST_TURN) await this.repairInterruptedStart(match)
      return this.openTurn(match, Math.max(number, FIRST_TURN))
    }
    let advanced = false
    if (turn.orderSetHash === null || turn.sealedSlots === null) {
      const [orders, players] = await Promise.all([
        this.deps.storage.turns.listOrderSummaries(match.id, number),
        this.deps.storage.players.listByMatch(match.id),
      ])
      const submitted = new Set(
        orders.filter((row) => row.ordersHash !== null).map((row) => row.playerId),
      )
      for (const player of activePlayers(players)) {
        // A seat with no row at all (a late joiner seated after this turn opened) was never
        // asked for orders, so it is not absent; only a row that stayed empty is.
        if (submitted.has(player.id) || !orders.some((row) => row.playerId === player.id)) continue
        if (
          await this.deps.storage.players.transitionStatus(player.id, ['active'], 'takeoverPending')
        ) {
          await this.openTakeoverPrompt(match.id, player.id, number)
        }
      }
      const currentPlayers = await this.deps.storage.players.listByMatch(match.id)
      const slotOf = new Map(
        humanParticipants(currentPlayers).map((player) => [player.id, player.slot]),
      )
      const sealedSlots: SealedSlot[] = []
      const entries: Array<{ slot: number; ordersHash: string }> = []
      for (const row of orders) {
        const slot = slotOf.get(row.playerId)
        if (slot === undefined || row.ordersHash === null) continue
        sealedSlots.push({ playerId: row.playerId, slot })
        entries.push({ slot, ordersHash: row.ordersHash })
      }
      const orderSetHash = await hashOrderSet(entries)
      const frozen = await this.deps.storage.turns.transition(match.id, number, [turn.status], {
        status: turn.status,
        orderSetHash,
        sealedSlots,
      })
      if (frozen) {
        advanced = true
        await this.publisher.publish(match.id, {
          type: 'turn.sealed',
          payload: { turn: number, orderSetHash },
        })
      }
    }
    return (await this.openTurn(match, number + 1)) || advanced
  }

  /**
   * Finish a `start` that died after running the match but before seating it and announcing it.
   * Seating is what the roster's slots are read from on every client, and `match.started` is what
   * makes them bootstrap at all; without both the sweep would open turn 1 for a match nobody can
   * play. Both steps are idempotent: seats are assigned only while one is still unassigned, and
   * the announcement only when the log does not already carry it.
   */
  private async repairInterruptedStart(match: Match): Promise<void> {
    if (match.seed === null) return
    const roster = await this.deps.storage.players.listByMatch(match.id)
    if (activePlayers(roster).some((player) => player.slot < 0)) {
      await this.deps.storage.players.assignSlots(assignSlots(roster, match.hostPlayerId))
    }
    if (await this.hasEvent(match.id, 'match.started')) return
    const seated = await this.deps.storage.players.listByMatch(match.id)
    this.deps.logger.warn('finished an interrupted start', { matchId: match.id })
    await this.publisher.publish(match.id, {
      type: 'match.started',
      payload: {
        seed: match.seed,
        players: activePlayers(seated).map((player) => toPlayerView(player, match.hostPlayerId)),
      },
    })
  }

  /** Whether the log carries an event of `type`. Only read on repair paths, where the log is short. */
  private async hasEvent(matchId: string, type: string): Promise<boolean> {
    let after = 0
    for (;;) {
      const page = await this.deps.storage.events.listAfter(matchId, after, 200)
      if (page.some((event) => event.type === type)) return true
      if (page.length < 200) return false
      after = page[page.length - 1]?.seq ?? after
    }
  }

  /**
   * Opens turn `number` for every active player and arms its deadline. Idempotent: the insert is
   * refused if the turn already exists, and only the caller that created it announces it. Returns
   * whether this call created the turn.
   */
  async openTurn(match: Match, number: number): Promise<boolean> {
    const openedAt = this.deps.clock.now()
    const roster = await this.deps.storage.players.listByMatch(match.id)
    const players = humanParticipants(roster)
    // An absence decision owns the screen on every remaining client. Starting the successor's
    // clock behind that modal would spend planning time nobody can use, so an open vote opens the
    // turn paused. The clock is restarted when the last absent seat returns or becomes computer
    // controlled.
    const hasAbsenceVote = await this.deps.storage.takeovers.hasOpenPrompts(match.id)
    const deadlineAt = hasAbsenceVote
      ? null
      : turnDeadline(openedAt, match.settings.turnTimerSeconds)
    const created = await this.deps.storage.turns.open(
      {
        matchId: match.id,
        number,
        status: 'open',
        openedAt,
        deadlineAt,
        sealedAt: null,
        orderSetHash: null,
        sealedSlots: null,
        stateHash: null,
      },
      players.map((player) => player.id),
    )
    // A desynced match counts: a repaired seal must still point `currentTurn` at the turn that is
    // actually open, even though nobody may submit to it until the pause lifts.
    await this.deps.storage.matches.transition(match.id, ['running', 'desynced'], {
      currentTurn: number,
      updatedAt: openedAt,
    })
    if (created) {
      await this.publisher.publish(match.id, {
        type: 'turn.opened',
        payload: { turn: number, deadlineAt: deadlineAt?.toISOString() ?? null },
      })
    }
    if (deadlineAt) {
      await this.deps.scheduler.schedule({ matchId: match.id, turn: number, dueAt: deadlineAt })
    }
    return created
  }

  /**
   * The safety net behind the scheduler and behind any interrupted seal: seal every open turn whose
   * deadline has passed, then finish every seal that stopped before opening its successor. Runtimes
   * call this on an interval (Node) or a cron (Cloudflare).
   */
  async sweep(limit = 100): Promise<{ sealed: number; repaired: number }> {
    const expired = await this.deps.storage.turns.listExpiredOpen(this.deps.clock.now(), limit)
    let sealed = 0
    for (const { matchId, number } of expired) {
      if (await this.trySeal(matchId, number, 'deadline')) sealed += 1
    }
    // A seal in flight looks stalled for the moment between its compare-and-swap and the next turn
    // opening, so this also runs against healthy matches. `completeSeal` changes nothing there, and
    // only a call that actually had work left to do is reported.
    let repaired = 0
    for (const { matchId, number } of await this.deps.storage.turns.listStalledSeals(limit)) {
      const match = await this.deps.storage.matches.get(matchId)
      if (!match) continue
      if (await this.completeSeal(match, number)) {
        this.deps.logger.warn('finished an interrupted seal', { matchId, turn: number })
        repaired += 1
      }
    }
    return { sealed, repaired }
  }

  async report(principal: Principal, number: number, request: TurnReportRequest): Promise<void> {
    const { match, player } = principal
    if (match.status !== 'running' && match.status !== 'desynced') {
      throw new ConflictError('The match is not in progress', { reason: 'match_not_running' })
    }
    if (player.status !== 'active' && player.status !== 'takeoverPending') {
      throw new ForbiddenError('You are no longer part of this match', { reason: 'not_active' })
    }
    const turn = await this.deps.storage.turns.get(match.id, number)
    if (!turn) throw new NotFoundError('No such turn', { reason: 'unknown_turn' })
    if (turn.status === 'open') {
      throw new ConflictError('The turn has not been sealed yet', { reason: 'turn_open' })
    }
    // A confirmed turn is settled consensus and its reports are the record of how it settled.
    // `settle` ignores a confirmed turn, so accepting a late report could not change the verdict;
    // refusing it keeps the evidence that produced that verdict immutable.
    if (turn.status === 'confirmed') {
      throw new ConflictError('That turn is already confirmed', { reason: 'turn_confirmed' })
    }
    await this.restorePendingPlayer(player.id, match.id)
    await this.deps.storage.turns.upsertReport({
      matchId: match.id,
      turn: number,
      playerId: player.id,
      stateHash: request.stateHash,
      finished: request.finished,
      reportedAt: this.deps.clock.now(),
    })
    // The order set is the turn increment the server retains. Publishing these few derived counters
    // with the host's report keeps late-join selection current without uploading another full save.
    if (player.id === match.hostPlayerId && request.seatSummaries !== undefined) {
      try {
        await this.deps.storage.matches.updateRuntimeGameSettings(
          match.id,
          mergeSeatSummaries(match.settings.gameSettings, request.seatSummaries),
          this.deps.clock.now(),
        )
      } catch (error) {
        // Late-join hints are optional metadata. A full settings blob or a transient metadata write
        // must not discard the authoritative hash report and strand every player at the barrier.
        this.deps.logger.warn('could not publish seat summaries', {
          matchId: match.id,
          turn: number,
          error: String(error),
        })
      }
    }
    await this.settle(match.id, number)
  }

  /** Authenticated turn activity wins the race with an AI vote and restores the human seat. */
  private async restorePendingPlayer(playerId: string, matchId: string): Promise<void> {
    if (await this.deps.storage.players.transitionStatus(playerId, ['takeoverPending'], 'active')) {
      await this.deps.storage.takeovers.closePrompt(matchId, playerId)
      await this.publisher.publish(matchId, {
        type: 'match.takeoverVoteCancelled',
        payload: { playerId },
      })
      await this.resumeAfterTakeoverVotes(matchId)
    }
  }

  /**
   * Ask the present players what to do with an absent human seat. The prompt is durable state
   * (see `TakeoverRepository`) and announced exactly once: a second request for a seat whose
   * prompt is already open changes nothing, so a repeated seal step or a rejoin that re-asks about
   * every absent seat cannot double the modal on anyone's screen.
   */
  async openTakeoverPrompt(matchId: string, playerId: string, turn: number): Promise<boolean> {
    const opened = await this.deps.storage.takeovers.openPrompt(
      matchId,
      playerId,
      turn,
      this.deps.clock.now(),
    )
    if (!opened) return false
    await this.publisher.publish(matchId, {
      type: 'match.takeoverVoteRequested',
      payload: { playerId, turn },
    })
    return true
  }

  /** Pause the open turn while players decide what to do with an absent human seat. */
  async pauseForTakeoverVote(matchId: string): Promise<void> {
    const match = await this.deps.storage.matches.get(matchId)
    if (!match || (match.status !== 'running' && match.status !== 'desynced')) return
    const turn = await this.deps.storage.turns.get(matchId, match.currentTurn)
    if (turn?.status !== 'open' || turn.deadlineAt === null) return
    if (!(await this.deps.storage.turns.rescheduleDeadline(matchId, match.currentTurn, null)))
      return
    await this.publisher.publish(matchId, {
      type: 'turn.deadlineExtended',
      payload: { turn: match.currentTurn, deadlineAt: null },
    })
  }

  /** Restart a full turn clock after the final outstanding absence vote closes. */
  async resumeAfterTakeoverVotes(matchId: string): Promise<void> {
    const match = await this.deps.storage.matches.get(matchId)
    if (!match || match.status !== 'running' || match.settings.turnTimerSeconds === 0) return
    if (await this.deps.storage.takeovers.hasOpenPrompts(matchId)) return
    const turn = await this.deps.storage.turns.get(matchId, match.currentTurn)
    if (turn?.status !== 'open' || turn.deadlineAt !== null) return
    const deadlineAt = turnDeadline(this.deps.clock.now(), match.settings.turnTimerSeconds)
    if (
      deadlineAt === null ||
      !(await this.deps.storage.turns.rescheduleDeadline(matchId, match.currentTurn, deadlineAt))
    ) {
      return
    }
    await this.publisher.publish(matchId, {
      type: 'turn.deadlineExtended',
      payload: { turn: match.currentTurn, deadlineAt: deadlineAt.toISOString() },
    })
    await this.deps.scheduler.schedule({ matchId, turn: match.currentTurn, dueAt: deadlineAt })
  }

  /** Re-run the verdict of every unconfirmed turn and the auto-seal of the open one. */
  async reevaluate(matchId: string): Promise<void> {
    const match = await this.deps.storage.matches.get(matchId)
    if (!match || (match.status !== 'running' && match.status !== 'desynced')) return
    for (const turn of await this.deps.storage.turns.listUnsettled(matchId)) {
      await this.settle(matchId, turn.number)
    }
    await this.trySeal(matchId, match.currentTurn, 'ready')
  }

  /** Decide turn `number` from the reports on file. Idempotent and race-safe through CAS. */
  async settle(matchId: string, number: number): Promise<void> {
    const [match, turn] = await Promise.all([
      this.deps.storage.matches.get(matchId),
      this.deps.storage.turns.get(matchId, number),
    ])
    if (!match || !turn || turn.status === 'open' || turn.status === 'confirmed') return
    const [players, reports, snapshot] = await Promise.all([
      this.deps.storage.players.listByMatch(matchId),
      this.deps.storage.turns.listReports(matchId, number),
      this.deps.storage.snapshots.get(matchId, number),
    ])
    const verdict = evaluateConsensus(players, reports, snapshot?.stateHash ?? null)
    const now = this.deps.clock.now()
    if (verdict.kind === 'confirmed') {
      const won = await this.deps.storage.turns.transition(
        matchId,
        number,
        ['sealed', 'desynced'],
        {
          status: 'confirmed',
          stateHash: verdict.stateHash,
        },
      )
      if (!won) return
      await this.publisher.publish(matchId, {
        type: 'turn.confirmed',
        payload: { turn: number, stateHash: verdict.stateHash },
      })
      if (match.status === 'desynced') await this.resumeAfterDesync(matchId, now)
      if (
        verdict.finished &&
        (await this.deps.storage.matches.transition(matchId, ['running', 'desynced'], {
          status: 'finished',
          updatedAt: now,
        }))
      ) {
        // The seal opened the successor before anyone had reported, so a finished match is left
        // holding an open turn with a live deadline. Nothing can be submitted to it (every write
        // requires a running match) but it would sit in `listExpiredOpen` forever, and a client
        // reading the match view would see a turn still counting down after the game ended.
        await this.deps.storage.turns.rescheduleDeadline(matchId, turn.number + 1, null)
        await this.publisher.publish(matchId, {
          type: 'match.statusChanged',
          payload: { status: 'finished' },
        })
      }
      return
    }
    if (verdict.kind === 'desynced') {
      const won = await this.deps.storage.turns.transition(matchId, number, ['sealed'], {
        status: 'desynced',
      })
      if (!won) return
      this.deps.logger.warn('turn desynced', { matchId, turn: number })
      await this.publisher.publish(matchId, {
        type: 'turn.desynced',
        payload: {
          turn: number,
          reports: verdict.reports,
          candidateStateHashes: verdict.candidateStateHashes,
        },
      })
      if (
        await this.deps.storage.matches.transition(matchId, ['running'], {
          status: 'desynced',
          updatedAt: now,
        })
      ) {
        await this.publisher.publish(matchId, {
          type: 'match.statusChanged',
          payload: { status: 'desynced' },
        })
      }
    }
  }

  /**
   * Lift a desync pause once nothing is unsettled. Orders were refused for the whole pause while
   * the open turn's deadline kept running, so the turn would otherwise seal empty the moment the
   * match resumes: its clock restarts here, and clients are told the new deadline.
   */
  private async resumeAfterDesync(matchId: string, now: Date): Promise<void> {
    if ((await this.deps.storage.turns.listUnsettled(matchId)).length > 0) return
    const match = await this.deps.storage.matches.get(matchId)
    if (!match) return
    if (
      !(await this.deps.storage.matches.transition(matchId, ['desynced'], {
        status: 'running',
        updatedAt: now,
      }))
    ) {
      return
    }
    await this.publisher.publish(matchId, {
      type: 'match.statusChanged',
      payload: { status: 'running' },
    })
    const deadlineAt = turnDeadline(now, match.settings.turnTimerSeconds)
    if (deadlineAt === null) return // A match without a turn timer has no clock to restart.
    if (
      !(await this.deps.storage.turns.rescheduleDeadline(matchId, match.currentTurn, deadlineAt))
    ) {
      return
    }
    await this.publisher.publish(matchId, {
      type: 'turn.deadlineExtended',
      payload: { turn: match.currentTurn, deadlineAt: deadlineAt.toISOString() },
    })
    await this.deps.scheduler.schedule({ matchId, turn: match.currentTurn, dueAt: deadlineAt })
  }
}

/**
 * Refuse a document whose ops act for a slot other than the submitter's.
 *
 * The sealed set attributes every op to the slot it was submitted from, and that attribution is
 * the one clients apply, so an op naming a different player can only be a client bug or an attempt
 * to act as somebody else. Catching it here keeps the two attributions from ever disagreeing, and
 * it is the one piece of order semantics the server can judge without knowing the rules.
 */
function assertOwnOps(request: SubmitOrdersRequest, slot: number): void {
  const foreign = foreignOps(request.orders, slot)
  if (foreign.length === 0) return
  throw new ValidationError('Orders may only act for your own slot', {
    reason: 'foreign_slot_ops',
    slot,
    ops: foreign.slice(0, 8).map((op) => ({ op: op.op, player: op.player })),
  })
}

function requireRunning(match: Match): void {
  if (match.status === 'desynced') {
    throw new ConflictError('The match is paused until the host uploads a snapshot', {
      reason: 'match_desynced',
    })
  }
  if (match.status !== 'running') {
    throw new ConflictError('The match is not in progress', { reason: 'match_not_running' })
  }
}
