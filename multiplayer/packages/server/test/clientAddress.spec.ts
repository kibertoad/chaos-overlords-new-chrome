import type { Context } from 'hono'
import { describe, expect, it } from 'vitest'
import { defaultClientAddress, rateLimitKey } from '../src'
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

describe('rateLimitKey', () => {
  it('keeps an IPv4 address, with or without a port', () => {
    expect(rateLimitKey('1.2.3.4')).toBe('1.2.3.4')
    expect(rateLimitKey('1.2.3.4:443')).toBe('1.2.3.4')
    expect(rateLimitKey('  1.2.3.4  ')).toBe('1.2.3.4')
    expect(rateLimitKey('')).toBe('unknown')
  })

  /**
   * A client is routed a whole /64, so the budget has to be the prefix. The compressed forms are
   * the ones that matter: `2001:db8::1` and `2001:db8::2` are one client, and giving each its own
   * key meant the per-address budget never bound for anybody who could pick their own suffix.
   */
  it('masks every spelling of an IPv6 address to its /64', () => {
    expect(rateLimitKey('2001:db8:85a3:8d3:1319:8a2e:370:7348')).toBe('2001:db8:85a3:8d3::/64')
    expect(rateLimitKey('2a02:8109:a1c0:2f0::abcd')).toBe('2a02:8109:a1c0:2f0::/64')
    expect(rateLimitKey('2001:db8::1')).toBe(rateLimitKey('2001:db8::2'))
    expect(rateLimitKey('2001:db8::1')).toBe('2001:db8:0:0::/64')
    expect(rateLimitKey('2001:db8:1::5')).toBe(rateLimitKey('2001:db8:1::6'))
    expect(rateLimitKey('[2001:db8::1]:443')).toBe(rateLimitKey('2001:db8::9'))
    expect(rateLimitKey('fd00::1')).toBe('fd00:0:0:0::/64')
    expect(rateLimitKey('::1')).toBe('0:0:0:0::/64')
  })

  it('reads one address under one key however it is written', () => {
    expect(rateLimitKey('2001:0DB8::0001')).toBe(rateLimitKey('2001:db8::1'))
    expect(rateLimitKey('2001:db8:0:0:0:0:0:1')).toBe(rateLimitKey('2001:db8::1'))
  })

  it('leaves an IPv4-mapped address whole, since its suffix is the address', () => {
    expect(rateLimitKey('::ffff:1.2.3.4')).toBe('::ffff:1.2.3.4')
  })

  /** Two clients behind one /64 must never be told apart; two /64s must never be merged. */
  it('separates different prefixes', () => {
    expect(rateLimitKey('2001:db8::1')).not.toBe(rateLimitKey('2001:db9::1'))
    expect(rateLimitKey('2001:db8:0:1::1')).not.toBe(rateLimitKey('2001:db8::1'))
  })
})
