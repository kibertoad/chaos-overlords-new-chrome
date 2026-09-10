import { type InferOutput, optional, strictObject } from 'valibot'
import { INT32_MAX, LIMITS } from './limits'
import { integerQueryParam } from './primitives'

/**
 * Query strings.
 *
 * They live apart from the request bodies because they are the one part of the wire the C# client
 * never mirrors as a type: a query string is built by the caller, not deserialized, and a schema
 * whose every field is a text-to-number coercion has no record worth generating.
 *
 * @example
 * ```ts
 * import { parse } from 'valibot'
 * import { eventsQuerySchema } from '@chaos-overlords/contracts'
 *
 * parse(eventsQuerySchema, {}) // => { after: 0, limit: 200 }
 * ```
 */

/**
 * `?after=&limit=`, both optional.
 *
 * The defaults are applied here rather than by the handler, so the contract says what a bare
 * `GET /events` means instead of each caller deciding again. They are written as the text a query
 * string would have carried, because that is what `optional` substitutes and the pipe then coerces:
 * a numeric default would skip the coercion it is standing in for.
 */
export const eventsQuerySchema = strictObject({
  after: optional(integerQueryParam(0, INT32_MAX), '0'),
  limit: optional(integerQueryParam(1, LIMITS.eventsPageSize), String(LIMITS.eventsPageSize)),
})

export type EventsQuery = InferOutput<typeof eventsQuerySchema>
