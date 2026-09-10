import {
  array,
  boolean,
  type InferOutput,
  literal,
  nullable,
  picklist,
  strictObject,
  variant,
} from 'valibot'
import {
  eventSeqSchema,
  formatVersionSchema,
  isoTimestampSchema,
  resourceIdSchema,
  seedSchema,
  sha256HexSchema,
  turnNumberSchema,
} from './primitives'
import { matchStatusSchema, playerViewSchema } from './views'

/**
 * The match event log: an ordered stream of facts, resumable by sequence number.
 *
 * Events name facts and carry references, not payloads: a sealed order set is announced here and
 * fetched once over REST. Delivery is at least once — a resume, or a seal finished by the repair
 * sweep, can repeat a fact a client already holds — so every client handler must be idempotent.
 *
 * Sequence numbers are gapless and allocated by the insert itself, so a client that has seen `seq`
 * has seen every event before it.
 *
 * @example
 * ```ts
 * import { parse } from 'valibot'
 * import { matchEventSchema } from '@chaos-overlords/contracts'
 *
 * const event = parse(matchEventSchema, JSON.parse(frame))
 * if (event.type === 'turn.sealed') fetchSealedOrders(event.payload.turn)
 * ```
 */

/**
 * The envelope on every event, spread into each member of the variant.
 *
 * It is on the members rather than wrapped around them because the C# side deserializes the
 * variant polymorphically: a wrapper the generator cannot express as one record would leave the
 * client reading `seq` out of a second, hand-written type that nothing keeps in step with this one.
 */
const eventEnvelope = {
  seq: eventSeqSchema,
  matchId: resourceIdSchema,
  createdAt: isoTimestampSchema,
}

export const lobbyPlayerJoinedEventSchema = strictObject({
  ...eventEnvelope,
  type: literal('lobby.playerJoined'),
  payload: strictObject({ player: playerViewSchema }),
})

export const lobbyPlayerLeftEventSchema = strictObject({
  ...eventEnvelope,
  type: literal('lobby.playerLeft'),
  payload: strictObject({
    playerId: resourceIdSchema,
    reason: picklist(['left', 'kicked']),
  }),
})

export const lobbyHostChangedEventSchema = strictObject({
  ...eventEnvelope,
  type: literal('lobby.hostChanged'),
  payload: strictObject({ hostPlayerId: resourceIdSchema }),
})

export const matchStartedEventSchema = strictObject({
  ...eventEnvelope,
  type: literal('match.started'),
  payload: strictObject({ seed: seedSchema, players: array(playerViewSchema) }),
})

export const matchStatusChangedEventSchema = strictObject({
  ...eventEnvelope,
  type: literal('match.statusChanged'),
  payload: strictObject({ status: matchStatusSchema }),
})

export const turnOpenedEventSchema = strictObject({
  ...eventEnvelope,
  type: literal('turn.opened'),
  payload: strictObject({ turn: turnNumberSchema, deadlineAt: nullable(isoTimestampSchema) }),
})

export const turnDeadlineExtendedEventSchema = strictObject({
  ...eventEnvelope,
  type: literal('turn.deadlineExtended'),
  payload: strictObject({ turn: turnNumberSchema, deadlineAt: nullable(isoTimestampSchema) }),
})

export const turnReadinessEventSchema = strictObject({
  ...eventEnvelope,
  type: literal('turn.readiness'),
  payload: strictObject({
    turn: turnNumberSchema,
    playerId: resourceIdSchema,
    ready: boolean(),
  }),
})

export const turnSealedEventSchema = strictObject({
  ...eventEnvelope,
  type: literal('turn.sealed'),
  payload: strictObject({ turn: turnNumberSchema, orderSetHash: sha256HexSchema }),
})

export const turnConfirmedEventSchema = strictObject({
  ...eventEnvelope,
  type: literal('turn.confirmed'),
  payload: strictObject({ turn: turnNumberSchema, stateHash: sha256HexSchema }),
})

export const turnDesyncedEventSchema = strictObject({
  ...eventEnvelope,
  type: literal('turn.desynced'),
  payload: strictObject({
    turn: turnNumberSchema,
    reports: array(strictObject({ playerId: resourceIdSchema, stateHash: sha256HexSchema })),
    /**
     * The hashes the most players reported, tied if more than one. A recovery snapshot has to
     * claim one of these, so a client can see from the event alone whether it is the odd one
     * out and what the match will converge on.
     */
    candidateStateHashes: array(sha256HexSchema),
  }),
})

export const snapshotAvailableEventSchema = strictObject({
  ...eventEnvelope,
  type: literal('snapshot.available'),
  payload: strictObject({
    turn: turnNumberSchema,
    formatVersion: formatVersionSchema,
    stateHash: sha256HexSchema,
    uploadedByPlayerId: resourceIdSchema,
  }),
})

export const matchEventSchema = variant('type', [
  lobbyPlayerJoinedEventSchema,
  lobbyPlayerLeftEventSchema,
  lobbyHostChangedEventSchema,
  matchStartedEventSchema,
  matchStatusChangedEventSchema,
  turnOpenedEventSchema,
  turnDeadlineExtendedEventSchema,
  turnReadinessEventSchema,
  turnSealedEventSchema,
  turnConfirmedEventSchema,
  turnDesyncedEventSchema,
  snapshotAvailableEventSchema,
])

export const eventPageSchema = strictObject({ events: array(matchEventSchema) })

/**
 * An event as persisted and delivered: the body plus its position in the match's log.
 */
export type MatchEvent = InferOutput<typeof matchEventSchema>
export type MatchEventType = MatchEvent['type']
export type EventPage = InferOutput<typeof eventPageSchema>

/**
 * An event before the log gives it a position: what a publisher hands to storage.
 *
 * The `extends` keeps the omission distributive, so the result is still a union discriminated on
 * `type` rather than one collapsed object with every payload shape merged.
 */
export type MatchEventBody = MatchEvent extends infer TEvent
  ? TEvent extends MatchEvent
    ? Omit<TEvent, 'seq' | 'matchId' | 'createdAt'>
    : never
  : never

/** The SSE stream also emits a keepalive comment; this is the one non-event frame. */
export const SSE_HEARTBEAT_COMMENT = 'keepalive'
