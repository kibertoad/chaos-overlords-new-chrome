import type { Player, PlayerRepository } from '@chaos-overlords/kernel'
import { and, asc, eq, exists, inArray, isNotNull, ne, notExists, or, sql } from 'drizzle-orm'
import type { BetterSQLite3Database } from 'drizzle-orm/better-sqlite3'
import type { DrizzleD1Database } from 'drizzle-orm/d1'
import type { BaseSQLiteDatabase } from 'drizzle-orm/sqlite-core'
import { firstOrNull, toPlayer } from '../shared/mappers'
import type { SqliteDatabase } from './database'
import * as schema from './schema'

/**
 * Players and their seats in the SQLite dialect: the lobby insert, the late seat claim and the
 * status transitions. Its own module because the late claim needs driver-specific atomicity (a
 * D1 batch or a better-sqlite3 transaction) that nothing else in the dialect does.
 */

/**
 * The rows that still hold a claim on their seat: every row but a computer-controlled one whose
 * token is gone. The SQL twin of `isVacated`, negated.
 */
function holdsClaim() {
  const { players } = schema
  return or(ne(players.status, 'computer'), isNotNull(players.tokenHash))
}

/**
 * The select list `create` and `createLate` feed their insert from: the player's own values, each
 * under the column name it is inserted as.
 *
 * The list is written out, which means it does NOT get Drizzle's column mapping: a column added to
 * `players` later has to be added here too, in the storage form the column expects. The
 * conformance suite compares a created player against its fixture field by field, so a dropped
 * column fails there rather than going unnoticed.
 */
function playerValues(player: Player) {
  return {
    id: sql`${player.id}`.as('id'),
    matchId: sql`${player.matchId}`.as('match_id'),
    slot: sql`${player.slot}`.as('slot'),
    joinOrder: sql`${player.joinOrder}`.as('join_order'),
    displayName: sql`${player.displayName}`.as('display_name'),
    portraitId: sql`${player.portraitId}`.as('portrait_id'),
    tokenHash: sql`${player.tokenHash}`.as('token_hash'),
    status: sql`${player.status}`.as('status'),
    joinedAt: sql`${player.joinedAt.getTime()}`.as('joined_at'),
  }
}

/** Whether the database is D1, the one SQLite driver that batches instead of transacting. */
function isD1(db: SqliteDatabase): db is SqliteDatabase & DrizzleD1Database<typeof schema> {
  return typeof (db as Partial<DrizzleD1Database<typeof schema>>).batch === 'function'
}

/**
 * The two statements of a late seat claim, in the order they run.
 *
 * The insert treats the seat's computer-controlled rows as already released: it refuses only a
 * seat with a row that is not computer controlled, and counts capacity without the seat's computer
 * rows. The release then revokes their tokens only when the insert landed, which it tests by the
 * claimant's row holding the claimant's token, so a refused claim releases nothing. Run as one
 * unit, nothing can come between them: a `rejoin` lands before the insert, which then sees a human
 * on the seat, or after the release, and finds its token revoked.
 *
 * Capacity is in the same statement as the insert. Two late joiners for two different free slots
 * each passed a capacity check taken a moment before the other's insert, and the match ended up
 * holding more players than `maxPlayers`.
 */
function lateSeatClaim<TKind extends 'sync' | 'async', TRunResult>(
  db: BaseSQLiteDatabase<TKind, TRunResult, typeof schema>,
  player: Player,
) {
  const { matches, players } = schema
  const seat = and(eq(players.matchId, player.matchId), eq(players.slot, player.slot))
  const humanHeld = db
    .select({ id: players.id })
    .from(players)
    .where(and(seat, ne(players.status, 'computer')))
  const insert = db
    .insert(players)
    .select((query) =>
      query
        .select(playerValues(player))
        .from(matches)
        .where(
          and(
            eq(matches.id, player.matchId),
            eq(matches.status, 'running'),
            notExists(humanHeld),
            sql`(select count(*) from ${players} where ${players.matchId} = ${player.matchId} and ${holdsClaim()} and not (${players.slot} = ${player.slot} and ${players.status} = 'computer')) < ${matches.maxPlayers}`,
          ),
        ),
    )
    .onConflictDoNothing()
    .returning({ id: players.id })
  const release = db
    .update(players)
    .set({ tokenHash: null })
    .where(
      and(
        seat,
        eq(players.status, 'computer'),
        isNotNull(players.tokenHash),
        sql`exists (select 1 from ${players} as claimant where claimant.id = ${player.id} and claimant.token_hash = ${player.tokenHash})`,
      ),
    )
    .returning({ id: players.id })
  return { insert, release }
}

