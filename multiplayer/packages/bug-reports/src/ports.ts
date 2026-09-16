import type { BugReportCodec, SubmitBugReportRequest } from '@chaos-overlords/contracts'

/** A stored report, as the service writes it and a triage reader gets it back. */
export interface StoredBugReport {
  id: string
  receivedAt: Date
  message: string
  clientVersion: string
  clientPlatform: string
  context: SubmitBugReportRequest['context'] | null
  state: StoredBugReportState | null
}

/** Where the archive came to rest, and what it is. */
export interface StoredBugReportState {
  codec: BugReportCodec
  replayFormatVersion: number
  uncompressedBytes: number
  compressedBytes: number
  sha256: string
  anonymized: boolean
  /** The object key when it went to the blob store, or null when `body` holds it instead. */
  blobKey: string | null
  /** The base64 archive when it was small enough to keep in the row, or null. */
  body: string | null
}

/** The writes and reads this area needs; implemented over Drizzle, faked in tests. */
export interface BugReportRepository {
  insert(report: StoredBugReport): Promise<void>
  get(id: string): Promise<StoredBugReport | null>
  /** Newest first, for triage. */
  list(limit: number): Promise<StoredBugReport[]>
  /** Compressed bytes filed since `since`, which is what the daily byte budget is spent against. */
  bytesSince(since: Date): Promise<number>
  /**
   * Delete reports received before `before`, oldest first, at most `limit` of them. Returns the
   * blob keys of the ones that went, so the caller can drop the objects the rows pointed at — the
   * blob store is a different system and nothing cascades into it.
   */
  deleteBefore(before: Date, limit: number): Promise<string[]>
}

/**
 * Somewhere to put bytes that have no business being in a database row.
 *
 * R2 on Cloudflare, a directory on a self-hosted server, an in-memory map in a test. Three methods
 * because that is all a bug report needs: it is written once, read when somebody triages it, and
 * deleted if the report is.
 */
export interface BlobStore {
  put(key: string, bytes: Uint8Array, contentType: string): Promise<void>
  get(key: string): Promise<Uint8Array | null>
  delete(key: string): Promise<void>
}
