import type { MatchEvent } from '@chaos-overlords/contracts'

/**
 * Parse an SSE body into match events. Frames are separated by a blank line; `data:` carries the
 * JSON event, `id:` its sequence. Comment lines (keepalives) are skipped.
 */
export async function* parseEventStream(
  body: ReadableStream<Uint8Array>,
): AsyncGenerator<MatchEvent> {
  const reader = body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  try {
    while (true) {
      const { value, done } = await reader.read()
      if (done) break
      buffer += decoder.decode(value, { stream: true })
      let boundary = buffer.indexOf('\n\n')
      while (boundary !== -1) {
        const frame = buffer.slice(0, boundary)
        buffer = buffer.slice(boundary + 2)
        const event = parseFrame(frame)
        if (event) yield event
        boundary = buffer.indexOf('\n\n')
      }
    }
  } finally {
    reader.releaseLock()
  }
}

function parseFrame(frame: string): MatchEvent | null {
  const data: string[] = []
  for (const line of frame.split('\n')) {
    if (line.startsWith(':')) continue
    if (line.startsWith('data:')) data.push(line.slice(5).trimStart())
  }
  if (data.length === 0) return null
  return JSON.parse(data.join('\n')) as MatchEvent
}
