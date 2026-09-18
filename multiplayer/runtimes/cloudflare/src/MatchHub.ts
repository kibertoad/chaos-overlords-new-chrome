import { isDomainError } from '@chaos-overlords/kernel'
import { DEFAULT_SERVER_CONFIG, LocalEventHub } from '@chaos-overlords/server'
import { createSqliteStorage, sqliteSchema } from '@chaos-overlords/storage/sqlite'
import type { DurableObjectState } from '@cloudflare/workers-types'
import { drizzle } from 'drizzle-orm/d1'
import type { Env } from './env'
import { buildKernel, HUB_PATHS, workerLogger } from './kernel'

interface PendingDeadline {
  matchId: string
  turn: number
}

const DEADLINE_KEY = 'deadline'

/**
 * One instance per match. It holds the open event streams of that match (so a notification from
 * any Worker isolate reaches every subscriber) and the alarm for the open turn's deadline. The
 * event log itself stays in D1; the object is a fan-out point, not a store.
 */
export class MatchHub {
  private readonly hub: LocalEventHub

  constructor(
    private readonly state: DurableObjectState,
    private readonly env: Env,
  ) {
    const storage = createSqliteStorage(drizzle(env.DB, { schema: sqliteSchema }))
    this.hub = new LocalEventHub(storage.events, DEFAULT_SERVER_CONFIG.sseHeartbeatMs)
  }

  async fetch(request: Request): Promise<Response> {
    const url = new URL(request.url)
    switch (url.pathname) {
      case HUB_PATHS.notify: {
        const { matchId } = (await request.json()) as { matchId: string }
        this.hub.wake(matchId)
        return new Response(null, { status: 204 })
      }
      case HUB_PATHS.schedule: {
        const body = (await request.json()) as PendingDeadline & { dueAt: string }
        await this.state.storage.put<PendingDeadline>(DEADLINE_KEY, {
          matchId: body.matchId,
          turn: body.turn,
        })
        await this.state.storage.setAlarm(new Date(body.dueAt).getTime())
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
      notifier: { notify: async (event) => this.hub.wake(event.matchId) },
      streams: this.hub,
      scheduler: {
        schedule: async (input) => {
          await this.state.storage.put<PendingDeadline>(DEADLINE_KEY, {
            matchId: input.matchId,
            turn: input.turn,
          })
          await this.state.storage.setAlarm(input.dueAt.getTime())
        },
      },
    })
    const sealed = await kernel.turns.trySeal(pending.matchId, pending.turn, 'deadline')
    workerLogger.info('deadline alarm handled', { ...pending, sealed })
    await this.forgetSpentDeadline(pending)
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
    const storage = createSqliteStorage(drizzle(this.env.DB, { schema: sqliteSchema }))
    const [match, turn] = await Promise.all([
      storage.matches.get(fired.matchId),
      storage.turns.get(fired.matchId, fired.turn),
    ])
    const stillDue =
      match?.status === 'running' && turn?.status === 'open' && turn.deadlineAt !== null
    if (stillDue) return
    await this.state.storage.delete(DEADLINE_KEY)
    await this.state.storage.deleteAlarm()
  }
}
