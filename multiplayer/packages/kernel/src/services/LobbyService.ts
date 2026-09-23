import {
  type CreateMatchRequest,
  foldName,
  GAME_BOUNDS,
  type JoinMatchRequest,
  type JoinRunningMatchRequest,
  LIMITS,
  type MatchSettings,
  type MembershipView,
  type TakeoverVoteRequest,
  type UpdatePlayerProfileRequest,
} from '@chaos-overlords/contracts'
import {
  ABSENT_HUMAN_STATUSES,
  activePlayers,
  humanParticipants,
  isInProgress,
  type Match,
  type Player,
} from '../domain/entities'
import {
  ConflictError,
  ForbiddenError,
  NotFoundError,
  RateLimitedError,
  UnauthorizedError,
} from '../domain/errors'
import { generateJoinCode, generateSeed, generateToken, hashToken } from '../logic/crypto'
import { hashPassword, verifyPassword } from '../logic/password'
import { assignSlots } from '../logic/turn-logic'
import type { CastVoteInput } from '../ports/storage'
import type { Principal } from './AuthService'
import type { KernelDeps } from './deps'
import type { EventPublisher } from './EventPublisher'
import { requireInProgress } from './guards'
import { MatchQueryService, matchStartedEvent, toPlayerView } from './MatchQueryService'
import { RateLimiter } from './RateLimiter'
import { FIRST_TURN, type TurnService } from './TurnService'

const JOIN_CODE_LENGTH = LIMITS.joinCodeLength
const MIN_PLAYERS_TO_START = LIMITS.minPlayers
/** Attempts at an unused join code. The space is ~40 bits, so a second attempt is already rare. */
const JOIN_CODE_ATTEMPTS = 5
/** The host always holds the first position in the join sequence. */
const HOST_JOIN_ORDER = 0
/** The face a client that predates portrait selection is seated with. */
const DEFAULT_PORTRAIT_ID = 0

/** Host statuses that mean the seat is genuinely empty and the role may move. */
const VACANT_HOST_STATUSES: ReadonlyArray<Player['status']> = ['left', 'kicked', 'computer']

/**
 * Passwords one caller will have verified against one match in a window.
 *
 * `verifyPassword` is PBKDF2 at 120,000 iterations, on a door no token guards, so tens of
 * milliseconds of CPU per attempt is the libuv thread pool on Node and billed CPU on Workers. The
 * budget is per caller AND per match rather than per match alone: a public listing carries the
 * join code and the `passwordProtected` flag, so one match's budget is something any stranger can
 * reach, and one that could be spent by a stranger would lock every legitimate player out of a
 * match for as long as the stranger cared to keep spending it. Someone mistyping their own
 * password is nowhere near ten tries a minute.
 */
const PASSWORD_ATTEMPTS_PER_CALLER = 10
/**
 * Wrong passwords one match will verify in a window before it stops taking a caller's word twice.
 *
 * The per-caller budget bounds one address; this bounds the match against an attacker who has
 * many. It is charged only by a verification that FAILED, so the traffic of a match whose players
 * know their password never approaches it. While it is spent, a caller still gets their first
 * attempt of the window — which is what keeps this a brake on a distributed attack rather than a
 * lever for closing someone else's match: an attacker under it costs the match one hash per
 * address per minute, and a player who knows the password is only ever refused a RETRY, never
 * their first try.
 */
const PASSWORD_FAILURES_PER_MATCH = 30
const PASSWORD_ATTEMPT_WINDOW_MS = 60_000

export interface LobbyServiceOptions {
  /** Generates the player/match ids; defaults to `crypto.randomUUID`. */
  newId?: () => string
}

/** Match creation, joining, leaving, kicking and the start transition. */
export class LobbyService {
  private readonly query: MatchQueryService
  private readonly newId: () => string
  private readonly passwordAttempts: RateLimiter
  private readonly passwordFailures: RateLimiter

  constructor(
    private readonly deps: KernelDeps,
    private readonly publisher: EventPublisher,
    private readonly turns: TurnService,
    options: LobbyServiceOptions = {},
  ) {
    this.query = new MatchQueryService(deps.storage)
    this.newId = options.newId ?? (() => crypto.randomUUID())
    this.passwordAttempts = new RateLimiter(deps.clock, {
      limit: PASSWORD_ATTEMPTS_PER_CALLER,
      windowMs: PASSWORD_ATTEMPT_WINDOW_MS,
    })
    this.passwordFailures = new RateLimiter(deps.clock, {
      limit: PASSWORD_FAILURES_PER_MATCH,
      windowMs: PASSWORD_ATTEMPT_WINDOW_MS,
    })
  }

