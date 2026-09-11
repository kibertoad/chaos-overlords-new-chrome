import type { Principal } from '@chaos-overlords/kernel'
import { NotFoundError } from '@chaos-overlords/kernel'

/**
 * The principal, checked against the `:matchId` the request named.
 *
 * A token for another match answers 404, never 403, so a player cannot probe which match ids exist.
 *
 * It takes the two values rather than the context because each contract handler's context is typed
 * from its own contract, and Hono's `Context` is invariant in its env: one guard signature could
 * not accept all of them.
 */
export function requireMember(principal: Principal, matchId: string): Principal {
  if (principal.match.id !== matchId) {
    throw new NotFoundError('No such match', { reason: 'unknown_match' })
  }
  return principal
}
