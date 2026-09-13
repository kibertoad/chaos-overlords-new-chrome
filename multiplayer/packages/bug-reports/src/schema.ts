import { index, integer, sqliteTable, text } from 'drizzle-orm/sqlite-core'

/**
 * One table, in a database of its own.
 *
 * Bug reports do not live beside matches. They arrive unauthenticated, they are kept for far longer
 * than a match is, and the thing that makes them worth having — an attached journal of somebody
 * else's game — is exactly the thing a live multiplayer database should never be holding. Keeping
 * them in a separate D1 instance (a separate SQLite file when self-hosted) means the two cannot
 * share a lock, a retention sweep, a migration lineage, or a blast radius: a report that arrives
 * while a turn is sealing writes to a different database entirely, and a mistake in the intake path
 * cannot reach a match.
 *
 * The journal itself is usually *not* here. `body` holds the base64 archive only when it is small
 * and the deployment has nowhere better; otherwise `blob_key` names an object in R2 (or in a
 * directory, self-hosted) and `body` stays null. Exactly one of the two is set — see
 * `BugReportService`.
 */
export const bugReports = sqliteTable(
  'bug_reports',
  {
    id: text('id').primaryKey(),
    receivedAt: integer('received_at', { mode: 'timestamp_ms' }).notNull(),
    /** What the player typed, verbatim. Never parsed, never interpolated anywhere. */
    message: text('message').notNull(),
    clientVersion: text('client_version').notNull(),
    clientPlatform: text('client_platform').notNull(),
    /** The triage fields (`bugReportContextSchema`), or null when the client sent none. */
    context: text('context', { mode: 'json' }),

    // --- The attached journal, when there is one. -------------------------------------------
    /** Null when no journal came with the report. */
    stateCodec: text('state_codec'),
    stateFormatVersion: integer('state_format_version'),
    stateUncompressedBytes: integer('state_uncompressed_bytes'),
    stateCompressedBytes: integer('state_compressed_bytes'),
    /** SHA-256 of the compressed archive, lowercase hex, as the client computed it. */
    stateSha256: text('state_sha256'),
    stateAnonymized: integer('state_anonymized', { mode: 'boolean' }),
    /** Object key in the blob store, when the archive was written there. */
    blobKey: text('blob_key'),
    /** Base64 archive, only for a small one on a deployment with no blob store. */
    body: text('body'),
  },
  (table) => [
    // Triage reads newest-first and nothing else does a range scan over this table.
    index('bug_reports_received_idx').on(table.receivedAt),
  ],
)
