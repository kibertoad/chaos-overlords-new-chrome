import { type InferOutput, integer, minValue, number, pipe, strictObject } from 'valibot'

/**
 * Version of the game/server wire protocol. Increment this for every change that can make one side
 * unable to communicate correctly with the other. The C# mirror is MultiplayerProtocolVersion.
 */
export const MULTIPLAYER_PROTOCOL_VERSION = 1

export const protocolVersionSchema = pipe(number(), integer(), minValue(0))

export const handshakeRequestSchema = strictObject({
  protocolVersion: protocolVersionSchema,
})

export const handshakeResponseSchema = strictObject({
  protocolVersion: protocolVersionSchema,
})

export type HandshakeRequest = InferOutput<typeof handshakeRequestSchema>
export type HandshakeResponse = InferOutput<typeof handshakeResponseSchema>