  /**
   * Check a match password, charging two budgets before the hash is computed.
   *
   * The caller's own budget is spent on every attempt rather than only on the wrong ones, because
   * a caller who knows the password does not need ten attempts a minute and an attacker who does
   * not would otherwise be charged for nothing. The match's budget is spent only by a failure, and
   * closes only the caller's SECOND and later attempts in a window, so a stranger grinding a
   * public match's password cannot turn the protection into a way of shutting its players out.
   * Both doors go through here; see `PASSWORD_ATTEMPTS_PER_CALLER`.
   *
   * `caller` is whatever the transport can attribute an attempt to — the client address, already
   * normalised. A transport that cannot attribute one passes nothing, and every such attempt then
   * shares a single budget, which is the safe direction: an unattributable flood is throttled
   * together rather than not at all.
   */
  private async verifyMatchPassword(
    match: Match,
    password: string | undefined,
    caller: string | undefined,
  ): Promise<void> {
    if (match.passwordHash === null) return
    if (!password) {
      throw new UnauthorizedError('This match needs a password', { reason: 'password_required' })
    }
    const callerKey = `${match.id}:${caller ?? 'unattributed'}`
    const retry = this.passwordAttempts.take(callerKey)
    if (retry !== null) throw this.tooManyPasswordAttempts(retry)
    if (this.passwordAttempts.spent(callerKey) > 1) {
      const underAttack = this.passwordFailures.peek(match.id)
      if (underAttack !== null) throw this.tooManyPasswordAttempts(underAttack)
    }
    if (!(await verifyPassword(password, match.passwordHash))) {
      this.passwordFailures.take(match.id)
      throw new UnauthorizedError('Wrong password', { reason: 'wrong_password' })
    }
  }

  private tooManyPasswordAttempts(retryAfterSeconds: number): RateLimitedError {
    return new RateLimitedError('Too many password attempts for this match', {
      reason: 'rate_limited',
      retryAfterSeconds,
    })
  }

  async createMatch(request: CreateMatchRequest): Promise<MembershipView> {
    const now = this.deps.clock.now()
    const matchId = this.newId()
    const hostId = this.newId()
    const token = generateToken()
    const passwordHash = request.password ? await hashPassword(request.password) : null
    const match = await this.createWithFreshJoinCode({
      id: matchId,
      protocolVersion: request.protocolVersion ?? 1,
      sessionVersion: request.sessionVersion ?? 1,
      status: 'lobby',
      settings: request.settings,
      hostPlayerId: hostId,
      joinCode: '',
      passwordHash,
      seed: null,
      currentTurn: 0,
      seatCount: 1,
      joinCounter: 1,
      createdAt: now,
      updatedAt: now,
    })
    const host: Player = {
      id: hostId,
      matchId,
      slot: -1,
      joinOrder: HOST_JOIN_ORDER,
      displayName: request.hostDisplayName,
      portraitId: request.hostPortraitId ?? DEFAULT_PORTRAIT_ID,
      tokenHash: await hashToken(token),
      status: 'active',
      joinedAt: now,
    }
    // The match was inserted a statement ago and is in the lobby, so this cannot legitimately fail;
    // treating it as a conflict rather than ignoring it keeps the host's token from being handed out
    // for a row that does not exist.
    if (!(await this.deps.storage.players.create(host))) {
      throw new ConflictError('The match could not be opened; retry', { reason: 'match_not_open' })
    }
    await this.publisher.publish(matchId, {
      type: 'lobby.playerJoined',
      payload: { player: toPlayerView(host, hostId) },
    })
    return this.membership(match, host, token)
  }

  async join(request: JoinMatchRequest, caller?: string): Promise<MembershipView> {
    const match = await this.deps.storage.matches.getByJoinCode(request.joinCode)
    // A code that names no match and a code that names a match which has already started answer the
    // same 404, so a scan of the code space learns nothing from the difference. The wording covers
    // both truthfully: a started match has no open lobby either, and a player who was told to join a
    // match that has since started wants the late-join door, not this one.
    if (match?.status !== 'lobby') {
      throw new NotFoundError('No lobby is open with that join code', {
        reason: 'unknown_join_code',
      })
    }
    await this.verifyMatchPassword(match, request.password, caller)
    await this.refuseDuplicateName(match.id, request.displayName)
    const joinOrder = await this.deps.storage.matches.claimSeat(match.id)
    if (joinOrder === null) {
      throw new ConflictError('The match is full or no longer joinable', { reason: 'match_full' })
    }
    const token = generateToken()
    const player: Player = {
      id: this.newId(),
      matchId: match.id,
      slot: -1,
      joinOrder,
      displayName: request.displayName,
      portraitId: request.portraitId ?? DEFAULT_PORTRAIT_ID,
      tokenHash: await hashToken(token),
      status: 'active',
      joinedAt: this.deps.clock.now(),
    }
    // Everything after the seat is claimed has to give it back on failure, or capacity drifts and a
    // phantom member keeps the turn barrier waiting for a player nobody can authenticate as. The
    // insert is itself conditional on the match still being in the lobby: the host may have pressed
    // start between the claim and here, and an unseated player in a running match would wedge it.
    try {
      const seated = await this.deps.storage.players.create(player)
      if (!seated) {
        throw new ConflictError('The match started while you were joining', {
          reason: 'match_not_joinable',
        })
      }
      const membership = await this.membership(match, player, token)
      await this.publisher.publish(match.id, {
        type: 'lobby.playerJoined',
        payload: { player: toPlayerView(player, match.hostPlayerId) },
      })
      return membership
    } catch (error) {
      await this.rollbackJoin(match.id, player.id)
      throw error
    }
  }

