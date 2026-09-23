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
    chargeAnonymous(c)
    throw new UnauthorizedError('Send the player token as a Bearer credential', {
      reason: 'missing_token',
    })
  }
  try {
    c.set('principal', await c.get('container').kernel.auth.authenticate(token))
  } catch (error) {
    if (error instanceof UnauthorizedError) chargeAnonymous(c)
    throw error
  }
  await next()
}

/**
 * Spend one unit of the anonymous budget and return the client address it was charged to.
 *
 * A caller who failed to authenticate shares the budget with create and join deliberately: an
 * address doing either at volume is the same address either way, and a separate tier would just be
 * a second thing to size. A caller who is over budget is told so (429) instead of being told the
 * token was wrong, which is the right order of refusals for a caller who has proved nothing.
 */
function chargeAnonymous(c: Context<AppEnv>): string {
  const key = addressOf(c)
  enforce(c.get('container').rateLimiters, 'anonymous', key, c)
  return key
}

/** Fixed-window limiter on the unauthenticated doors, keyed by client address. */
export const rateLimited: MiddlewareHandler<AppEnv> = async (c, next) => {
  const key = chargeAnonymous(c)
  // The join doors charge a per-caller budget of their own in front of PBKDF2, in the kernel,
  // where there is no request to work an address out from. Normalised here so that one client is
  // one key there too; see `rateLimitKey`.
  c.set('caller', rateLimitKey(key))
  await next()
}

/**
 * Limiter for the bug report door, keyed by client address like the other unauthenticated ones but
 * spending its own budget: a report is a rare, large call and a join is a frequent, tiny one, and
 * one must not be able to exhaust the other.
 */
export const bugReportRateLimited: MiddlewareHandler<AppEnv> = async (c, next) => {
  const container = c.get('container')
  const key = addressOf(c)
  enforce(container.rateLimiters, 'bugReport', key, c)
  // The handler reserves one unit only for a validated report carrying a journal, then releases it
  // if the journal fails its digest check or is omitted by the storage budget.
  c.set('bugReportJournalBudget', () =>
    container.rateLimiters.bugReportState.reserve(`bugReportState:${rateLimitKey(key)}`),
  )
  await next()
}

/**
 * The process-wide budget on creating a match, checked here and spent by the create handler.
 *
 * Mounted for `POST /matches` alone, because the same path also serves the public listing, which
 * must not spend it. It runs after `rateLimited`, so a single address over its own budget is
 * refused without touching the shared one. It only PEEKS: a spent budget is refused before the
 * body is read, but a request is charged only once its body has passed the contract validator,
 * through `spendMatchCreation`. Charging here would let a handful of addresses sending malformed
 * bodies, which never store a lobby, hold the shared budget at zero for every legitimate host.
 */
export const matchCreationRateLimited: MiddlewareHandler<AppEnv> = async (c, next) => {
  const limiters = c.get('container').rateLimiters
  const retryAfter = limiters.matchCreation.peek(`matchCreation:${MATCH_CREATION_KEY}`)
  if (retryAfter !== null) refuse(c, retryAfter)
  c.set('spendMatchCreation', () => enforce(limiters, 'matchCreation', MATCH_CREATION_KEY, c))
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

/** The client address this deployment attributes a request to, before it is normalised. */
function addressOf(c: Context<AppEnv>): string {
  const container = c.get('container')
  return (container.clientAddress ?? defaultClientAddress)(c)
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
  // IPv6 is case-insensitive, so the spelling has to be settled before anything below compares or
  // slices it. Lowercasing after the IPv4-mapped test let `::FFFF:1.2.3.4` and `::ffff:1.2.3.4`
  // hold a budget each.
  const lowered = withoutPort.toLowerCase()
  // An embedded IPv4 address is the client's own address rather than a prefix it was routed, so it
  // stays whole.
  if (lowered.includes('.')) return lowered
  const mapped = ipv4MappedFromHex(lowered)
  if (mapped !== null) return mapped
  // Otherwise IPv6, masked to the /64 a single client is routed.
  return `${ipv6Prefix64(lowered)}::/64`
}

/**
 * The dotted spelling of an IPv4-mapped address whose embedded address is written as hex groups,
 * or null when the address is not one.
 *
 * RFC 4291 lets `::ffff:1.2.3.4` also be written `::ffff:0102:0304`, and only the dotted form was
 * recognised. The hex form has no `.`, so it fell through to the /64 mask — where the mapped
 * prefix is all zeros, so every IPv4 client written that way shared `0:0:0:0::/64` with each
 * other, with `::` and with `::1`. One noisy source in that bucket spent the anonymous and
 * bug-report budgets for the rest of it. Both spellings now reduce to the same key.
 */
function ipv4MappedFromHex(address: string): string | null {
  const groups = /^::ffff:([0-9a-f]{1,4}):([0-9a-f]{1,4})$/.exec(address)
  if (groups === null) return null
  const high = Number.parseInt(groups[1] as string, 16)
  const low = Number.parseInt(groups[2] as string, 16)
  return `::ffff:${high >> 8}.${high & 0xff}.${low >> 8}.${low & 0xff}`
}

/**
 * The first four groups of an IPv6 address: the /64 a single client is routed.
 *
 * The compressed form has to be expanded before it is sliced. `2001:db8::1` writes two groups and
 * then hides five zero groups behind the `::`, so taking the groups as written either kept the
 * interface identifier or gave up and used the whole address — and every address in one routed /64
 * then had a budget of its own, which is the exact thing this masking exists to stop. Groups are
 * stripped of leading zeros so the two spellings of one address cannot hold two budgets either.
 */
function ipv6Prefix64(address: string): string {
  const [head, tail] = address.split('::', 2)
  const leading = head === '' ? [] : (head as string).split(':')
  const trailing = tail === undefined || tail === '' ? [] : tail.split(':')
  const hidden = tail === undefined ? 0 : Math.max(0, 8 - leading.length - trailing.length)
  const groups = [...leading, ...Array.from({ length: hidden }, () => '0'), ...trailing]
  while (groups.length < 4) groups.push('0')
  return groups
    .slice(0, 4)
    .map((group) => group.replace(/^0+(?=.)/, ''))
    .join(':')
}

/**
 * Tiers keyed by something that is already a single identity rather than a client address.
 *
 * `rateLimitKey` masks IPv6 prefixes and strips ports, which is meaningless work on a player UUID
 * and runs on every authenticated request.
 */
const IDENTITY_TIERS: ReadonlySet<string> = new Set(['member', 'upload', 'matchCreation'])

/** The one key the match-creation tier counts under; it is a process-wide budget, not a per-caller one. */
const MATCH_CREATION_KEY = 'all'

function enforce(
  limiters: RateLimiters,
  tier: keyof RateLimiters,
  key: string,
  c: Context<AppEnv>,
): void {
  const retryAfter = limiters[tier].take(
    `${tier}:${IDENTITY_TIERS.has(tier) ? key : rateLimitKey(key)}`,
  )
  if (retryAfter !== null) refuse(c, retryAfter)
}

function refuse(c: Context<AppEnv>, retryAfter: number): never {
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
