import type { TakeoverRepository } from '@chaos-overlords/kernel'
import { and, asc, eq, sql } from 'drizzle-orm'
import { toTakeoverVote } from '../shared/mappers'
import type { PostgresDatabase } from './database'
import * as schema from './schema'

/**
 * Absence prompts and their votes in the Postgres dialect.
 *
 * Its own module because it is a self-contained pair of tables with no overlap with the turn
 * barrier: the kernel asks it two questions (is any prompt open, what has each voter chosen) and
 * both are one indexed read, which is the whole point of keeping prompts as durable state instead
 * of replaying them out of the event log.
 */
export function postgresTakeoverRepository(db: PostgresDatabase): TakeoverRepository {
  const { takeoverPrompts, takeoverVotes } = schema
  return {
    async openPrompt(matchId, playerId, turn, openedAt) {
      const rows = await db
        .insert(takeoverPrompts)
        .values({ matchId, playerId, turn, openedAt })
        .onConflictDoNothing()
        .returning({ playerId: takeoverPrompts.playerId })
      return rows.length === 1
    },
    async closePrompt(matchId, playerId) {
      // Votes first: a death between the two leaves an open prompt with no votes, which is the
      // safe state, rather than votes that a later prompt for the same seat would inherit.
      await db
        .delete(takeoverVotes)
        .where(and(eq(takeoverVotes.matchId, matchId), eq(takeoverVotes.targetPlayerId, playerId)))
      await db
        .delete(takeoverPrompts)
        .where(and(eq(takeoverPrompts.matchId, matchId), eq(takeoverPrompts.playerId, playerId)))
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
     * earlier choice.
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
              castAt: sql`${castAt}`.as('cast_at'),
            })
            .from(takeoverPrompts)
            .where(
              and(
                eq(takeoverPrompts.matchId, matchId),
                eq(takeoverPrompts.playerId, targetPlayerId),
              ),
            )
            // Holds "the prompt is open" through the insert under READ COMMITTED: a prompt delete
            // that commits while this waits is seen, rather than voting against an old snapshot.
            .for('share'),
        )
        .onConflictDoUpdate({
          target: [
            takeoverVotes.matchId,
            takeoverVotes.targetPlayerId,
            takeoverVotes.voterPlayerId,
          ],
          set: { decision, castAt },
        })
        .returning({ voterPlayerId: takeoverVotes.voterPlayerId })
      return rows.length === 1
    },
    async listVotes(matchId, targetPlayerId) {
      const rows = await db
        .select()
        .from(takeoverVotes)
        .where(
          and(eq(takeoverVotes.matchId, matchId), eq(takeoverVotes.targetPlayerId, targetPlayerId)),
        )
        .orderBy(asc(takeoverVotes.voterPlayerId))
      return rows.map(toTakeoverVote)
    },
  }
}
