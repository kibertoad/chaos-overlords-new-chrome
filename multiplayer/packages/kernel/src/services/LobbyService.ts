import {
  type CreateMatchRequest,
  foldName,
  type JoinMatchRequest,
  type JoinRunningMatchRequest,
  LIMITS,
  type MatchSettings,
  type MembershipView,
  type TakeoverVoteRequest,
} from '@chaos-overlords/contracts'
import { activePlayers, type Match, type Player } from '../domain/entities'
import { ConflictError, ForbiddenError, NotFoundError, UnauthorizedError } from '../domain/errors'
import { generateJoinCode, generateSeed, generateToken, hashToken } from '../logic/crypto'
import { hashPassword, verifyPassword } from '../logic/password'
import { assignSlots } from '../logic/turn-logic'
import type { Principal } from './AuthService'
import type { KernelDeps } from './deps'
import type { EventPublisher } from './EventPublisher'
import { MatchQueryService, toPlayerView } from './MatchQueryService'
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
/** A human seat nobody is playing: the ones an absence vote can hand to the computer. */
const ABSENT_HUMAN_STATUSES: ReadonlyArray<Player['status']> = ['takeoverPending', 'left', 'kicked']

export interface LobbyServiceOptions {
  /** Generates the player/match ids; defaults to `crypto.randomUUID`. */
  newId?: () => string
}

/** Match creation, joining, leaving, kicking and the start transition. */
export class LobbyService {
  private readonly query: MatchQueryService
  private readonly newId: () => string

