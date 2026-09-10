import type { Principal } from '@chaos-overlords/kernel'
import { NotFoundError, ValidationError } from '@chaos-overlords/kernel'
import type { Context } from 'hono'
import type { ZodType } from 'zod'
import type { AppEnv } from './types'

/**
 * The principal, checked against the `:matchId` in the path. A token for another match answers
 * 404, never 403, so a player cannot probe which match ids exist.
 */
export function requireMember(c: Context<AppEnv>): Principal {
  const principal = c.get('principal')
  if (principal.match.id !== c.req.param('matchId')) {
    throw new NotFoundError('No such match', { reason: 'unknown_match' })
  }
  return principal
}

export function turnParam(c: Context<AppEnv>): number {
  const raw = c.req.param('turn')
  const number = Number(raw)
  if (!Number.isInteger(number) || number < 0) {
    throw new ValidationError('Turn must be a non-negative integer', { reason: 'invalid_turn' })
  }
  return number
}

export async function parseBody<T>(c: Context<AppEnv>, schema: ZodType<T>): Promise<T> {
  let raw: unknown
  try {
    raw = await c.req.json()
  } catch {
    throw new ValidationError('Request body must be JSON', { reason: 'invalid_json' })
  }
  return schema.parse(raw)
}
