// Replays a transcript written by tools/ResolverDeterminism (Program.cs) through a MatchResolver
// and lists every hash that differs from the native build's. The tests and the determinism check
// run it against both hosts.
//
// A transcript is the match's event log as the server stores it (a step per event, a seal with its
// sealed set), with the hash after each, and a snapshot taken after turn 10.

/**
 * @param {import('../../dist/index.js').MatchResolver} resolver
 * @param {any} transcript
 * @param {string} [prefix] the matches are `${prefix}live`, `${prefix}restored` and `${prefix}walked`
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
  const walked = `${prefix}walked`
  const players = transcript.players
  check('session version', transcript.sessionVersion, (await resolver.describe()).sessionVersion)
  const boot = await resolver.bootstrap(live, {
    seed: transcript.seed,
    gameSettings: transcript.gameSettings,
    players,
  })
  check('bootstrap', transcript.bootstrapHash, boot.stateHash)

  // After the snapshot step, a second match picked up from the native build's snapshot is fed the
  // events after it beside the live one, so a snapshot crosses from .NET to WebAssembly and carries
  // on identically.
  let matches = [live]
  let snapshot
  let resolveMs = 0
  /** This host's own snapshot of the live match at the snapshot step. */
  let ownSnapshot
  for (const step of transcript.steps) {
    if (step.kind === 'event') {
      for (const id of matches) {
        const started = Date.now()
        const status = await resolver.applyEvent(id, step.event, step.sealedOrders)
        if (id === live) resolveMs += Date.now() - started
        check(`${step.event.type} at ${step.event.seq} (${id})`, step.hash, status.stateHash)
      }
    } else if (step.kind === 'snapshot') {
      snapshot = step
      check('state at the snapshot', step.hash, (await resolver.status(live))?.stateHash)
      // The archive the native build wrote, as a client uploads it.
      const picked = await resolver.restore(
        restored,
        { body: step.archive, stateHash: step.hash },
        { players },
      )
      check('native snapshot restored', step.hash, picked.stateHash)
      matches = [live, restored]
      // A body without the archive header is read as a bare payload, as clients read an old upload.
      const bare = await resolver.restore(
        `${prefix}bare`,
        { body: step.savePayload, stateHash: step.hash },
        { players },
      )
      check('bare payload restored', step.hash, bare.stateHash)
      await resolver.release(`${prefix}bare`)
      // And this host's own archive restores to the hash it was taken at.
      const own = await resolver.snapshot(live)
      ownSnapshot = own
      check('own snapshot hash', step.hash, own.stateHash)
      const roundTrip = await resolver.restore(`${prefix}own`, own, { players })
      check('own snapshot round trip', step.hash, roundTrip.stateHash)
      await resolver.release(`${prefix}own`)
    } else {
      throw new Error(`unknown transcript step ${step.kind}`)
    }
  }
  const final = transcript.steps.findLast((step) => step.kind === 'event')?.hash

  // A match picked up from the snapshot folds the whole log from its start, as a reconnecting
  // client does: the seals and the handovers the snapshot holds are passed over.
  if (snapshot) {
    await resolver.restore(
      walked,
      { body: snapshot.archive, stateHash: snapshot.hash },
      { players, logTurn: 1 },
    )
    let status
    for (const step of transcript.steps) {
      if (step.kind === 'event')
        status = await resolver.applyEvent(walked, step.event, step.sealedOrders)
    }
    check('whole log folded over the snapshot', final, status?.stateHash)
    matches.push(walked)
  }

  for (const id of matches) {
    const status = await resolver.status(id)
    check(`final hash (${id})`, final, status?.stateHash)
    check(`finished (${id})`, transcript.finished, status?.finished)
    await resolver.release(id)
  }
  return { checks, mismatches, resolveMs, ownSnapshot }
}
