import { type InferOutput, looseObject, optional, picklist, strictObject, string } from 'valibot'

/** Status class of a refusal. The machine-readable cause rides `details.reason`. */
export const errorCodeSchema = picklist([
  'bad_request',
  'unauthorized',
  'forbidden',
  'not_found',
  'conflict',
  'validation_failed',
  'payload_too_large',
  'rate_limited',
  'internal',
])

export type ErrorCode = InferOutput<typeof errorCodeSchema>

export const STATUS_BY_CODE: Record<ErrorCode, number> = {
  bad_request: 400,
  unauthorized: 401,
  forbidden: 403,
  not_found: 404,
  conflict: 409,
  validation_failed: 422,
  payload_too_large: 413,
  rate_limited: 429,
  internal: 500,
}

/**
 * The one refusal shape. Every route answers this and nothing else on a failure, so a client parses
 * one body no matter which layer said no.
 *
 * @example
 * ```ts
 * import { parse } from 'valibot'
 * import { errorEnvelopeSchema } from '@chaos-overlords/contracts'
 *
 * const { error } = parse(errorEnvelopeSchema, await response.json())
 * console.log(error.code, error.details?.reason)
 * ```
 */
export const errorEnvelopeSchema = strictObject({
  error: strictObject({
    code: errorCodeSchema,
    message: string(),
    /**
     * Free-form context. `reason` is the machine-readable cause a client branches on and is named
     * here for that; everything beside it (a validation issue list, a retry-after) is for a human
     * reading the log, so the shape stays open rather than enumerating every producer's extras.
     */
    details: optional(looseObject({ reason: optional(string()) })),
    requestId: optional(string()),
  }),
})

export type ErrorEnvelope = InferOutput<typeof errorEnvelopeSchema>
