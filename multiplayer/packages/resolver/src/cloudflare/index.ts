/**
 * The coordination Worker's side of the Cloudflare host: the resolver Worker's service binding as a
 * {@link MatchResolver}, with snapshot archives written and read here through `node:zlib`, which
 * the coordination Worker has under `nodejs_compat` and the resolver Worker does not.
 */
import { nodeBrotliCodec } from '../codec-node.js'
import { decodeResolverError } from '../errors.js'
import { type MatchResolver, type PayloadResolver, withArchives } from '../resolver.js'
import type { ResolverService } from '../workerd/index.js'

export type { ResolverService } from '../workerd/index.js'

/**
 * The service binding as a {@link PayloadResolver}. An error the resolver Worker raised arrives as a
 * plain error carrying its code in the message; it is turned back into `MatchNotHeldError` or
 * `ResolverRefusedError` here. Anything else (the binding failing, the isolate running out of CPU or
 * memory) rejects as it came.
 */
export function cloudflareResolverHost(binding: ResolverService): PayloadResolver {
  const call = async <T>(matchId: string, run: () => Promise<T>): Promise<T> => {
    try {
      return await run()
    } catch (error: unknown) {
      throw error instanceof Error ? decodeResolverError(error.message, matchId) : error
    }
  }
  return {
    describe: () => binding.describe(),
    bootstrap: (matchId, input) => call(matchId, () => binding.bootstrap(matchId, input)),
    restore: (matchId, payload, stateHash, input) =>
      call(matchId, () => binding.restore(matchId, payload, stateHash, input)),
    applyEvent: (matchId, event, sealedOrders) =>
      call(matchId, () => binding.applyEvent(matchId, event, sealedOrders ?? null)),
    applyEvents: (matchId, fromTurn, steps) =>
      call(matchId, () => binding.applyEvents(matchId, fromTurn, steps)),
    status: (matchId) => call(matchId, () => binding.status(matchId)),
    savePayload: (matchId) => call(matchId, () => binding.savePayload(matchId)),
    release: (matchId) => call(matchId, () => binding.release(matchId)),
  }
}

/** The service binding as the coordination server calls it, snapshots as client archives. */
export function cloudflareMatchResolver(binding: ResolverService): MatchResolver {
  return withArchives(cloudflareResolverHost(binding), nodeBrotliCodec)
}
