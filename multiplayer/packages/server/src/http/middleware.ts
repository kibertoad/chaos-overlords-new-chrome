import { RateLimitedError, UnauthorizedError } from '@chaos-overlords/kernel'
import type { Context, MiddlewareHandler } from 'hono'
import type { RateLimiters } from '../container'
import type { AppEnv } from './types'

/** Mints or adopts `X-Request-Id`; it rides every error envelope so a player can quote it. */
export const requestId: MiddlewareHandler<AppEnv> = async (c, next) => {
  const id = c.req.header('x-request-id')?.slice(0, 64) || crypto.randomUUID()
  c.set('requestId', id)
  await next()
  c.header('X-Request-Id', id)
}

/** Resolves the bearer token to a principal or refuses with 401. */
export const bearerAuth: MiddlewareHandler<AppEnv> = async (c, next) => {
  const header = c.req.header('authorization') ?? ''
  const [scheme, token] = header.split(' ', 2)
  if (scheme?.toLowerCase() !== 'bearer' || !token) {
    throw new UnauthorizedError('Send the player token as a Bearer credential', {
      reason: 'missing_token',
    })
  }
  c.set('principal', await c.get('container').kernel.auth.authenticate(token))
  await next()
}

/** Fixed-window limiter on the unauthenticated doors, keyed by client address. */
export const rateLimited: MiddlewareHandler<AppEnv> = async (c, next) => {
  const container = c.get('container')
  const key = (container.clientAddress ?? defaultClientAddress)(c)
  enforce(container.rateLimiters, 'anonymous', key, c)
  await next()
}

/**
 * Limiter for an authenticated member, keyed by player rather than address so one player on a shared
 * address cannot spend another's budget. Must run after `bearerAuth`.
 */
export function memberRateLimited(tier: keyof RateLimiters = 'member'): MiddlewareHandler<AppEnv> {
  return async (c, next) => {
    const container = c.get('container')
    enforce(container.rateLimiters, tier, c.get('principal').player.id, c)
    await next()
  }
}

function enforce(
  limiters: RateLimiters,
  tier: keyof RateLimiters,
  key: string,
  c: Context<AppEnv>,
): void {
  const retryAfter = limiters[tier].take(`${tier}:${key}`)
  if (retryAfter === null) return
  c.header('Retry-After', String(retryAfter))
  throw new RateLimitedError('Too many attempts; slow down', {
    reason: 'rate_limited',
    retryAfterSeconds: retryAfter,
  })
}

export function defaultClientAddress(c: Context<AppEnv>): string {
  return (
    c.req.header('cf-connecting-ip') ??
    c.req.header('x-forwarded-for')?.split(',')[0]?.trim() ??
    'unknown'
  )
}
