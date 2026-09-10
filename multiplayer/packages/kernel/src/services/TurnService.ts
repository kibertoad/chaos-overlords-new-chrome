import type {
  OwnSubmissionView,
  SubmitOrdersRequest,
  TurnReportRequest,
} from '@chaos-overlords/contracts'
import { activePlayers, type Match, type Turn } from '../domain/entities'
import { ConflictError, ForbiddenError, NotFoundError } from '../domain/errors'
import { hashOrderDocument, hashOrderSet } from '../logic/crypto'
import { allActiveReady, evaluateConsensus, turnDeadline } from '../logic/turn-logic'
import type { Principal } from './AuthService'
import type { KernelDeps } from './deps'
import type { EventPublisher } from './EventPublisher'

export type SealTrigger = 'ready' | 'deadline'

/**
 * The simultaneous-turn barrier. Players submit orders privately; the turn seals when every
 * active player is ready or the deadline passes; the sealed set becomes readable and the next
 * turn opens at once. Clients resolve the turn locally and report the resulting state hash, and
 * the server confirms the turn on agreement or flags a desync.
 *
 * Every state change here is a compare-and-swap, so concurrent callers (the last player's ready
 * racing the timer, two players' reports landing together) produce exactly one seal and one
 * verdict.
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
    if (player.status !== 'active') {
      throw new ForbiddenError('You are no longer part of this match', { reason: 'not_active' })
    }
    if (number !== match.currentTurn) {
      throw new ConflictError(`Turn ${match.currentTurn} is the open turn`, {
        reason: 'not_current_turn',
        currentTurn: match.currentTurn,
      })
    }
    const previous = await this.deps.storage.turns.getOrders(match.id, number, player.id)
    const ordersHash = await hashOrderDocument(request.orders)
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
        this.deps.storage.turns.listOrders(matchId, number),
      ])
      if (!allActiveReady(players, orders)) return false
    }
    return this.seal(match, turn, trigger)
  }

  private async seal(match: Match, turn: Turn, trigger: SealTrigger): Promise<boolean> {
    const sealedAt = this.deps.clock.now()
    const won = await this.deps.storage.turns.transition(match.id, turn.number, ['open'], {
      status: 'sealed',
      sealedAt,
    })
    if (!won) return false
    // Orders are immutable from here: submitOrders is conditional on `open`.
    const [orders, players] = await Promise.all([
      this.deps.storage.turns.listOrders(match.id, turn.number),
      this.deps.storage.players.listByMatch(match.id),
    ])
    const slotOf = new Map(activePlayers(players).map((player) => [player.id, player.slot]))
    const entries = orders.flatMap((row) => {
      const slot = slotOf.get(row.playerId)
      return slot === undefined || row.ordersHash === null
        ? []
        : [{ slot, ordersHash: row.ordersHash }]
    })
    const orderSetHash = await hashOrderSet(entries)
    await this.deps.storage.turns.transition(match.id, turn.number, ['sealed'], {
      status: 'sealed',
      orderSetHash,
    })
    this.deps.logger.info('turn sealed', { matchId: match.id, turn: turn.number, trigger })
    await this.publisher.publish(match.id, {
      type: 'turn.sealed',
      payload: { turn: turn.number, orderSetHash },
    })
    await this.openTurn(match, turn.number + 1)
    return true
  }

  /** Opens turn `number` for every active player and arms its deadline. */
  async openTurn(match: Match, number: number): Promise<void> {
    const openedAt = this.deps.clock.now()
    const deadlineAt = turnDeadline(openedAt, match.settings.turnTimerSeconds)
    const players = activePlayers(await this.deps.storage.players.listByMatch(match.id))
    await this.deps.storage.turns.open(
      {
        matchId: match.id,
        number,
        status: 'open',
        openedAt,
        deadlineAt,
        sealedAt: null,
        orderSetHash: null,
      },
      players.map((player) => player.id),
    )
    await this.deps.storage.matches.transition(match.id, ['running'], {
      currentTurn: number,
      updatedAt: openedAt,
    })
    await this.publisher.publish(match.id, {
      type: 'turn.opened',
      payload: { turn: number, deadlineAt: deadlineAt?.toISOString() ?? null },
    })
    if (deadlineAt) {
      await this.deps.scheduler.schedule({ matchId: match.id, turn: number, dueAt: deadlineAt })
    }
  }

  /** The safety net behind the scheduler: seal every open turn whose deadline has passed. */
  async sweepExpiredTurns(limit = 100): Promise<number> {
    const expired = await this.deps.storage.turns.listExpiredOpen(this.deps.clock.now(), limit)
    let sealed = 0
    for (const { matchId, number } of expired) {
      if (await this.trySeal(matchId, number, 'deadline')) sealed += 1
    }
    return sealed
  }

  async report(principal: Principal, number: number, request: TurnReportRequest): Promise<void> {
    const { match, player } = principal
    if (match.status !== 'running' && match.status !== 'desynced') {
      throw new ConflictError('The match is not in progress', { reason: 'match_not_running' })
    }
    const turn = await this.deps.storage.turns.get(match.id, number)
    if (!turn) throw new NotFoundError('No such turn', { reason: 'unknown_turn' })
    if (turn.status === 'open') {
      throw new ConflictError('The turn has not been sealed yet', { reason: 'turn_open' })
    }
    await this.deps.storage.turns.upsertReport({
      matchId: match.id,
      turn: number,
      playerId: player.id,
      stateHash: request.stateHash,
      finished: request.finished,
      reportedAt: this.deps.clock.now(),
    })
    await this.settle(match.id, number)
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
        },
      )
      if (!won) return
      await this.publisher.publish(matchId, {
        type: 'turn.confirmed',
        payload: { turn: number, stateHash: verdict.stateHash },
      })
      if (
        match.status === 'desynced' &&
        (await this.deps.storage.turns.listUnsettled(matchId)).length === 0 &&
        (await this.deps.storage.matches.transition(matchId, ['desynced'], {
          status: 'running',
          updatedAt: now,
        }))
      ) {
        await this.publisher.publish(matchId, {
          type: 'match.statusChanged',
          payload: { status: 'running' },
        })
      }
      if (
        verdict.finished &&
        (await this.deps.storage.matches.transition(matchId, ['running', 'desynced'], {
          status: 'finished',
          updatedAt: now,
        }))
      ) {
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
        payload: { turn: number, reports: verdict.reports },
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
