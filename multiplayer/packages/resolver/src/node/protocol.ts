/** The messages between the Node host and its worker thread. */

export type ThreadMethod =
  | 'describe'
  | 'info'
  | 'bootstrap'
  | 'restore'
  | 'applyEvent'
  | 'applyEvents'
  | 'status'
  | 'savePayload'
  | 'seatViewPayload'
  | 'release'
  | 'heldMatches'

export interface ThreadRequest {
  id: number
  method: ThreadMethod
  args: unknown[]
}

export type ThreadReply =
  | { id: number; ok: true; value: unknown }
  | { id: number; ok: false; error: string }

export type ThreadStartup = { ready: true } | { ready: false; error: string }
