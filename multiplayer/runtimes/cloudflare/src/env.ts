import type { D1Database, DurableObjectNamespace } from '@cloudflare/workers-types'

export interface Env {
  DB: D1Database
  MATCH_HUB: DurableObjectNamespace
  PUBLIC_LISTING?: string
  RATE_LIMIT_PER_MINUTE?: string
  MEMBER_RATE_LIMIT_PER_MINUTE?: string
  UPLOAD_RATE_LIMIT_PER_MINUTE?: string
  /** Days before a finished, abandoned or never-started match is deleted. 0 keeps everything. */
  RETENTION_DAYS?: string
}
