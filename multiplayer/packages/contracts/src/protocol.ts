import { type InferOutput, integer, maxValue, minValue, number, pipe, strictObject } from 'valibot'
import { notNegativeZero } from './primitives'

/**
 * Version of the game/server wire protocol. Increment this for every change that can make one side
 * unable to communicate correctly with the other. The C# mirror is MultiplayerProtocolVersion.
 *
 * It says nothing about the sessions a server holds: the handshake settles whether this client and
 * this server can talk at all, and a match already stored is described by
 * {@link MULTIPLAYER_SESSION_VERSION} instead.
 */
export const MULTIPLAYER_PROTOCOL_VERSION = 13

/**
 * Version of the session as it is stored: the match row, its turns, its orders and its snapshots,
 * together with what a client has to know to carry on playing them.
 *
 * Increment this only when a session written by an older build can no longer be resumed correctly
 * by this one — a change to the deterministic rules, to the order document, to the settings a city
 * is generated from, or to how a state is hashed. A wire change that leaves those alone moves
 * {@link MULTIPLAYER_PROTOCOL_VERSION} and leaves this where it is, so an in-flight match survives
 * the client and server being updated underneath it. The C# mirror is MultiplayerSessionVersion.
 */
export const MULTIPLAYER_SESSION_VERSION = 6

export const protocolVersionSchema = pipe(
  number(),
  integer(),
  minValue(0),
  maxValue(2_147_483_647),
  notNegativeZero,
)

export const sessionVersionSchema = pipe(
  number(),
  integer(),
  minValue(0),
  maxValue(2_147_483_647),
  notNegativeZero,
)

export const handshakeRequestSchema = strictObject({
  protocolVersion: protocolVersionSchema,
})

export const handshakeResponseSchema = strictObject({
  protocolVersion: protocolVersionSchema,
})

export type HandshakeRequest = InferOutput<typeof handshakeRequestSchema>
export type HandshakeResponse = InferOutput<typeof handshakeResponseSchema>
