import { RateLimitedError, UnauthorizedError } from '@chaos-overlords/kernel'
import type { Context, MiddlewareHandler } from 'hono'
import type { RateLimiters } from '../container'
import type { AppEnv } from './types'

/**
 * Mints or adopts `X-Request-Id`; it rides every error envelope so a player can quote it.
 *
 * The header is set before the handler runs, not after: a handler that throws never comes back
 * here, and the response most worth correlating is exactly the one that failed.
 */
export const requestId: MiddlewareHandler<AppEnv> = async (c, next) => {
  const id = safeRequestId(c.req.header('x-request-id')) ?? crypto.randomUUID()
  c.set('requestId', id)
  c.header('X-Request-Id', id)
  await next()
}

/**
 * A client-supplied correlation id, or null when it is not one.
 *
 * The value is echoed in a response header and written into the structured logs, so it is a string
 * a stranger chooses that ends up in an operator's tooling. Restricting it to the characters ids
 * are actually made of leaves header splitting, log-line forgery and terminal escapes with nothing
 * to work with; anything else is simply replaced by a minted one, because a caller sending a
 * malformed id wanted correlation, not a refusal.
 */
function safeRequestId(raw: string | undefined): string | null {
  const trimmed = raw?.trim() ?? ''
  return trimmed !== '' && trimmed.length <= 64 && /^[A-Za-z0-9_-]+$/.test(trimmed) ? trimmed : null
}

/**
 * Resolves the bearer token to a principal or refuses with 401.
 *
 * A refusal is charged to the caller's address. The member limiter is keyed by player and cannot
 * run until there is a player, so without this a stranger sending a made-up token drives one
 * SHA-256 and one indexed lookup per request at line rate, against no budget at all. Guessing a
 * 256-bit token is not the concern; the database reads are.
 */
export const bearerAuth: MiddlewareHandler<AppEnv> = async (c, next) => {
  const header = c.req.header('authorization') ?? ''
  const [scheme, token] = header.split(' ', 2)
  if (scheme?.toLowerCase() !== 'bearer' || !token) {
    chargeFailedAuth(c)
    throw new UnauthorizedError('Send the player token as a Bearer credential', {
      reason: 'missing_token',
    })
  }
  try {
    c.set('principal', await c.get('container').kernel.auth.authenticate(token))
  } catch (error) {
    if (error instanceof UnauthorizedError) chargeFailedAuth(c)
    throw error
  }
  await next()
}

/**
 * Spend one unit of the anonymous budget for a caller who failed to authenticate.
 *
 * It shares the budget with create and join deliberately: an address doing either at volume is the
 * same address either way, and a separate tier would just be a second thing to size. A caller who
 * is over budget is told so (429) instead of being told the token was wrong, which is the right
 * order of refusals for a caller who has proved nothing.
 */
function chargeFailedAuth(c: Context<AppEnv>): void {
  const container = c.get('container')
  const key = (container.clientAddress ?? defaultClientAddress)(c)
  enforce(container.rateLimiters, 'anonymous', key, c)
}

/** Fixed-window limiter on the unauthenticated doors, keyed by client address. */
export const rateLimited: MiddlewareHandler<AppEnv> = async (c, next) => {
  const container = c.get('container')
  const key = (container.clientAddress ?? defaultClientAddress)(c)
  enforce(container.rateLimiters, 'anonymous', key, c)
  await next()
}

/**
 * Limiter for the bug report door, keyed by client address like the other unauthenticated ones but
 * spending its own budget: a report is a rare, large call and a join is a frequent, tiny one, and
 * one must not be able to exhaust the other.
 */
