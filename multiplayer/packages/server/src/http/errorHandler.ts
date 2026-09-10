import { type ErrorCode, type ErrorEnvelope, STATUS_BY_CODE } from '@chaos-overlords/contracts'
import { isDomainError } from '@chaos-overlords/kernel'
import type { Context } from 'hono'
import { HTTPException } from 'hono/http-exception'
import { ZodError } from 'zod'
import type { AppEnv } from './types'

/** The ONE producer of the error envelope. Controllers throw; nothing else builds `{ error }`. */
export function handleError(error: Error, c: Context<AppEnv>): Response {
  const requestId = c.get('requestId')
  if (isDomainError(error)) {
    return respond(c, error.code, error.message, error.details, requestId)
  }
  if (error instanceof ZodError) {
    return respond(
      c,
      'validation_failed',
      'Request body failed validation',
      { reason: 'invalid_body', issues: error.issues },
      requestId,
    )
  }
  if (error instanceof HTTPException) {
    const code = codeForStatus(error.status)
    return respond(c, code, error.message || code, { reason: `http_${error.status}` }, requestId)
  }
  c.get('container')?.kernel.deps.logger.error('unhandled request error', {
    requestId,
    error: error.stack ?? String(error),
  })
  return respond(c, 'internal', 'Internal server error', { reason: 'internal' }, requestId)
}

/**
 * The error code a framework `HTTPException` maps to. Written out rather than derived by scanning
 * `STATUS_BY_CODE` for a matching value: that scan silently picks whichever code was declared first
 * the moment two of them share a status, and the mapping is worth being able to read.
 */
const CODE_BY_STATUS: Readonly<Record<number, ErrorCode>> = {
  400: 'bad_request',
  401: 'unauthorized',
  403: 'forbidden',
  404: 'not_found',
  409: 'conflict',
  413: 'payload_too_large',
  422: 'validation_failed',
  429: 'rate_limited',
}

function codeForStatus(status: number): ErrorCode {
  return CODE_BY_STATUS[status] ?? 'internal'
}

function respond(
  c: Context<AppEnv>,
  code: ErrorCode,
  message: string,
  details: NonNullable<ErrorEnvelope['error']['details']>,
  requestId: string,
): Response {
  const body: ErrorEnvelope = { error: { code, message, details, requestId } }
  return c.json(body, STATUS_BY_CODE[code] as 400)
}
