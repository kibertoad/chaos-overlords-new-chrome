// The Cloudflare host under workerd, through Miniflare: the resolver Worker with its Durable
// Objects, reached from a coordination-side Worker over a service binding (see harness/caller.js).
// Errors cross that boundary as plain errors, so these check the code each carries.
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { WORKERD_RESOLVER_DEFAULTS } from '../dist/workerd/limits.js'
import { startWorkerdResolver } from './harness/workerd.mjs'
import {
  apply,
  expectBatchedFeed,
  expectNativeHashes,
  missing,
  playToSnapshot,
  restoreInput,
  transcript,
} from './hosts.js'

const bootstrapInput = () => ({
  seed: transcript.seed,
  gameSettings: transcript.gameSettings,
  players: transcript.players,
})

describe.skipIf(missing)('the Cloudflare host under workerd', () => {
  let host: Awaited<ReturnType<typeof startWorkerdResolver>>

  beforeAll(async () => {
    host = await startWorkerdResolver()
  })
  afterAll(() => host?.dispose())

  it('reaches every hash the native build reached, from the start and from its snapshot', async () => {
    await expectNativeHashes(host.resolver, 'workerd-')
  })

  it('feeds a run of the log in one call, once', async () => {
    await expectBatchedFeed(host.resolver, 'workerd-batch')
  })

  it('answers match_not_held for a match its object does not hold', async () => {
    await expect(host.resolver.applyEvent('nobody', {})).rejects.toMatchObject({
      code: 'match_not_held',
    })
    expect(await host.resolver.status('nobody')).toBeNull()
  })

  it('answers resolver_refused for a snapshot stored under another hash', async () => {
    const { snapshotStep } = await playToSnapshot(host.resolver, 'refused')
    await expect(
      host.resolver.restore(
        'refused-copy',
        {
          body: snapshotStep.archive,
          stateHash: transcript.bootstrapHash,
        },
        restoreInput(),
      ),
    ).rejects.toMatchObject({ code: 'resolver_refused' })
    await host.resolver.release('refused')
  })

  it('stays within the isolate memory with the default limits and matches the native snapshot', async () => {
    // Every Durable Object of the class shares the one local isolate, as objects may in production.
    const ids = Array.from(
      { length: WORKERD_RESOLVER_DEFAULTS.maxMatches + 2 },
      (_, i) => `full-${i}`,
    )
    for (const id of ids) await playToSnapshot(host.resolver, id)
    const info = await host.resolver.info(ids.at(-1))
    expect(info.held).toBeLessThanOrEqual(WORKERD_RESOLVER_DEFAULTS.maxMatches)
    expect(info.memoryBytes).toBeLessThan(128 * 1024 * 1024)
    for (const id of ids) await host.resolver.release(id)
  })
})

describe.skipIf(missing)('the Cloudflare host with RESOLVER_MAX_MATCHES', () => {
  let host: Awaited<ReturnType<typeof startWorkerdResolver>>

  beforeAll(async () => {
    host = await startWorkerdResolver({ vars: { RESOLVER_MAX_MATCHES: '1' } })
  })
  afterAll(() => host?.dispose())

  it('evicts beyond the limit, and an evicted match rebuilt from a snapshot carries on identically', async () => {
    const { rest } = await playToSnapshot(host.resolver, 'first')
    const snapshot = await host.resolver.snapshot('first')
    await host.resolver.bootstrap('second', bootstrapInput())
    expect(await host.resolver.status('first')).toBeNull()
    await expect(apply(host.resolver, 'first', rest[0])).rejects.toMatchObject({
      code: 'match_not_held',
    })
    expect((await host.resolver.info('first')).heldMatches).toEqual(['second'])

    await host.resolver.restore(
      'first',
      { body: snapshot.body, stateHash: snapshot.stateHash },
      restoreInput(),
    )
    let last
    for (const step of rest) last = (await apply(host.resolver, 'first', step)) ?? last
    expect(last?.stateHash).toBe(rest.at(-1).hash)
    expect(last?.finished).toBe(transcript.finished)
  })
})