export function sqlitePlayerRepository(db: SqliteDatabase): PlayerRepository {
  const { matches, players } = schema
  return {
    /**
     * An insert fed by a select over the match row, so "the match is still in the lobby" is tested
     * by the same statement that writes the player. The seat counter was claimed a moment earlier
     * and the match may have started since; without this the player would land in a running match
     * that had already seated its roster, holding a seat nobody can play.
     */
    async create(player) {
      const rows = await db
        .insert(players)
        .select(
          db
            .select(playerValues(player))
            .from(matches)
            .where(and(eq(matches.id, player.matchId), eq(matches.status, 'lobby'))),
        )
        .returning({ id: players.id })
      return rows.length === 1
    },
    /**
     * Two statements run as one unit: D1 has no interactive transactions and runs a `batch`
     * atomically, and better-sqlite3 runs a transaction's callback synchronously. See
     * `lateSeatClaim` for the statements.
     */
    async createLate(player) {
      if (isD1(db)) {
        const { insert, release } = lateSeatClaim(db, player)
        const [inserted, released] = await db.batch([insert, release])
        return inserted.length === 1 ? { released: released.map((row) => row.id) } : null
      }
      return (db as unknown as BetterSQLite3Database<typeof schema>).transaction((tx) => {
        const { insert, release } = lateSeatClaim(tx, player)
        if (insert.all().length !== 1) return null
        return { released: release.all().map((row) => row.id) }
      })
    },
    async get(id) {
      return firstOrNull((await db.select().from(players).where(eq(players.id, id))).map(toPlayer))
    },
    async getByTokenHash(tokenHash) {
      return firstOrNull(
        (await db.select().from(players).where(eq(players.tokenHash, tokenHash))).map(toPlayer),
      )
    },
    async listByMatch(matchId) {
      const rows = await db
        .select()
        .from(players)
        .where(eq(players.matchId, matchId))
        .orderBy(asc(players.slot), asc(players.joinOrder), asc(players.id))
      return rows.map(toPlayer)
    },
    async listSeats(matchIds) {
      if (matchIds.length === 0) return []
      const rows = await db
        .select({
          matchId: players.matchId,
          slot: players.slot,
          status: players.status,
          tokenHash: players.tokenHash,
        })
        .from(players)
        .where(inArray(players.matchId, [...matchIds]))
      return rows.map((row) => ({
        matchId: row.matchId,
        slot: row.slot,
        computer: row.status === 'computer',
        vacated: row.status === 'computer' && row.tokenHash === null,
      }))
    },
    async setStatus(playerId, status) {
      await db.update(players).set({ status }).where(eq(players.id, playerId))
    },
    async transitionStatus(playerId, from, status, options) {
      const rows = await db
        .update(players)
        .set({ status })
        .where(
          and(
            eq(players.id, playerId),
            inArray(players.status, from),
            options?.holdingToken === true ? isNotNull(players.tokenHash) : undefined,
          ),
        )
        .returning({ id: players.id })
      return rows.length === 1
    },
    /**
     * "The match is still in the lobby" is tested by the statement that writes the profile, so an
     * update racing `start` either lands before the roster is seated or not at all.
     */
    async updateProfile(playerId, profile) {
      const inLobby = exists(
        db
          .select({ one: sql`1` })
          .from(matches)
          .where(and(eq(matches.id, players.matchId), eq(matches.status, 'lobby'))),
      )
      const rows = await db
        .update(players)
        .set({ displayName: profile.displayName, portraitId: profile.portraitId })
        .where(and(eq(players.id, playerId), eq(players.status, 'active'), inLobby))
        .returning({ id: players.id })
      return rows.length === 1
    },
    async revokeToken(playerId) {
      await db.update(players).set({ tokenHash: null }).where(eq(players.id, playerId))
    },
    async assignSlots(assignments) {
      if (assignments.length === 0) return
      // One statement: a CASE that maps each id to its slot. A loop of updates could commit some
      // seats and not others, and the transition that made the roster final has already landed.
      const cases = assignments.map(
        ({ playerId, slot }) => sql`when ${players.id} = ${playerId} then ${slot}`,
      )
      await db
        .update(players)
        .set({ slot: sql`case ${sql.join(cases, sql` `)} else ${players.slot} end` })
        .where(
          inArray(
            players.id,
            assignments.map(({ playerId }) => playerId),
          ),
        )
    },
    async delete(playerId) {
      const rows = await db
        .delete(players)
        .where(eq(players.id, playerId))
        .returning({ id: players.id })
      return rows.length === 1
    },
  }
}
