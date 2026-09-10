import { RateLimiter } from '@chaos-overlords/kernel'
import { createApp, DEFAULT_SERVER_CONFIG, type ServerContainer } from '@chaos-overlords/server'
import type { ExecutionContext, ScheduledController } from '@cloudflare/workers-types'
import type { Env } from './env'
import { buildKernel, HUB_PATHS, hubFor, workerLogger } from './kernel'

export { MatchHub } from './MatchHub'

/**
 * The limiter is per isolate, so it only softens abuse on one edge node; put Cloudflare's own
 * rate limiting rule in front of `/api/v1/matches` and `/api/v1/matches/join` for the real gate.
 */
const rateLimiter = new RateLimiter({ now: () => new Date() }, { limit: 30, windowMs: 60_000 })

export function buildContainer(env: Env): ServerContainer {
  const kernel = buildKernel(env)
  const limit = Number(env.RATE_LIMIT_PER_MINUTE ?? '30')
  return {
    kernel,
    eventStream: {
      open: async ({ matchId, afterSeq, signal }) => {
        const url = `https://hub${HUB_PATHS.subscribe}?matchId=${encodeURIComponent(matchId)}&after=${afterSeq}`
        const response = await hubFor(env, matchId).fetch(url, { signal })
        return new Response(response.body as ReadableStream<Uint8Array> | null, {
          status: response.status,
          headers: response.headers as unknown as HeadersInit,
        })
      },
    },
    rateLimiter:
      Number.isFinite(limit) && limit > 0
        ? new RateLimiter({ now: () => new Date() }, { limit, windowMs: 60_000 })
        : rateLimiter,
    config: { ...DEFAULT_SERVER_CONFIG, publicListing: env.PUBLIC_LISTING === 'true' },
  }
}

export default {
  async fetch(request: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
    const app = createApp(buildContainer(env))
    return app.fetch(request, env, ctx)
  },
  async scheduled(
    _controller: ScheduledController,
    env: Env,
    ctx: ExecutionContext,
  ): Promise<void> {
    const kernel = buildKernel(env)
    ctx.waitUntil(
      kernel.turns.sweepExpiredTurns().then((sealed) => {
        if (sealed > 0) workerLogger.info('cron sealed expired turns', { sealed })
      }),
    )
  },
}
