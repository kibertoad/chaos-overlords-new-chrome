import type { Context } from 'hono'
import { describe, expect, it } from 'vitest'
import { defaultClientAddress } from '../src'
import type { AppEnv } from '../src/http/types'

/** Just enough context to answer `c.req.header(name)`. */
function requestWith(headers: Record<string, string>): Context<AppEnv> {
  return {
    req: { header: (name: string) => headers[name.toLowerCase()] },
  } as unknown as Context<AppEnv>
}

describe('defaultClientAddress', () => {
  /**
   * Proxies append to `X-Forwarded-For`, so everything left of the last entry is whatever the client
   * sent. Reading the leftmost would let a caller put a fresh address on every request and never
   * meet the anonymous limit, which is the only guard on the join-code and password doors.
   */
  it('reads the entry the trusted proxy wrote, not the one the client sent', () => {
    const forged = requestWith({ 'x-forwarded-for': '1.2.3.4, 198.51.100.7' })
    expect(defaultClientAddress(forged)).toBe('198.51.100.7')
  })

  it('skips the hops an operator says are trusted', () => {
    // A CDN in front of a load balancer: the last entry is the load balancer's view of the CDN, and
    // the client is one further left.
    const chained = requestWith({ 'x-forwarded-for': '1.2.3.4, 203.0.113.9, 198.51.100.7' })
    expect(defaultClientAddress(chained, { hops: 1 })).toBe('203.0.113.9')
    expect(defaultClientAddress(chained, { hops: 5 })).toBe('unknown')
  })

  it('trusts CF-Connecting-IP only on Cloudflare', () => {
    // Nothing but Cloudflare strips or overwrites this header, so anywhere else it is a field the
    // client fills in itself.
    const spoofed = requestWith({
      'cf-connecting-ip': '1.2.3.4',
      'x-forwarded-for': '198.51.100.7',
    })
    expect(defaultClientAddress(spoofed)).toBe('198.51.100.7')
    expect(defaultClientAddress(spoofed, { cloudflare: true })).toBe('1.2.3.4')
  })

  it('answers unknown rather than guessing when there is no chain', () => {
    expect(defaultClientAddress(requestWith({}))).toBe('unknown')
    expect(defaultClientAddress(requestWith({ 'x-forwarded-for': ' , ' }))).toBe('unknown')
  })
})
