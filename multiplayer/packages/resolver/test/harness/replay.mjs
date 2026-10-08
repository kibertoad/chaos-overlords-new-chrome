// Replays a transcript written by tools/ResolverDeterminism (Program.cs) through a MatchResolver
// and lists every hash that differs from the native build's. The tests and the determinism check
// run it against both hosts.

/**
 * @param {import('../../dist/index.js').MatchResolver} resolver
 * @param {any} transcript
 * @param {string} [prefix] the matches are `${prefix}live` and `${prefix}restored`
 */
export async function replayTranscript(resolver, transcript, prefix = '') {
  const mismatches = []
  let checks = 0
  const check = (what, expected, actual) => {
    checks++
    if (expected !== actual) mismatches.push({ what, expected, actual })
  }
  const live = `${prefix}live`
  const restored = `${prefix}restored`
  check('session version', transcript.sessionVersion, (await resolver.describe()).sessionVersion)
  const boot = await resolver.bootstrap(live, {
    seed: transcript.seed,
    gameSettings: transcript.gameSettings,
    players: transcript.players,
  })
  check('bootstrap', transcript.bootstrapHash, boot.stateHash)

  // After the snapshot step, a second match picked up from the native build's snapshot is driven
  // beside the live one, so a snapshot crosses from .NET to WebAssembly and carries on identically.
  let matches = [live]
  let resolveMs = 0
  /** This host's own snapshot of the live match at the snapshot step. */
  let ownSnapshot
  for (const step of transcript.steps) {
    if (step.kind === 'handOver') {
      for (const id of matches) {
        const status = await resolver.handOverSeat(id, step.slot, step.toComputer)
        check(`handover of slot ${step.slot} (${id})`, step.hash, status.stateHash)
      }
    } else if (step.kind === 'sealed') {
      for (const id of matches) {
        const started = Date.now()
        const status = await resolver.applySealedTurn(id, step.sealedOrders)
        if (id === live) resolveMs += Date.now() - started
        check(`turn ${step.sealedOrders.turn} (${id})`, step.hash, status.stateHash)
      }
    } else if (step.kind === 'snapshot') {
      check('state at the snapshot', step.hash, (await resolver.status(live))?.stateHash)
      // The archive the native build wrote, as a client uploads it.
      const picked = await resolver.restore(restored, { body: step.archive, stateHash: step.hash })
      check('native snapshot restored', step.hash, picked.stateHash)
      matches = [live, restored]
      // A body without the archive header is read as a bare payload, as clients read an old upload.
      const bare = await resolver.restore(`${prefix}bare`, {
        body: step.savePayload,
        stateHash: step.hash,
      })
      check('bare payload restored', step.hash, bare.stateHash)
      await resolver.release(`${prefix}bare`)
      // And this host's own archive restores to the hash it was taken at.
      const own = await resolver.snapshot(live)
      ownSnapshot = own
      check('own snapshot hash', step.hash, own.stateHash)
      const roundTrip = await resolver.restore(`${prefix}own`, own)
      check('own snapshot round trip', step.hash, roundTrip.stateHash)
      await resolver.release(`${prefix}own`)
    } else {
      throw new Error(`unknown transcript step ${step.kind}`)
    }
  }
  for (const id of matches) {
    check(`finished (${id})`, transcript.finished, (await resolver.status(id))?.finished)
    await resolver.release(id)
  }
  return { checks, mismatches, resolveMs, ownSnapshot }
}
