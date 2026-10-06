import { describe, expect, it } from 'vitest'
import type { BootedResolver, ResolverExports } from '../src/boot.js'
import { ResolverCore } from '../src/core.js'
import { MatchNotHeldError, ResolverRefusedError, resolverErrorCode } from '../src/errors.js'

/** The interop layer's class for a managed exception, which the core recognises by its name. */
class ManagedError extends Error {}

/**
 * A runtime that keeps a turn counter per handle. Every live match weighs `matchBytes` of managed
 * heap and the garbage since the last collection `garbageBytes`, which a collection clears.
 */
function fakeRuntime(matchBytes = 10, garbagePerTurn = 0) {
  const matches = new Map<number, { turn: number }>()
  let nextHandle = 1
  let garbage = 0
  const collections: boolean[] = []
  const held = (handle: number) => {
    const match = matches.get(handle)
    if (!match) throw new ManagedError(`no match under handle ${handle}`)
    return match
  }
  const exports: ResolverExports = {
    SessionVersion: () => 7,
    SnapshotFormatVersion: () => 3,
    Bootstrap: (seed) => {
      if (seed < 0) throw new ManagedError('settings this build cannot read')
      matches.set(nextHandle, { turn: 1 })
      return nextHandle++
    },
    Restore: (payload, stateHash) => {
      if (stateHash !== `turn-${payload[0]}`)
        throw new ManagedError('the snapshot hashes elsewhere')
      matches.set(nextHandle, { turn: payload[0] ?? 0 })
      return nextHandle++
    },
    ApplySealedTurn: (handle, json) => {
      const match = held(handle)
      if (JSON.parse(json).turn !== match.turn)
        throw new ManagedError('the sealed set is for another turn')
      match.turn++
      garbage += garbagePerTurn
      return `turn-${match.turn}`
    },
    HandOverSeat: () => {},
    StateHash: (handle) => `turn-${held(handle).turn}`,
    Turn: (handle) => held(handle).turn,
    IsFinished: () => false,
    SavePayload: (handle) => new Uint8Array([held(handle).turn]),
    ManagedHeapBytes: (collect) => {
      collections.push(collect)
      if (collect) garbage = 0
      return matches.size * matchBytes + garbage
    },
    Release: (handle) => {
      matches.delete(handle)
    },
  }
  const booted: BootedResolver = {
    exports,
    manifest: {
      sessionVersion: 7,
      snapshotFormatVersion: 3,
    } as BootedResolver['manifest'],
    memoryBytes: () => 1024,
  }
  return { booted, matches, collections }
}

const input = { seed: 1, gameSettings: {}, players: [] }

describe('ResolverCore', () => {
  it('builds, resolves and snapshots a match under its id', () => {
    const { booted } = fakeRuntime()
    const core = new ResolverCore(booted, { maxMatches: 4, managedHeapBudgetBytes: 1000 })
    expect(core.bootstrap('a', input)).toEqual({ stateHash: 'turn-1', turn: 1, finished: false })
    expect(core.applySealedTurn('a', { turn: 1 }).stateHash).toBe('turn-2')
    const { payload, status } = core.savePayload('a')
    expect([...payload]).toEqual([2])
    expect(core.restore('b', payload, status.stateHash).turn).toBe(2)
    expect(core.info()).toMatchObject({ sessionVersion: 7, snapshotFormatVersion: 3, held: 2 })
  })

  it('answers MatchNotHeldError for a match it does not hold, and null from status', () => {
    const core = new ResolverCore(fakeRuntime().booted, {
      maxMatches: 4,
      managedHeapBudgetBytes: 1000,
    })
    expect(core.status('a')).toBeNull()
    expect(() => core.applySealedTurn('a', { turn: 1 })).toThrow(MatchNotHeldError)
    expect(() => core.savePayload('a')).toThrow(MatchNotHeldError)
    expect(() => core.release('a')).not.toThrow()
  })

  it('turns a managed exception into ResolverRefusedError and keeps the match', () => {
    const core = new ResolverCore(fakeRuntime().booted, {
      maxMatches: 4,
      managedHeapBudgetBytes: 1000,
    })
    core.bootstrap('a', input)
    const error = (() => {
      try {
        core.applySealedTurn('a', { turn: 5 })
      } catch (thrown) {
        return thrown
      }
    })()
    expect(error).toBeInstanceOf(ResolverRefusedError)
    expect(resolverErrorCode(error)).toBe('resolver_refused')
    expect(core.status('a')?.turn).toBe(1)
    expect(() => core.restore('b', new Uint8Array([3]), 'turn-4')).toThrow(ResolverRefusedError)
    expect(() => core.bootstrap('c', { ...input, seed: -1 })).toThrow(ResolverRefusedError)
    expect(core.heldMatches()).toEqual(['a'])
  })

  it('lets an error that is not a managed exception through as it is', () => {
    const { booted } = fakeRuntime()
    booted.exports.Bootstrap = () => {
      throw new TypeError('the runtime is gone')
    }
    const core = new ResolverCore(booted, { maxMatches: 4, managedHeapBudgetBytes: 1000 })
    expect(() => core.bootstrap('a', input)).toThrow(TypeError)
  })

  it('releases the least recently used match beyond maxMatches', () => {
    const { booted, matches } = fakeRuntime()
    const core = new ResolverCore(booted, { maxMatches: 2, managedHeapBudgetBytes: 1000 })
    core.bootstrap('a', input)
    core.bootstrap('b', input)
    core.applySealedTurn('a', { turn: 1 })
    core.bootstrap('c', input)
    expect(core.heldMatches()).toEqual(['a', 'c'])
    expect(matches.size).toBe(2)
    expect(() => core.applySealedTurn('b', { turn: 1 })).toThrow(MatchNotHeldError)
  })

  it('replaces the match held under an id and releases the old handle', () => {
    const { booted, matches } = fakeRuntime()
    const core = new ResolverCore(booted, { maxMatches: 4, managedHeapBudgetBytes: 1000 })
    core.bootstrap('a', input)
    core.applySealedTurn('a', { turn: 1 })
    expect(core.bootstrap('a', input).turn).toBe(1)
    expect(matches.size).toBe(1)
  })

  it('collects before evicting over the heap budget, so garbage alone evicts nothing', () => {
    const { booted, collections } = fakeRuntime(10, 50)
    const core = new ResolverCore(booted, { maxMatches: 10, managedHeapBudgetBytes: 35 })
    core.bootstrap('a', input)
    core.bootstrap('b', input)
    core.bootstrap('c', input)
    collections.length = 0
    core.applySealedTurn('a', { turn: 1 })
    expect(collections).toEqual([false, true])
    expect(core.heldMatches()).toEqual(['b', 'c', 'a'])
  })

  it('evicts the least recently used until the live heap fits, keeping the match in use', () => {
    const { booted } = fakeRuntime(10)
    const core = new ResolverCore(booted, { maxMatches: 10, managedHeapBudgetBytes: 25 })
    core.bootstrap('a', input)
    core.bootstrap('b', input)
    core.bootstrap('c', input)
    expect(core.heldMatches()).toEqual(['b', 'c'])
    const tight = new ResolverCore(fakeRuntime(10).booted, {
      maxMatches: 10,
      managedHeapBudgetBytes: 5,
    })
    tight.bootstrap('a', input)
    tight.bootstrap('b', input)
    expect(tight.heldMatches()).toEqual(['b'])
  })
})
