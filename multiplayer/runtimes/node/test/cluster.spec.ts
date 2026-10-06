import type { AddressInfo } from 'node:net'
import { MultiplayerClient } from '@chaos-overlords/client'
import type { MatchEvent } from '@chaos-overlords/contracts'
import { noopLogger, type PersistedEvent } from '@chaos-overlords/kernel'
import { type ServerType, serve } from '@hono/node-server'
import pg from 'pg'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { buildNodeRuntime, loadConfig, type NodeRuntime } from '../src'
import { PostgresClusterBus } from '../src/cluster'

const url = process.env.TEST_DATABASE_URL

// See the note in `packages/storage/test/postgres.spec.ts`.
if (process.env.REQUIRE_POSTGRES === '1' && !url) {
  throw new Error('REQUIRE_POSTGRES=1 but TEST_DATABASE_URL is empty.')
}

/**
 * A database of this file's own, created on the server `TEST_DATABASE_URL` names.
 *
 * The runtimes here run their background jobs on a real clock, and the turn sweep settles every
 * confirmed turn it finds. Sharing the database with the storage suite, which turbo runs at the
 * same time, let a sweep stamp the turns that suite had just written and was about to list. A
 * separate database also keeps other suites' notifications off this file's channel.
 */
let databaseUrl = ''
const isolatedName = `chaos_cluster_${crypto.randomUUID().replaceAll('-', '')}`

async function admin<T>(run: (client: pg.Client) => Promise<T>): Promise<T> {
  const client = new pg.Client({ connectionString: url })
  await client.connect()
  try {
    return await run(client)
  } finally {
    await client.end()
  }
}

beforeAll(async () => {
  if (!url) return
  await admin((client) => client.query(`create database ${isolatedName}`))
  const isolated = new URL(url)
  isolated.pathname = `/${isolatedName}`
  databaseUrl = isolated.toString()
})

afterAll(async () => {
  if (!url) return
  await admin((client) => client.query(`drop database if exists ${isolatedName} with (force)`))
})

/**
 * Well inside the twenty-second heartbeat and far inside the periodic catch-up read (five
 * heartbeats), so anything that arrives within it was carried by the cluster bus.
 */
const DELIVERY_MS = 3_000

const settings = {
  name: 'Two instances',
  maxPlayers: 3,
  turnTimerSeconds: 0,
  visibility: 'private' as const,
  gameSettings: { scenario: 'smg-islands' },
}

async function listen(runtime: NodeRuntime): Promise<{ server: ServerType; baseUrl: string }> {
  const server = serve({ fetch: runtime.app.fetch, hostname: '127.0.0.1', port: 0 })
  await new Promise<void>((resolve) => server.once('listening', () => resolve()))
  return { server, baseUrl: `http://127.0.0.1:${(server.address() as AddressInfo).port}` }
}

/** The first event an iterator yields, or null when it ends or `ms` passes first. */
async function firstWithin(
  events: AsyncGenerator<MatchEvent>,
  controller: AbortController,
  ms: number,
): Promise<MatchEvent | null | 'ended'> {
  const timer = setTimeout(() => controller.abort(), ms)
  try {
    const next = await events.next()
    return next.done ? 'ended' : next.value
  } catch {
    return controller.signal.aborted ? null : 'ended'
  } finally {
    clearTimeout(timer)
    controller.abort()
  }
}

/**
 * Two Node instances over one Postgres database, each with its own HTTP listener and its own
 * streams: what one instance writes must reach the streams the other holds, at once.
 */
describe.skipIf(!url)('node runtime as two instances over postgres', () => {
  const instances: { runtime: NodeRuntime; server: ServerType; baseUrl: string }[] = []

  beforeAll(async () => {
    for (let i = 0; i < 2; i += 1) {
      const runtime = await buildNodeRuntime(
        loadConfig({
          DATABASE_URL: databaseUrl,
          LOG_LEVEL: 'error',
          RATE_LIMIT_PER_MINUTE: '10000',
          MEMBER_RATE_LIMIT_PER_MINUTE: '10000',
          MATCH_CREATION_RATE_LIMIT_PER_MINUTE: '10000',
        }),
      )
      instances.push({ runtime, ...(await listen(runtime)) })
    }
  })

  afterAll(async () => {
    for (const { runtime, server } of instances) {
      runtime.closeStreams()
      await new Promise<void>((resolve) => server.close(() => resolve()))
      await runtime.close()
    }
  })

  const clientOf = (index: number) =>
    new MultiplayerClient({ baseUrl: (instances[index] as { baseUrl: string }).baseUrl })

  it('delivers an event written through one instance to a stream held by the other', async () => {
    const host = await clientOf(0).createMatch({ settings, hostDisplayName: 'Ada' })
    const onB = clientOf(1).withToken(host.token).match(host.match.id)
    const detail = await onB.get()

    const controller = new AbortController()
    const stream = onB.streamOnce({ after: detail.match.lastEventSeq, signal: controller.signal })
    // The stream has to be open before the write, or the opening catch-up read finds the event and
    // proves nothing about the bus. Its first frame is the server's comment, not an event, so wait
    // for the stream to be counted instead.
    const first = firstWithin(stream, controller, DELIVERY_MS + 2_000)
    await waitFor(() => streamsOn(1) > 0)

    await clientOf(0).join({ joinCode: host.joinCode, displayName: 'Grace' })
    const event = await first
    expect(event).toMatchObject({ type: 'lobby.playerJoined' })
  })

  it('hangs up a kicked player stream held by the other instance', async () => {
    const host = await clientOf(0).createMatch({ settings, hostDisplayName: 'Ada' })
    const guest = await clientOf(0).join({ joinCode: host.joinCode, displayName: 'Grace' })
    const guestOnB = clientOf(1).withToken(guest.token).match(host.match.id)
    const after = (await guestOnB.get()).match.lastEventSeq

    const before = streamsOn(1)
    const controller = new AbortController()
    const stream = guestOnB.streamOnce({ after, signal: controller.signal })
    const ended = firstWithin(stream, controller, DELIVERY_MS + 2_000)
    await waitFor(() => streamsOn(1) > before)

    await clientOf(0).withToken(host.token).match(host.match.id).kick(guest.player.id)
    expect(await ended).toBe('ended')
  })

  function streamsOn(index: number): number {
    return (instances[index] as { runtime: NodeRuntime }).runtime.openStreams
  }
})

