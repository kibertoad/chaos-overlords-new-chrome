import type { EventRepository, PersistedEvent } from '@chaos-overlords/kernel'
import { and, asc, desc, eq, sql } from 'drizzle-orm'
import { APPEND_ATTEMPTS, appendBackoff, isUniqueViolation } from '../shared/constraints'
import { toEvent } from '../shared/mappers'
import type { SqliteDatabase } from './database'
import * as schema from './schema'

/**
 * The match event log in the SQLite dialect: the gapless, self-allocating sequence every stream
 * cursor moves along. Its own module because the allocation rule — `max(seq) + 1` inside the
 * insert, retried on the primary key — is the one invariant the whole delivery contract rests on.
 */
export function sqliteEventRepository(db: SqliteDatabase): EventRepository {
  const { matchEvents } = schema
  return {
    /**
     * The sequence number comes from the log itself inside the insert, so the allocation cannot be
     * separated from the write: a committed `seq` therefore implies every lower one is committed,
     * which is the invariant a stream cursor relies on. Concurrent appends collide on the primary
     * key and the loser simply re-reads the maximum.
     */
    async append(event): Promise<PersistedEvent> {
      const nextSeq = sql<number>`(select coalesce(max(${matchEvents.seq}), 0) + 1 from ${matchEvents} where ${eq(matchEvents.matchId, event.matchId)})`
      for (let attempt = 1; attempt <= APPEND_ATTEMPTS; attempt += 1) {
        try {
          const rows = await db
            .insert(matchEvents)
            .values({
              matchId: event.matchId,
              seq: nextSeq,
              type: event.type,
              payload: event.payload,
              createdAt: new Date(event.createdAt),
            })
            .returning({ seq: matchEvents.seq })
          const row = rows[0]
          if (!row) throw new Error(`append to ${event.matchId} reported no row`)
          return { ...event, seq: row.seq } as PersistedEvent
        } catch (error) {
          if (!isUniqueViolation(error) || attempt === APPEND_ATTEMPTS) throw error
          await appendBackoff(attempt)
        }
      }
      throw new Error(`could not append an event for ${event.matchId}`)
    },
    async listAfter(matchId, afterSeq, limit) {
      const rows = await db
        .select()
        .from(matchEvents)
        .where(and(eq(matchEvents.matchId, matchId), sql`${matchEvents.seq} > ${afterSeq}`))
        .orderBy(asc(matchEvents.seq))
        .limit(limit)
      return rows.map(toEvent)
    },
    async lastSeq(matchId) {
      const rows = await db
        .select({ seq: matchEvents.seq })
        .from(matchEvents)
        .where(eq(matchEvents.matchId, matchId))
        .orderBy(desc(matchEvents.seq))
        .limit(1)
      return rows[0]?.seq ?? 0
    },
  }
}
