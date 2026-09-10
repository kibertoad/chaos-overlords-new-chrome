import type { MatchEvent } from '@chaos-overlords/contracts'

/**
 * Parse an SSE body into match events. Frames are separated by a blank line; `data:` carries the
 * JSON event, `id:` its sequence. Comment lines (keepalives) are skipped. Line endings may be LF or
 * CRLF, as the event-stream format allows either.
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
  for (const line of frame.split(/\r\n|\n|\r/)) {
    if (line.startsWith(':')) continue
    if (line.startsWith('data:')) data.push(line.slice(5).trimStart())
  }
  if (data.length === 0) return null
  return JSON.parse(data.join('\n')) as MatchEvent
}
