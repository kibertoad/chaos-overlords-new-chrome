import type { SpectatorRepository } from '@chaos-overlords/kernel'
import { and, asc, eq, isNull, sql } from 'drizzle-orm'
import { firstOrNull, toSpectator } from '../shared/mappers'
import type { PostgresDatabase } from './database'
import * as schema from './schema'

/**
 * Spectators in the Postgres dialect.
 *
 * `create` locks the match row first and then runs the capped insert as a new READ COMMITTED
 * statement, so the count it compares with the cap includes every join committed before it. A
 * count inside the insert alone reads the statement's snapshot, and concurrent joins each see the
 * same old number and all get in.
 */
export function postgresSpectatorRepository(db: PostgresDatabase): SpectatorRepository {
  const { matches, spectators } = schema
  return {
    async create(spectator, cap) {
      // `no key update` serializes joins on one match without blocking the foreign-key checks
      // every other insert under the match takes; see `createLate` for players.
      return db.transaction(async (tx) => {
        const locked = await tx
          .select({ id: matches.id })
          .from(matches)
          .where(eq(matches.id, spectator.matchId))
          .for('no key update')
        if (locked.length === 0) return false
        const rows = await tx
          .insert(spectators)
          .select(
            tx
              .select({
                id: sql`${spectator.id}`.as('id'),
                matchId: sql`${spectator.matchId}`.as('match_id'),
                displayName: sql`${spectator.displayName}`.as('display_name'),
                tokenHash: sql`${spectator.tokenHash}`.as('token_hash'),
                joinedAt: sql`${spectator.joinedAt}`.as('joined_at'),
                leftAt: sql`null::timestamptz`.as('left_at'),
              })
              .from(matches)
              .where(
                and(
                  eq(matches.id, spectator.matchId),
                  sql`(select count(*) from ${spectators} where ${spectators.matchId} = ${spectator.matchId}) < ${cap}`,
                ),
              ),
          )
          .onConflictDoNothing()
          .returning({ id: spectators.id })
        return rows.length === 1
      })
    },
    async get(id) {
      return firstOrNull(
        (await db.select().from(spectators).where(eq(spectators.id, id))).map(toSpectator),
      )
    },
    async getByTokenHash(tokenHash) {
      return firstOrNull(
        (await db.select().from(spectators).where(eq(spectators.tokenHash, tokenHash))).map(
          toSpectator,
        ),
      )
    },
    async listActive(matchId) {
      const rows = await db
        .select()
        .from(spectators)
        .where(and(eq(spectators.matchId, matchId), isNull(spectators.leftAt)))
        .orderBy(asc(spectators.joinedAt), asc(spectators.id))
      return rows.map(toSpectator)
    },
    async revoke(id, at) {
      const rows = await db
        .update(spectators)
        .set({ tokenHash: null, leftAt: at })
        .where(and(eq(spectators.id, id), isNull(spectators.leftAt)))
        .returning({ id: spectators.id })
      return rows.length === 1
    },
  }
}
