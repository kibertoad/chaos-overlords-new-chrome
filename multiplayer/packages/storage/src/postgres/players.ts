import type { Player, PlayerRepository } from '@chaos-overlords/kernel'
import {
  and,
  asc,
  eq,
  exists,
  inArray,
  isNotNull,
  isNull,
  ne,
  notExists,
  or,
  sql,
} from 'drizzle-orm'
import { firstOrNull, toPlayer } from '../shared/mappers'
import type { PostgresDatabase } from './database'
import * as schema from './schema'

/** Players and their seats in the Postgres dialect; see the SQLite twin. */

/** Thrown inside the late-claim transaction to roll it back when the claim is refused. */
const LATE_CLAIM_REFUSED = new Error('late seat claim refused')

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
    joinedAt: sql`${player.joinedAt}`.as('joined_at'),
    comlinkKey: sql`${player.comlinkKey}`.as('comlink_key'),
  }
}

export function postgresPlayerRepository(db: PostgresDatabase): PlayerRepository {
  const { matches, players } = schema
  return {
    /**
     * An insert fed by a select over the match row, so "the match is still in the lobby" is tested
     * by the same statement that writes the player. A row lock makes this guard hold through the
     * insert under READ COMMITTED, including when the match starts concurrently.
     */
    async create(player) {
      const rows = await db
        .insert(players)
        .select(
          db
            .select(playerValues(player))
            .from(matches)
            .where(and(eq(matches.id, player.matchId), eq(matches.status, 'lobby')))
            .for('share'),
        )
        .returning({ id: players.id })
      return rows.length === 1
    },
    async createLate(player) {
      // Lock first, then release, then run the guarded insert as a new READ COMMITTED statement.
      // Putting the count in the locking SELECT would still let two joins evaluate it against the
      // same old snapshot. `no key update` serializes late joins without blocking the foreign-key
      // checks every other insert under this match takes.
      //
      // The release comes before the insert because `rejoin` does not take the match lock. Its
      // update and the release meet on the former player's row: one waits for the other, and a
      // `rejoin` that commits first leaves a human on the seat for the insert's fresh snapshot to
      // see. Released after the insert instead, a `rejoin` committed between the two would have
      // been skipped by the release and missed by the insert, and both players would hold the
      // seat. A refused insert rolls the release back.
      try {
        return await db.transaction(async (tx) => {
          const locked = await tx
            .select({ id: matches.id })
            .from(matches)
            .where(and(eq(matches.id, player.matchId), eq(matches.status, 'running')))
            .for('no key update')
          if (locked.length === 0) throw LATE_CLAIM_REFUSED
          const released = await tx
            .update(players)
            .set({ tokenHash: null })
            .where(
              and(
                eq(players.matchId, player.matchId),
                eq(players.slot, player.slot),
                eq(players.status, 'computer'),
                isNotNull(players.tokenHash),
              ),
            )
            .returning({ id: players.id })
          const occupied = tx
            .select({ id: players.id })
            .from(players)
            .where(
              and(eq(players.matchId, player.matchId), eq(players.slot, player.slot), holdsClaim()),
            )
          const rows = await tx
            .insert(players)
            .select(
              tx
                .select(playerValues(player))
                .from(matches)
                .where(
                  and(
                    eq(matches.id, player.matchId),
                    eq(matches.status, 'running'),
                    notExists(occupied),
                    // Capacity in the same statement as the insert; see the SQLite twin.
                    sql`(select count(*) from ${players} where ${players.matchId} = ${player.matchId} and ${holdsClaim()}) < ${matches.maxPlayers}`,
                  ),
                ),
            )
            .onConflictDoNothing()
            .returning({ id: players.id })
          if (rows.length !== 1) throw LATE_CLAIM_REFUSED
          return { released: released.map((row) => row.id) }
        })
      } catch (error) {
        if (error === LATE_CLAIM_REFUSED) return null
        throw error
      }
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
     * The share lock on the match row is what makes that hold under READ COMMITTED: a `start`
     * racing this either waits for it, or makes it re-read the row and find the match running.
     */
    async updateProfile(playerId, profile) {
      const inLobby = exists(
        db
          .select({ one: sql`1` })
          .from(matches)
          .where(and(eq(matches.id, players.matchId), eq(matches.status, 'lobby')))
          .for('share'),
      )
      const rows = await db
        .update(players)
        .set({ displayName: profile.displayName, portraitId: profile.portraitId })
        .where(and(eq(players.id, playerId), eq(players.status, 'active'), inLobby))
        .returning({ id: players.id })
      return rows.length === 1
    },
    /** Writes only a key that differs, so republishing the stored key changes nothing. */
    async setComlinkKey(playerId, comlinkKey) {
      const rows = await db
        .update(players)
        .set({ comlinkKey })
        .where(
          and(
            eq(players.id, playerId),
            or(isNull(players.comlinkKey), ne(players.comlinkKey, comlinkKey)),
          ),
        )
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
