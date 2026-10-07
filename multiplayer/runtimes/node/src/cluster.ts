import { randomUUID } from 'node:crypto'
import type { Logger, PersistedEvent } from '@chaos-overlords/kernel'
import type { LocalEventHub } from '@chaos-overlords/server'
import pg from 'pg'

/** A background job that one instance at a time should run against a shared database. */
export type ExclusiveJob = 'sweep' | 'retention'

/**
 * What the instances serving one database tell each other.
 *
 * Every Node instance keeps its own event streams in its own `LocalEventHub`. An event appended
 * through one instance has to wake the streams of that match on every other instance, and a kick
 * has to hang up the kicked player's streams wherever they are held. The background jobs read and
 * delete across every match, so only one instance at a time needs to run each pass.
 *
 * A single SQLite process has nobody to tell, so `singleProcessBus` does nothing and runs every
 * job. Postgres is the store several instances can share, and `PostgresClusterBus` carries the
 * messages over `LISTEN/NOTIFY` and takes the jobs in turn through advisory locks.
 */
export interface ClusterBus {
  /** Tell the other instances that this one appended `event`. Called after the append commits. */
  appended(event: PersistedEvent): Promise<void>
  /** Tell the other instances to hang up a revoked membership's streams. */
  revoked(input: { matchId: string; playerId: string }): Promise<void>
  /** Run one pass of `job`, unless another instance is running a pass of it right now. */
  exclusive(job: ExclusiveJob, run: () => Promise<void>): Promise<void>
  close(): Promise<void>
}

export const singleProcessBus: ClusterBus = {
  appended: async () => {},
  revoked: async () => {},
  exclusive: (_job, run) => run(),
  close: async () => {},
}

/** The channel every instance listens on. Payloads stay far below the 8000-byte NOTIFY limit. */
export const CLUSTER_CHANNEL = 'chaos_overlords_cluster'

/**
 * Advisory lock keys, one per job. Any fixed 64-bit values do as long as every instance uses the
 * same ones and none collides with the migration lock in `@chaos-overlords/storage`.
 */
const JOB_LOCK_KEYS: Record<ExclusiveJob, string> = {
  sweep: '7262803811402602',
  retention: '7262803811402603',
}

/** Backoff between attempts to re-establish the listening connection. */
const RECONNECT_MIN_MS = 500
const RECONNECT_MAX_MS = 30_000

type ClusterMessage =
  | { from: string; kind: 'event'; matchId: string; seq: number; type: string }
  | { from: string; kind: 'revoke'; matchId: string; playerId: string }

export interface PostgresClusterBusOptions {
  connectionString: string
  hub: Pick<LocalEventHub, 'announce' | 'close' | 'resync'>
  logger: Logger
  /** Overrides the reconnect delays; tests shorten them. */
  reconnectMinMs?: number
}

/**
 * The cluster bus over Postgres.
 *
 * One dedicated connection per instance holds `LISTEN`. A notification names the match and the
 * sequence (or the revoked player) and never carries the event itself: the receiving instance
 * reads the row from the log, which already holds it because `NOTIFY` is sent after the append
 * returns. Each message carries its sender's id and an instance ignores its own, since it has
 * already told its own hub.
 *
 * A lost notification costs a delay and nothing else. Streams still re-read the log on their
 * periodic catch-up, so an event whose announcement never arrives is delivered at the next one.
 * When the listening connection drops, the bus reconnects with backoff and then has the hub re-read
 * the log for every open stream, which covers whatever was announced while nobody was listening.
 */
export class PostgresClusterBus implements ClusterBus {
  readonly instanceId = randomUUID()
  /**
   * The listening connection's `application_name`, so an operator can tell it apart in
   * `pg_stat_activity` from the pooled connections.
   */
  readonly listenerName = `chaos-cluster-${this.instanceId}`
  private readonly pool: pg.Pool
  private listener: pg.Client | undefined
  private closed = false
  private reconnectDelay: number
  private reconnectTimer: NodeJS.Timeout | undefined

  private constructor(private readonly options: PostgresClusterBusOptions) {
    this.reconnectDelay = options.reconnectMinMs ?? RECONNECT_MIN_MS
    // Notifications and the job locks. Small, because every query on it is a short one; the
    // storage pool keeps serving requests while a job pass holds one of these for its lock.
    this.pool = new pg.Pool({
      connectionString: options.connectionString,
      max: 4,
      connectionTimeoutMillis: 10_000,
      idleTimeoutMillis: 30_000,
    })
    this.pool.on('error', (error) =>
      options.logger.warn('cluster pool reported an error', { error: String(error) }),
    )
  }

  /** Connects the listener before returning, so an instance that cannot listen does not start. */
  static async start(options: PostgresClusterBusOptions): Promise<PostgresClusterBus> {
    const bus = new PostgresClusterBus(options)
    try {
      await bus.listen()
    } catch (error) {
      await bus.pool.end()
      throw error
    }
    return bus
  }

  async appended(event: PersistedEvent): Promise<void> {
    await this.send({
      from: this.instanceId,
      kind: 'event',
      matchId: event.matchId,
      seq: event.seq,
      type: event.type,
    })
  }

  async revoked(input: { matchId: string; playerId: string }): Promise<void> {
    await this.send({ from: this.instanceId, kind: 'revoke', ...input })
  }

