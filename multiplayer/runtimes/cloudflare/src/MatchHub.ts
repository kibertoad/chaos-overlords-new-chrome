import {
  EARLY_DEADLINE_RETRY_MS,
  isDomainError,
  type MultiplayerStorage,
  type PersistedEvent,
} from '@chaos-overlords/kernel'
import {
  DEFAULT_EVENT_HUB_LIMITS,
  DEFAULT_SERVER_CONFIG,
  isActiveMember,
  LocalEventHub,
  logStreamClosed,
} from '@chaos-overlords/server'
import { createSqliteStorage, sqliteSchema } from '@chaos-overlords/storage/sqlite'
import type { DurableObjectState } from '@cloudflare/workers-types'
import { drizzle } from 'drizzle-orm/d1'
import type { Env } from './env'
import { buildKernel, HUB_PATHS, workerLogger } from './kernel'

interface PendingDeadline {
  matchId: string
  turn: number
  /** When the alarm was set for. Absent on keys written before it was recorded. */
  dueAtMs?: number
}

const DEADLINE_KEY = 'deadline'

/**
 * Where a deadline the kernel re-arms from inside an alarm may be set for.
 *
 * An alarm that the kernel finds early is re-armed for the same turn, floored against the object's
 * `Date.now()`. That floor means nothing to the alarm scheduler when the object's clock lags it:
 * the scheduler has already passed the time the alarm fired at, so a deadline at or before that
 * time fires again at once, and again, building a kernel and reading D1 each time until the
 * object's clock catches up. Having fired proves the scheduler's clock reached `fired.dueAtMs`, so
 * the retry goes at least the kernel's retry interval past that, which bounds the rate on the
 * scheduler's own clock. Any other turn's deadline (the successor a seal opened) is set as asked.
 */
function retryFloor(
  fired: PendingDeadline,
  next: { matchId: string; turn: number },
  dueAtMs: number,
): number {
  const sameTurn = next.matchId === fired.matchId && next.turn === fired.turn
  if (!sameTurn || fired.dueAtMs === undefined) return dueAtMs
  return Math.max(dueAtMs, fired.dueAtMs + EARLY_DEADLINE_RETRY_MS)
}

/** Whether a pending deadline is still exactly the one read earlier, due time included. */
function isSameDeadline(pending: PendingDeadline | undefined, seen: PendingDeadline): boolean {
  return (
    pending?.matchId === seen.matchId &&
    pending.turn === seen.turn &&
    pending.dueAtMs === seen.dueAtMs
  )
}

/**
 * One instance per match. It holds the open event streams of that match (so a notification from
 * any Worker isolate reaches every subscriber) and the alarm for the open turn's deadline. The
 * event log itself stays in D1; the object is a fan-out point, not a store.
 */
export class MatchHub {
  private readonly hub: LocalEventHub
  private readonly repositories: MultiplayerStorage

  constructor(
    private readonly state: DurableObjectState,
    private readonly env: Env,
  ) {
    this.repositories = createSqliteStorage(drizzle(env.DB, { schema: sqliteSchema }))
    this.hub = new LocalEventHub(
      this.repositories.events,
      DEFAULT_SERVER_CONFIG.sseHeartbeatMs,
      DEFAULT_EVENT_HUB_LIMITS,
      {
        // The request signal here is the Worker's fetch into this object, not the client's own,
        // so a run of these is where a signal that only looks aborted would show up.
        abandoned: (matchId, playerId) =>
          workerLogger.info('answered an already abandoned event stream', { matchId, playerId }),
        revalidate: (matchId, playerId) =>
          isActiveMember(this.repositories.players, matchId, playerId),
        closed: logStreamClosed(workerLogger),
      },
    )
  }