  async joinRunning(request: JoinRunningMatchRequest, caller?: string): Promise<MembershipView> {
    const byId = await this.deps.storage.matches.get(request.match)
    // A private match is reachable by its join code only. Its id is not a secret — it rides every
    // event, the client's recovery file and any log line — so looking one up by id would make a
    // code-gated lobby joinable by anyone who ever saw the id. A public match is listed with both,
    // so there is nothing left for the code to gate there.
    const match =
      byId?.settings.visibility === 'public'
        ? byId
        : await this.deps.storage.matches.getByJoinCode(request.match)
    if (!match)
      throw new NotFoundError('No match with that id or join code', { reason: 'unknown_match' })
    if (match.status !== 'running') {
      throw new ConflictError('The match is not running', { reason: 'match_not_running' })
    }
    if (match.settings.gameSettings.allowLateJoin !== true) {
      throw new ForbiddenError('This match does not allow joining after it starts', {
        reason: 'late_join_disabled',
      })
    }
    if (!(await this.deps.storage.snapshots.getLatestSummary(match.id))) {
      throw new ConflictError('Late join is available after the bootstrap snapshot is uploaded', {
        reason: 'late_join_not_ready',
      })
    }
    await this.verifyMatchPassword(match, request.password, caller)
    const existing = await this.deps.storage.players.listByMatch(match.id)
    // A match every human has left is paused by `remove`, with no clock and nobody to restart it:
    // `rejoin` is the door back into one, and it needs a token this caller does not have. Seating
    // a stranger at a stopped table would strand them there, so the late-join door refuses it —
    // which is also exactly the test the public listing leaves such a match out on.
    if (humanParticipants(existing).length === 0) {
      throw new ConflictError('Every player has left this match', { reason: 'match_abandoned' })
    }
    if (existing.some((player) => player.slot === request.slot)) {
      throw new ConflictError('That seat has already belonged to a human', {
        reason: 'seat_reserved',
      })
    }
    // A courtesy refusal with the right reason before any work is done; `createLate` tests
    // capacity again inside its own insert, which is what actually decides the race between two
    // late joiners taking two different free seats.
    if (existing.length >= match.settings.maxPlayers) {
      throw new ConflictError('The match is full', { reason: 'match_full' })
    }
    this.assertNameIsFree(existing, request.displayName)
    const token = generateToken()
    const seatKey = (await hashToken(`${match.id}:${request.slot}`)).slice(0, 32)
    // The position is taken before the insert because the port has no transactions to take both
    // in one; a join `createLate` then refuses leaves a gap in the sequence, which only has to be
    // unique and increasing. The checks above refuse the requests that are doomed from the start.
    const joinOrder = await this.deps.storage.matches.claimLateJoinOrder(match.id)
    if (joinOrder === null) throw await this.lateJoinClaimRefused(match.id)
    const player: Player = {
      id: `late-${seatKey}`,
      matchId: match.id,
      slot: request.slot,
      joinOrder,
      displayName: request.displayName,
      portraitId: request.portraitId ?? DEFAULT_PORTRAIT_ID,
      tokenHash: await hashToken(token),
      status: 'active',
      joinedAt: this.deps.clock.now(),
    }
    if (!(await this.deps.storage.players.createLate(player))) {
      throw new ConflictError('That seat was claimed by another player, or the match filled up', {
        reason: 'seat_reserved',
      })
    }
    await this.refreshRetention(match.id)
    await this.topUpCurrentTurn(match.id, player.id)
    await this.publisher.publish(match.id, {
      type: 'match.latePlayerJoined',
      payload: { playerId: player.id, slot: player.slot },
    })
    return this.membership(match, player, token)
  }

