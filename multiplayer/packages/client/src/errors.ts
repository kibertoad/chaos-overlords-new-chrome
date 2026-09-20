import {
  type ErrorCode,
  type ErrorEnvelope,
  errorEnvelopeSchema,
  validateSync,
} from '@chaos-overlords/contracts'

/** The decoded error envelope a failed response carried, or the defaults stood in for it. */
export interface MultiplayerApiErrorInit {
  status: number
  code: ErrorCode
  message: string
  reason: string | undefined
  details: Record<string, unknown> | undefined
  requestId: string | undefined
}

export class MultiplayerApiError extends Error {
  readonly status: number
  readonly code: ErrorCode
  readonly reason: string | undefined
  readonly details: Record<string, unknown> | undefined
  readonly requestId: string | undefined

  constructor(init: MultiplayerApiErrorInit) {
    super(init.message)
    this.name = 'MultiplayerApiError'
    this.status = init.status
    this.code = init.code
    this.reason = init.reason
    this.details = init.details
    this.requestId = init.requestId
  }

  static async fromResponse(response: Response): Promise<MultiplayerApiError> {
    let envelope: Partial<ErrorEnvelope> = {}
    try {
      envelope = validateSync(errorEnvelopeSchema, await response.json())
    } catch {
      // A non-JSON body (a proxy page, an empty 502) still yields a typed error.
    }
    const error = envelope.error
    return new MultiplayerApiError({
      status: response.status,
      code: error?.code ?? 'internal',
      message: error?.message ?? `HTTP ${response.status}`,
      reason: error?.details?.reason,
      details: error?.details,
      requestId: error?.requestId,
    })
  }
}
