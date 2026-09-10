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
import { and, asc, desc, eq, exists, inArray, isNotNull, lt, lte, ne, sql } from 'drizzle-orm'
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
      const collectable = db
        .select({ id: matches.id })
        .from(matches)
        .where(and(inArray(matches.status, [...statuses]), lt(matches.updatedAt, before)))
        .limit(limit)
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
  const { players } = schema
  return {
    async create(player) {
      await db.insert(players).values(player)
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
      for (const { playerId, slot } of assignments) {
        await db.update(players).set({ slot }).where(eq(players.id, playerId))
      }
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
    async listStalledSeals(limit) {
      return db
        .select({ matchId: turns.matchId, number: turns.number })
        .from(turns)
        .innerJoin(matches, eq(matches.id, turns.matchId))
        .where(
          and(
            inArray(matches.status, ['running', 'desynced']),
            eq(turns.number, matches.currentTurn),
            ne(turns.status, 'open'),
          ),
        )
        .orderBy(asc(turns.matchId), asc(turns.number))
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
