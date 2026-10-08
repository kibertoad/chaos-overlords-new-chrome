/**
 * The two ways a resolver call fails that a caller acts on differently.
 *
 * Both carry a `code` so they survive the trip across a worker thread or a Durable Object call,
 * where a thrown class arrives as a plain error with its message: {@link resolverErrorCode} reads it
 * back.
 */

/**
 * The host does not hold the match: it was never built here, was evicted to stay within the memory
 * budget, or went with a runtime that was lost. The caller rebuilds it from a snapshot and the facts
 * after it.
 */
export class MatchNotHeldError extends Error {
  readonly code = 'match_not_held'

  constructor(readonly matchId: string) {
    super(`resolver: match ${matchId} is not held here`)
    this.name = 'MatchNotHeldError'
  }
}

/**
 * The resolver refused what it was given: a sealed set that does not match its own digest or is for
 * another turn, a snapshot that does not hash to what it is stored under, settings this build cannot
 * read. Rebuilding does not help; the input is the problem.
 */
export class ResolverRefusedError extends Error {
  readonly code = 'resolver_refused'

  constructor(message: string) {
    super(`resolver: ${message}`)
    this.name = 'ResolverRefusedError'
  }
}

export type ResolverErrorCode = 'match_not_held' | 'resolver_refused'

const CODE_PREFIX = /^\[(match_not_held|resolver_refused)\] /

/** The message an error crosses a structured-clone boundary with, its code in front. */
export function encodeResolverError(error: unknown): string {
  if (error instanceof MatchNotHeldError || error instanceof ResolverRefusedError) {
    return `[${error.code}] ${error.message}`
  }
  return error instanceof Error ? error.message : String(error)
}

/** The code an error carries, whether it is the class itself or one rebuilt from its message. */
export function resolverErrorCode(error: unknown): ResolverErrorCode | undefined {
  if (error instanceof MatchNotHeldError || error instanceof ResolverRefusedError) return error.code
  if (error instanceof Error) return CODE_PREFIX.exec(error.message)?.[1] as ResolverErrorCode
  return undefined
}

/** Rebuilds the class an encoded message stands for, so callers can `instanceof` it. */
export function decodeResolverError(message: string, matchId: string): Error {
  const match = CODE_PREFIX.exec(message)
  if (!match) return new Error(message)
  const text = message.slice(match[0].length).replace(/^resolver: /, '')
  return match[1] === 'match_not_held'
    ? new MatchNotHeldError(matchId)
    : new ResolverRefusedError(text)
}
