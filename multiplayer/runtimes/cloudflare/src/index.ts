import { RateLimitedError, RateLimiter } from '@chaos-overlords/kernel'
import {
  type AppEnv,
  createApp,
  DEFAULT_RATE_LIMITS,
  DEFAULT_SERVER_CONFIG,
  defaultClientAddress,
  type ServerContainer,
} from '@chaos-overlords/server'
import type { ExecutionContext, ScheduledController } from '@cloudflare/workers-types'
import type { Hono } from 'hono'
import type { Env } from './env'
import { buildBugReports, buildKernel, HUB_PATHS, hubFor, workerLogger } from './kernel'

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
  const bugReports = buildBugReports(env)
  return {
    kernel: buildKernel(env),
    ...(bugReports ? { bugReports } : {}),
    eventStream: {
      open: async ({ matchId, playerId, afterSeq, signal }) => {
        const query = new URLSearchParams({ matchId, playerId, after: String(afterSeq) })
        const url = `https://hub${HUB_PATHS.subscribe}?${query}`
        const response = await hubFor(env, matchId).fetch(url, { signal })
        // The object refuses an over-cap stream with a bare 429; turning it back into the domain
        // error here is what gets the caller the same envelope every other refusal has.
        if (response.status === 429) {
          const scope = response.headers.get('X-Stream-Refusal') === 'process' ? 'process' : 'match'
          throw new RateLimitedError(
            scope === 'process'
              ? 'This server is holding as many event streams as it can'
              : 'This match is holding as many event streams as it can',
            { reason: 'too_many_streams', scope },
          )
        }
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
      bugReport: perMinute(
        env.BUG_REPORT_RATE_LIMIT_PER_MINUTE,
        DEFAULT_RATE_LIMITS.bugReportPerMinute,
      ),
      bugReportState: new RateLimiter(clock, {
        limit: DEFAULT_RATE_LIMITS.bugReportStatePerDay,
        windowMs: 24 * 60 * 60 * 1000,
      }),
    },
    // Listing is on unless a deployment turns it off: an unset var means the Browse screen works,
    // rather than every client being told the server lists nothing.
    config: { ...DEFAULT_SERVER_CONFIG, publicListing: env.PUBLIC_LISTING !== 'false' },
    // `CF-Connecting-IP` is authoritative here and only here: Cloudflare sets it on every request
    // that reaches a Worker and a client cannot forge it through the edge. Off Cloudflare it is a
    // header anyone can write, which is why the default resolver ignores it unless told otherwise.
    clientAddress: (c) => defaultClientAddress(c, { cloudflare: true }),
  }
}

/**
 * Whether this isolate has ever seen the cron fire, and when it started.
 *
 * A deployment with no cron trigger loses every safety net the `scheduled` handler is: the seal of
 * a turn whose Durable Object alarm never fired, the repair of a seal an isolate died in the
 * middle of, the re-run of a verdict cut short, and all of retention. None of that fails loudly —
 * matches just stop advancing for the people in them — so it is worth one log line. Per isolate,
 * which means a busy Worker says it a few times and then never again; that is the right volume for
 * something whose remedy is four lines of `wrangler.toml`.
 */
let cronSeen = false
let cronWatchStartedAt = 0
const CRON_GRACE_MS = 60 * 60 * 1000

function warnIfCronIsMissing(): void {
  if (cronSeen) return
  const now = Date.now()
  if (cronWatchStartedAt === 0) {
    cronWatchStartedAt = now
    return
  }
  if (now - cronWatchStartedAt < CRON_GRACE_MS) return
  cronWatchStartedAt = now
  workerLogger.warn('cron trigger has not fired', {
    hint: 'add [triggers] crons = ["*/5 * * * *"] to wrangler.toml; without it nothing seals a missed deadline, finishes an interrupted seal or collects retention',
  })
}

export default {
  async fetch(request: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
    warnIfCronIsMissing()
    return containerFor(env).app.fetch(request, env, ctx)
  },
  /** The cron safety net: expired deadlines, interrupted seals, and retention. */
  async scheduled(
    _controller: ScheduledController,
    env: Env,
    ctx: ExecutionContext,
  ): Promise<void> {
    cronSeen = true
    const { container } = containerFor(env)
    const { kernel } = container
    // Each step in its own `try`, as the Node sweeper already does. One shared `catch` meant a
    // throw inside the turn sweep also stopped both retention sweeps, every time the cron ran.
    const step = async (name: string, run: () => Promise<void>): Promise<void> => {
      try {
        await run()
      } catch (error: unknown) {
        workerLogger.error('cron step failed', { step: name, error: String(error) })
      }
    }
    ctx.waitUntil(
      (async () => {
        await step('turns', async () => {
          const { sealed, repaired } = await kernel.turns.sweep()
          if (sealed > 0 || repaired > 0) {
            workerLogger.info('cron advanced turns', { sealed, repaired })
          }
        })
        await step('retention', () => kernel.retention.collect().then(() => undefined))
        // A separate database with a separate window; nothing about a match's retention decides
        // when a bug report and its R2 object go.
        await step('bugReports', async () => {
          await container.bugReports?.collect()
        })
      })(),
    )
  },
}
