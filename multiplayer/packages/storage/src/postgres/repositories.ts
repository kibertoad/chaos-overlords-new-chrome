import type { LobbyListing } from '@chaos-overlords/contracts'
import type {
  EventRepository,
  MatchRepository,
  MultiplayerStorage,
  PersistedEvent,
  PlayerRepository,
  SnapshotRepository,
  TurnRepository,
} from '@chaos-overlords/kernel'
import {
  and,
  asc,
  desc,
  eq,
  exists,
  inArray,
  isNotNull,
  isNull,
  lt,
  lte,
  ne,
  or,
  sql,
} from 'drizzle-orm'
import type { PgDatabase, PgQueryResultHKT } from 'drizzle-orm/pg-core'
import { APPEND_ATTEMPTS, insertUnlessTaken, isUniqueViolation } from '../shared/constraints'
import {
  firstOrNull,
  toEvent,
  toMatch,
  toMatchInsert,
  toPlayer,
  toSnapshot,
  toTurn,
  toTurnOrders,
  toTurnReport,
} from '../shared/mappers'
import * as schema from './schema'

/**
 * The Postgres twin of `sqlite/repositories.ts`: the same statements over the pg-core schema.
 * Any driver Drizzle wraps as a `PgDatabase` works (node-postgres in the Node runtime). The two
 * files are kept in step by the conformance suite, not by sharing code across dialect types.
 */
export type PostgresDatabase = PgDatabase<PgQueryResultHKT, typeof schema>

export function createPostgresStorage(db: PostgresDatabase): MultiplayerStorage {
  return {
    matches: postgresMatchRepository(db),
    players: postgresPlayerRepository(db),
    turns: postgresTurnRepository(db),
    snapshots: postgresSnapshotRepository(db),
    events: postgresEventRepository(db),
  }
}

function postgresMatchRepository(db: PostgresDatabase): MatchRepository {
  const { matches, players } = schema
  return {
    async create(match) {
      return insertUnlessTaken(() => db.insert(matches).values(toMatchInsert(match)))
    },
    async get(id) {
      return firstOrNull((await db.select().from(matches).where(eq(matches.id, id))).map(toMatch))
    },
    async getByJoinCode(joinCode) {
      return firstOrNull(
        (await db.select().from(matches).where(eq(matches.joinCode, joinCode))).map(toMatch),
      )
    },
    async listPublicLobbies(limit): Promise<LobbyListing[]> {
      const rows = await db
        .select({
          id: matches.id,
          name: matches.name,
          hostDisplayName: players.displayName,
          playerCount: matches.seatCount,
          maxPlayers: matches.maxPlayers,
          passwordHash: matches.passwordHash,
          createdAt: matches.createdAt,
        })
        .from(matches)
        .innerJoin(players, eq(players.id, matches.hostPlayerId))
        .where(and(eq(matches.status, 'lobby'), eq(matches.visibility, 'public')))
        .orderBy(desc(matches.createdAt), asc(matches.id))
        .limit(limit)
      return rows.map(({ passwordHash, createdAt, ...rest }) => ({
        ...rest,
        passwordProtected: passwordHash !== null,
        createdAt: createdAt.toISOString(),
      }))
    },
    async claimSeat(matchId) {
      const rows = await db
        .update(matches)
        .set({
          seatCount: sql`${matches.seatCount} + 1`,
          joinCounter: sql`${matches.joinCounter} + 1`,
        })
        .where(
          and(
            eq(matches.id, matchId),
            eq(matches.status, 'lobby'),
            sql`${matches.seatCount} < ${matches.maxPlayers}`,
          ),
        )
        .returning({ joinCounter: matches.joinCounter })
      const row = rows[0]
      return row ? row.joinCounter - 1 : null
    },
    async releaseSeat(matchId) {
      await db
        .update(matches)
        .set({ seatCount: sql`${matches.seatCount} - 1` })
        .where(and(eq(matches.id, matchId), sql`${matches.seatCount} > 0`))
    },
    async deleteInactive(statuses, before, limit) {
      // `for update skip locked`: two sweeps (two server instances, or a cron overlapping itself)
      // would otherwise pick overlapping batches and deadlock on each other's row locks. Skipping
      // what a peer already holds means each pass simply collects a different batch.
      const collectable = db
        .select({ id: matches.id })
        .from(matches)
        .where(and(inArray(matches.status, [...statuses]), lt(matches.updatedAt, before)))
        .limit(limit)
        .for('update', { skipLocked: true })
      // Children cascade from the match row, so one delete takes the whole match with it.
      const rows = await db
        .delete(matches)
        .where(inArray(matches.id, collectable))
        .returning({ id: matches.id })
      return rows.length
    },
    async transition(matchId, from, patch) {
      const rows = await db
        .update(matches)
        .set(patch)
        .where(and(eq(matches.id, matchId), inArray(matches.status, [...from])))
        .returning({ id: matches.id })
      return rows.length === 1
    },
  }
}

