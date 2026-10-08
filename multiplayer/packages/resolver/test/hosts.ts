// What the host specs share: the transcript, or the reason they skip, and the checks both hosts
// must pass on it.
import { expect } from 'vitest'
import type { MatchResolver } from '../src/resolver.js'
import { replayTranscript } from './harness/replay.mjs'
import { loadTranscript, resolverRequired } from './harness/transcript.mjs'

export const { transcript, missing } = loadTranscript()
if (missing && resolverRequired) throw new Error(`REQUIRE_RESOLVER is set, but ${missing}`)

/** The transcript replays with every hash equal to the native build's. */
export async function expectNativeHashes(resolver: MatchResolver, prefix: string) {
  const result = await replayTranscript(resolver, transcript, prefix)
  expect(result.mismatches).toEqual([])
  expect(result.checks).toBeGreaterThan(transcript.steps.length)
}

/** Drives a match from the transcript up to its snapshot step, and returns the steps after it. */
export async function playToSnapshot(resolver: MatchResolver, matchId: string) {
  await resolver.bootstrap(matchId, {
    seed: transcript.seed,
    gameSettings: transcript.gameSettings,
    players: transcript.players,
  })
  const at = transcript.steps.findIndex((step: { kind: string }) => step.kind === 'snapshot')
  for (const step of transcript.steps.slice(0, at)) await apply(resolver, matchId, step)
  return { snapshotStep: transcript.steps[at], rest: transcript.steps.slice(at + 1) }
}

interface TranscriptStep {
  kind: string
  event?: { type: string; payload: { turn?: number } }
  sealedOrders?: unknown
}

/** Feeds one event step to a match; a snapshot step does nothing. */
export async function apply(resolver: MatchResolver, matchId: string, step: TranscriptStep) {
  if (step.kind !== 'event') return undefined
  return resolver.applyEvent(matchId, step.event, step.sealedOrders)
}

/** The seals among `steps`. */
export function seals(steps: TranscriptStep[]) {
  return steps.filter((step) => step.event?.type === 'turn.sealed')
}

/** The roster the transcript's match is played with, as a restore takes it. */
export const restoreInput = () => ({ players: transcript.players })
