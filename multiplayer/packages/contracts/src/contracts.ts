import { noBodyResponse, sseResponse } from '@toad-contracts/core'
import { ContractNoBody, defineApiContract, withObjectKeys } from '@toad-contracts/valibot'
import { object } from 'valibot'
import { errorEnvelopeSchema } from './errors'
import { eventPageSchema, matchEventSchema } from './events'
import { resourceIdSchema, turnPathParamSchema } from './primitives'
import {
  createMatchRequestSchema,
  eventsQuerySchema,
  joinMatchRequestSchema,
  submitOrdersRequestSchema,
  turnReportRequestSchema,
  uploadSnapshotRequestSchema,
} from './schemas'
import {
  lobbyListSchema,
  matchDetailSchema,
  membershipViewSchema,
  ownSubmissionViewSchema,
  sealedOrdersViewSchema,
  snapshotViewSchema,
} from './views'

/**
 * Every endpoint, once: its method, its path, what it accepts and what it answers.
 *
 * These are the source of truth the server registers routes from, the TypeScript client calls
 * through, and the C# client's records are generated beside. A route that exists here and nowhere
 * else cannot drift from its handler, because the handler is mounted from it.
 *
 * @example
 * ```ts
 * import { sealedOrdersContract } from '@chaos-overlords/contracts'
 *
 * sealedOrdersContract.pathResolver({ matchId: 'm1', turn: '7' })
 * // => '/matches/m1/turns/7/orders'
 * ```
 */

/** Refusals every route can produce; the envelope is the same for all of them. */
const REFUSALS = {
  400: errorEnvelopeSchema,
  401: errorEnvelopeSchema,
  403: errorEnvelopeSchema,
  404: errorEnvelopeSchema,
  409: errorEnvelopeSchema,
  413: errorEnvelopeSchema,
  422: errorEnvelopeSchema,
  429: errorEnvelopeSchema,
  500: errorEnvelopeSchema,
} as const

/**
 * Path params, validated as what they mean rather than as bare strings: an id has to be URL-safe
 * and a turn has to be a bounded integer, both before a handler sees them.
 *
 * Resolvers interpolate their params raw. `mapApiContractToPath` derives the `:param` route pattern
 * by calling the same resolver with `:matchId` as the value, so escaping here would turn the
 * pattern into `%3AmatchId`. What makes that safe is `resourceIdSchema`, which refuses an id
 * carrying anything that is not URL-safe at the point the id is parsed.
 */
const matchParams = withObjectKeys(object({ matchId: resourceIdSchema }))
const turnParams = withObjectKeys(object({ matchId: resourceIdSchema, turn: turnPathParamSchema }))
const kickParams = withObjectKeys(object({ matchId: resourceIdSchema, playerId: resourceIdSchema }))

// ---------------------------------------------------------------------------
// Lobby
// ---------------------------------------------------------------------------

export const listLobbiesContract = defineApiContract({
  method: 'get',
  pathResolver: () => '/matches',
  responsesByStatusCode: { 200: lobbyListSchema, ...REFUSALS },
  summary: 'Public lobbies, when the server enables listing.',
})

export const createMatchContract = defineApiContract({
  method: 'post',
  pathResolver: () => '/matches',
  requestBodySchema: createMatchRequestSchema,
  responsesByStatusCode: { 201: membershipViewSchema, ...REFUSALS },
  summary: 'Open a lobby and take its host seat.',
})

export const joinMatchContract = defineApiContract({
  method: 'post',
  pathResolver: () => '/matches/join',
  requestBodySchema: joinMatchRequestSchema,
  responsesByStatusCode: { 201: membershipViewSchema, ...REFUSALS },
  summary: 'Claim a seat by join code.',
})

export const getMatchContract = defineApiContract({
  method: 'get',
  requestPathParamsSchema: matchParams,
  pathResolver: ({ matchId }) => `/matches/${matchId}`,
  responsesByStatusCode: { 200: matchDetailSchema, ...REFUSALS },
  summary: "A member's read of the match.",
})

export const startMatchContract = defineApiContract({
  method: 'post',
  requestPathParamsSchema: matchParams,
  pathResolver: ({ matchId }) => `/matches/${matchId}/start`,
  requestBodySchema: ContractNoBody,
  responsesByStatusCode: { 204: noBodyResponse(), ...REFUSALS },
  summary: 'Seat the players, draw the seed and open turn 1.',
})

export const leaveMatchContract = defineApiContract({
  method: 'post',
  requestPathParamsSchema: matchParams,
  pathResolver: ({ matchId }) => `/matches/${matchId}/leave`,
  requestBodySchema: ContractNoBody,
  responsesByStatusCode: { 204: noBodyResponse(), ...REFUSALS },
  summary: 'Give up the seat; the slot becomes a computer player.',
})

export const kickPlayerContract = defineApiContract({
  method: 'post',
  requestPathParamsSchema: kickParams,
  pathResolver: ({ matchId, playerId }) => `/matches/${matchId}/players/${playerId}/kick`,
  requestBodySchema: ContractNoBody,
  responsesByStatusCode: { 204: noBodyResponse(), ...REFUSALS },
  summary: 'Host removes a player, exactly as if they had left.',
})

