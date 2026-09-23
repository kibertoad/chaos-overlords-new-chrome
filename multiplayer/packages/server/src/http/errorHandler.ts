import {
  type ErrorCode,
  type ErrorEnvelope,
  errorEnvelopeSchema,
  STATUS_BY_CODE,
  validateSync,
} from '@chaos-overlords/contracts'
import { isDomainError } from '@chaos-overlords/kernel'
import { SchemaValidationError } from '@toad-contracts/core'
import type { Context } from 'hono'
import { HTTPException } from 'hono/http-exception'
import type { AppEnv } from './types'

/** The ONE producer of the error envelope. Controllers throw; nothing else builds `{ error }`. */
export function handleError(error: Error, c: Context<AppEnv>): Response {
  const requestId = c.get('requestId')
  if (isDomainError(error)) {
    return respond(c, {
      code: error.code,
      message: error.message,
      details: error.details,
      requestId,
    })
  }
  if (error instanceof SchemaValidationError) {
    // The contract validator refused a path param, a query value or the body. The issue paths and
    // messages come straight from valibot rather than being restated per endpoint, so a client is
    // told which field it got wrong.
    return respond(c, {
      code: 'validation_failed',
      message: 'Request failed contract validation',
      details: { reason: 'invalid_request', issues: error.issues.map(describeIssue) },
      requestId,
    })
  }
  if (error instanceof HTTPException) {
    const code = codeForStatus(error.status)
    return respond(c, {
      code,
      message: error.message || code,
      details: { reason: `http_${error.status}` },
      requestId,
    })
  }
  c.get('container')?.kernel.deps.logger.error('unhandled request error', {
    requestId,
    error: error.stack ?? String(error),
  })
  return respond(c, {
    code: 'internal',
    message: 'Internal server error',
    details: { reason: 'internal' },
    requestId,
  })
}

/**
 * What a client is told about a refused field: where and why, never what it sent.
 *
 * A validator's issue carries the offending input beside the message (valibot's `input` and
 * `received`, and the value at every step of the path). Echoing it would put a mistyped password
 * or a whole order document into the response and into any proxy log on the way.
 */
function describeIssue(issue: SchemaValidationError['issues'][number]): {
  message: string
  path: string[]
} {
  const path = (issue.path ?? []).map((segment: unknown) =>
    String(
      typeof segment === 'object' && segment !== null && 'key' in segment ? segment.key : segment,
    ),
  )
  return { message: describeMessage(issue), path }
}

/**
 * The message with the received value taken back out of it.
 *
 * Stripping valibot's `input` and `received` FIELDS was not enough, because its default message
 * embeds the same value in prose: every schema step without a message of its own reports
 * `Invalid type: Expected string but received "<value>"`, and the same shape covers `literal`,
 * `picklist`, `minValue` and `maxValue`. So `{ "password": 123456 }` answered with the password,
 * and `"visibility": "secret"` answered with the word. A wrong password that is at least a STRING
 * fails on its length instead and was never echoed, which is why the conformance case for the
 * no-echo rule passed while the rule did not hold.
 *
 * What the caller needs is which field and what was expected; what it sent, it already knows.
 */
function describeMessage(issue: { message: string; received?: unknown }): string {
  if (issue.received === undefined) return issue.message
  const withoutReceived = issue.message.replace(/\s*but received\b.*$/is, '')
  return withoutReceived === '' ? 'Invalid value' : withoutReceived
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

/** The parts of the envelope a caller supplies; the response status follows from `code`. */
interface ErrorBody {
  code: ErrorCode
  message: string
  details: NonNullable<ErrorEnvelope['error']['details']>
  requestId: string
}

function respond(c: Context<AppEnv>, { code, message, details, requestId }: ErrorBody): Response {
  const body: ErrorEnvelope = { error: { code, message, details, requestId } }
  // Hono retains headers already set on this context when an error replaces a handler response.
  // A sealed-orders handler may have marked that response immutable before validation failed.
  c.header('Cache-Control', 'no-store')
  return c.json(validateSync(errorEnvelopeSchema, body), STATUS_BY_CODE[code] as 400)
}
