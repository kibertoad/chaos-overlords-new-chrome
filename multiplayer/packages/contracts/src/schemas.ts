import { z } from 'zod'
import { LIMITS } from './limits'
import { orderDocumentSchema } from './orders'

const sha256Hex = z.string().regex(/^[0-9a-f]{64}$/, 'expected a lowercase hex SHA-256')
/**
 * A join code, normalised before it is matched: players read these off a chat message or hear them
 * over voice and type them back in whatever case they like, and the alphabet is uppercase only, so
 * refusing `abcd2345` would be refusing a correct code. Surrounding space goes the same way.
 */
const joinCode = z
  .string()
  .trim()
  .toUpperCase()
  .pipe(
    z
      .string()
      .regex(
        new RegExp(`^[A-Z0-9]{${LIMITS.joinCodeLength}}$`),
        `expected a ${LIMITS.joinCodeLength} character join code`,
      ),
  )
const displayName = z.string().trim().min(1).max(LIMITS.displayNameLength)
const password = z.string().min(LIMITS.passwordMinLength).max(LIMITS.passwordMaxLength)

const jsonPrimitive = z.union([z.string(), z.number(), z.boolean(), z.null()])
type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue }

/**
 * Depth-bounded JSON. An unbounded `z.lazy` recursion would let a body that is well within the
 * size limit (a few kilobytes of `[[[[…]]]]`) recurse until the stack overflows, and a `RangeError`
 * is not a `ZodError`, so the caller would get a 500 where a 422 is the truth. The bound is checked
 * before the recursion rather than after it.
 */
const jsonValue = (depth: number = LIMITS.gameSettingsMaxDepth): z.ZodType<JsonValue> =>
  z.lazy(() =>
    depth <= 0
      ? jsonPrimitive
      : z.union([
          jsonPrimitive,
          z.array(jsonValue(depth - 1)),
          z.record(z.string(), jsonValue(depth - 1)),
        ]),
  )

/**
 * UTF-8 length without a `TextEncoder`: this package is deliberately environment-free, imported by
 * the server, a Worker and a browser alike.
 */
function utf8Bytes(text: string): number {
  let bytes = 0
  for (const character of text) {
    const code = character.codePointAt(0) ?? 0
    bytes += code < 0x80 ? 1 : code < 0x800 ? 2 : code < 0x10000 ? 3 : 4
  }
  return bytes
}

/** An opaque object the client owns (scenario, portraits, difficulty…); size-bounded only. */
export const gameSettingsSchema = z
  .record(z.string(), jsonValue())
  .refine((value) => utf8Bytes(JSON.stringify(value)) <= LIMITS.gameSettingsBytes, {
    message: `gameSettings exceeds ${LIMITS.gameSettingsBytes} bytes`,
  })

export const matchVisibilitySchema = z.enum(['public', 'private'])

export const matchSettingsSchema = z.object({
  name: z.string().trim().min(1).max(LIMITS.matchNameLength),
  maxPlayers: z.number().int().min(LIMITS.minPlayers).max(LIMITS.maxPlayers),
  turnTimerSeconds: z.union([
    z.literal(0),
    z.number().int().min(LIMITS.turnTimerMinSeconds).max(LIMITS.turnTimerMaxSeconds),
  ]),
  visibility: matchVisibilitySchema,
  gameSettings: gameSettingsSchema,
})

export const createMatchRequestSchema = z.object({
  settings: matchSettingsSchema,
  hostDisplayName: displayName,
  password: password.optional(),
})

export const joinMatchRequestSchema = z.object({
  joinCode,
  displayName,
  password: password.optional(),
})

export const submitOrdersRequestSchema = z.object({
  orders: orderDocumentSchema,
  /** `true` = the player has finished planning; the turn seals once every human is ready. */
  ready: z.boolean(),
})

export const turnReportRequestSchema = z.object({
  /** Canonical state hash after the client applied the sealed turn. */
  stateHash: sha256Hex,
  /** The client observed a completed match after this turn. */
  finished: z.boolean(),
})

export const uploadSnapshotRequestSchema = z.object({
  turn: z.number().int().min(0),
  formatVersion: z.number().int().min(1),
  stateHash: sha256Hex,
  /**
   * The client's native snapshot. The server never decodes it, so this is the only chance to
   * notice that it is not decodable at all: the alphabet, the padding, and the length, which
   * standard base64 always makes a multiple of four.
   */
  body: z
    .string()
    .max(LIMITS.snapshotBase64Bytes)
    .regex(/^[A-Za-z0-9+/]*={0,2}$/, 'expected standard base64')
    .refine((value) => value.length % 4 === 0, 'base64 length must be a multiple of four'),
})

export const eventsQuerySchema = z.object({
  after: z.coerce.number().int().min(0).default(0),
  limit: z.coerce.number().int().min(1).max(LIMITS.eventsPageSize).default(LIMITS.eventsPageSize),
})

export type MatchSettings = z.infer<typeof matchSettingsSchema>
export type MatchVisibility = z.infer<typeof matchVisibilitySchema>
export type CreateMatchRequest = z.infer<typeof createMatchRequestSchema>
export type JoinMatchRequest = z.infer<typeof joinMatchRequestSchema>
export type SubmitOrdersRequest = z.infer<typeof submitOrdersRequestSchema>
export type TurnReportRequest = z.infer<typeof turnReportRequestSchema>
export type UploadSnapshotRequest = z.infer<typeof uploadSnapshotRequestSchema>
export type EventsQuery = z.infer<typeof eventsQuerySchema>
