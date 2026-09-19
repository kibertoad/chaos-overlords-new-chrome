import type { D1Database, DurableObjectNamespace, R2Bucket } from '@cloudflare/workers-types'

export interface Env {
  DB: D1Database
  MATCH_HUB: DurableObjectNamespace
  /**
   * Bug reports, in a D1 instance of their own.
   *
   * Separate from `DB` on purpose: reports arrive unauthenticated, outlive every match they
   * describe, and carry other players' game journals. They share no schema, no migration lineage
   * and no blast radius with the database that is holding live matches. Unbound, the bug report
   * route answers 404 and the rest of the server is unaffected.
   */
  BUG_DB?: D1Database
  /**
   * Where the compressed match journals go.
   *
   * A journal is hundreds of kilobytes to a few megabytes; D1 refuses a row over 2 MB and would
   * make every triage query drag base64 through the query path for the ones that fit. With this
   * bound, `BUG_DB` keeps the metadata and R2 keeps the bytes. Without it, only journals under
   * `BUG_REPORT_LIMITS.inlineStateBytes` are kept.
   */
  BUG_BLOBS?: R2Bucket
  /** `"false"` stops serving `GET /api/v1/matches`; anything else (unset included) serves it. */
  PUBLIC_LISTING?: string
  RATE_LIMIT_PER_MINUTE?: string
  MEMBER_RATE_LIMIT_PER_MINUTE?: string
  UPLOAD_RATE_LIMIT_PER_MINUTE?: string
  BUG_REPORT_RATE_LIMIT_PER_MINUTE?: string
  /** Days before a finished, abandoned or never-started match is deleted. 0 keeps everything. */
  RETENTION_DAYS?: string
  /**
   * Days before a RUNNING match with nobody active in it is deleted. 0 keeps them forever.
   *
   * The ordinary end of a public match is everyone walking away from one that is still running, and
   * nothing else ever collects one of those.
   */
  ABANDONED_RETENTION_DAYS?: string
  /** Days a bug report and its R2 object are kept. 0 keeps them forever. */
  BUG_REPORT_RETENTION_DAYS?: string
  /** Attached journal megabytes accepted per rolling day across every reporter. 0 lifts the cap. */
  BUG_REPORT_DAILY_STATE_MB?: string
}