  /** Why the late-join claim found no running match: it stopped running, or it is gone. */
  private async lateJoinClaimRefused(matchId: string): Promise<Error> {
    return (await this.deps.storage.matches.get(matchId))
      ? new ConflictError('The match is not running', { reason: 'match_not_running' })
      : new NotFoundError('No match with that id or join code', { reason: 'unknown_match' })
  }

  async leave(principal: Principal): Promise<void> {
    await this.remove(principal.match, principal.player, 'left')
  }

  async rejoin(principal: Principal): Promise<void> {
    const { match, player } = principal
    requireInProgress(match)
    if (player.status === 'kicked') {
      throw new ForbiddenError('A kicked player cannot rejoin', { reason: 'kicked' })
    }
    await this.refreshRetention(match.id)
    if (player.status === 'active') {
      // Nothing to reclaim, but a seat whose row an interrupted turn open never wrote is repaired
      // here too, so the returning client is waited for instead of silently sealed past.
      await this.topUpCurrentTurn(match.id, player.id)
      return
    }
    const replacedComputer = player.status === 'computer'
    if (
      !(await this.deps.storage.players.transitionStatus(
        player.id,
        ['left', 'takeoverPending', 'computer'],
        'active',
      ))
    ) {
      throw new ConflictError('The player seat could not be reclaimed', { reason: 'rejoin_race' })
    }
    await this.topUpCurrentTurn(match.id, player.id)
    await this.deps.storage.takeovers.closePrompt(match.id, player.id)
    await this.publisher.publish(match.id, {
      type: 'match.playerReturned',
      payload: { playerId: player.id, replacedComputer },
    })
    const players = await this.deps.storage.players.listByMatch(match.id)
    // A seat that went quiet while nobody was present to ask was never put to a vote (see
    // `remove`), and one whose vote closed with the last present player leaving has no prompt
    // either. The returning player is present now, so every absent human seat is put to them; a
    // prompt that is already open is left exactly as it is.
    for (const absent of players) {
      if (absent.id === player.id || !ABSENT_HUMAN_STATUSES.includes(absent.status)) continue
      await this.turns.openTakeoverPrompt(match.id, absent.id, match.currentTurn)
    }
    const currentHost = players.find((candidate) => candidate.id === match.hostPlayerId)
    // Only a host who is gone is replaced. `takeoverPending` is set on a player who is still
    // connected and merely missed one timed deadline, so treating it as absence would hand the role
    // to any former member who called `rejoin` at that moment — and the new host can kick the old
    // one, which revokes their token for good. A pending host is still present; the takeover vote,
    // or the pending host's own `leave`, is the path that decides otherwise.
    if (currentHost === undefined || VACANT_HOST_STATUSES.includes(currentHost.status)) {
      await this.handHostTo(match.id, player.id, this.deps.clock.now())
    }
    await this.turns.reevaluate(match.id)
    await this.turns.resumeAfterTakeoverVotes(match.id)
  }

  /** A seat claim can cross a seal, so use the turn current after the seat was committed. */
  private async topUpCurrentTurn(matchId: string, playerId: string): Promise<void> {
    const match = await this.deps.storage.matches.get(matchId)
    if (match) await this.turns.topUpSeat(matchId, match.currentTurn, playerId)
  }

  async updateSettings(principal: Principal, settings: MatchSettings): Promise<void> {
    requireHost(principal)
    if (principal.match.status !== 'lobby') {
      throw new ConflictError('The match has already started', { reason: 'match_not_in_lobby' })
    }
    if (
      !(await this.deps.storage.matches.updateSettings(
        principal.match.id,
        settings,
        this.deps.clock.now(),
      ))
    ) {
      throw new ConflictError('The lobby has more players than that limit', {
        reason: 'players_exceed_limit',
      })
    }
  }

  /**
   * Change the caller's own name and portrait while the match is still in the lobby.
   *
   * Any seated member may, not only the host: it is their own seat. The name is held to the same
   * uniqueness a join is, against everybody but the caller, so renaming to a different case of
   * one's own name is allowed. Refused once the match has started, because the roster is then what
   * every client has generated its city from.
   */
  async updateProfile(principal: Principal, request: UpdatePlayerProfileRequest): Promise<void> {
    const { match, player } = principal
    if (match.status !== 'lobby') {
      throw new ConflictError('The match has already started', { reason: 'match_not_in_lobby' })
    }
    const others = (await this.deps.storage.players.listByMatch(match.id)).filter(
      (candidate) => candidate.id !== player.id,
    )
    this.assertNameIsFree(others, request.displayName)
    const profile = { displayName: request.displayName, portraitId: request.portraitId }
    if (!(await this.deps.storage.players.updateProfile(player.id, profile))) {
      throw new ConflictError('The match has already started', { reason: 'match_not_in_lobby' })
    }
    await this.publisher.publish(match.id, {
      type: 'lobby.playerUpdated',
      payload: { player: toPlayerView({ ...player, ...profile }, match.hostPlayerId) },
    })
  }