describe.skipIf(!url)('postgres cluster bus', () => {
  function recordingHub() {
    const announced: { matchId: string; seq: number; type: string }[] = []
    const closed: { matchId: string; playerId: string }[] = []
    let resyncs = 0
    return {
      announced,
      closed,
      get resyncs() {
        return resyncs
      },
      hub: {
        announce: (matchId: string, seq: number, type: string) => {
          announced.push({ matchId, seq, type })
        },
        close: async (input: { matchId: string; playerId: string }) => {
          closed.push(input)
        },
        resync: () => {
          resyncs += 1
        },
      },
    }
  }

  const event = (matchId: string, seq: number): PersistedEvent =>
    ({
      matchId,
      seq,
      type: 'lobby.playerJoined',
      payload: {},
      createdAt: new Date().toISOString(),
    }) as unknown as PersistedEvent

  it('carries appends and revocations to other instances and not back to the sender', async () => {
    const a = recordingHub()
    const b = recordingHub()
    const busA = await PostgresClusterBus.start({
      connectionString: databaseUrl,
      hub: a.hub,
      logger: noopLogger,
    })
    const busB = await PostgresClusterBus.start({
      connectionString: databaseUrl,
      hub: b.hub,
      logger: noopLogger,
    })
    try {
      const matchId = crypto.randomUUID()
      await busA.appended(event(matchId, 7))
      await busA.revoked({ matchId, playerId: 'p1' })
      // Filtered by match: the other tests of this file announce on the same channel.
      const ofMatch = <T extends { matchId: string }>(list: T[]) =>
        list.filter((entry) => entry.matchId === matchId)
      await waitFor(() => ofMatch(b.announced).length === 1 && ofMatch(b.closed).length === 1)
      expect(ofMatch(b.announced)).toEqual([{ matchId, seq: 7, type: 'lobby.playerJoined' }])
      expect(ofMatch(b.closed)).toEqual([{ matchId, playerId: 'p1' }])
      // The sender's own messages come back to it too; they must stop there.
      await busB.appended(event(matchId, 8))
      await waitFor(() => ofMatch(a.announced).length === 1)
      expect(ofMatch(a.announced)).toEqual([{ matchId, seq: 8, type: 'lobby.playerJoined' }])
      expect(ofMatch(a.closed)).toEqual([])
    } finally {
      await busA.close()
      await busB.close()
    }
  })

  it('runs a job on one instance at a time', async () => {
    const busA = await PostgresClusterBus.start({
      connectionString: databaseUrl,
      hub: recordingHub().hub,
      logger: noopLogger,
    })
    const busB = await PostgresClusterBus.start({
      connectionString: databaseUrl,
      hub: recordingHub().hub,
      logger: noopLogger,
    })
    try {
      let release = () => {}
      const held = new Promise<void>((resolve) => {
        release = resolve
      })
      let ranOnB = false
      const passOnA = busA.exclusive('sweep', () => held)
      // Give A's lock query time to land before B asks.
      await new Promise((resolve) => setTimeout(resolve, 200))
      await busB.exclusive('sweep', async () => {
        ranOnB = true
      })
      expect(ranOnB).toBe(false)
      release()
      await passOnA
      await busB.exclusive('sweep', async () => {
        ranOnB = true
      })
      expect(ranOnB).toBe(true)
    } finally {
      await busA.close()
      await busB.close()
    }
  })

  it('listens again after its connection is cut and has the hub re-read the log', async () => {
    const a = recordingHub()
    const busA = await PostgresClusterBus.start({
      connectionString: databaseUrl,
      hub: a.hub,
      logger: noopLogger,
      reconnectMinMs: 50,
    })
    const busB = await PostgresClusterBus.start({
      connectionString: databaseUrl,
      hub: recordingHub().hub,
      logger: noopLogger,
    })
    const killer = new pg.Client({ connectionString: databaseUrl })
    await killer.connect()
    try {
      await killer.query(
        'select pg_terminate_backend(pid) from pg_stat_activity where application_name = $1',
        [busA.listenerName],
      )
      await waitFor(() => a.resyncs === 1)
      const matchId = crypto.randomUUID()
      await busB.appended(event(matchId, 3))
      await waitFor(() => a.announced.some((entry) => entry.matchId === matchId))
      expect(a.announced.find((entry) => entry.matchId === matchId)).toMatchObject({ seq: 3 })
    } finally {
      await killer.end()
      await busA.close()
      await busB.close()
    }
  })
})

async function waitFor(condition: () => boolean, timeoutMs = DELIVERY_MS): Promise<void> {
  const deadline = Date.now() + timeoutMs
  while (!condition()) {
    if (Date.now() > deadline) throw new Error('condition not met in time')
    await new Promise((resolve) => setTimeout(resolve, 20))
  }
}
