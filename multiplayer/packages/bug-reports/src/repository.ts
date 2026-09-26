import type { BugReportCodec, SubmitBugReportRequest } from '@chaos-overlords/contracts'
import { asc, desc, eq, gte, inArray, lt, sql } from 'drizzle-orm'
import type { BaseSQLiteDatabase } from 'drizzle-orm/sqlite-core'
import type { BugReportRepository, StoredBugReport } from './ports'
import type * as schema from './schema'
import { bugReports } from './schema'

/**
 * Either SQLite driver, exactly as the multiplayer storage takes either: better-sqlite3 (`'sync'`)
 * or D1 (`'async'`), with one implementation over both because awaiting a synchronous result is a
 * no-op. One migration lineage, two drivers — and a different database from the match one.
 */
export type BugReportDatabase = BaseSQLiteDatabase<'sync' | 'async', unknown, typeof schema>

type Row = typeof bugReports.$inferSelect

export function createBugReportRepository(db: BugReportDatabase): BugReportRepository {
  return {
    async insert(report) {
      await db.insert(bugReports).values(toRow(report))
    },

    /**
     * An insert fed by a select with no table, whose `where` is the budget: SQLite (and D1, which
     * runs one statement at a time) evaluates the sum and writes the row as one step. Values are
     * encoded by hand, as the column modes would encode them, because a raw select bypasses them.
     */
    async insertWithinBudget(report, since, budget) {
      const row = toRow(report)
      const values: unknown[] = [
        row.id,
        report.receivedAt.getTime(),
        row.message,
        row.clientVersion,
        row.clientPlatform,
        row.context === null || row.context === undefined ? null : JSON.stringify(row.context),
        row.stateCodec ?? null,
        row.stateFormatVersion ?? null,
        row.stateUncompressedBytes ?? null,
        row.stateCompressedBytes ?? null,
        row.stateSha256 ?? null,
        row.stateAnonymized === null || row.stateAnonymized === undefined
          ? null
          : row.stateAnonymized
            ? 1
            : 0,
        row.blobKey ?? null,
        row.body ?? null,
      ]
      const incoming = row.stateCompressedBytes ?? 0
      const rows = await db
        .insert(bugReports)
        .select(
          sql`select ${sql.join(
            values.map((value) => sql`${value}`),
            sql`, `,
          )} where (select coalesce(sum(${bugReports.stateCompressedBytes}), 0) from ${bugReports}
            where ${bugReports.receivedAt} >= ${since.getTime()}) + ${incoming} <= ${budget}`,
        )
        .returning({ id: bugReports.id })
      return rows.length === 1
    },

    async get(id) {
      const rows = await db.select().from(bugReports).where(eq(bugReports.id, id)).limit(1)
      const row = rows[0]
      return row ? fromRow(row) : null
    },

    async list(limit) {
      const rows = await db
        .select()
        .from(bugReports)
        .orderBy(desc(bugReports.receivedAt))
        .limit(limit)
      return rows.map(fromRow)
    },

    async bytesSince(since) {
      const rows = await db
        .select({ bytes: sql<number>`coalesce(sum(${bugReports.stateCompressedBytes}), 0)` })
        .from(bugReports)
        .where(gte(bugReports.receivedAt, since))
      return Number(rows[0]?.bytes ?? 0)
    },

    async deleteBefore(before, limit) {
      const doomed = await db
        .select({ id: bugReports.id, blobKey: bugReports.blobKey })
        .from(bugReports)
        .where(lt(bugReports.receivedAt, before))
        .orderBy(asc(bugReports.receivedAt))
        .limit(limit)
      if (doomed.length === 0) return []
      await db.delete(bugReports).where(
        inArray(
          bugReports.id,
          doomed.map((row) => row.id),
        ),
      )
      return doomed.flatMap((row) => (row.blobKey === null ? [] : [row.blobKey]))
    },
  }
}

function toRow(report: StoredBugReport): typeof bugReports.$inferInsert {
  const state = report.state
  return {
    id: report.id,
    receivedAt: report.receivedAt,
    message: report.message,
    clientVersion: report.clientVersion,
    clientPlatform: report.clientPlatform,
    context: report.context ?? null,
    stateCodec: state?.codec ?? null,
    stateFormatVersion: state?.replayFormatVersion ?? null,
    stateUncompressedBytes: state?.uncompressedBytes ?? null,
    stateCompressedBytes: state?.compressedBytes ?? null,
    stateSha256: state?.sha256 ?? null,
    stateAnonymized: state?.anonymized ?? null,
    blobKey: state?.blobKey ?? null,
    body: state?.body ?? null,
  }
}

function fromRow(row: Row): StoredBugReport {
  return {
    id: row.id,
    receivedAt: row.receivedAt,
    message: row.message,
    clientVersion: row.clientVersion,
    clientPlatform: row.clientPlatform,
    context: (row.context as SubmitBugReportRequest['context']) ?? null,
    // Every state column is written in one go or not at all, so the codec standing in for all of
    // them is not a guess: a row with a codec has the rest, and a row without has none of it.
    state:
      row.stateCodec === null
        ? null
        : {
            codec: row.stateCodec as BugReportCodec,
            replayFormatVersion: row.stateFormatVersion ?? 0,
            uncompressedBytes: row.stateUncompressedBytes ?? 0,
            compressedBytes: row.stateCompressedBytes ?? 0,
            sha256: row.stateSha256 ?? '',
            anonymized: row.stateAnonymized ?? false,
            blobKey: row.blobKey,
            body: row.body,
          },
  }
}