  async kick(principal: Principal, targetPlayerId: string): Promise<void> {
    const { match, player } = principal
    requireHost(principal)
    if (targetPlayerId === player.id) {
      throw new ConflictError('Leave the match instead of kicking yourself', {
        reason: 'self_kick',
      })
    }
    const target = await this.requireTarget(match, targetPlayerId)
    await this.remove(match, target, 'kicked')
  }

  /** The player a host or a voter named, refused unless it is a member of the caller's match. */
  private async requireTarget(match: Match, playerId: string): Promise<Player> {
    const target = await this.deps.storage.players.get(playerId)
    if (!target || target.matchId !== match.id) {
      throw new NotFoundError('No such player in this match', { reason: 'unknown_player' })
    }
    return target
  }

  /**
   * Record one present player's latest choice. AI control is deliberately unanimous: a single
   * `wait` vote preserves the human seat, and there is no timeout that silently changes it.
   */
  async voteOnTakeover(
    principal: Principal,
    targetPlayerId: string,
    request: TakeoverVoteRequest,
  ): Promise<void> {
    const { match, player } = principal
    requireInProgress(match)
    const currentVoter = await this.deps.storage.players.get(player.id)
    if (currentVoter?.status !== 'active') {
      throw new ForbiddenError('Only present players may vote', { reason: 'not_active' })
    }
    const target = await this.requireTarget(match, targetPlayerId)
    if (!ABSENT_HUMAN_STATUSES.includes(target.status)) {
      throw new ConflictError('That player is not awaiting a takeover vote', {
        reason: 'takeover_not_pending',
      })
    }
    const vote: CastVoteInput = {
      matchId: match.id,
      targetPlayerId: target.id,
      voterPlayerId: player.id,
      decision: request.decision,
      castAt: this.deps.clock.now(),
    }
    if (!(await this.deps.storage.takeovers.castVote(vote))) {
      // The seat is absent but nobody asked about it yet: it went quiet before this table existed,
      // or while nobody was present to ask. The vote itself opens the question, so the seat can
      // still be decided rather than left idle for the rest of the match.
      await this.turns.openTakeoverPrompt(match.id, target.id, match.currentTurn)
      if (!(await this.deps.storage.takeovers.castVote(vote))) {
        throw new ConflictError('That player is not awaiting a takeover vote', {
          reason: 'takeover_not_pending',
        })
      }
    }
    await this.publisher.publish(match.id, {
      type: 'match.takeoverVoteCast',
      payload: { playerId: target.id, voterPlayerId: player.id, decision: request.decision },
    })
    if (request.decision === 'wait') return
    await this.tallyTakeoverVote(match, target.id)
  }

  /**
   * Hand a seat to the computer once every remaining active player has voted for it.
   *
   * Extracted from `voteOnTakeover` because the tally can also be completed by the voter set
   * shrinking. When the one player who had not voted (or had voted `wait`) leaves or is kicked,
   * everyone left has already approved, and the prompt used to stay open with the clock stopped
   * until somebody clicked the same button a second time — on a modal reading 2 of 2.
   */
  private async tallyTakeoverVote(match: Match, targetPlayerId: string): Promise<void> {
    const target = await this.deps.storage.players.get(targetPlayerId)
    if (!target || target.status === 'computer') return
    const votes = new Map(
      (await this.deps.storage.takeovers.listVotes(match.id, targetPlayerId)).map((vote) => [
        vote.voterPlayerId,
        vote.decision,
      ]),
    )
    const voters = activePlayers(await this.deps.storage.players.listByMatch(match.id))
    if (voters.length === 0 || voters.some((voter) => votes.get(voter.id) !== 'computer')) return
    if (
      !(await this.deps.storage.players.transitionStatus(
        targetPlayerId,
        [target.status],
        'computer',
      ))
    ) {
      return
    }
    await this.deps.storage.takeovers.closePrompt(match.id, targetPlayerId)
    await this.publisher.publish(match.id, {
      type: 'match.playerTakenOver',
      payload: { playerId: targetPlayerId },
    })
    if (targetPlayerId === match.hostPlayerId) {
      await this.handHostToFirstActive(match.id, this.deps.clock.now())
    }
    await this.turns.reevaluate(match.id)
    await this.turns.resumeAfterTakeoverVotes(match.id)
  }

