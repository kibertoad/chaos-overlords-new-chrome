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
 * How recently a match must have been touched for the ordinary sweep pass to visit it.
 *
 * A seal in flight is seconds old and a verdict interrupted after its compare-and-swap is too, so
 * this window is generous by orders of magnitude for both. What it excludes is the standing
 * population of a public server: matches deliberately kept `running` for ninety days after everyone
 * walked away, and matches parked in `desynced` because the host never uploaded a repair. Those
 * used to be re-judged, and joined against, on every single tick forever.
 */
const SWEEP_WINDOW_MS = 5 * 60_000

/**
 * Passes between two unbounded scans, which is what still finds work left behind while the process
 * was down. A fresh service does one immediately, so a restart never has to wait for it.
 */
const FULL_SCAN_EVERY = 20

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
  /** Passes since the last unbounded scan; see `sweep`. Starts due so the first pass is a full one. */
  private passesSinceFullScan = FULL_SCAN_EVERY

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
      const persisted = await this.deps.storage.turns.getOrderSummary(match.id, number, player.id)
      if (persisted?.ordersHash === ordersHash && persisted.ready === request.ready) {
        return { turn: number, orders: request.orders, ready: request.ready, ordersHash }
      }
      throw new ConflictError(`Turn ${match.currentTurn} is the open turn`, {
        reason: 'not_current_turn',
        currentTurn: match.currentTurn,
      })
    }
    await this.restorePendingPlayer(player.id, match.id)
    const previous = await this.deps.storage.turns.getOrderSummary(match.id, number, player.id)
    const accepted = await this.deps.storage.turns.submitOrders(match.id, number, player.id, {
      orders: request.orders,
      ordersHash,
      ready: request.ready,
      submittedAt: this.deps.clock.now(),
    })
    if (!accepted) {
      throw new ConflictError('The turn is no longer accepting orders', { reason: 'turn_not_open' })
    }
    if (previous?.ready !== request.ready) {
      // Only on a CHANGE of readiness. Re-publishing the same value used to be deliberate, to
      // repair an event lost between the row write and its publish, but a member may submit at the
      // member rate limit and the client sends a whole-document replacement per queued command: one
      // misbehaving client could grow a match's durable log by hundreds of thousands of rows a day
      // and wake every subscriber of the match for each. The window it covered is closed from the
      // other end instead — the match view carries `readyPlayerIds`, and every client reconciles
      // its readiness from the view when it reconnects.
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
      // A second guard behind `pauseAbandonedMatch`: a deadline that survived it, or one armed
      // before this rule existed, must not go on sealing empty turns in a match nobody is in.
      const roster = await this.deps.storage.players.listByMatch(matchId)
      if (activePlayers(roster).length === 0) {
        await this.clearOpenDeadline(matchId)
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
      // Conditional on the set not being frozen already, NOT on the status: the status stays
      // `sealed` for the whole window this branch runs in, and the sweep deliberately runs
      // `completeSeal` against a seal in flight. Two callers with a departure between their two
      // computations would otherwise each write a different digest and each announce it, and the
      // client that fetched the set after the overwrite failed its digest check.
      const frozen = await this.deps.storage.turns.freezeSeal(match.id, number, {
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

  /**
   * Whether the log carries an event of `type`.
   *
   * Only the interrupted-start repair asks this, and only about turn 1, so the log it pages is a
   * handful of rows. The verdict used to ask it about `turn.desynced` on every sweep of a paused
   * match, where the log is the whole match; that answer lives on the turn row now
   * (`claimDesyncAnnouncement`), and this must not grow another steady-state caller.
   */
  private async hasEvent(matchId: string, type: string): Promise<boolean> {
    const matches = (event: { type: string }): boolean => event.type === type
    let after = 0
    for (;;) {
      const page = await this.deps.storage.events.listAfter(matchId, after, 200)
      if (page.some(matches)) return true
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
        desyncedAt: null,
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
      try {
        await this.deps.scheduler.schedule({ matchId: match.id, turn: number, dueAt: deadlineAt })
      } catch (error) {
        // The deadline is durable on the turn row and `listExpiredOpen` is the safety net behind
        // every timer, so a scheduler that cannot be reached costs at most one sweep interval of
        // lateness. It used to cost the submitter a 500 on a seal that had already completed,
        // which left them retrying a request the server had in fact finished.
        this.deps.logger.warn('could not arm a turn deadline; the sweep will seal it', {
          matchId: match.id,
          turn: number,
          error: String(error),
        })
      }
    }
    return created
  }

  /**
   * The safety net behind the scheduler and behind any interrupted seal: seal every open turn whose
   * deadline has passed, then finish every seal that stopped before opening its successor. Runtimes
   * call this on an interval (Node) or a cron (Cloudflare).
   */
  async sweep(limit = 100): Promise<{ sealed: number; repaired: number }> {
    const now = this.deps.clock.now()
    // Every pass but one in `FULL_SCAN_EVERY` looks only at matches something happened to
    // recently; see `SWEEP_WINDOW_MS`. The counter starts at the period, so a process that has
    // just started scans everything once before settling into the cheap cadence.
    this.passesSinceFullScan += 1
    const full = this.passesSinceFullScan >= FULL_SCAN_EVERY
    if (full) this.passesSinceFullScan = 0
    const touchedSince = full ? null : new Date(now.getTime() - SWEEP_WINDOW_MS)
    const expired = await this.deps.storage.turns.listExpiredOpen(now, limit)
    let sealed = 0
    for (const { matchId, number } of expired) {
      // One match that throws must not abort the pass. `listExpiredOpen` is ordered oldest first,
      // so the same match would be at the head of the next pass too and no later expired turn or
      // stalled seal would ever be reached — and this is the safety net behind a lost timer.
      if (await this.guard(matchId, number, () => this.trySeal(matchId, number, 'deadline'))) {
        sealed += 1
      }
    }
    // A seal in flight looks stalled for the moment between its compare-and-swap and the next turn
    // opening, so this also runs against healthy matches. `completeSeal` changes nothing there, and
    // only a call that actually had work left to do is reported.
    let repaired = 0
    for (const { matchId, number } of await this.deps.storage.turns.listStalledSeals(
      limit,
      touchedSince,
    )) {
      const finished = await this.guard(matchId, number, async () => {
        const match = await this.deps.storage.matches.get(matchId)
        if (!match) return false
        return this.completeSeal(match, number)
      })
      if (finished) {
        this.deps.logger.warn('finished an interrupted seal', { matchId, turn: number })
        repaired += 1
      }
    }
    // A verdict cut short after its compare-and-swap leaves the match `desynced` with nothing
    // unsettled, which no other path revisits.
    for (const matchId of await this.deps.storage.matches.listDesynced(limit, touchedSince)) {
      await this.guard(matchId, 0, async () => {
        await this.reevaluate(matchId)
        return false
      })
    }
    return { sealed, repaired }
  }

  private async guard(
    matchId: string,
    turn: number,
    step: () => Promise<boolean>,
  ): Promise<boolean> {
    try {
      return await step()
    } catch (error) {
      this.deps.logger.warn('a match was skipped by the sweep', {
        matchId,
        turn,
        error: String(error),
      })
      return false
    }
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
    // Conditional on the turn still awaiting a verdict, because its status was read a statement
    // ago: a report that lost the race with a `settle` confirming the turn would otherwise land
    // behind the verdict it could not have changed, against the rule that the evidence a verdict
    // was taken on is immutable. The client treats the refusal as success, as it already does for
    // the check above.
    if (
      !(await this.deps.storage.turns.upsertReport({
        matchId: match.id,
        turn: number,
        playerId: player.id,
        stateHash: request.stateHash,
        finished: request.finished,
        reportedAt: this.deps.clock.now(),
      }))
    ) {
      throw new ConflictError('That turn is already confirmed', { reason: 'turn_confirmed' })
    }
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
   *
   * Opening a prompt always stops the open turn's clock, because the decision owns the screen on
   * every remaining client and time spent behind that modal is time nobody can plan in. The pause
   * belongs here rather than at the call sites: a prompt raised by a rejoin re-asking about the
   * seats it finds absent, or by a vote on a seat that went quiet before anyone was present to ask,
   * is exactly as blocking as one raised by a kick, and each of those used to leave the countdown
   * running. `openTurn` applies the same rule to a turn opening while a prompt is already up.
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
    await this.pauseForTakeoverVote(matchId)
    return true
  }

  /**
   * Pause the open turn while players decide what to do with an absent human seat.
   *
   * Only `openTakeoverPrompt` calls this, so every prompt pauses and none has to remember to. It is
   * a no-op when there is no open turn to stop — during a seal, where the turn the absence was
   * noticed on is already sealed and its successor opens paused instead — and when the clock is
   * stopped already, so a second absent seat neither double-publishes nor disturbs the first pause.
   */
  private async pauseForTakeoverVote(matchId: string): Promise<void> {
    await this.clearOpenDeadline(matchId)
  }

  /**
   * Stop the clock of a match whose last active player has just left.
   *
   * There is nobody left to ask about the empty seat, so no prompt is opened and nothing else would
   * pause the turn. `resumeAfterTakeoverVotes`, which `rejoin` calls, restarts it.
   */
  async pauseAbandonedMatch(matchId: string): Promise<void> {
    await this.clearOpenDeadline(matchId)
  }

  private async clearOpenDeadline(matchId: string): Promise<void> {
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
    // A prompt that opened between the check above and this write would have cleared a deadline
    // that did not exist yet, so the clock would be left running behind a modal nobody can dismiss.
    // Asking again after the write closes that interleaving: whichever of the two ran last, the
    // deadline ends up cleared.
    if (await this.deps.storage.takeovers.hasOpenPrompts(matchId)) {
      await this.clearOpenDeadline(matchId)
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
    const unsettled = await this.deps.storage.turns.listUnsettled(matchId)
    for (const turn of unsettled) {
      await this.settle(matchId, turn.number)
    }
    // A match left `desynced` with nothing unsettled is a verdict whose consequences were cut short
    // after its compare-and-swap. Nothing else reaches `resumeAfterDesync`, so without this the
    // match answers `match_desynced` to every submission for the rest of its life.
    if (match.status === 'desynced' && unsettled.length === 0) {
      await this.resumeAfterDesync(matchId, this.deps.clock.now())
    }
    await this.trySeal(matchId, match.currentTurn, 'ready')
  }

  /** Decide turn `number` from the reports on file. Idempotent and race-safe through CAS. */
  async settle(matchId: string, number: number): Promise<void> {
    const [match, turn] = await Promise.all([
      this.deps.storage.matches.get(matchId),
      this.deps.storage.turns.get(matchId, number),
    ])
    if (!match || !turn || turn.status === 'open') return
    if (turn.status === 'confirmed') {
      // The compare-and-swap already happened; what may not have is what follows it. A process that
      // died between the two left the match `desynced` for good with nothing unsettled, so
      // `submitOrders` answered `match_desynced` forever and nothing could reach `resumeAfterDesync`
      // again — it is private and `reevaluate` only visits sealed and desynced turns. The seal path
      // was built to survive exactly this; the verdict path was not.
      if (match.status === 'desynced') await this.resumeAfterDesync(matchId, this.deps.clock.now())
      return
    }
    const [players, reports, snapshot] = await Promise.all([
      this.deps.storage.players.listByMatch(matchId),
      this.deps.storage.turns.listReports(matchId, number),
      // The summary, not the row: the verdict reads one hash, and `get` carries up to a megabyte
      // of base64 with it, on every unsettled turn of every paused match on every sweep.
      this.deps.storage.snapshots.getSummary(matchId, number),
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
      // Each consequence is conditional on its own state rather than on winning the transition, so
      // a retry after an interrupted verdict finishes the job. Losing the CAS and returning here
      // left the match `running` with the turn already `desynced`: `turn.desynced` was never
      // announced, `SnapshotService.upload` refused the repair with `snapshot_not_required` because
      // the match was not desynced, and the turn stayed unsettled for good.
      const won =
        turn.status === 'desynced' ||
        (await this.deps.storage.turns.transition(matchId, number, ['sealed'], {
          status: 'desynced',
        }))
      if (!won) return
      this.deps.logger.warn('turn desynced', { matchId, turn: number })
      // The announcement is claimed on the turn row, the way the seal's digest is, so exactly one
      // caller publishes it. Asking the event log instead paged every event the match had ever
      // logged, on every sweep, for the whole of a pause that can last the retention window.
      if (await this.deps.storage.turns.claimDesyncAnnouncement(matchId, number, now)) {
        await this.publisher.publish(matchId, {
          type: 'turn.desynced',
          payload: {
            turn: number,
            reports: verdict.reports,
            candidateStateHashes: verdict.candidateStateHashes,
          },
        })
      }
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
    // Not behind a modal. Kicking the odd one out is the documented remedy for a desync, and the
    // kick opens an absence prompt for the kicked seat, so this path reached a paused turn every
    // time in a timed match and put a full deadline back on it while the vote nobody could dismiss
    // was still up. `resumeAfterTakeoverVotes` restarts the clock when the last prompt closes, the
    // same way `openTurn` leaves a turn paused when it opens behind one.
    if (await this.deps.storage.takeovers.hasOpenPrompts(matchId)) return
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
