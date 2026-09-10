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

function codeForStatus(status: number): ErrorCode {
  const match = (Object.entries(STATUS_BY_CODE) as Array<[ErrorCode, number]>).find(
    ([, value]) => value === status,
  )
  return match?.[0] ?? 'internal'
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