  /** Re-tally every open prompt, for when the set of voters has just shrunk. */
  private async retallyOpenPrompts(match: Match): Promise<void> {
    for (const playerId of await this.deps.storage.takeovers.listOpenPrompts(match.id)) {
      await this.tallyTakeoverVote(match, playerId)
    }
  }

  async start(principal: Principal): Promise<void> {
    const { match } = principal
    requireHost(principal)
    if (match.status !== 'lobby') {
      throw new ConflictError('The match has already started', { reason: 'match_not_in_lobby' })
    }
    const roster = activePlayers(await this.deps.storage.players.listByMatch(match.id))
    if (roster.length < MIN_PLAYERS_TO_START) {
      throw new ConflictError(`At least ${MIN_PLAYERS_TO_START} players are needed`, {
        reason: 'not_enough_players',
      })
    }
    // The seat counter is what admits a join, so a roster larger than the match allows means the
    // counter has been undercounted. Refuse rather than seat a seventh player, whose slot fails
    // `seatSchema` in every player view and makes the match a 500 for the whole roster.
    if (roster.length > Math.min(match.settings.maxPlayers, GAME_BOUNDS.playerCount)) {
      throw new ConflictError('The lobby holds more players than the match allows', {
        reason: 'match_full',
      })
    }
    const seed = generateSeed()
    const started = await this.deps.storage.matches.transition(match.id, ['lobby'], {
      status: 'running',
      seed,
      currentTurn: 0,
      updatedAt: this.deps.clock.now(),
    })
    if (!started) {
      throw new ConflictError('The match has already started', { reason: 'match_not_in_lobby' })
    }
    // Seats can no longer be claimed, so the roster is final from here.
    const finalRoster = await this.deps.storage.players.listByMatch(match.id)
    await this.deps.storage.players.assignSlots(assignSlots(finalRoster, match.hostPlayerId))
    const seated = await this.deps.storage.players.listByMatch(match.id)
    await this.publisher.publish(match.id, matchStartedEvent(seed, seated, match.hostPlayerId))
    await this.turns.openTurn({ ...match, status: 'running', seed }, FIRST_TURN)
  }

