import type { D1Database, DurableObjectNamespace } from '@cloudflare/workers-types'

export interface Env {
  DB: D1Database
  MATCH_HUB: DurableObjectNamespace
  PUBLIC_LISTING?: string
  RATE_LIMIT_PER_MINUTE?: string
}