  /**
   * A session-level advisory lock on a connection of its own, held for the pass.
   *
   * `pg_try_advisory_lock` never waits: an instance that finds the job taken skips this pass and
   * tries again on its next interval. If the instance holding the lock dies, Postgres releases the
   * lock with its connection, so the next instance to try takes the job over.
   */
  async exclusive(job: ExclusiveJob, run: () => Promise<void>): Promise<void> {
    const client = await this.pool.connect()
    let locked = false
    try {
      const result = await client.query<{ locked: boolean }>(
        'select pg_try_advisory_lock($1) as locked',
        [JOB_LOCK_KEYS[job]],
      )
      locked = result.rows[0]?.locked === true
      if (locked) await run()
    } finally {
      let unlockFailed: Error | undefined
      if (locked) {
        await client
          .query('select pg_advisory_unlock($1)', [JOB_LOCK_KEYS[job]])
          .catch((error: unknown) => {
            unlockFailed = error instanceof Error ? error : new Error(String(error))
          })
      }
      // A connection that may still hold the lock is destroyed rather than pooled: the pool would
      // keep reusing it for announcements, and the lock would keep every other instance off the
      // job for as long as it stayed open.
      client.release(unlockFailed)
    }
  }

  async close(): Promise<void> {
    if (this.closed) return
    this.closed = true
    clearTimeout(this.reconnectTimer)
    const listener = this.listener
    this.listener = undefined
    await listener?.end().catch(() => undefined)
    await this.pool.end()
  }

  private async send(message: ClusterMessage): Promise<void> {
    if (this.closed) return
    await this.pool.query('select pg_notify($1, $2)', [CLUSTER_CHANNEL, JSON.stringify(message)])
  }

  private async listen(): Promise<void> {
    const client = new pg.Client({
      connectionString: this.options.connectionString,
      connectionTimeoutMillis: 10_000,
      application_name: this.listenerName,
      // The listener sits idle between notifications; keepalive lets the OS notice a dead peer
      // instead of the instance waiting on a connection that will never deliver again. The first
      // probe goes out after ten idle seconds; left at 0, `pg` keeps the OS default, which on Linux
      // is two hours of silence before the first probe.
      keepAlive: true,
      keepAliveInitialDelayMillis: 10_000,
    })
    // An error or an end on the listening connection is the same event: whatever arrives next is
    // lost until a new connection listens again.
    const onLost = (error?: unknown) => {
      // Only the connection that is listening counts. One that failed while connecting is the
      // caller's failure to handle, and a second event from a connection already given up on is
      // not a second loss.
      if (this.listener !== client) return
      this.listener = undefined
      client.end().catch(() => undefined)
      if (this.closed) return
      this.options.logger.warn('cluster listener lost; reconnecting', {
        error: error === undefined ? 'connection ended' : String(error),
      })
      this.scheduleReconnect()
    }
    client.on('error', onLost)
    client.on('end', () => onLost())
    client.on('notification', (notification) => {
      if (notification.channel === CLUSTER_CHANNEL) this.receive(notification.payload)
    })
    try {
      await client.connect()
      await client.query(`LISTEN ${CLUSTER_CHANNEL}`)
    } catch (error) {
      await client.end().catch(() => undefined)
      throw error
    }
    if (this.closed) {
      await client.end().catch(() => undefined)
      return
    }
    this.listener = client
  }

  private scheduleReconnect(): void {
    const delay = this.reconnectDelay
    this.reconnectDelay = Math.min(this.reconnectDelay * 2, RECONNECT_MAX_MS)
    this.reconnectTimer = setTimeout(() => {
      if (this.closed) return
      this.listen().then(
        () => {
          this.reconnectDelay = this.options.reconnectMinMs ?? RECONNECT_MIN_MS
          this.options.logger.info('cluster listener reconnected')
          // Whatever was announced while nobody listened is in the log; read it now rather than
          // at each stream's next periodic catch-up.
          this.options.hub.resync()
        },
        (error: unknown) => {
          this.options.logger.warn('cluster listener reconnect failed', { error: String(error) })
          this.scheduleReconnect()
        },
      )
    }, delay)
    this.reconnectTimer.unref()
  }

  private receive(payload: string | undefined): void {
    const message = parseMessage(payload)
    if (!message) {
      this.options.logger.warn('ignored an unreadable cluster message')
      return
    }
    if (message.from === this.instanceId) return
    if (message.kind === 'event') {
      this.options.hub.announce(message.matchId, message.seq, message.type)
    } else {
      void this.options.hub.close({ matchId: message.matchId, playerId: message.playerId })
    }
  }
}

function parseMessage(payload: string | undefined): ClusterMessage | null {
  if (payload === undefined) return null
  let value: unknown
  try {
    value = JSON.parse(payload)
  } catch {
    return null
  }
  if (typeof value !== 'object' || value === null) return null
  const message = value as Record<string, unknown>
  if (typeof message.from !== 'string' || typeof message.matchId !== 'string') return null
  if (
    message.kind === 'event' &&
    typeof message.seq === 'number' &&
    Number.isSafeInteger(message.seq) &&
    typeof message.type === 'string'
  ) {
    return message as ClusterMessage
  }
  if (message.kind === 'revoke' && typeof message.playerId === 'string') {
    return message as ClusterMessage
  }
  return null
}
