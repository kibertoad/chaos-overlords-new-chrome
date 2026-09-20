/**
 * The one thing this needs of a context: somewhere to put the value.
 *
 * Structural rather than `Context<AppEnv>`, because a contract route hands its handler a context
 * typed against that route's own environment. Naming only the capability lets the helper be called
 * from both without either side widening.
 */
interface RecordsResponseBody {
  set(key: 'responseBody', value: { value: unknown }): void
}

/**
 * Hands `value` straight back, having noted it on the context for the response validator.
 *
 * Validation happens after the handler, and the only thing it used to be handed was the finished
 * `Response`: to see the value again it cloned the body, parsed the JSON back out and walked the
 * schema over the result. For `GET /snapshots/latest` that is a megabyte stringified, a megabyte
 * cloned, a megabyte parsed and a base64 regex over the result, on every reconnect and every repair
 * adoption. For a sealed set it is up to six order documents parsed and walked a second time.
 *
 * The object the handler produced is the thing worth checking, and it is right here. Noting it
 * turns the second pass into a schema walk over values already in memory, and the body is
 * stringified exactly once.
 *
 * It wraps the value rather than the call to `c.json` so the contract adapter keeps type-checking
 * the handler's response against the route it is mounted from, which is the other half of the same
 * guarantee. Written inline at the return: `return c.json(answering(c, view), 200)`.
 */
export function answering<T>(c: RecordsResponseBody, value: T): T {
  c.set('responseBody', { value })
  return value
}
