import { noBodyResponse, sseResponse } from '@toad-contracts/core'
import { ContractNoBody, defineApiContract, withObjectKeys } from '@toad-contracts/valibot'
import { object } from 'valibot'
import { bugReportReceiptSchema, submitBugReportRequestSchema } from './bug-reports'
import { errorEnvelopeSchema } from './errors'
import { eventPageSchema, MATCH_EVENT_SSE_NAME, matchEventSchema } from './events'
import { resourceIdSchema, turnPathParamSchema } from './primitives'
import { handshakeRequestSchema, handshakeResponseSchema } from './protocol'
import { eventsQuerySchema } from './queries'
import {
  createMatchRequestSchema,
  joinMatchRequestSchema,
  joinRunningMatchRequestSchema,
  submitOrdersRequestSchema,
  takeoverVoteRequestSchema,
  turnReportRequestSchema,
  uploadSnapshotRequestSchema,
} from './schemas'
import { matchSettingsSchema } from './settings'
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
const takeoverVoteParams = kickParams

// ---------------------------------------------------------------------------
// Protocol handshake
// ---------------------------------------------------------------------------

export const handshakeContract = defineApiContract({
  method: 'post',
  pathResolver: () => '/handshake',
  requestBodySchema: handshakeRequestSchema,
  responsesByStatusCode: { 200: handshakeResponseSchema, ...REFUSALS },
  summary: 'Verify that the game and coordination server speak the same protocol version.',
})

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

export const joinRunningMatchContract = defineApiContract({
  method: 'post',
  pathResolver: () => '/matches/join-running',
  requestBodySchema: joinRunningMatchRequestSchema,
  responsesByStatusCode: { 201: membershipViewSchema, ...REFUSALS },
  summary: 'Claim a never-human computer seat in an ongoing match.',
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

export const updateMatchSettingsContract = defineApiContract({
  method: 'put',
  requestPathParamsSchema: matchParams,
  pathResolver: ({ matchId }) => `/matches/${matchId}/settings`,
  requestBodySchema: matchSettingsSchema,
  responsesByStatusCode: { 204: noBodyResponse(), ...REFUSALS },
  summary: 'Host-only lobby game configuration.',
})

export const leaveMatchContract = defineApiContract({
  method: 'post',
  requestPathParamsSchema: matchParams,
  pathResolver: ({ matchId }) => `/matches/${matchId}/leave`,
  requestBodySchema: ContractNoBody,
  responsesByStatusCode: { 204: noBodyResponse(), ...REFUSALS },
  summary: 'Give up the seat and open a player vote on computer control.',
})

export const rejoinMatchContract = defineApiContract({
  method: 'post',
  requestPathParamsSchema: matchParams,
  pathResolver: ({ matchId }) => `/matches/${matchId}/rejoin`,
  requestBodySchema: ContractNoBody,
  responsesByStatusCode: { 204: noBodyResponse(), ...REFUSALS },
  summary: 'Return to a former human seat, replacing its computer controller when necessary.',
})

export const kickPlayerContract = defineApiContract({
  method: 'post',
  requestPathParamsSchema: kickParams,
  pathResolver: ({ matchId, playerId }) => `/matches/${matchId}/players/${playerId}/kick`,
  requestBodySchema: ContractNoBody,
  responsesByStatusCode: { 204: noBodyResponse(), ...REFUSALS },
  summary: 'Host removes a player, exactly as if they had left.',
})

export const takeoverVoteContract = defineApiContract({
  method: 'post',
  requestPathParamsSchema: takeoverVoteParams,
  pathResolver: ({ matchId, playerId }) => `/matches/${matchId}/players/${playerId}/takeover-vote`,
  requestBodySchema: takeoverVoteRequestSchema,
  responsesByStatusCode: { 204: noBodyResponse(), ...REFUSALS },
  summary: 'Vote to keep waiting for an absent player or hand their seat to the computer.',
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
 * declared under the one {@link MATCH_EVENT_SSE_NAME} event rather than one per match event: an
 * `EventSource` listening for `message` is what a browser client writes, and the discriminator
 * inside the payload is what everything downstream branches on anyway. The server frames its events
 * under the same constant and both clients refuse a frame named anything else, so the name on the
 * wire cannot drift from the name declared here.
 */
export const streamEventsContract = defineApiContract({
  method: 'get',
  requestPathParamsSchema: matchParams,
  pathResolver: ({ matchId }) => `/matches/${matchId}/stream`,
  responsesByStatusCode: {
    200: sseResponse({ [MATCH_EVENT_SSE_NAME]: matchEventSchema }),
    ...REFUSALS,
  },
  summary: 'The event log as a resumable SSE stream.',
})

// ---------------------------------------------------------------------------
// Bug reports
// ---------------------------------------------------------------------------

/**
 * The one door a player who is not in a match may knock on.
 *
 * It is unauthenticated by design: the reports worth having most come from a player who could not
 * get into a match at all, and a token requirement would silence exactly those. What stands in for
 * authentication is a budget — the route carries its own per-address limit, far below the lobby
 * one — and a body limit sized for a compressed match journal and nothing larger.
 */
export const submitBugReportContract = defineApiContract({
  method: 'post',
  pathResolver: () => '/bug-reports',
  requestBodySchema: submitBugReportRequestSchema,
  responsesByStatusCode: { 201: bugReportReceiptSchema, ...REFUSALS },
  summary: 'File a bug report, optionally with a replayable journal of the match.',
})

/** Every contract, for a client that wants to enumerate the surface. */
export const API_CONTRACTS = {
  handshake: handshakeContract,
  listLobbies: listLobbiesContract,
  createMatch: createMatchContract,
  joinMatch: joinMatchContract,
  joinRunningMatch: joinRunningMatchContract,
  getMatch: getMatchContract,
  updateMatchSettings: updateMatchSettingsContract,
  startMatch: startMatchContract,
  leaveMatch: leaveMatchContract,
  rejoinMatch: rejoinMatchContract,
  kickPlayer: kickPlayerContract,
  takeoverVote: takeoverVoteContract,
  submitOrders: submitOrdersContract,
  ownSubmission: ownSubmissionContract,
  sealedOrders: sealedOrdersContract,
  reportTurn: reportTurnContract,
  uploadSnapshot: uploadSnapshotContract,
  latestSnapshot: latestSnapshotContract,
  snapshot: snapshotContract,
  listEvents: listEventsContract,
  streamEvents: streamEventsContract,
  submitBugReport: submitBugReportContract,
} as const
