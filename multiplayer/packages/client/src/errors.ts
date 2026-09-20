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
      envelope = validateSync(errorEnvelopeSchema, JSON.parse(await readCapped(response)))
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

/**
 * What a client will read of a failure body before giving up on it.
 *
 * An error envelope is a few hundred bytes. The thing on the other end of a failed request,
 * though, is not necessarily the server: a proxy, a captive portal or a compromised hop answers
 * with whatever it likes, and `response.json()` reads all of it into memory first. The C# client
 * caps a receipt at the same order of magnitude for the same reason.
 */
const MAXIMUM_ERROR_BODY_BYTES = 64 * 1024

/** The response body as text, truncated at the cap, with the rest of the connection cancelled. */
async function readCapped(response: Response): Promise<string> {
  const body = response.body
  if (!body) return ''
  const reader = body.getReader()
  const decoder = new TextDecoder()
  let text = ''
  try {
    for (;;) {
      const { value, done } = await reader.read()
      if (done) break
      text += decoder.decode(value, { stream: true })
      if (text.length >= MAXIMUM_ERROR_BODY_BYTES) {
        return text.slice(0, MAXIMUM_ERROR_BODY_BYTES)
      }
    }
  } finally {
    await reader.cancel().catch(() => {})
    reader.releaseLock()
  }
  return text
}