  async fetch(request: Request): Promise<Response> {
    const url = new URL(request.url)
    switch (url.pathname) {
      case HUB_PATHS.notify: {
        const event = (await request.json()) as PersistedEvent & { matchId?: string }
        // The event body when the caller sent one; a bare match id is still accepted, so an isolate
        // running an older build cannot silence this object's streams.
        if (typeof event.seq === 'number') await this.hub.notify(event)
        else if (event.matchId) this.hub.wake(event.matchId)
        return new Response(null, { status: 204 })
      }
      case HUB_PATHS.schedule: {
        const body = (await request.json()) as PendingDeadline & { dueAt: string }
        await this.arm(body.matchId, body.turn, new Date(body.dueAt).getTime())
        return new Response(null, { status: 204 })
      }
      case HUB_PATHS.subscribe: {
        const matchId = url.searchParams.get('matchId') ?? ''
        const playerId = url.searchParams.get('playerId') ?? ''
        const afterSeq = Number(url.searchParams.get('after') ?? '0')
        try {
          return await this.hub.open({ matchId, playerId, afterSeq, signal: request.signal })
        } catch (error) {
          // The stream caps refuse here, inside the object, where the app's error handler cannot
          // reach. A bare status crosses back instead and the Worker rethrows it as the domain
          // error, so the envelope still has exactly one producer.
          if (!isDomainError(error)) throw error
          return new Response(null, {
            status: 429,
            headers: { 'X-Stream-Refusal': String(error.details?.scope ?? 'match') },
          })
        }
      }
      case HUB_PATHS.disconnect: {
        const body = (await request.json()) as { matchId: string; playerId: string }
        await this.hub.close(body)
        return new Response(null, { status: 204 })
      }
      default:
        return new Response('not found', { status: 404 })
    }
  }

  async alarm(): Promise<void> {
    const pending = await this.state.storage.get<PendingDeadline>(DEADLINE_KEY)
    if (!pending) return
    const kernel = buildKernel(this.env, {
      // Already inside the hub: wake local streams directly instead of calling ourselves.
      notifier: { notify: async (event) => this.hub.notify(event) },
      streams: this.hub,
      scheduler: {
        schedule: async (input) =>
          this.arm(input.matchId, input.turn, retryFloor(pending, input, input.dueAt.getTime())),
      },
    })
    const sealed = await kernel.turns.trySeal(pending.matchId, pending.turn, 'deadline')
    workerLogger.info('deadline alarm handled', { ...pending, sealed })
    await this.forgetSpentDeadline(pending)
  }

  /** Record which turn the alarm is for, then set the alarm. One deadline is pending at a time. */
  private async arm(matchId: string, turn: number, dueAtMs: number): Promise<void> {
    await this.state.storage.put<PendingDeadline>(DEADLINE_KEY, { matchId, turn, dueAtMs })
    await this.state.storage.setAlarm(dueAtMs)
  }

  /**
   * Drop the pending deadline once nothing is left to fire for. A seal on readiness, a finished
   * match and a match retention deleted all leave the alarm armed for a turn that is no longer
   * open; each such alarm builds a kernel and reads the match for nothing, and the key kept every
   * timed match's object in storage forever. A seal that just ran has already replaced the key
   * with the next turn's deadline, which is why the key is re-read rather than deleted outright.
   */
  private async forgetSpentDeadline(fired: PendingDeadline): Promise<void> {
    const current = await this.state.storage.get<PendingDeadline>(DEADLINE_KEY)
    if (!current || current.matchId !== fired.matchId || current.turn !== fired.turn) return
    const [match, turn] = await Promise.all([
      this.repositories.matches.get(fired.matchId),
      this.repositories.turns.get(fired.matchId, fired.turn),
    ])
    const stillDue =
      match?.status === 'running' && turn?.status === 'open' && turn.deadlineAt !== null
    if (stillDue) return
    // Asked again after the D1 reads, which open the object's input gate: a ready seal in another
    // isolate can arm its successor's deadline in between. Deleting on the first answer threw that
    // deadline away, and the new turn then counted down to zero on every screen and sat there until
    // a player ended it or the cron swept it. Nothing awaits between this read and the delete
    // except the object's own storage, which keeps the gate shut.
    if (!isSameDeadline(await this.state.storage.get<PendingDeadline>(DEADLINE_KEY), current)) {
      return
    }
    await this.state.storage.delete(DEADLINE_KEY)
    await this.state.storage.deleteAlarm()
  }
}
