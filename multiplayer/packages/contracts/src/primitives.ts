import {
  check,
  integer,
  maxLength,
  maxValue,
  minLength,
  minValue,
  number,
  pipe,
  regex,
  string,
  toUpperCase,
  transform,
  trim,
} from 'valibot'
import { INT32_MAX, INT32_MIN, LIMITS } from './limits'

/**
 * The scalar shapes every request, view and event is built from.
 *
 * Two rules run through all of them and both exist for the C# client:
 *
 * 1. **Every integer states both bounds.** The bounds are what let the generated C# hold the value
 *    in an `int` instead of a `long` or a `double`; an unbounded JavaScript integer runs to 2^53
 *    and has no narrower type that could not refuse a legal value.
 * 2. **No number is ever a float.** The order digest is taken over canonical JSON, and a float's
 *    shortest round-trip spelling is not portable (`1e+21` from JavaScript, `1E+21` from .NET), so
 *    a value with no shared text form is a digest no other language could reproduce.
 *
 * @example
 * ```ts
 * import { safeParse } from 'valibot'
 * import { slotSchema } from '@chaos-overlords/contracts'
 *
 * safeParse(slotSchema, 6).success // false: there are six slots, 0 through 5
 * ```
 */

export const NEGATIVE_ZERO_MESSAGE = 'negative zero has no portable JSON form'

/**
 * The `-0` guard, shared by every integer on the wire.
 *
 * `-0` passes every numeric comparison a range check makes, but it has no portable canonical JSON
 * form, and the order digest is taken over exactly that text. Letting it through here would turn a
 * doctored request into a canonicalisation failure deeper in, which is a 500 where a 422 is the
 * truth.
 */
export const notNegativeZero = check(
  (value: number) => !Object.is(value, -0),
  NEGATIVE_ZERO_MESSAGE,
)

/** A player's seat, 0 through 5. `MatchLimits.PlayerCount`. */
export const slotSchema = pipe(number(), integer(), minValue(0), maxValue(5), notNegativeZero)

/** A seat as a view reports it: the same range, or -1 for a player still in the lobby. */
export const seatSchema = pipe(number(), integer(), minValue(-1), maxValue(5), notNegativeZero)

/** A count of players, 0 through the six seats. */
export const playerCountSchema = pipe(
  number(),
  integer(),
  minValue(0),
  maxValue(6),
  notNegativeZero,
)

/** A turn number. Turn 0 is the lobby; the client holds it in an `int`. */
export const turnNumberSchema = pipe(
  number(),
  integer(),
  minValue(0),
  maxValue(INT32_MAX),
  notNegativeZero,
)

/** An event's position in a match's log. Allocated by the insert, gapless, ever-increasing. */
export const eventSeqSchema = pipe(
  number(),
  integer(),
  minValue(0),
  maxValue(INT32_MAX),
  notNegativeZero,
)

/**
 * The match seed, drawn to fit `MatchSetup.InitialSeed`.
 *
 * It is a SIGNED 32-bit integer: a client that deserializes it into anything narrower, or into
 * anything unsigned, rejects half of all matches.
 */
export const seedSchema = pipe(
  number(),
  integer(),
  minValue(INT32_MIN),
  maxValue(INT32_MAX),
  notNegativeZero,
)

/** A native save format version (`MatchReplaySerializer.CurrentFormatVersion` and its lineage). */
export const formatVersionSchema = pipe(
  number(),
  integer(),
  minValue(1),
  maxValue(INT32_MAX),
  notNegativeZero,
)

/** A lowercase hex SHA-256, the only digest shape the protocol carries. */
export const sha256HexSchema = pipe(
  string(),
  regex(/^[0-9a-f]{64}$/, 'expected a lowercase hex SHA-256'),
)

/**
 * An ISO-8601 instant, as `Date.prototype.toISOString` writes it.
 *
 * Pinning the spelling rather than accepting anything `Date` can parse keeps the C# side on
 * `DateTimeOffset.Parse` with a round-trip format instead of a guess, and keeps a server that
 * formats its timestamps differently from being silently tolerated until a client trips over it.
 */
