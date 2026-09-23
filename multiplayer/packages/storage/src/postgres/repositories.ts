import type {
  MatchRepository,
  MultiplayerStorage,
  Player,
  PlayerRepository,
  PublicLobbyRow,
  SnapshotRepository,
  TurnRepository,
} from '@chaos-overlords/kernel'
import {
  and,
  asc,
  desc,
  eq,
  exists,
  gte,
  inArray,
  isNotNull,
  isNull,
  lt,
  lte,
  ne,
  notExists,
  or,
  type SQL,
  type SQLWrapper,
  sql,
} from 'drizzle-orm'
import { insertUnlessTaken } from '../shared/constraints'
import {
  firstOrNull,
  toMatch,
  toMatchInsert,
  toPlayer,
  toPublicLobbyRow,
  toSnapshot,
  toSnapshotSummary,
  toTurn,
  toTurnOrders,
  toTurnReport,
} from '../shared/mappers'
import type { PostgresDatabase } from './database'
import { postgresEventRepository } from './events'
import * as schema from './schema'
import { postgresTakeoverRepository } from './takeovers'

/**
 * The Postgres twin of `sqlite/repositories.ts`: the same statements over the pg-core schema.
 * Any driver Drizzle wraps as a `PgDatabase` works (node-postgres in the Node runtime). The two
 * files are kept in step by the conformance suite, not by sharing code across dialect types.
 */
export type { PostgresDatabase }

export function createPostgresStorage(db: PostgresDatabase): MultiplayerStorage {
  return {
    matches: postgresMatchRepository(db),
    players: postgresPlayerRepository(db),
    turns: postgresTurnRepository(db),
    takeovers: postgresTakeoverRepository(db),
    snapshots: postgresSnapshotRepository(db),
    events: postgresEventRepository(db),
  }
}