  private async remove(match: Match, target: Player, reason: 'left' | 'kicked'): Promise<void> {
    if (target.status === 'kicked') return
    // A seat already handed to the computer is not a human to remove. Marking it `kicked` put it
    // back in ABSENT_HUMAN_STATUSES, so the next rejoin or vote opened a fresh prompt for a seat
    // every client is already playing as AI, stopped the clock, and a unanimous vote published a
    // second `match.playerTakenOver` for it. Revoke the owner's token, leave the seat alone.
    if (target.status === 'computer') {
      if (reason === 'kicked') {
        await this.deps.storage.players.revokeToken(target.id)
        await this.hangUp(match.id, target.id)
      }
      return
    }
    // Leaving is something only a seated, active player does — or one whose seat is waiting on an
    // absence vote, who would otherwise be waited on for the rest of the match after answering 204.
    // A kick has to reach the seat whatever state it is in: a player who left or went quiet keeps a
    // working token until it is revoked here, and `rejoin` turns away nobody but the kicked, so
    // answering 204 and doing nothing let a kicked player walk straight back in.
    const wasActive = target.status === 'active'
    if (!wasActive && reason !== 'kicked' && target.status !== 'takeoverPending') return
    const now = this.deps.clock.now()
    if (match.status === 'lobby') {
      if (target.id === match.hostPlayerId) {
        await this.abandon(match, now)
        return
      }
      // `match.status` was read at authentication, so the host's `start` may have landed since. A
      // re-read here is not a transaction, but it turns the window from "the whole request" into
      // "two statements", and the running path below is the correct one for a seated player.
      const current = await this.deps.storage.matches.get(match.id)
      if (current && current.status !== 'lobby') {
        await this.remove(current, target, reason)
        return
      }
      // The seat is released only when a row actually went. Two requests that both authenticated
      // before either deleted — a `leave` sent twice, or this kick racing the target's own `leave` —
      // each decremented the counter for the one row that went, so the lobby admitted more than
      // `maxPlayers` and a seventh player got slot 6, which fails `seatSchema` in every player view
      // and turns the match into a 500 for everybody.
      if (!(await this.deps.storage.players.delete(target.id))) return
      await this.deps.storage.matches.releaseSeat(match.id)
      // A kicked member's token is dead with the row, but a stream they already hold only re-checks
      // its membership on the hub's catch-up heartbeat: without this it goes on delivering
      // `match.started` with the seed and every seal, desync and roster change until then.
      if (reason === 'kicked') await this.hangUp(match.id, target.id)
      await this.publisher.publish(match.id, {
        type: 'lobby.playerLeft',
        payload: { playerId: target.id, reason },
      })
      return
    }
    // A compare-and-swap, not a write. `target.status` was read at authentication, and
    // `tallyTakeoverVote` concurrently swaps `takeoverPending` to `computer` and announces the
    // takeover. Writing `left` or `kicked` over that put a seat every client already plays as AI
    // back among the absent human statuses, so the next rejoin or vote opened a fresh prompt for
    // it, paused the clock, and published a second `match.playerTakenOver` — the regression the
    // note at the top of this method says was fixed.
    const claimed = await this.deps.storage.players.transitionStatus(
      target.id,
      ['active', 'takeoverPending', 'left'],
      reason,
    )
    // Revoking and hanging up stay unconditional for a kick: the seat may now be computer
    // controlled, but the person behind it must still lose the token and the streams either way.
    // Membership is the only thing the token ever proved, so it stops working here: a kicked player
    // keeps neither the event stream nor the sealed order sets of the turns that follow. The revoke
    // closes the next request and the hang-up closes the streams already open, which otherwise
    // outlive the membership until the hub's periodic membership check catches up with them.
    if (reason === 'kicked') {
      await this.deps.storage.players.revokeToken(target.id)
      await this.hangUp(match.id, target.id)
    }
    if (claimed) {
      await this.publisher.publish(match.id, {
        type: 'lobby.playerLeft',
        payload: { playerId: target.id, reason },
      })
    }
    if (!isInProgress(match)) return
    // Not claimed: the takeover won the seat, so it is no longer a human absence anyone has to
    // decide on. The verdict is still re-run below, because the seat leaving `humanParticipants`
    // can complete a readiness or a consensus that was waiting on it.
    //
    // Claimed but no longer active: a `takeoverPending` seat is not idle. `humanParticipants`
    // counts it, so both readiness and consensus wait on it. Kicking one and returning here left
    // the turn waiting on a seat that could never answer, with the clock paused by its own prompt
    // and everyone present already ready. A `left` seat really is idle, and re-running the verdict
    // for it costs one query.
    //
    // A `takeoverPending` host who leaves is gone, not merely late, so the role moves on the way it
    // does for an active host; otherwise it stayed with a departed seat until a takeover vote or a
    // rejoin happened to move it. With nobody present to take it, the role stays put and the first
    // former member to rejoin becomes host, as below.
    if (!claimed || !wasActive) {
      if (
        claimed &&
        target.id === match.hostPlayerId &&
        !(await this.handHostToFirstActive(match.id, now))
      ) {
        await this.turns.pauseAbandonedMatch(match.id)
      }
      await this.turns.reevaluate(match.id)
      await this.retallyOpenPrompts(match)
      return
    }
    const remaining = activePlayers(await this.deps.storage.players.listByMatch(match.id))
    if (remaining.length === 0) {
      // Keep the durable match available. The first former member to rejoin becomes host.
      //
      // Stop the clock on the way out. Nobody opens a takeover prompt on the last seat — there is
      // nobody left to ask — so nothing else would pause it, and a timed match went on sealing an
      // empty turn every `turnTimerSeconds` for as long as the server ran: one turn row and two
      // events a cycle, about 2,900 turns a day, with every cycle refreshing `updatedAt` so the
      // 90-day abandoned-live sweep never reached it. `rejoin` restarts the clock through
      // `resumeAfterTakeoverVotes`.
      await this.turns.pauseAbandonedMatch(match.id)
      return
    }
    await this.turns.openTakeoverPrompt(match.id, target.id, match.currentTurn)
    if (target.id === match.hostPlayerId) {
      await this.handHostTo(match.id, (remaining[0] as Player).id, now)
    }
    // A departure can complete readiness or a consensus that was waiting on the leaver.
    await this.turns.reevaluate(match.id)
    await this.retallyOpenPrompts(match)
  }

  /**
   * Restart a running or desynced match's retention age: somebody has just come (back) to it.
   *
   * The abandoned and silent windows both run from `updatedAt`, and neither a join nor a rejoin
   * otherwise moves it — a player returning to an untimed match whose partner is still away opens
   * no turn and changes no status — so a match somebody was demonstrably playing could be collected
   * on the age it had before they came back. A no-op on any other status.
   */
  private async refreshRetention(matchId: string): Promise<void> {
    await this.deps.storage.matches.transition(matchId, ['running', 'desynced'], {
      updatedAt: this.deps.clock.now(),
    })
  }

