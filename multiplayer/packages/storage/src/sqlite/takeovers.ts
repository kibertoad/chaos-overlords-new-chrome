import { ABSENT_HUMAN_STATUSES, type TakeoverRepository } from '@chaos-overlords/kernel'
import { and, asc, eq, gte, inArray, notExists, sql } from 'drizzle-orm'
import { toTakeoverVote } from '../shared/mappers'
import type { SqliteDatabase } from './database'
import * as schema from './schema'

/**
 * Absence prompts and their votes in the SQLite dialect.
 *
 * Its own module because it is a self-contained pair of tables with no overlap with the turn
 * barrier: the kernel asks it two questions (is any prompt open, what has each voter chosen) and
 * both are one indexed read, which is the whole point of keeping prompts as durable state instead
 * of replaying them out of the event log.
 */
export function sqliteTakeoverRepository(db: SqliteDatabase): TakeoverRepository {
  const { players, takeoverPrompts, takeoverVotes } = schema
  /** Votes on a seat with no prompt on file; an open prompt keeps its own votes untouched. */
  const deleteVotesWithoutPrompt = (matchId: string, playerId: string) =>
    db.delete(takeoverVotes).where(
      and(
        eq(takeoverVotes.matchId, matchId),
        eq(takeoverVotes.targetPlayerId, playerId),
        notExists(
          db
            .select({ one: sql`1` })
            .from(takeoverPrompts)
            .where(
              and(eq(takeoverPrompts.matchId, matchId), eq(takeoverPrompts.playerId, playerId)),
            ),
        ),
      ),
    )
  return {
    async openPrompt(matchId, playerId, turn, openedAt) {
      // A crash after deleting the old prompt but before deleting its votes can leave orphans.
      // Clear them before a new prompt uses this key.
      await deleteVotesWithoutPrompt(matchId, playerId)
      const rows = await db
        .insert(takeoverPrompts)
        .select(
          db
            .select({
              matchId: sql`${matchId}`.as('match_id'),
              playerId: sql`${playerId}`.as('player_id'),
              turn: sql`${turn}`.as('turn'),
              openedAt: sql`${openedAt.getTime()}`.as('opened_at'),
            })
            .from(players)
            .where(
              and(
                eq(players.id, playerId),
                eq(players.matchId, matchId),
                inArray(players.status, [...ABSENT_HUMAN_STATUSES]),
              ),
            ),
        )
        .onConflictDoNothing()
        .returning({ playerId: takeoverPrompts.playerId })
      return rows.length === 1
    },
    async closePrompt(matchId, playerId) {
      // Close first so no concurrent vote can be admitted between vote cleanup and prompt removal,
      // and leave alone the votes of a prompt reopened for the seat between the two statements.
      await db
        .delete(takeoverPrompts)
        .where(and(eq(takeoverPrompts.matchId, matchId), eq(takeoverPrompts.playerId, playerId)))
      await deleteVotesWithoutPrompt(matchId, playerId)
    },
    async hasOpenPrompts(matchId) {
      const rows = await db
        .select({ playerId: takeoverPrompts.playerId })
        .from(takeoverPrompts)
        .where(eq(takeoverPrompts.matchId, matchId))
        .limit(1)
      return rows.length > 0
    },
    async listOpenPrompts(matchId) {
      const rows = await db
        .select({ playerId: takeoverPrompts.playerId })
        .from(takeoverPrompts)
        .where(eq(takeoverPrompts.matchId, matchId))
        .orderBy(asc(takeoverPrompts.playerId))
      return rows.map((row) => row.playerId)
    },
    /**
     * An insert fed by a select over the prompt row, so "the prompt is open" is tested by the same
     * statement that writes the vote; the conflict clause makes it a replacement of the voter's
     * earlier choice. The stored `castAt` is never before the prompt's `openedAt`: a vote that
     * opened the prompt itself read the clock first, and `listVotes` only counts votes cast within
     * the current prompt's lifetime.
     */
    async castVote({ matchId, targetPlayerId, voterPlayerId, decision, castAt }) {
      const rows = await db
        .insert(takeoverVotes)
        .select(
          db
            .select({
              matchId: sql`${matchId}`.as('match_id'),
              targetPlayerId: sql`${targetPlayerId}`.as('target_player_id'),
              voterPlayerId: sql`${voterPlayerId}`.as('voter_player_id'),
              decision: sql`${decision}`.as('decision'),
              castAt: sql`max(${castAt.getTime()}, ${takeoverPrompts.openedAt})`.as('cast_at'),
            })
            .from(takeoverPrompts)
            .where(
              and(
                eq(takeoverPrompts.matchId, matchId),
                eq(takeoverPrompts.playerId, targetPlayerId),
              ),
            ),
        )
        .onConflictDoUpdate({
          target: [
            takeoverVotes.matchId,
            takeoverVotes.targetPlayerId,
            takeoverVotes.voterPlayerId,
          ],
          set: { decision, castAt: sql`excluded.cast_at` },
        })
        .returning({ voterPlayerId: takeoverVotes.voterPlayerId })
      return rows.length === 1
    },
    async listVotes(matchId, targetPlayerId) {
      const rows = await db
        .select({
          matchId: takeoverVotes.matchId,
          targetPlayerId: takeoverVotes.targetPlayerId,
          voterPlayerId: takeoverVotes.voterPlayerId,
          decision: takeoverVotes.decision,
          castAt: takeoverVotes.castAt,
        })
        .from(takeoverVotes)
        .innerJoin(
          takeoverPrompts,
          and(
            eq(takeoverPrompts.matchId, takeoverVotes.matchId),
            eq(takeoverPrompts.playerId, takeoverVotes.targetPlayerId),
          ),
        )
        .where(
          and(
            eq(takeoverVotes.matchId, matchId),
            eq(takeoverVotes.targetPlayerId, targetPlayerId),
            gte(takeoverVotes.castAt, takeoverPrompts.openedAt),
          ),
        )
        .orderBy(asc(takeoverVotes.voterPlayerId))
      return rows.map(toTakeoverVote)
    },
  }
}