function postgresMatchRepository(db: PostgresDatabase): MatchRepository {
  const { matches, players, snapshots } = schema

  /**
   * The one statement both join doors take a `joinOrder` from: when `condition` holds, move
   * `joinCounter` to `next` (setting `extra` alongside) and return the position just taken, or null
   * when the condition refused the claim.
   */
  async function takeJoinOrder(
    condition: SQL | undefined,
    next: SQL,
    extra: { seatCount?: SQL } = {},
  ): Promise<number | null> {
    const rows = await db
      .update(matches)
      .set({ ...extra, joinCounter: next })
      .where(condition)
      .returning({ joinCounter: matches.joinCounter })
    const row = rows[0]
    return row ? row.joinCounter - 1 : null
  }

  // Children cascade from the match row, so one delete takes the whole match with it.
  const deleteCollectable = async (collectable: SQLWrapper): Promise<number> => {
    const rows = await db
      .delete(matches)
      .where(inArray(matches.id, collectable))
      .returning({ id: matches.id })
    return rows.length
  }
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
    /** See the SQLite twin: the counts and the snapshot test ride in the listing statement. */
    async listPublicLobbies(limit): Promise<PublicLobbyRow[]> {
      const humanSeats = sql<number>`(select count(*) from ${players} where ${players.matchId} = ${matches.id} and ${players.status} in ('active', 'takeoverPending'))`
      const rows = await db
        .select({
          id: matches.id,
          joinCode: matches.joinCode,
          name: matches.name,
          hostDisplayName: players.displayName,
          seatCount: matches.seatCount,
          humanCount: humanSeats,
          maxPlayers: matches.maxPlayers,
          passwordHash: matches.passwordHash,
          status: matches.status,
          settings: matches.settings,
          createdAt: matches.createdAt,
          hasSnapshot: exists(
            db
              .select({ one: sql`1` })
              .from(snapshots)
              .where(eq(snapshots.matchId, matches.id)),
          ),
        })
        .from(matches)
        .innerJoin(players, eq(players.id, matches.hostPlayerId))
        .where(
          and(
            inArray(matches.status, ['lobby', 'running']),
            eq(matches.visibility, 'public'),
            or(ne(matches.status, 'running'), sql`${humanSeats} > 0`),
          ),
        )
        .orderBy(desc(matches.createdAt), asc(matches.id))
        .limit(limit)
      return rows.map(toPublicLobbyRow)
    },
    async claimSeat(matchId) {
      return takeJoinOrder(
        and(
          eq(matches.id, matchId),
          eq(matches.status, 'lobby'),
          sql`${matches.seatCount} < ${matches.maxPlayers}`,
        ),
        sql`${matches.joinCounter} + 1`,
        { seatCount: sql`${matches.seatCount} + 1` },
      )
    },
    async claimLateJoinOrder(matchId) {
      // Never below a position a stored player already holds: late joiners seated before the
      // counter covered them took `joinCounter` without advancing it, so the counter alone could
      // hand one of their positions out again.
      const nextFree = sql`(select coalesce(max(${players.joinOrder}) + 1, 0) from ${players} where ${players.matchId} = ${matchId})`
      return takeJoinOrder(
        and(eq(matches.id, matchId), eq(matches.status, 'running')),
        sql`greatest(${matches.joinCounter}, ${nextFree}) + 1`,
      )
    },
    async releaseSeat(matchId) {
      await db
        .update(matches)
        .set({ seatCount: sql`${matches.seatCount} - 1` })
        .where(and(eq(matches.id, matchId), sql`${matches.seatCount} > 0`))
    },
    async updateSettings(matchId, settings, updatedAt) {
      const rows = await db
        .update(matches)
        .set({
          name: settings.name,
          visibility: settings.visibility,
          maxPlayers: settings.maxPlayers,
          settings,
          updatedAt,
        })
        .where(
          and(
            eq(matches.id, matchId),
            eq(matches.status, 'lobby'),
            sql`${matches.seatCount} <= ${settings.maxPlayers}`,
          ),
        )
        .returning({ id: matches.id })
      return rows.length === 1
    },
    async updateRuntimeGameSettings(matchId, gameSettings, updatedAt) {
      const rows = await db
        .update(matches)
        .set({
          settings: sql`jsonb_set(${matches.settings}, '{gameSettings}', ${JSON.stringify(gameSettings)}::jsonb)`,
          updatedAt,
        })
        .where(and(eq(matches.id, matchId), inArray(matches.status, ['running', 'desynced'])))
        .returning({ id: matches.id })
      return rows.length === 1
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
      return deleteCollectable(collectable)
    },
    /**
     * The same delete, aimed at a live match nobody is in any more. The roster test is a
     * `not exists` over the players rather than a count: one active row is enough to spare the
     * match, and asking whether any exists stops at the first.
     */
    async deleteAbandonedLive(before, limit, requireEmptyRoster) {
      const emptyRoster = notExists(
        db
          .select({ one: sql`1` })
          .from(players)
          .where(and(eq(players.matchId, matches.id), eq(players.status, 'active'))),
      )
      const collectable = db
        .select({ id: matches.id })
        .from(matches)
        .where(
          and(
            inArray(matches.status, ['running', 'desynced']),
            lt(matches.updatedAt, before),
            ...(requireEmptyRoster ? [emptyRoster] : []),
          ),
        )
        .limit(limit)
        .for('update', { skipLocked: true })
      return deleteCollectable(collectable)
    },
    async listDesynced(limit, touchedSince) {
      const rows = await db
        .select({ id: matches.id })
        .from(matches)
        .where(
          and(
            eq(matches.status, 'desynced'),
            ...(touchedSince ? [gte(matches.updatedAt, touchedSince)] : []),
          ),
        )
        .orderBy(asc(matches.updatedAt))
        .limit(limit)
      return rows.map((row) => row.id)
    },
    async transition(matchId, from, patch) {
      const rows = await db
        .update(matches)
        .set(patch)
        .where(and(eq(matches.id, matchId), inArray(matches.status, [...from])))
        .returning({ id: matches.id })
      return rows.length === 1
    },
    async advanceCurrentTurn(matchId, number, updatedAt) {
      const rows = await db
        .update(matches)
        .set({ currentTurn: number, updatedAt })
        .where(
          and(
            eq(matches.id, matchId),
            inArray(matches.status, ['running', 'desynced']),
            lt(matches.currentTurn, number),
          ),
        )
        .returning({ id: matches.id })
      return rows.length === 1
    },
  }
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
    async createLate(player) {
      const occupied = db
        .select({ id: players.id })
        .from(players)
        .where(and(eq(players.matchId, player.matchId), eq(players.slot, player.slot)))
      const rows = await db
        .insert(players)
        .select(
          db
            .select(playerValues(player))
            .from(matches)
            .where(
              and(
                eq(matches.id, player.matchId),
                eq(matches.status, 'running'),
                notExists(occupied),
                // Capacity in the same statement as the insert; see the SQLite twin.
                sql`(select count(*) from ${players} where ${players.matchId} = ${player.matchId}) < ${matches.maxPlayers}`,
              ),
            ),
        )
        .onConflictDoNothing()
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
    async listSeats(matchIds) {
      if (matchIds.length === 0) return []
      return db
        .select({ matchId: players.matchId, slot: players.slot })
        .from(players)
        .where(inArray(players.matchId, [...matchIds]))
    },
    async setStatus(playerId, status) {
      await db.update(players).set({ status }).where(eq(players.id, playerId))
    },
    async transitionStatus(playerId, from, status) {
      const rows = await db
        .update(players)
        .set({ status })
        .where(and(eq(players.id, playerId), inArray(players.status, from)))
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

/** The orders half of the turn repository; see the SQLite twin. */
function postgresTurnOrderMethods(
  db: PostgresDatabase,
): Pick<
  TurnRepository,
  'submitOrders' | 'getOrders' | 'getOrderSummary' | 'listOrders' | 'listOrderSummaries'
> {
  const { turns, turnOrders } = schema
  const orderSummaryColumns = {
    matchId: turnOrders.matchId,
    turn: turnOrders.turn,
    playerId: turnOrders.playerId,
    ordersHash: turnOrders.ordersHash,
    ready: turnOrders.ready,
  }
  return {
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
    async getOrderSummary(matchId, number, playerId) {
      const rows = await db
        .select(orderSummaryColumns)
        .from(turnOrders)
        .where(
          and(
            eq(turnOrders.matchId, matchId),
            eq(turnOrders.turn, number),
            eq(turnOrders.playerId, playerId),
          ),
        )
      return firstOrNull(rows)
    },
    async listOrders(matchId, number) {
      const rows = await db
        .select()
        .from(turnOrders)
        .where(and(eq(turnOrders.matchId, matchId), eq(turnOrders.turn, number)))
        .orderBy(asc(turnOrders.playerId))
      return rows.map(toTurnOrders)
    },
    async listOrderSummaries(matchId, number) {
      return db
        .select(orderSummaryColumns)
        .from(turnOrders)
        .where(and(eq(turnOrders.matchId, matchId), eq(turnOrders.turn, number)))
        .orderBy(asc(turnOrders.playerId))
    },
  }
}

function postgresTurnRepository(db: PostgresDatabase): TurnRepository {
  const { matches, turns, turnOrders, turnReports } = schema
  return {
    ...postgresTurnOrderMethods(db),
    async open(turn, playerIds) {
      const created = await insertUnlessTaken(() => db.insert(turns).values(turn))
      if (playerIds.length > 0) {
        // Topped up rather than assumed: a re-run of the open step (a repaired seal) fills any row
        // an interrupted one never wrote. One statement for the whole roster, gated on the turn
        // still being open; the shared lock keeps a concurrent seal behind this insert.
        const seats = sql.join(
          playerIds.map((playerId) => sql`(${playerId})`),
          sql`, `,
        )
        await db
          .insert(turnOrders)
          .select(
            sql`select ${turns.matchId}, ${turns.number}, seat.player_id, null, null, false, null
              from ${turns} cross join (values ${seats}) as seat(player_id)
              where ${turns.matchId} = ${turn.matchId} and ${turns.number} = ${turn.number}
                and ${turns.status} = 'open'
              for share of ${turns}`,
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
    async freezeSeal(matchId, number, frozen) {
      const rows = await db
        .update(turns)
        .set(frozen)
        .where(
          and(
            eq(turns.matchId, matchId),
            eq(turns.number, number),
            ne(turns.status, 'open'),
            // The compare-and-swap is on what is being frozen, not on the status; see the twin.
            isNull(turns.orderSetHash),
          ),
        )
        .returning({ number: turns.number })
      return rows.length === 1
    },
    async claimDesyncAnnouncement(matchId, number, at) {
      const rows = await db
        .update(turns)
        .set({ desyncedAt: at })
        .where(
          and(
            eq(turns.matchId, matchId),
            eq(turns.number, number),
            eq(turns.status, 'desynced'),
            isNull(turns.desyncedAt),
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
    /** Conditional on the turn still awaiting a verdict; see the SQLite twin. */
    async upsertReport(report) {
      const rows = await db
        .insert(turnReports)
        .select(
          db
            .select({
              matchId: sql`${report.matchId}`.as('match_id'),
              turn: sql`${report.turn}`.as('turn'),
              playerId: sql`${report.playerId}`.as('player_id'),
              stateHash: sql`${report.stateHash}`.as('state_hash'),
              finished: sql`${report.finished}`.as('finished'),
              reportedAt: sql`${report.reportedAt}`.as('reported_at'),
            })
            .from(turns)
            .where(
              and(
                eq(turns.matchId, report.matchId),
                eq(turns.number, report.turn),
                inArray(turns.status, ['sealed', 'desynced']),
              ),
            ),
        )
        .onConflictDoUpdate({
          target: [turnReports.matchId, turnReports.turn, turnReports.playerId],
          set: {
            stateHash: report.stateHash,
            finished: report.finished,
            reportedAt: report.reportedAt,
          },
        })
        .returning({ playerId: turnReports.playerId })
      return rows.length === 1
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
        .innerJoin(matches, eq(matches.id, turns.matchId))
        .where(
          and(
            eq(turns.status, 'open'),
            isNotNull(turns.deadlineAt),
            lte(turns.deadlineAt, now),
            // Only a running match can seal; a desynced one would squat at the head of this page.
            eq(matches.status, 'running'),
          ),
        )
        .orderBy(asc(turns.deadlineAt))
        .limit(limit)
    },
    /**
     * Driven from `matches`, not from `turns`: a match whose current turn has no row at all (a
     * `start` that died before opening turn 1) is as stalled as one whose seal stopped halfway,
     * and an inner join would never see it. The repair opens the successor in both cases.
     */
    async listStalledSeals(limit, touchedSince) {
      return (
        db
          .select({ matchId: matches.id, number: matches.currentTurn })
          .from(matches)
          .leftJoin(
            turns,
            and(eq(turns.matchId, matches.id), eq(turns.number, matches.currentTurn)),
          )
          .where(
            and(
              inArray(matches.status, ['running', 'desynced']),
              or(isNull(turns.status), ne(turns.status, 'open')),
              ...(touchedSince ? [gte(matches.updatedAt, touchedSince)] : []),
            ),
          )
          // Ordered by the recency the window is taken on; see the SQLite twin.
          .orderBy(desc(matches.updatedAt), asc(matches.id))
          .limit(limit)
      )
    },
  }
}

function postgresSnapshotRepository(db: PostgresDatabase): SnapshotRepository {
  const { snapshots } = schema
  const snapshotSummaryColumns = {
    matchId: snapshots.matchId,
    turn: snapshots.turn,
    formatVersion: snapshots.formatVersion,
    protocolVersion: snapshots.protocolVersion,
    sessionVersion: snapshots.sessionVersion,
    stateHash: snapshots.stateHash,
    uploadedByPlayerId: snapshots.uploadedByPlayerId,
    uploadedAt: snapshots.uploadedAt,
  }
  return {
    async put(snapshot) {
      await db
        .insert(snapshots)
        .values(snapshot)
        .onConflictDoUpdate({
          target: [snapshots.matchId, snapshots.turn],
          set: {
            formatVersion: snapshot.formatVersion,
            // The versions move with the bytes they describe; see the SQLite twin.
            protocolVersion: snapshot.protocolVersion,
            sessionVersion: snapshot.sessionVersion,
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
    async getSummary(matchId, turn) {
      const rows = await db
        .select(snapshotSummaryColumns)
        .from(snapshots)
        .where(and(eq(snapshots.matchId, matchId), eq(snapshots.turn, turn)))
      return firstOrNull(rows.map(toSnapshotSummary))
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
    async getLatestSummary(matchId) {
      const rows = await db
        .select(snapshotSummaryColumns)
        .from(snapshots)
        .where(eq(snapshots.matchId, matchId))
        .orderBy(desc(snapshots.turn))
        .limit(1)
      return firstOrNull(rows.map(toSnapshotSummary))
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
