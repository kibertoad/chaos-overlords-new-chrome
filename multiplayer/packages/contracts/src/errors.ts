/** Status class of a refusal. The machine-readable cause rides `details.reason`. */
export type ErrorCode =
  | 'bad_request'
  | 'unauthorized'
  | 'forbidden'
  | 'not_found'
  | 'conflict'
  | 'validation_failed'
  | 'payload_too_large'
  | 'rate_limited'
  | 'internal'

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

export interface ErrorEnvelope {
  error: {
    code: ErrorCode
    message: string
    details?: Record<string, unknown> & { reason?: string }
    requestId?: string
  }
}
