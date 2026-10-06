// The defaults live apart from index.ts, which imports `cloudflare:workers`, so that Node can read them.
import type { ResolverLimits } from '../core.js'

/**
 * How much one isolate holds. Cloudflare gives an isolate 128 MB, and Durable Objects of the same
 * class share isolates, so the matches of every object in one isolate share these limits. The .NET
 * runtime's memory only grows and reaches about two to three times the live managed heap; a 26-turn
 * match is about 1.5 MiB of it and a 104-turn match about 8 MiB, and with these limits the memory
 * stays near 100 MiB even with three matches of 104 turns (measured for #511).
 */
export const WORKERD_RESOLVER_DEFAULTS: ResolverLimits = {
  maxMatches: 4,
  managedHeapBudgetBytes: 24 * 1024 * 1024,
}
