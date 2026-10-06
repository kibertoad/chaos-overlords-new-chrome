// Replays a transcript written by the native tool (Program.cs) through the WebAssembly resolver's
// exports and reports every hash that differs. The same function runs under Node and inside a
// workerd isolate, so it uses nothing but the exports and plain JavaScript.

const fromBase64 = (text) => Uint8Array.from(atob(text), (c) => c.charCodeAt(0));

/**
 * @param {Record<string, Function>} resolver the ResolverExports object of Rechaos.Resolver.Wasm
 * @param {any} transcript the parsed transcript
 * @returns {{ checks: number, mismatches: { what: string, expected: unknown, actual: unknown }[], resolveMs: number }}
 */
export function replayTranscript(resolver, transcript) {
  const mismatches = [];
  let checks = 0;
  const check = (what, expected, actual) => {
    checks++;
    if (expected !== actual) mismatches.push({ what, expected, actual });
  };

  check('session version', transcript.sessionVersion, resolver.SessionVersion());
  const live = resolver.Bootstrap(
    transcript.seed,
    JSON.stringify(transcript.gameSettings),
    JSON.stringify(transcript.players),
  );
  check('bootstrap', transcript.bootstrapHash, resolver.StateHash(live));

  // After the snapshot step, a second match picked up from the native build's snapshot is driven
  // beside the live one, so a snapshot crosses from .NET to WebAssembly and carries on identically.
  let restored = 0;
  const handles = () => (restored ? [live, restored] : [live]);
  let resolveMs = 0;
  for (const step of transcript.steps) {
    if (step.kind === 'handOver') {
      for (const handle of handles()) {
        resolver.HandOverSeat(handle, step.slot, step.toComputer);
        check(`handover of slot ${step.slot} (handle ${handle})`, step.hash, resolver.StateHash(handle));
      }
    } else if (step.kind === 'sealed') {
      const body = JSON.stringify(step.sealedOrders);
      for (const handle of handles()) {
        const started = Date.now();
        const hash = resolver.ApplySealedTurn(handle, body);
        if (handle === live) resolveMs += Date.now() - started;
        check(`turn ${step.sealedOrders.turn} (handle ${handle})`, step.hash, hash);
      }
    } else if (step.kind === 'snapshot') {
      check('state at the snapshot', step.hash, resolver.StateHash(live));
      restored = resolver.Restore(fromBase64(step.savePayload), step.hash);
      // And this build's own payload restores to the hash it was taken at.
      const own = resolver.Restore(resolver.SavePayload(live), step.hash);
      check('own snapshot round trip', step.hash, resolver.StateHash(own));
      resolver.Release(own);
    } else {
      throw new Error(`unknown transcript step ${step.kind}`);
    }
  }
  for (const handle of handles()) {
    check(`finished (handle ${handle})`, transcript.finished, resolver.IsFinished(handle));
    resolver.Release(handle);
  }
  return { checks, mismatches, resolveMs };
}
