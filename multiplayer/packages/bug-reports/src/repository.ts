import type { BugReportCodec, SubmitBugReportRequest } from '@chaos-overlords/contracts'
import { desc, eq } from 'drizzle-orm'
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
