import { RateLimitedError, UnauthorizedError } from '@chaos-overlords/kernel'
import type { Context, MiddlewareHandler } from 'hono'
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
  const retryAfter = container.rateLimiter.take(key)
  if (retryAfter !== null) {
    c.header('Retry-After', String(retryAfter))
    throw new RateLimitedError('Too many attempts; slow down', {
      reason: 'rate_limited',
      retryAfterSeconds: retryAfter,
    })
  }
  await next()
}

export function defaultClientAddress(c: Context<AppEnv>): string {
  return (
    c.req.header('cf-connecting-ip') ??
    c.req.header('x-forwarded-for')?.split(',')[0]?.trim() ??
    'unknown'
  )
}
