import type { MiddlewareHandler } from 'hono'
import { bodyLimit } from 'hono/body-limit'
import { HTTPException } from 'hono/http-exception'

/**
 * A request body cap that refuses by `Content-Length` before anything opens the body.
 *
 * Hono's `bodyLimit` reads `c.req.raw.body` before it looks at the header. On the Node listener
 * that getter wraps the socket in a web stream which pauses the socket whenever its one-chunk
 * queue is full. After a refusal nobody reads that stream, so the socket stays paused; the
 * listener's half-second drain of the unread body then never moves, and it destroys the keep-alive
 * connection under whatever request the client sent on it next. Refusing on the header leaves the
 * body untouched, the drain reads it off the socket, and the connection carries on.
 *
 * A body without a usable `Content-Length` (chunked, or with `Transfer-Encoding` beside it) still
 * goes through `bodyLimit`, which counts it as it streams. The game client always sends a length,
 * so that path is one only another client takes.
 */
export function bodyCap(maxSize: number): MiddlewareHandler {
  const streamed = bodyLimit({ maxSize })
  return async (c, next) => {
    const length = c.req.header('content-length')
    if (length !== undefined && c.req.header('transfer-encoding') === undefined) {
      // The refusal `bodyLimit` makes, so the error envelope is the same either way. A length that
      // does not parse is let through, as `bodyLimit` lets it through.
      if (Number.parseInt(length, 10) > maxSize) throw new HTTPException(413)
      return next()
    }
    return streamed(c, next)
  }
}
