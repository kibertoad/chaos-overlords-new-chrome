import type { MatchEvent } from '@chaos-overlords/contracts'

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
export async function* parseEventStream(
  body: ReadableStream<Uint8Array>,
): AsyncGenerator<MatchEvent> {
  const reader = body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  let drained = false
  try {
    while (true) {
      const { value, done } = await reader.read()
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

function parseFrame(frame: string): MatchEvent | null {
  const data: string[] = []
  let id: string | undefined
  for (const line of frame.split(/\r\n|\n|\r/)) {
    if (line.startsWith(':')) continue
    if (line.startsWith('data:')) data.push(line.slice(5).trimStart())
    else if (line.startsWith('id:')) id = line.slice(3).trim()
  }
  if (data.length === 0) return null
  const event = JSON.parse(data.join('\n')) as MatchEvent
  if (id !== undefined && Number(id) !== event.seq) {
    throw new Error(`event stream frame id ${id} disagrees with its payload seq ${event.seq}`)
  }
  return event
}
