import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { MatchNotHeldError, ResolverRefusedError } from '../dist/errors.js'
import { startNodeMatchResolver } from '../dist/node/index.js'
import {
  apply,
  expectNativeHashes,
  missing,
  playToSnapshot,
  restoreInput,
  seals,
  transcript,
} from './hosts.js'

describe.skipIf(missing)('the Node host', () => {
  let resolver: Awaited<ReturnType<typeof startNodeMatchResolver>>

  beforeAll(async () => {
    resolver = await startNodeMatchResolver()
  })
  afterAll(() => resolver?.close())

  it('describes the session version the native build plays', async () => {
    expect((await resolver.describe()).sessionVersion).toBe(transcript.sessionVersion)
  })

  it('reaches every hash the native build reached, from the start and from its snapshot', async () => {
    await expectNativeHashes(resolver, 'node-')
  })

  it('answers MatchNotHeldError for a match it never built', async () => {
    await expect(resolver.applyEvent('nobody', {})).rejects.toBeInstanceOf(MatchNotHeldError)
    expect(await resolver.status('nobody')).toBeNull()
  })

  it('answers ResolverRefusedError for a snapshot stored under another hash', async () => {
    const { snapshotStep } = await playToSnapshot(resolver, 'refused')
    await expect(
      resolver.restore(
        'refused-copy',
        {
          body: snapshotStep.archive,
          stateHash: transcript.bootstrapHash,
        },
        restoreInput(),
      ),
    ).rejects.toBeInstanceOf(ResolverRefusedError)
    await resolver.release('refused')
  })

  it('answers ResolverRefusedError for a seal ahead of the match, and keeps the match', async () => {
    const { rest } = await playToSnapshot(resolver, 'wrong-turn')
    const before = await resolver.status('wrong-turn')
    const [, later] = seals(rest)
    expect(later).toBeDefined()
    await expect(
      resolver.applyEvent('wrong-turn', later?.event, later?.sealedOrders),
    ).rejects.toBeInstanceOf(ResolverRefusedError)
    expect(await resolver.status('wrong-turn')).toEqual(before)
    await resolver.release('wrong-turn')
  })

  it('evicts beyond its limit, and an evicted match rebuilt from a snapshot carries on identically', async () => {
    const small = await startNodeMatchResolver({ maxMatches: 1 })
    try {
      const { rest } = await playToSnapshot(small, 'first')
      const snapshot = await small.snapshot('first')
      await small.bootstrap('second', {
        seed: transcript.seed,
        gameSettings: transcript.gameSettings,
        players: transcript.players,
      })
      await expect(small.status('first')).resolves.toBeNull()
      await expect(apply(small, 'first', rest[0])).rejects.toBeInstanceOf(MatchNotHeldError)
      expect((await small.info()).held).toBe(1)

      await small.restore('first', snapshot, restoreInput())
      let last
      for (const step of rest) last = (await apply(small, 'first', step)) ?? last
      expect(last?.stateHash).toBe(rest.at(-1).hash)
      expect(last?.finished).toBe(transcript.finished)
    } finally {
      await small.close()
    }
  })

  it('refuses calls after it is closed', async () => {
    const closing = await startNodeMatchResolver()
    await closing.close()
    await expect(closing.describe()).rejects.toThrow(/closed/)
  })
})
