import { RateLimiter } from '@chaos-overlords/kernel'
import {
  type AppEnv,
  createApp,
  DEFAULT_RATE_LIMITS,
  DEFAULT_SERVER_CONFIG,
  type ServerContainer,
} from '@chaos-overlords/server'
import type { ExecutionContext, ScheduledController } from '@cloudflare/workers-types'
import type { Hono } from 'hono'
import type { Env } from './env'
import { buildKernel, HUB_PATHS, hubFor, workerLogger } from './kernel'

export { MatchHub } from './MatchHub'

type Built = { container: ServerContainer; app: Hono<AppEnv> }

/**
 * One container per isolate, held in module scope.
 *
 * It has to be cached: a rate limiter counts requests within a window, so building a fresh one per
 * request would reset the window every time and limit nothing. The router and the D1-backed kernel
 * are per-isolate state for the same reason a server builds them once at startup — there is nothing
 * request-specific in either. Cloudflare's own rate limiting rules still belong in front of a public
 * deployment, because an isolate is not the whole world.
 *
 * A module-scoped singleton rather than a `WeakMap` keyed on `env`: the bindings object being the
 * same identity on every request is not a documented guarantee, and if it ever stopped being one the
 * cache would silently miss and the rate limiter would reset per request — a limiter that looks
 * configured and enforces nothing. Module scope has exactly the lifetime we want, the isolate's.
 * `env` is captured from the first request, which is the same bindings for the isolate's whole life.
 */
let built: Built | undefined

export function containerFor(env: Env): Built {
  if (!built) {
    const container = buildContainer(env)
    built = { container, app: createApp(container) }
  }
  return built
}

/** Drops the cached container. Tests that assert per-isolate construction need a fresh isolate. */
export function resetContainerForTests(): void {
  built = undefined
}

export function buildContainer(env: Env): ServerContainer {
  const clock = { now: () => new Date() }
  const perMinute = (raw: string | undefined, fallback: number) => {
    const limit = Number(raw ?? fallback)
    const effective = Number.isInteger(limit) && limit > 0 ? limit : fallback
    return new RateLimiter(clock, { limit: effective, windowMs: 60_000 })
  }
  return {
    kernel: buildKernel(env),
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
    rateLimiters: {
      anonymous: perMinute(env.RATE_LIMIT_PER_MINUTE, DEFAULT_RATE_LIMITS.anonymousPerMinute),
      member: perMinute(env.MEMBER_RATE_LIMIT_PER_MINUTE, DEFAULT_RATE_LIMITS.memberPerMinute),
      upload: perMinute(env.UPLOAD_RATE_LIMIT_PER_MINUTE, DEFAULT_RATE_LIMITS.uploadPerMinute),
    },
    config: { ...DEFAULT_SERVER_CONFIG, publicListing: env.PUBLIC_LISTING === 'true' },
  }
}

export default {
  async fetch(request: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
    return containerFor(env).app.fetch(request, env, ctx)
  },
  /** The cron safety net: expired deadlines, interrupted seals, and retention. */
  async scheduled(
    _controller: ScheduledController,
    env: Env,
    ctx: ExecutionContext,
  ): Promise<void> {
    const { container } = containerFor(env)
    const { kernel } = container
    ctx.waitUntil(
      (async () => {
        const { sealed, repaired } = await kernel.turns.sweep()
        if (sealed > 0 || repaired > 0) {
          workerLogger.info('cron advanced turns', { sealed, repaired })
        }
        await kernel.retention.collect()
      })().catch((error: unknown) => {
        workerLogger.error('cron sweep failed', { error: String(error) })
      }),
    )
  },
}