function postgresPlayerRepository(db: PostgresDatabase): PlayerRepository {
  const { matches, players } = schema
  return {
    /**
     * An insert fed by a select over the match row, so "the match is still in the lobby" is tested
     * by the same statement that writes the player. The seat counter was claimed a moment earlier
     * and the match may have started since; without this the player would land in a running match
     * that had already seated its roster, holding a seat nobody can play.
     *
     * The select list is written out, which means it does NOT get Drizzle's column mapping: a
     * column added to `players` later has to be added here too, in the storage form the column
     * expects. The conformance suite compares a created player against its fixture field by field,
     * so a dropped column fails there rather than going unnoticed.
     */
    async create(player) {
      const rows = await db
        .insert(players)
        .select(
          db
            .select({
              id: sql`${player.id}`.as('id'),
              matchId: sql`${player.matchId}`.as('match_id'),
              slot: sql`${player.slot}`.as('slot'),
              joinOrder: sql`${player.joinOrder}`.as('join_order'),
              displayName: sql`${player.displayName}`.as('display_name'),
              tokenHash: sql`${player.tokenHash}`.as('token_hash'),
              status: sql`${player.status}`.as('status'),
              joinedAt: sql`${player.joinedAt}`.as('joined_at'),
            })
            .from(matches)
            .where(and(eq(matches.id, player.matchId), eq(matches.status, 'lobby'))),
        )
        .returning({ id: players.id })
      return rows.length === 1
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
    async setStatus(playerId, status) {
      await db.update(players).set({ status }).where(eq(players.id, playerId))
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
      await db.delete(players).where(eq(players.id, playerId))
    },
  }
}

function postgresTurnRepository(db: PostgresDatabase): TurnRepository {
  const { matches, turns, turnOrders, turnReports } = schema
  return {
    async open(turn, playerIds) {
      const created = await insertUnlessTaken(() => db.insert(turns).values(turn))
      if (playerIds.length > 0) {
        // Topped up rather than assumed: a re-run of the open step (a repaired seal) fills any row
        // an interrupted one never wrote, and a player who already has a row keeps it untouched.
        await db
          .insert(turnOrders)
          .values(
            playerIds.map((playerId) => ({ matchId: turn.matchId, turn: turn.number, playerId })),
          )
          .onConflictDoNothing()
      }
      return created
    },
    async get(matchId, number) {
      const rows = await db
        .select()
        .from(turns)
        .where(and(eq(turns.matchId, matchId), eq(turns.number, number)))
      return firstOrNull(rows.map(toTurn))
    },
    async submitOrders(matchId, number, playerId, submission) {
      const turnIsOpen = exists(
        db
          .select({ one: sql`1` })
          .from(turns)
          .where(
            and(eq(turns.matchId, matchId), eq(turns.number, number), eq(turns.status, 'open')),
          ),
      )
      const rows = await db
        .update(turnOrders)
        .set(submission)
        .where(
          and(
            eq(turnOrders.matchId, matchId),
            eq(turnOrders.turn, number),
            eq(turnOrders.playerId, playerId),
            turnIsOpen,
          ),
        )
        .returning({ playerId: turnOrders.playerId })
      return rows.length === 1
    },
    async getOrders(matchId, number, playerId) {
      const rows = await db
        .select()
        .from(turnOrders)
        .where(
          and(
            eq(turnOrders.matchId, matchId),
            eq(turnOrders.turn, number),
            eq(turnOrders.playerId, playerId),
          ),
        )
      return firstOrNull(rows.map(toTurnOrders))
    },
    async listOrders(matchId, number) {
      const rows = await db
        .select()
        .from(turnOrders)
        .where(and(eq(turnOrders.matchId, matchId), eq(turnOrders.turn, number)))
        .orderBy(asc(turnOrders.playerId))
      return rows.map(toTurnOrders)
    },
    async transition(matchId, number, from, patch) {
      const rows = await db
        .update(turns)
        .set(patch)
        .where(
          and(
            eq(turns.matchId, matchId),
            eq(turns.number, number),
            inArray(turns.status, [...from]),
          ),
        )
        .returning({ number: turns.number })
      return rows.length === 1
    },
    async rescheduleDeadline(matchId, number, deadlineAt) {
      const rows = await db
        .update(turns)
        .set({ deadlineAt })
        .where(and(eq(turns.matchId, matchId), eq(turns.number, number), eq(turns.status, 'open')))
        .returning({ number: turns.number })
      return rows.length === 1
    },
    async upsertReport(report) {
      await db
        .insert(turnReports)
        .values(report)
        .onConflictDoUpdate({
          target: [turnReports.matchId, turnReports.turn, turnReports.playerId],
          set: {
            stateHash: report.stateHash,
            finished: report.finished,
            reportedAt: report.reportedAt,
          },
        })
    },
    async listReports(matchId, number) {
      const rows = await db
        .select()
        .from(turnReports)
        .where(and(eq(turnReports.matchId, matchId), eq(turnReports.turn, number)))
        .orderBy(asc(turnReports.playerId))
      return rows.map(toTurnReport)
    },
    async listUnsettled(matchId) {
      const rows = await db
        .select()
        .from(turns)
        .where(and(eq(turns.matchId, matchId), inArray(turns.status, ['sealed', 'desynced'])))
        .orderBy(asc(turns.number))
      return rows.map(toTurn)
    },
    async listExpiredOpen(now, limit) {
      return db
        .select({ matchId: turns.matchId, number: turns.number })
        .from(turns)
        .where(
          and(eq(turns.status, 'open'), isNotNull(turns.deadlineAt), lte(turns.deadlineAt, now)),
        )
        .orderBy(asc(turns.deadlineAt))
        .limit(limit)
    },
    /**
     * Driven from `matches`, not from `turns`: a match whose current turn has no row at all (a
     * `start` that died before opening turn 1) is as stalled as one whose seal stopped halfway,
     * and an inner join would never see it. The repair opens the successor in both cases.
     */
    async listStalledSeals(limit) {
      return db
        .select({ matchId: matches.id, number: matches.currentTurn })
        .from(matches)
        .leftJoin(turns, and(eq(turns.matchId, matches.id), eq(turns.number, matches.currentTurn)))
        .where(
          and(
            inArray(matches.status, ['running', 'desynced']),
            or(isNull(turns.status), ne(turns.status, 'open')),
          ),
        )
        .orderBy(asc(matches.id))
        .limit(limit)
    },
  }
}

function postgresSnapshotRepository(db: PostgresDatabase): SnapshotRepository {
  const { snapshots } = schema
  return {
    async put(snapshot) {
      await db
        .insert(snapshots)
        .values(snapshot)
        .onConflictDoUpdate({
          target: [snapshots.matchId, snapshots.turn],
          set: {
            formatVersion: snapshot.formatVersion,
            stateHash: snapshot.stateHash,
            uploadedByPlayerId: snapshot.uploadedByPlayerId,
            uploadedAt: snapshot.uploadedAt,
            body: snapshot.body,
          },
        })
    },
    async get(matchId, turn) {
      const rows = await db
        .select()
        .from(snapshots)
        .where(and(eq(snapshots.matchId, matchId), eq(snapshots.turn, turn)))
      return firstOrNull(rows.map(toSnapshot))
    },
    async getLatest(matchId) {
      const rows = await db
        .select()
        .from(snapshots)
        .where(eq(snapshots.matchId, matchId))
        .orderBy(desc(snapshots.turn))
        .limit(1)
      return firstOrNull(rows.map(toSnapshot))
    },
    async prune(matchId, keep) {
      // The turns to keep are the newest `keep`; everything strictly below the oldest of them goes.
      const kept = await db
        .select({ turn: snapshots.turn })
        .from(snapshots)
        .where(eq(snapshots.matchId, matchId))
        .orderBy(desc(snapshots.turn))
        .limit(keep)
      const oldestKept = kept.at(-1)?.turn
      if (oldestKept === undefined || kept.length < keep) return 0
      const rows = await db
        .delete(snapshots)
        .where(and(eq(snapshots.matchId, matchId), lt(snapshots.turn, oldestKept)))
        .returning({ turn: snapshots.turn })
      return rows.length
    },
  }
}

function postgresEventRepository(db: PostgresDatabase): EventRepository {
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
