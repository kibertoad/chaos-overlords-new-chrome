import type { RateLimitPolicy, RateLimitStore, RateLimitWindow } from '@chaos-overlords/kernel'
import { and, eq, gt, inArray, lte, sql } from 'drizzle-orm'
import type { PostgresDatabase } from './database'
import { rateLimitWindows } from './schema'

/** A {@link RateLimitStore} that can also delete the windows that have rolled. */
export interface SweepableRateLimitStore extends RateLimitStore {
  /** Deletes up to `limit` windows that rolled at or before `now`; returns how many went. */
  sweep(now: number, limit?: number): Promise<number>
}

/**
 * Rate limit windows in a Postgres table, so every Node instance on one database spends the same
 * budgets.
 *
 * `consume` is one `INSERT … ON CONFLICT DO UPDATE`: Postgres locks the conflicting row for the
 * update, so concurrent calls from any number of instances are counted one after another, and the
 * decision comes back in the same round trip. Every expression in the `SET` list reads the row as it
 * was before the statement, which is what lets `allowed` record whether this call found room. The
 * rules are the kernel's `consumeWindow`, written in SQL.
 */
export function postgresRateLimitStore(db: PostgresDatabase): SweepableRateLimitStore {
  const t = rateLimitWindows
  return {
    async consume(key: string, policy: RateLimitPolicy, now: number): Promise<RateLimitWindow> {
      const rolled = sql`${t.resetAt} <= ${now}`
      const rows = await db
        .insert(t)
        .values({ key, windowStart: now, resetAt: now + policy.windowMs, count: 1, allowed: true })
        .onConflictDoUpdate({
          target: t.key,
          set: {
            allowed: sql`(${rolled} or ${t.count} < ${policy.limit})`,
            windowStart: sql`case when ${rolled} then ${now} else ${t.windowStart} end`,
            resetAt: sql`case when ${rolled} then ${now + policy.windowMs} else ${t.resetAt} end`,
            count: sql`case when ${rolled} then 1 when ${t.count} < ${policy.limit} then ${t.count} + 1 else ${t.count} end`,
          },
        })
        .returning({
          allowed: t.allowed,
          count: t.count,
          windowStart: t.windowStart,
          resetAt: t.resetAt,
        })
      const row = rows[0]
      if (!row) throw new Error('rate limit upsert returned no row')
      return row
    },

    async inspect(key: string, now: number): Promise<{ count: number; resetAt: number } | null> {
      const rows = await db
        .select({ count: t.count, resetAt: t.resetAt })
        .from(t)
        .where(and(eq(t.key, key), gt(t.resetAt, now)))
      return rows[0] ?? null
    },

    async refund(key: string, windowStart: number): Promise<void> {
      await db
        .update(t)
        .set({ count: sql`${t.count} - 1` })
        .where(and(eq(t.key, key), eq(t.windowStart, windowStart), gt(t.count, 0)))
    },

    /**
     * Batched, so a sweep after a flood of distinct addresses is many short statements rather than
     * one that holds row locks every `consume` is waiting on.
     */
    async sweep(now: number, limit = 1000): Promise<number> {
      const doomed = db
        .select({ key: t.key })
        .from(t)
        .where(lte(t.resetAt, now))
        .orderBy(t.resetAt)
        .limit(limit)
      const deleted = await db
        .delete(t)
        .where(and(inArray(t.key, doomed), lte(t.resetAt, now)))
        .returning({ key: t.key })
      return deleted.length
    },
  }
}
