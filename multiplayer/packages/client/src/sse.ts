import { type MatchEvent, matchEventSchema, validateSync } from '@chaos-overlords/contracts'

/**
 * Parse an SSE body into match events. Frames are separated by a blank line; `data:` carries the
 * JSON event and `id:` its sequence. Comment lines (keepalives) are skipped. Line endings may be LF
 * or CRLF, as the event-stream format allows either.
 *
 * The `id:` field is read and reconciled with the `seq` inside the JSON rather than ignored. They
 * are written from the same number, so a disagreement means the frame was mangled in transit or the
 * server is not the one this client thinks it is; resuming from the wrong number would skip events
 * silently, so the frame is refused instead. This also keeps the stream honest for a client built on
 * a stock `EventSource`, which resumes from `id:` and never looks inside the payload.
 *
 * Leaving the loop early (a `break` in the consumer, or an error) cancels the body rather than only
 * releasing the lock, so the underlying connection is closed instead of being left to a collector.
 */
export interface ParseOptions {
  /**
   * Give up on a connection that has carried nothing, not even a keepalive, for this long. A
   * half-open TCP connection (a suspended laptop, a NAT entry that expired, a network switch)
   * delivers no error and no end; without a deadline the read waits for the operating system's
   * keepalive, which is tens of minutes away, while the player misses every seal. `0` disables it.
   */
  idleTimeoutMs?: number
}

export class StreamIdleError extends Error {
  constructor(idleMs: number) {
    super(`event stream carried nothing for ${idleMs} ms`)
    this.name = 'StreamIdleError'
  }
}

export async function* parseEventStream(
  body: ReadableStream<Uint8Array>,
  options: ParseOptions = {},
): AsyncGenerator<MatchEvent> {
  const reader = body.getReader()
  const decoder = new TextDecoder()
  const idleMs = options.idleTimeoutMs ?? 0
  let buffer = ''
  let drained = false
  try {
    while (true) {
      const { value, done } = await readWithDeadline(reader, idleMs)
      if (done) {
        drained = true
        break
      }
      buffer += decoder.decode(value, { stream: true })
      for (const frame of takeFrames()) {
        const event = parseFrame(frame)
        if (event) yield event
      }
    }
  } finally {
    if (!drained) await reader.cancel().catch(() => {})
    reader.releaseLock()
  }

  function* takeFrames(): Generator<string> {
    for (;;) {
      const match = /\r\n\r\n|\n\n|\r\r/.exec(buffer)
      if (!match) return
      const frame = buffer.slice(0, match.index)
      buffer = buffer.slice(match.index + match[0].length)
      yield frame
    }
  }
}

/** One read, abandoned (and the connection with it) when the idle deadline passes first. */
async function readWithDeadline(
  reader: ReadableStreamDefaultReader<Uint8Array>,
  idleMs: number,
): Promise<ReadableStreamReadResult<Uint8Array>> {
  if (idleMs <= 0) return reader.read()
  let timer: ReturnType<typeof setTimeout> | undefined
  const deadline = new Promise<never>((_, reject) => {
    timer = setTimeout(() => reject(new StreamIdleError(idleMs)), idleMs)
  })
  try {
    return await Promise.race([reader.read(), deadline])
  } finally {
    clearTimeout(timer)
  }
}

function parseFrame(frame: string): MatchEvent | null {
  const data: string[] = []
  let id: string | undefined
  for (const line of frame.split(/\r\n|\n|\r/)) {
    if (line.startsWith(':')) continue
    if (line.startsWith('data:')) data.push(line.slice(5).trimStart())
    else if (line.startsWith('id:')) id = line.slice(3).trim()
  }
  if (data.length === 0) return null
  const event = validateSync(matchEventSchema, JSON.parse(data.join('\n')))
  if (id !== undefined && Number(id) !== event.seq) {
    throw new Error(`event stream frame id ${id} disagrees with its payload seq ${event.seq}`)
  }
  return event
}
