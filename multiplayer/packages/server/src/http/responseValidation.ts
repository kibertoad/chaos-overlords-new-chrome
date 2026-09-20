import { resolveResponseEntry, validate } from '@toad-contracts/core'
import type { MiddlewareHandler } from 'hono'
import type { AppEnv } from './types'

/**
 * Refuses a response that does not match the same contract which mounted its route.
 *
 * The Hono adapter constrains handlers at compile time, but a database, a JSON column, or an
 * unsafe cast can still put a different runtime value behind a correct TypeScript type. This is
 * the last server-side boundary before those values become wire JSON, so it turns producer drift
 * into a local 500. Error envelopes are validated directly by their single producer.
 */
export const validateContractResponse: MiddlewareHandler<AppEnv> = async (c, next) => {
  await next()
  const contract = c.get('apiContract')
  if (!contract) return

  const contentType = c.res.headers.get('content-type') ?? undefined
  const responseKind = resolveResponseEntry(
    contract.responsesByStatusCode,
    c.res.status,
    contentType,
    true,
  )
  if (!responseKind) {
    throw new ContractResponseError(
      `status ${c.res.status} with content type ${contentType ?? '(none)'} is not in the contract`,
    )
  }
  if (responseKind.kind !== 'json') return

  try {
    // The handler's own object when it recorded one (`contractJson`), which is every contract
    // route: checking that is checking what was serialised, without serialising it again. A
    // response that arrives without one is still read back rather than trusted.
    const recorded = c.get('responseBody')
    await validate(responseKind.schema, recorded ? recorded.value : await c.res.clone().json())
  } catch (error) {
    throw new ContractResponseError(
      `status ${c.res.status} body does not match the contract`,
      error,
    )
  }
}

export class ContractResponseError extends Error {
  constructor(message: string, cause?: unknown) {
    super(message, { cause })
    this.name = 'ContractResponseError'
  }
}
