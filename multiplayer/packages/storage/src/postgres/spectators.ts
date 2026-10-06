import type { SpectatorRepository } from '@chaos-overlords/kernel'
import { and, asc, eq, isNull, sql } from 'drizzle-orm'
import { firstOrNull, toSpectator } from '../shared/mappers'
import type { PostgresDatabase } from './database'
import * as schema from './schema'

/**
 * Spectators in the Postgres dialect.
 *
 * `create` is an insert fed by a select over the match row, so the cap on how many spectators a
 * match admits is counted by the statement that writes the new one: two joins at once cannot both
 * take the last place.
 */
export function postgresSpectatorRepository(db: PostgresDatabase): SpectatorRepository {
  const { matches, spectators } = schema
  return {
    async create(spectator, cap) {
      const rows = await db
        .insert(spectators)
        .select(
          db
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
