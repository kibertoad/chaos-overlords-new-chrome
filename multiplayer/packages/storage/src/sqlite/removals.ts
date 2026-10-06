import type { RemovalDecision, RemovalVoteRepository } from '@chaos-overlords/kernel'
import { and, asc, eq } from 'drizzle-orm'
import type { SqliteDatabase } from './database'
import * as schema from './schema'

/**
 * Votes to remove a seat, in the SQLite dialect. One table and no prompt row: the kernel reads
 * whether a vote is open off the votes themselves, and closing one is a single delete whose row
 * count says which of two racing callers announces it.
 */
export function sqliteRemovalVoteRepository(db: SqliteDatabase): RemovalVoteRepository {
  const { removalVotes } = schema
  const onSeat = (matchId: string, targetPlayerId: string) =>
    and(eq(removalVotes.matchId, matchId), eq(removalVotes.targetPlayerId, targetPlayerId))
  return {
    async castVote({ matchId, targetPlayerId, voterPlayerId, decision, castAt }) {
      await db
        .insert(removalVotes)
        .values({ matchId, targetPlayerId, voterPlayerId, decision, castAt })
        .onConflictDoUpdate({
          target: [removalVotes.matchId, removalVotes.targetPlayerId, removalVotes.voterPlayerId],
          set: { decision, castAt },
        })
    },
    async listVotes(matchId, targetPlayerId) {
      const rows = await db
        .select()
        .from(removalVotes)
        .where(onSeat(matchId, targetPlayerId))
        .orderBy(asc(removalVotes.voterPlayerId))
      return rows.map((row) => ({ ...row, decision: row.decision as RemovalDecision }))
    },
    async listTargets(matchId) {
      const rows = await db
        .selectDistinct({ targetPlayerId: removalVotes.targetPlayerId })
        .from(removalVotes)
        .where(eq(removalVotes.matchId, matchId))
        .orderBy(asc(removalVotes.targetPlayerId))
      return rows.map((row) => row.targetPlayerId)
    },
    async clear(matchId, targetPlayerId) {
      const rows = await db
        .delete(removalVotes)
        .where(onSeat(matchId, targetPlayerId))
        .returning({ voterPlayerId: removalVotes.voterPlayerId })
      return rows.length > 0
    },
  }
}