// ---------------------------------------------------------------------------
// Turn barrier
// ---------------------------------------------------------------------------

export const submitOrdersContract = defineApiContract({
  method: 'put',
  requestPathParamsSchema: turnParams,
  pathResolver: ({ matchId, turn }) => `/matches/${matchId}/turns/${turn}/orders`,
  requestBodySchema: submitOrdersRequestSchema,
  responsesByStatusCode: { 200: ownSubmissionViewSchema, ...REFUSALS },
  summary: "Replace the caller's order document for the open turn.",
})

export const ownSubmissionContract = defineApiContract({
  method: 'get',
  requestPathParamsSchema: turnParams,
  pathResolver: ({ matchId, turn }) => `/matches/${matchId}/turns/${turn}/orders/mine`,
  responsesByStatusCode: { 200: ownSubmissionViewSchema, ...REFUSALS },
  summary: "The caller's own submission, for a reconnecting client.",
})

export const sealedOrdersContract = defineApiContract({
  method: 'get',
  requestPathParamsSchema: turnParams,
  pathResolver: ({ matchId, turn }) => `/matches/${matchId}/turns/${turn}/orders`,
  responsesByStatusCode: { 200: sealedOrdersViewSchema, ...REFUSALS },
  summary: 'The sealed set, in slot order, with its digest. Refused while the turn is open.',
})

export const reportTurnContract = defineApiContract({
  method: 'post',
  requestPathParamsSchema: turnParams,
  pathResolver: ({ matchId, turn }) => `/matches/${matchId}/turns/${turn}/report`,
  requestBodySchema: turnReportRequestSchema,
  responsesByStatusCode: { 204: noBodyResponse(), ...REFUSALS },
  summary: 'The state hash after applying the sealed turn locally.',
})

// ---------------------------------------------------------------------------
// Snapshots
// ---------------------------------------------------------------------------

export const uploadSnapshotContract = defineApiContract({
  method: 'post',
  requestPathParamsSchema: matchParams,
  pathResolver: ({ matchId }) => `/matches/${matchId}/snapshots`,
  requestBodySchema: uploadSnapshotRequestSchema,
  responsesByStatusCode: { 204: noBodyResponse(), ...REFUSALS },
  summary: 'Host uploads a native snapshot for a desynced turn.',
})

export const latestSnapshotContract = defineApiContract({
  method: 'get',
  requestPathParamsSchema: matchParams,
  pathResolver: ({ matchId }) => `/matches/${matchId}/snapshots/latest`,
  responsesByStatusCode: { 200: snapshotViewSchema, ...REFUSALS },
  summary: 'The most recent snapshot, for a reconnecting client.',
})

export const snapshotContract = defineApiContract({
  method: 'get',
  requestPathParamsSchema: turnParams,
  pathResolver: ({ matchId, turn }) => `/matches/${matchId}/snapshots/${turn}`,
  responsesByStatusCode: { 200: snapshotViewSchema, ...REFUSALS },
  summary: "One turn's snapshot.",
})

// ---------------------------------------------------------------------------
// Event log
// ---------------------------------------------------------------------------

export const listEventsContract = defineApiContract({
  method: 'get',
  requestPathParamsSchema: matchParams,
  pathResolver: ({ matchId }) => `/matches/${matchId}/events`,
  requestQuerySchema: eventsQuerySchema,
  responsesByStatusCode: { 200: eventPageSchema, ...REFUSALS },
  summary: 'The log, paged. The fallback for a network that cannot hold a streaming response.',
})

/**
 * The same log as server-sent events.
 *
 * Every frame carries the same `matchEventSchema` body regardless of its `type`, so the stream is
 * declared under the one `message` event name rather than one per match event: an `EventSource`
 * listening for `message` is what a browser client writes, and the discriminator inside the payload
 * is what everything downstream branches on anyway.
 */
export const streamEventsContract = defineApiContract({
  method: 'get',
  requestPathParamsSchema: matchParams,
  pathResolver: ({ matchId }) => `/matches/${matchId}/stream`,
  responsesByStatusCode: {
    200: sseResponse({ message: matchEventSchema }),
    ...REFUSALS,
  },
  summary: 'The event log as a resumable SSE stream.',
})

/** Every contract, for a client that wants to enumerate the surface. */
export const API_CONTRACTS = {
  listLobbies: listLobbiesContract,
  createMatch: createMatchContract,
  joinMatch: joinMatchContract,
  getMatch: getMatchContract,
  startMatch: startMatchContract,
  leaveMatch: leaveMatchContract,
  kickPlayer: kickPlayerContract,
  submitOrders: submitOrdersContract,
  ownSubmission: ownSubmissionContract,
  sealedOrders: sealedOrdersContract,
  reportTurn: reportTurnContract,
  uploadSnapshot: uploadSnapshotContract,
  latestSnapshot: latestSnapshotContract,
  snapshot: snapshotContract,
  listEvents: listEventsContract,
  streamEvents: streamEventsContract,
} as const
