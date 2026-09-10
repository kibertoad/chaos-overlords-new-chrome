import { z } from 'zod'
import { LIMITS } from './limits'

const sha256Hex = z.string().regex(/^[0-9a-f]{64}$/, 'expected a lowercase hex SHA-256')
const joinCode = z
  .string()
  .regex(new RegExp(`^[A-Z0-9]{${LIMITS.joinCodeLength}}$`), 'expected an 8 character join code')
const displayName = z.string().trim().min(1).max(LIMITS.displayNameLength)
const password = z.string().min(LIMITS.passwordMinLength).max(LIMITS.passwordMaxLength)

const jsonPrimitive = z.union([z.string(), z.number(), z.boolean(), z.null()])
type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue }
const jsonValue: z.ZodType<JsonValue> = z.lazy(() =>
  z.union([jsonPrimitive, z.array(jsonValue), z.record(z.string(), jsonValue)]),
)

/** An opaque object the client owns (scenario, portraits, difficulty…); size-bounded only. */
export const gameSettingsSchema = z
  .record(z.string(), jsonValue)
  .refine((value) => JSON.stringify(value).length <= LIMITS.gameSettingsBytes, {
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

/**
 * One authoritative operation as the game core records it in a replay: a named op with flat
 * scalar arguments. The server never interprets ops; it bounds, stores, hashes and relays them.
 */
export const orderOpSchema = z.object({
  op: z
    .string()
    .min(1)
    .max(LIMITS.opNameLength)
    .regex(/^[a-z][a-zA-Z0-9]*$/, 'op names are lowerCamelCase identifiers'),
  args: z
    .record(
      z.string().min(1).max(LIMITS.opArgKeyLength),
      z.union([
        z.string().max(LIMITS.opArgStringLength),
        z.number().finite(),
        z.boolean(),
        z.null(),
      ]),
    )
    .refine((args) => Object.keys(args).length <= LIMITS.opArgsMaxKeys, {
      message: `an op takes at most ${LIMITS.opArgsMaxKeys} arguments`,
    }),
})

export const orderDocumentSchema = z.object({
  schemaVersion: z.literal(1),
  ops: z.array(orderOpSchema).max(LIMITS.ordersMaxOps),
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
  body: z
    .string()
    .max(LIMITS.snapshotBase64Bytes)
    .regex(/^[A-Za-z0-9+/]*={0,2}$/, 'expected standard base64'),
})

export const eventsQuerySchema = z.object({
  after: z.coerce.number().int().min(0).default(0),
  limit: z.coerce.number().int().min(1).max(LIMITS.eventsPageSize).default(LIMITS.eventsPageSize),
})

export type MatchSettings = z.infer<typeof matchSettingsSchema>
export type MatchVisibility = z.infer<typeof matchVisibilitySchema>
export type CreateMatchRequest = z.infer<typeof createMatchRequestSchema>
export type JoinMatchRequest = z.infer<typeof joinMatchRequestSchema>
export type OrderOp = z.infer<typeof orderOpSchema>
export type OrderDocument = z.infer<typeof orderDocumentSchema>
export type SubmitOrdersRequest = z.infer<typeof submitOrdersRequestSchema>
export type TurnReportRequest = z.infer<typeof turnReportRequestSchema>
export type UploadSnapshotRequest = z.infer<typeof uploadSnapshotRequestSchema>
export type EventsQuery = z.infer<typeof eventsQuerySchema>