  /**
   * Move the host role of a started match and announce it. `at` is the caller's to supply: `remove`
   * stamps the handoff with the time it read on entry, and the other callers read the clock as
   * they call.
   */
  private async handHostTo(matchId: string, playerId: string, at: Date): Promise<void> {
    await this.deps.storage.matches.transition(matchId, ['running', 'desynced'], {
      hostPlayerId: playerId,
      updatedAt: at,
    })
    await this.publisher.publish(matchId, {
      type: 'lobby.hostChanged',
      payload: { hostPlayerId: playerId },
    })
  }

  /**
   * Hand the host role of a started match to its first active player, if it has one. Answers
   * whether anybody was there to take it.
   */
  private async handHostToFirstActive(matchId: string, at: Date): Promise<boolean> {
    const successor = activePlayers(await this.deps.storage.players.listByMatch(matchId))[0]
    if (!successor) return false
    await this.handHostTo(matchId, successor.id, at)
    return true
  }

  /**
   * Hang up a revoked membership's streams. Best effort by design: the token is already gone, so a
   * fan-out that cannot be reached costs one stale stream until it drops, never the revoke itself.
   */
  private async hangUp(matchId: string, playerId: string): Promise<void> {
    try {
      await this.deps.streams.close({ matchId, playerId })
    } catch (error) {
      this.deps.logger.warn('could not close a revoked membership event stream', {
        matchId,
        playerId,
        error: String(error),
      })
    }
  }

  /**
   * Refuse a join whose name is already on the roster.
   *
   * Names are the only thing a player has to tell their peers apart by, and nothing else in a match
   * is tied to one: a second "Alice", or a copy of the host's name, makes every roster decision a
   * guess — which seat to vote onto the computer, which player to kick for desyncing. The comparison
   * is {@link foldName}, so a differing case or a doubled space is the same name.
   *
   * Checked before the seat is claimed, which leaves a window two simultaneous joins of the same
   * name could both pass. That is deliberate: a unique index on (match, folded name) would be a
   * fourth thing the seat-claim dance has to unwind on failure, and two people racing for one name
   * is a cosmetic collision, not a capability.
   */
  private async refuseDuplicateName(matchId: string, displayName: string): Promise<void> {
    this.assertNameIsFree(await this.deps.storage.players.listByMatch(matchId), displayName)
  }

  private assertNameIsFree(roster: readonly Player[], displayName: string): void {
    const wanted = foldName(displayName)
    if (roster.some((player) => foldName(player.displayName) === wanted)) {
      throw new ConflictError('Somebody in this match already plays under that name', {
        reason: 'display_name_taken',
      })
    }
  }

  /**
   * Undo a join that could not be completed. The token was never returned, so the row is
   * unreachable: leaving it would hold a seat and keep `allActiveReady` waiting forever on a player
   * who does not exist. Deleting the row is safe precisely because nobody ever held its token.
   */
  private async rollbackJoin(matchId: string, playerId: string): Promise<void> {
    try {
      await this.deps.storage.players.delete(playerId)
      await this.deps.storage.matches.releaseSeat(matchId)
    } catch (error) {
      this.deps.logger.error('could not roll back an incomplete join', {
        matchId,
        playerId,
        error: String(error),
      })
    }
  }

  private async abandon(match: Match, now: Date): Promise<void> {
    const changed = await this.deps.storage.matches.transition(
      match.id,
      ['lobby', 'running', 'desynced'],
      { status: 'abandoned', updatedAt: now },
    )
    if (changed) {
      await this.publisher.publish(match.id, {
        type: 'match.statusChanged',
        payload: { status: 'abandoned' },
      })
    }
  }

  /**
   * Inserts the match with a join code no other match holds. The uniqueness is the database's to
   * enforce, not ours to check first: a `getByJoinCode` probe before the insert would be a race, so
   * a refused insert is what drives the retry.
   */
  private async createWithFreshJoinCode(draft: Match): Promise<Match> {
    for (let attempt = 0; attempt < JOIN_CODE_ATTEMPTS; attempt += 1) {
      const match: Match = { ...draft, joinCode: generateJoinCode(JOIN_CODE_LENGTH) }
      if (await this.deps.storage.matches.create(match)) return match
    }
    throw new ConflictError('Could not allocate a join code; retry', {
      reason: 'join_code_exhausted',
    })
  }

  private async membership(match: Match, player: Player, token: string): Promise<MembershipView> {
    return {
      match: await this.query.view(match),
      player: toPlayerView(player, match.hostPlayerId),
      token,
      joinCode: match.joinCode,
    }
  }
}

function requireHost(principal: Principal): void {
  if (principal.player.id !== principal.match.hostPlayerId) {
    throw new ForbiddenError('Only the host can do that', { reason: 'host_only' })
  }
}
