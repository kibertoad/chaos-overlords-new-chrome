import type { ErrorCode, ErrorEnvelope } from '@chaos-overlords/contracts'

export class MultiplayerApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: ErrorCode,
    message: string,
    readonly reason: string | undefined,
    readonly details: Record<string, unknown> | undefined,
    readonly requestId: string | undefined,
  ) {
    super(message)
    this.name = 'MultiplayerApiError'
  }

  static async fromResponse(response: Response): Promise<MultiplayerApiError> {
    let envelope: Partial<ErrorEnvelope> = {}
    try {
      envelope = (await response.json()) as ErrorEnvelope
    } catch {
      // A non-JSON body (a proxy page, an empty 502) still yields a typed error.
    }
    const error = envelope.error
    return new MultiplayerApiError(
      response.status,
      error?.code ?? 'internal',
      error?.message ?? `HTTP ${response.status}`,
      error?.details?.reason,
      error?.details,
      error?.requestId,
    )
  }
}