export const isoTimestampSchema = pipe(
  string(),
  regex(
    /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,3})?Z$/,
    'expected an ISO-8601 UTC timestamp',
  ),
)

/**
 * An opaque server-minted id (match, player); `crypto.randomUUID` by default.
 *
 * The alphabet is narrowed to what is safe in a path segment, not merely bounded in length. Every
 * URL a client builds interpolates one of these, and a contract's `pathResolver` cannot escape them
 * (the same resolver is what derives the `:param` route pattern, so an escaped colon would mangle
 * it). Refusing an id that could carry a `/` or a `?` at the point it is parsed is what makes the
 * un-escaped interpolation safe.
 */
export const resourceIdSchema = pipe(
  string(),
  regex(/^[A-Za-z0-9_-]{1,64}$/, 'expected a URL-safe id'),
)

/** A bearer token, shown once on create or join. */
export const tokenSchema = pipe(string(), minLength(1), maxLength(256))

/** A lobby's name, as the host typed it. */
export const matchNameSchema = pipe(
  string(),
  trim(),
  minLength(1),
  maxLength(LIMITS.matchNameLength),
)

export const displayNameSchema = pipe(
  string(),
  trim(),
  minLength(1),
  maxLength(LIMITS.displayNameLength),
)

export const passwordSchema = pipe(
  string(),
  minLength(LIMITS.passwordMinLength),
  maxLength(LIMITS.passwordMaxLength),
)

/**
 * A join code as it is stored: the uppercase alphabet the server draws from.
 *
 * @see joinCodeInputSchema for the normalising form requests use.
 */
export const joinCodeSchema = pipe(
  string(),
  regex(
    new RegExp(`^[A-Z0-9]{${LIMITS.joinCodeLength}}$`),
    `expected a ${LIMITS.joinCodeLength} character join code`,
  ),
)

/**
 * A join code as a player types it.
 *
 * Players read these off a chat message or hear them over voice and type them back in whatever
 * case they like, and the alphabet is uppercase only, so refusing `abcd2345` would be refusing a
 * correct code. Surrounding space goes the same way.
 */
export const joinCodeInputSchema = pipe(
  string(),
  trim(),
  toUpperCase(),
  regex(
    new RegExp(`^[A-Z0-9]{${LIMITS.joinCodeLength}}$`),
    `expected a ${LIMITS.joinCodeLength} character join code`,
  ),
)

/**
 * A base64 body the server stores but never decodes.
 *
 * That makes this the only chance to notice it could not be decoded at all: the alphabet, the
 * padding, and the length, which standard base64 always makes a multiple of four.
 */
export const base64BodySchema = pipe(
  string(),
  maxLength(LIMITS.snapshotBase64Bytes),
  regex(/^[A-Za-z0-9+/]*={0,2}$/, 'expected standard base64'),
  check((value) => value.length % 4 === 0, 'base64 length must be a multiple of four'),
)

/**
 * The turn number in a path, which arrives as text and means a bounded integer.
 *
 * Declaring the coercion on the contract is what keeps every handler from re-parsing `:turn` and
 * re-deciding what a bad one means; `c.req.valid('param').turn` is already the number.
 */
export const turnPathParamSchema = pipe(
  string(),
  regex(/^\d+$/, 'expected a non-negative integer turn'),
  transform(Number),
  number(),
  integer(),
  minValue(0),
  maxValue(INT32_MAX),
)

/**
 * A query parameter that arrives as text and means a bounded integer.
 *
 * Hono hands query values through as strings, so the coercion belongs to the schema rather than to
 * every handler that reads one. `Number('')` is 0 and `Number('x')` is NaN, and both have to fail
 * rather than default, or a typo silently reads the log from the beginning.
 */
export function integerQueryParam(min: number, max: number) {
  return pipe(
    string(),
    regex(/^\d+$/, 'expected a non-negative integer'),
    transform(Number),
    number(),
    integer(),
    minValue(min),
    maxValue(max),
  )
}
