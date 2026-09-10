import type { EventStreamOpener, Kernel, RateLimiter } from '@chaos-overlords/kernel'
import type { Context } from 'hono'
import type { AppEnv } from './http/types'

export interface ServerConfig {
  /** Serve `GET /api/v1/matches` (public lobby browsing). Off by default for self-hosted servers. */
  publicListing: boolean
  /** Interval between SSE keepalive comments. */
  sseHeartbeatMs: number
}

export interface ServerContainer {
  kernel: Kernel
  eventStream: EventStreamOpener
  /** Guards the unauthenticated doors (create, join) per client address. */
  rateLimiter: RateLimiter
  config: ServerConfig
  /** How the runtime identifies a caller for rate limiting; defaults to proxy headers. */
  clientAddress?: (c: Context<AppEnv>) => string
}

export const DEFAULT_SERVER_CONFIG: ServerConfig = {
  publicListing: false,
  sseHeartbeatMs: 20_000,
}