export const bugReportRateLimited: MiddlewareHandler<AppEnv> = async (c, next) => {
  const container = c.get('container')
  const key = (container.clientAddress ?? defaultClientAddress)(c)
  enforce(container.rateLimiters, 'bugReport', key, c)
  // The daily journal budget is NOT spent here. It used to be, before the body had even been read,
  // so five text-only reports or five requests the contract validator answered 413 or 422 for spent
  // it, and the sixth — the one that actually carried a journal — was quietly filed with
  // `stateStored: 'omitted'`. Everyone behind one NAT shares those five. The handler spends it
  // instead, once it knows there is a journal to spend it on; see the bug report route.
  c.set(
    'bugReportJournalBudget',
    () =>
      container.rateLimiters.bugReportState.take(`bugReportState:${rateLimitKey(key)}`) === null,
  )
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

/**
 * The budget key for a client address.
 *
 * Two normalisations, both because the raw string is not one client. A routed IPv6 /64 gives a
 * client 2^64 source addresses, so every request could carry a new key and no per-address budget
 * would ever bind; and proxies that write `ip:port` into `X-Forwarded-For` (Azure Application
 * Gateway does) give a new port per connection, with the same result. Each unrecognised key also
 * costs a map entry that lives up to two windows, so the limiter's own memory grew with the flood
 * it was meant to stop.
 */
export function rateLimitKey(address: string): string {
  const trimmed = address.trim()
  if (trimmed === '') return 'unknown'
  // `[2001:db8::1]:443` — a bracketed IPv6 host with a port.
  const bracketed = /^\[([^\]]+)](?::\d+)?$/.exec(trimmed)
  const host = bracketed?.[1] ?? trimmed
  // `1.2.3.4:443` — an IPv4 host with a port. A bare IPv6 address has more than one colon.
  const withoutPort = /^[^:]+:\d+$/.test(host) ? (host.split(':')[0] as string) : host
  if (!withoutPort.includes(':')) return withoutPort
  // IPv6, masked to the /64 a single client is routed. IPv4-mapped forms keep their full address.
  if (withoutPort.includes('.')) return withoutPort
  const groups = withoutPort.toLowerCase().split('::')
  if (groups.length > 1) {
    const leading = (groups[0] as string).split(':').filter((part) => part !== '')
    return leading.length >= 4 ? `${leading.slice(0, 4).join(':')}::/64` : `${withoutPort}::/64`
  }
  return `${withoutPort.split(':').slice(0, 4).join(':')}::/64`
}

/**
 * Tiers keyed by something that is already a single identity rather than a client address.
 *
 * `rateLimitKey` masks IPv6 prefixes and strips ports, which is meaningless work on a player UUID
 * and runs on every authenticated request.
 */
const IDENTITY_TIERS: ReadonlySet<string> = new Set(['member', 'upload'])

function enforce(
  limiters: RateLimiters,
  tier: keyof RateLimiters,
  key: string,
  c: Context<AppEnv>,
): void {
  const retryAfter = limiters[tier].take(
    `${tier}:${IDENTITY_TIERS.has(tier) ? key : rateLimitKey(key)}`,
  )
  if (retryAfter === null) return
  c.header('Retry-After', String(retryAfter))
  throw new RateLimitedError('Too many attempts; slow down', {
    reason: 'rate_limited',
    retryAfterSeconds: retryAfter,
  })
}

/**
 * The client address as a trusted proxy reports it.
 *
 * Only reached when the runtime has been told there IS a trusted proxy; see the Node container,
 * which reads the socket address otherwise.
 *
 * Two rules, and both are about which parts of the chain the client could have written:
 *
 * 1. **The RIGHTMOST `X-Forwarded-For` entry**, not the leftmost. Proxies append, so the last entry
 *    is the one the trusted proxy wrote and every entry to its left is whatever the client sent.
 *    Reading the leftmost would let a caller put a different address on every request and never meet
 *    the anonymous or bug-report limit, which are the only guards on the join-code door, the
 *    password door and multi-megabyte uploads.
 * 2. **`CF-Connecting-IP` only where Cloudflare sets it.** No other proxy strips or overwrites that
 *    header, so behind anything else it is a field the client fills in itself. The Cloudflare runtime
 *    passes `cloudflare: true`; nothing else does.
 *
 * A deployment behind two proxies (a CDN in front of a load balancer, say) has the CDN's address as
 * the rightmost entry; `hops` says how many entries from the right to skip to reach the real client.
 */
export function defaultClientAddress(
  c: Context<AppEnv>,
  options: { cloudflare?: boolean; hops?: number } = {},
): string {
  if (options.cloudflare) {
    const connecting = c.req.header('cf-connecting-ip')?.trim()
    if (connecting) return connecting
  }
  const forwarded = c.req.header('x-forwarded-for')
  if (!forwarded) return 'unknown'
  const chain = forwarded
    .split(',')
    .map((entry) => entry.trim())
    .filter((entry) => entry !== '')
  const hops = Math.max(0, options.hops ?? 0)
  return chain[chain.length - 1 - hops] ?? 'unknown'
}
