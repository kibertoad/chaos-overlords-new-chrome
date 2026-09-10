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
        const afterSeq = Number(url.searchParams.get('after') ?? '0')
        return this.hub.open({ matchId, afterSeq, signal: request.signal })
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
  }
}