  constructor(
    private readonly deps: KernelDeps,
    private readonly publisher: EventPublisher,
    private readonly turns: TurnService,
    options: LobbyServiceOptions = {},
  ) {
    this.query = new MatchQueryService(deps.storage)
    this.newId = options.newId ?? (() => crypto.randomUUID())
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

  async join(request: JoinMatchRequest): Promise<MembershipView> {
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
    if (match.passwordHash !== null) {
      if (!request.password) {
        throw new UnauthorizedError('This match needs a password', { reason: 'password_required' })
      }
      if (!(await verifyPassword(request.password, match.passwordHash))) {
        throw new UnauthorizedError('Wrong password', { reason: 'wrong_password' })
      }
    }
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

  async joinRunning(request: JoinRunningMatchRequest): Promise<MembershipView> {
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
    if (match.passwordHash !== null) {
      if (!request.password || !(await verifyPassword(request.password, match.passwordHash))) {
        throw new UnauthorizedError('Wrong password', { reason: 'wrong_password' })
      }
    }
    const existing = await this.deps.storage.players.listByMatch(match.id)
    if (existing.some((player) => player.slot === request.slot)) {
      throw new ConflictError('That seat has already belonged to a human', {
        reason: 'seat_reserved',
      })
    }
    // The lobby door counts seats through `claimSeat`; this one has no counter behind it, so the
    // host's own limit has to be read here or a two-seat match could gather humans up to six.
    if (existing.length >= match.settings.maxPlayers) {
      throw new ConflictError('The match is full', { reason: 'match_full' })
    }
    this.assertNameIsFree(existing, request.displayName)
    const token = generateToken()
    const seatKey = (await hashToken(`${match.id}:${request.slot}`)).slice(0, 32)
    const player: Player = {
      id: `late-${seatKey}`,
      matchId: match.id,
      slot: request.slot,
      joinOrder: match.joinCounter,
      displayName: request.displayName,
      portraitId: request.portraitId ?? DEFAULT_PORTRAIT_ID,
      tokenHash: await hashToken(token),
      status: 'active',
      joinedAt: this.deps.clock.now(),
    }
    if (!(await this.deps.storage.players.createLate(player))) {
      throw new ConflictError('That seat was claimed by another player', {
        reason: 'seat_reserved',
      })
    }
    const openTurn = await this.deps.storage.turns.get(match.id, match.currentTurn)
    if (openTurn?.status === 'open') await this.deps.storage.turns.open(openTurn, [player.id])
    await this.publisher.publish(match.id, {
      type: 'match.latePlayerJoined',
      payload: { playerId: player.id, slot: player.slot },
    })
    return this.membership(match, player, token)
  }

  async leave(principal: Principal): Promise<void> {
    await this.remove(principal.match, principal.player, 'left')
  }

  async rejoin(principal: Principal): Promise<void> {
    const { match, player } = principal
    if (match.status !== 'running' && match.status !== 'desynced') {
      throw new ConflictError('The match is not in progress', { reason: 'match_not_running' })
    }
    if (player.status === 'kicked') {
      throw new ForbiddenError('A kicked player cannot rejoin', { reason: 'kicked' })
    }
    if (player.status === 'active') return
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
    const openTurn = await this.deps.storage.turns.get(match.id, match.currentTurn)
    if (openTurn?.status === 'open') {
      // `open` is idempotent and tops up missing participant rows even when the turn already exists.
      await this.deps.storage.turns.open(openTurn, [player.id])
    }
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
    // one, which revokes their token for good. A pending host is still present; the takeover vote is
    // the path that decides otherwise.
    if (currentHost === undefined || VACANT_HOST_STATUSES.includes(currentHost.status)) {
      await this.deps.storage.matches.transition(match.id, ['running', 'desynced'], {
        hostPlayerId: player.id,
        updatedAt: this.deps.clock.now(),
      })
      await this.publisher.publish(match.id, {
        type: 'lobby.hostChanged',
        payload: { hostPlayerId: player.id },
      })
    }
    await this.turns.reevaluate(match.id)
    await this.turns.resumeAfterTakeoverVotes(match.id)
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

  async kick(principal: Principal, targetPlayerId: string): Promise<void> {
    const { match, player } = principal
    requireHost(principal)
    if (targetPlayerId === player.id) {
      throw new ConflictError('Leave the match instead of kicking yourself', {
        reason: 'self_kick',
      })
    }
    const target = await this.deps.storage.players.get(targetPlayerId)
    if (!target || target.matchId !== match.id) {
      throw new NotFoundError('No such player in this match', { reason: 'unknown_player' })
    }
    await this.remove(match, target, 'kicked')
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
    if (match.status !== 'running' && match.status !== 'desynced') {
      throw new ConflictError('The match is not in progress', { reason: 'match_not_running' })
    }
    const currentVoter = await this.deps.storage.players.get(player.id)
    if (currentVoter?.status !== 'active') {
      throw new ForbiddenError('Only present players may vote', { reason: 'not_active' })
    }
    const target = await this.deps.storage.players.get(targetPlayerId)
    if (!target || target.matchId !== match.id) {
      throw new NotFoundError('No such player in this match', { reason: 'unknown_player' })
    }
    if (!ABSENT_HUMAN_STATUSES.includes(target.status)) {
      throw new ConflictError('That player is not awaiting a takeover vote', {
        reason: 'takeover_not_pending',
      })
    }
    const now = this.deps.clock.now()
    if (
      !(await this.deps.storage.takeovers.castVote(
        match.id,
        target.id,
        player.id,
        request.decision,
        now,
      ))
    ) {
      // The seat is absent but nobody asked about it yet: it went quiet before this table existed,
      // or while nobody was present to ask. The vote itself opens the question, so the seat can
      // still be decided rather than left idle for the rest of the match.
      await this.turns.openTakeoverPrompt(match.id, target.id, match.currentTurn)
      if (
        !(await this.deps.storage.takeovers.castVote(
          match.id,
          target.id,
          player.id,
          request.decision,
          now,
        ))
      ) {
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

    const votes = new Map(
      (await this.deps.storage.takeovers.listVotes(match.id, target.id)).map((vote) => [
        vote.voterPlayerId,
        vote.decision,
      ]),
    )
    const voters = activePlayers(await this.deps.storage.players.listByMatch(match.id))
    if (voters.length === 0 || voters.some((voter) => votes.get(voter.id) !== 'computer')) return
    if (
      !(await this.deps.storage.players.transitionStatus(target.id, [target.status], 'computer'))
    ) {
      return
    }
    await this.deps.storage.takeovers.closePrompt(match.id, target.id)
    await this.publisher.publish(match.id, {
      type: 'match.playerTakenOver',
      payload: { playerId: target.id },
    })
    if (target.id === match.hostPlayerId) {
      const successor = activePlayers(await this.deps.storage.players.listByMatch(match.id))[0]
      if (successor) {
        await this.deps.storage.matches.transition(match.id, ['running', 'desynced'], {
          hostPlayerId: successor.id,
          updatedAt: this.deps.clock.now(),
        })
        await this.publisher.publish(match.id, {
          type: 'lobby.hostChanged',
          payload: { hostPlayerId: successor.id },
        })
      }
    }
    await this.turns.reevaluate(match.id)
    await this.turns.resumeAfterTakeoverVotes(match.id)
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
    await this.publisher.publish(match.id, {
      type: 'match.started',
      payload: {
        seed,
        players: activePlayers(seated).map((player) => toPlayerView(player, match.hostPlayerId)),
      },
    })
    await this.turns.openTurn({ ...match, status: 'running', seed }, FIRST_TURN)
  }

  private async remove(match: Match, target: Player, reason: 'left' | 'kicked'): Promise<void> {
    if (target.status === 'kicked') return
    // Leaving is something only a seated, active player does. A kick has to reach the seat whatever
    // state it is in: a player who left or went quiet keeps a working token until it is revoked
    // here, and `rejoin` turns away nobody but the kicked, so answering 204 and doing nothing let a
    // kicked player walk straight back in.
    const wasActive = target.status === 'active'
    if (!wasActive && reason !== 'kicked') return
    const now = this.deps.clock.now()
    if (match.status === 'lobby') {
      if (target.id === match.hostPlayerId) {
        await this.abandon(match, now)
        return
      }
      await this.deps.storage.players.delete(target.id)
      await this.deps.storage.matches.releaseSeat(match.id)
      await this.publisher.publish(match.id, {
        type: 'lobby.playerLeft',
        payload: { playerId: target.id, reason },
      })
      return
    }
    await this.deps.storage.players.setStatus(target.id, reason)
    // Membership is the only thing the token ever proved, so it stops working here: a kicked player
    // keeps neither the event stream nor the sealed order sets of the turns that follow. The revoke
    // closes the next request and the hang-up closes the streams already open, which are never
    // re-authenticated and would otherwise outlive the membership for as long as the client liked.
    if (reason === 'kicked') {
      await this.deps.storage.players.revokeToken(target.id)
      await this.hangUp(match.id, target.id)
    }
    await this.publisher.publish(match.id, {
      type: 'lobby.playerLeft',
      payload: { playerId: target.id, reason },
    })
    // A seat that was already absent changes no tally and holds no host role: whatever vote or
    // succession its absence called for ran when it went quiet, and it is not a seat any turn is
    // waiting on now. Nor does a match that is over have anything left to vote on.
    if (!wasActive || (match.status !== 'running' && match.status !== 'desynced')) return
    const remaining = activePlayers(await this.deps.storage.players.listByMatch(match.id))
    if (remaining.length === 0) {
      // Keep the durable match available. The first former member to rejoin becomes host.
      return
    }
    await this.turns.openTakeoverPrompt(match.id, target.id, match.currentTurn)
    await this.turns.pauseForTakeoverVote(match.id)
    if (target.id === match.hostPlayerId) {
      const successor = remaining[0] as Player
      await this.deps.storage.matches.transition(match.id, ['running', 'desynced'], {
        hostPlayerId: successor.id,
        updatedAt: now,
      })
      await this.publisher.publish(match.id, {
        type: 'lobby.hostChanged',
        payload: { hostPlayerId: successor.id },
      })
    }
    // A departure can complete readiness or a consensus that was waiting on the leaver.
    await this.turns.reevaluate(match.id)
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
