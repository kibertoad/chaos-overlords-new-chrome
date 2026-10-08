import {
  type AiSeatSummary,
  type GameSettings,
  gameSettingsSchema,
  type SnapshotView,
  type UploadSnapshotRequest,
} from '@chaos-overlords/contracts'
import { safeParse } from 'valibot'
import type { Match, Snapshot, Turn } from '../domain/entities'
import { ConflictError, ForbiddenError, NotFoundError } from '../domain/errors'
import { authoritativeCandidates, tieBreaker } from '../logic/turn-logic'
import type { Principal } from './AuthService'
import type { KernelDeps } from './deps'
import type { EventPublisher } from './EventPublisher'
import {
  requireInProgress,
  requireLockstep,
  requireParticipant,
  requireReleased,
  requireTurn,
} from './guards'
import { type Referee, SNAPSHOTS_KEPT_PER_MATCH } from './Referee'
import type { TurnService } from './TurnService'

/**
 * Host-uploaded native snapshots: the recovery path for a desync and the bootstrap for a
 * reconnecting client. The server stores bytes it never decodes; the hash beside them is what
 * a desynced turn's reports are then judged against.
 */
export class SnapshotService {
  constructor(
    private readonly deps: KernelDeps,
    private readonly publisher: EventPublisher,
    private readonly turns: TurnService,
    private readonly referee?: Referee,
  ) {}

  async upload(principal: Principal, request: UploadSnapshotRequest): Promise<void> {
    const { match, player } = principal
    // No client of a match played from views holds its state: the server writes every snapshot.
    requireLockstep(match, 'snapshot_not_required')
    const isBootstrap = request.turn === 0 && match.status === 'running' && match.currentTurn === 1
    // The bootstrap snapshot is the host's alone: it is unconstrained (nothing has been reported
    // yet) and it is the match's starting state, which only the host has.
    if (isBootstrap && player.id !== match.hostPlayerId) {
      throw new ForbiddenError('Only the host uploads the starting snapshot', {
        reason: 'host_only',
      })
    }
    requireParticipant(player)
    requireInProgress(match)
    if (request.turn > match.currentTurn) {
      throw new ConflictError('Cannot snapshot a turn that has not opened', {
        reason: 'future_turn',
      })
    }
    if (request.turn === match.currentTurn && match.currentTurn > 0) {
      throw new ConflictError('The current turn is still open; snapshot the last sealed turn', {
        reason: 'turn_open',
      })
    }
    if (isBootstrap) {
      // A refereed match starts from the state the server built itself, which it stores as the
      // turn-0 snapshot; the host's upload has to be that state, and only its seat summaries are
      // kept. Otherwise nothing has been reported yet, so there is no consensus a bootstrap could
      // contradict.
      const serverStart = (await this.referee?.ensureSnapshot(match, 0)) ?? null
      if (serverStart === null) {
        await this.store(match, player.id, request)
        return
      }
      if (serverStart !== request.stateHash) {
        throw new ConflictError('The starting snapshot must be the state the server built', {
          reason: 'uncorroborated_state_hash',
          candidateStateHashes: [serverStart],
        })
      }
      await this.storeSeatSummaries(match, request)
      return
    }
    const turn = await requireTurn(this.deps.storage.turns, match.id, request.turn)
    if (turn.status === 'confirmed') {
      await this.storeCheckpoint(match, player, turn, request)
      return
    }
    this.requireDesyncedTurn(turn)
    await this.requireRepairAuthority(match, player, request.turn, request.stateHash)
    await this.store(match, player.id, request)
    await this.publisher.publish(match.id, {
      type: 'snapshot.available',
      payload: {
        turn: request.turn,
        formatVersion: request.formatVersion,
        stateHash: request.stateHash,
        uploadedByPlayerId: player.id,
      },
    })
    await this.turns.settle(match.id, request.turn)
  }

  /**
   * A periodic checkpoint of a turn the match has already agreed on.
   *
   * Only two kinds of snapshot used to exist, the host's turn-0 bootstrap and desync repairs, so a
   * client reconnecting at turn 80 replayed eighty turns from the bootstrap — eighty sequential
   * fetches, each under its own deadline, and eighty full turn resolutions, before the player saw
   * anything. The design doc talks about matches of hundreds of turns.
   *
   * It is not a second way to say what a turn's state was. The turn is CONFIRMED, so the verdict
   * has already been reached and written on the turn row, and the checkpoint is refused unless it
   * claims exactly that hash: it can only agree with what every client already computed. The host
   * writes it because the host is the one client the protocol already asks for bytes, storage stays
   * bounded by the per-match snapshot pruning, and the rate stays bounded by the upload budget.
   *
   * Nothing is announced. A checkpoint changes nothing about the match; it is there for whoever
   * reconnects next, and `getLatest` is what finds it.
   */
  private async storeCheckpoint(
    match: Principal['match'],
    player: Principal['player'],
    turn: Turn,
    request: UploadSnapshotRequest,
  ): Promise<void> {
    if (player.id !== match.hostPlayerId) {
      throw new ForbiddenError('Only the host uploads checkpoints', { reason: 'host_only' })
    }
    if (turn.stateHash !== request.stateHash) {
      throw new ConflictError('A checkpoint must be the state the turn was confirmed on', {
        reason: 'uncorroborated_state_hash',
        candidateStateHashes: turn.stateHash === null ? [] : [turn.stateHash],
      })
    }
    // A server that referees the match writes its own checkpoints of the same state, and the
    // bytes are not written twice.
    const stored = await this.deps.storage.snapshots.getSummary(match.id, turn.number)
    if (stored?.stateHash === request.stateHash) {
      await this.storeSeatSummaries(match, request)
      return
    }
    await this.store(match, player.id, request)
  }

  /** The seat summaries an upload carries, held to the settings cap, without its bytes. */
  private async storeSeatSummaries(
    match: Principal['match'],
    request: UploadSnapshotRequest,
  ): Promise<void> {
    const current = await this.deps.storage.matches.get(match.id)
    if (current) mergeSeatSummaries(current.settings.gameSettings, request.seatSummaries)
    await this.writeSeatSummaries(match.id, request.seatSummaries)
  }

  /**
   * Write an accepted snapshot: the row, the seat summaries it carries, then the prune.
   *
   * The merge is judged before anything is written: a refusal after the row landed would leave a
   * stored snapshot the caller was told was rejected, with no `snapshot.available` and no verdict.
   * Re-running the settings schema over the merge is what holds the blob to its cap, which is why
   * every upload path comes through here before its write. It is judged once, against the blob as
   * it is stored now rather than the copy read when the request was authenticated, and the write
   * after the row only merges what was judged.
   */
  private async store(
    match: Principal['match'],
    uploadedByPlayerId: string,
    request: UploadSnapshotRequest,
  ): Promise<void> {
    const current = await this.deps.storage.matches.get(match.id)
    if (current) mergeSeatSummaries(current.settings.gameSettings, request.seatSummaries)
    await this.deps.storage.snapshots.put(this.snapshotOf(match, uploadedByPlayerId, request))
    await this.writeSeatSummaries(match.id, request.seatSummaries)
    await this.pruneOldSnapshots(match.id)
  }

  private snapshotOf(
    match: Principal['match'],
    uploadedByPlayerId: string,
    request: UploadSnapshotRequest,
  ): Snapshot {
    return {
      matchId: match.id,
      turn: request.turn,
      formatVersion: request.formatVersion,
      protocolVersion: request.protocolVersion ?? 1,
      sessionVersion: request.sessionVersion ?? 1,
      stateHash: request.stateHash,
      uploadedByPlayerId,
      uploadedAt: this.deps.clock.now(),
      body: request.body,
    }
  }

  /**
   * Late-join hints are optional metadata; the snapshot the players are waiting on is not. A
   * transient failure of this write is logged and never fails the upload that already succeeded.
   */
  private async writeSeatSummaries(
    matchId: string,
    seatSummaries: UploadSnapshotRequest['seatSummaries'],
  ): Promise<void> {
    try {
      await this.deps.storage.matches.updateSeatSummaries(
        matchId,
        seatSummaries,
        this.deps.clock.now(),
      )
    } catch (error) {
      this.deps.logger.warn('could not publish seat summaries', { matchId, error: String(error) })
    }
  }

  /**
   * A repair must name the turn that actually diverged.
   *
   * "Below `currentTurn` while the match is desynced" was too loose, and the corroboration check
   * below cannot make up the difference: it counts reports, and a turn with none — the successor a
   * seal opened before anybody had reported on it — has no candidates to contradict, so it returned
   * early and let the host store any hash it liked for that turn. `evaluateConsensus` then had an
   * authoritative hash to judge against and could only confirm or wait, never desync. That is
   * exactly the "host arbitrates a disagreement it is a party to" case the corroboration rule
   * exists to prevent; it needs the turn's own status to close it.
   */
  private requireDesyncedTurn(turn: Turn): void {
    if (turn.status === 'desynced') return
    throw new ConflictError('Only a turn that diverged may be repaired with a snapshot', {
      reason: 'turn_not_desynced',
    })
  }

  /**
   * Who may post a repair, and which hash they may claim.
   *
   * The hash is the hard rule and it binds everyone: the snapshot for a desynced turn becomes the
   * state every other client is told to converge on, so a client free to name any hash could
   * resolve a deliberate desync in favour of a doctored one. A hash that more active players
   * reported than any other cannot be minted by one of them.
   *
   * Who may post it follows from that. A hash that is the SOLE most-reported one is already the
   * majority's, so whoever holds it may post the repair whether or not they are the host. That is
   * the one case a host-only rule could not serve at all: a host that is itself the odd one out can
   * never name a majority hash, so no repair existed and the match stayed paused until retention
   * collected it. A TIE — most of all the 1-1 split of a two-player match — is different: there is
   * no majority, nothing to count, and no third party to ask, so exactly one player breaks it and
   * nobody else may. Letting either side of a tie impose its state would hand a two-player match to
   * whoever uploaded first. That player is {@link tieBreaker}'s: the host when the host holds one
   * of the tied hashes, which is every tie of four players or fewer, and otherwise the
   * lowest-numbered seat that does. It is judged on the reports and the roster as they are now, the
   * same inputs the announcement that named the player was computed from.
   */
  private async requireRepairAuthority(
    match: Principal['match'],
    player: Principal['player'],
    turn: number,
    stateHash: string,
  ): Promise<void> {
    const [players, reports] = await Promise.all([
      this.deps.storage.players.listByMatch(match.id),
      this.deps.storage.turns.listReports(match.id, turn),
    ])
    const candidates = authoritativeCandidates(players, reports)
    if (!candidates.includes(stateHash)) {
      throw new ConflictError(
        'A recovery snapshot must match a state hash the players reported most often',
        { reason: 'uncorroborated_state_hash', candidateStateHashes: candidates },
      )
    }
    if (candidates.length === 1) return
    if (player.id === tieBreaker(players, reports, candidates, match.hostPlayerId)) return
    throw new ForbiddenError('Only the designated player may break a tie between reported states', {
      reason: 'not_tie_breaker',
      candidateStateHashes: candidates,
    })
  }

  /**
   * Bound what one live match holds. A snapshot is a megabyte of base64 and a long match can desync
   * many times; retention only collects matches that are over, so without this a single match that
   * runs for months is unbounded growth. Only the recent ones have a use — a desync is repaired
   * from the turn it happened on, and a reconnecting client bootstraps from the newest — so the
   * older ones are dead weight. Failing to prune must never fail the upload that just succeeded.
   */
  private async pruneOldSnapshots(matchId: string): Promise<void> {
    try {
      const dropped = await this.deps.storage.snapshots.prune(matchId, SNAPSHOTS_KEPT_PER_MATCH)
      if (dropped > 0) this.deps.logger.debug('pruned old snapshots', { matchId, dropped })
    } catch (error) {
      this.deps.logger.warn('could not prune old snapshots', { matchId, error: String(error) })
    }
  }

  async latest(matchId: string): Promise<SnapshotView> {
    const snapshot = await this.deps.storage.snapshots.getLatest(matchId)
    if (!snapshot) throw new NotFoundError('No snapshot uploaded yet', { reason: 'no_snapshot' })
    return toView(snapshot)
  }

  /**
   * The newest snapshot as a member reads it: refused in a match played from views until it has
   * ended, since a snapshot is the whole state.
   */
  async memberLatest(match: Match): Promise<SnapshotView> {
    requireReleased(match, 'The whole state')
    return this.latest(match.id)
  }

  /** One turn's snapshot as a member reads it; see {@link memberLatest}. */
  async memberGet(match: Match, turn: number): Promise<SnapshotView> {
    requireReleased(match, 'The whole state')
    return this.get(match.id, turn)
  }

  async get(matchId: string, turn: number): Promise<SnapshotView> {
    const snapshot = await this.deps.storage.snapshots.get(matchId, turn)
    if (!snapshot) throw new NotFoundError('No snapshot for that turn', { reason: 'no_snapshot' })
    return toView(snapshot)
  }
}

/**
 * Write the host's seat summaries into the match's stored `gameSettings`.
 *
 * Only the summaries are written, and the storage merges them into the blob as it stands when the
 * statement runs. The whole blob used to be replaced with the caller's copy, which was the one read
 * when the request was authenticated — a report and an upload in flight together each put back the
 * settings they started from. The cap is judged against the blob as it is now, for the same reason.
 */
export async function publishSeatSummaries(
  deps: Pick<KernelDeps, 'storage' | 'clock'>,
  matchId: string,
  seatSummaries: readonly AiSeatSummary[],
): Promise<void> {
  const current = await deps.storage.matches.get(matchId)
  if (!current) return
  mergeSeatSummaries(current.settings.gameSettings, [...seatSummaries])
  await deps.storage.matches.updateSeatSummaries(matchId, seatSummaries, deps.clock.now())
}

/**
 * The settings blob with the seat summaries merged in, or a refusal when it would not fit.
 *
 * `createMatch` holds `gameSettings` to 8 KiB and to a nesting depth; the summaries go in through
 * `json_set` and would otherwise skip both, which would make the merge a way around a cap that is
 * there because the blob is served on every match read and in every public listing. Re-running the
 * schema over the merged object is what keeps the blob's cap the blob's cap. The array itself is
 * already bounded to one entry per seat by `uploadSnapshotRequestSchema`, so reaching this is a host
 * that filled the settings almost to the cap before starting.
 */
export function mergeSeatSummaries(
  gameSettings: GameSettings,
  seatSummaries: UploadSnapshotRequest['seatSummaries'],
): GameSettings {
  const merged = { ...gameSettings, seatSummaries }
  const checked = safeParse(gameSettingsSchema, merged)
  if (!checked.success) {
    throw new ConflictError('The seat summaries do not fit inside the match settings', {
      reason: 'game_settings_too_large',
    })
  }
  return checked.output
}

function toView(snapshot: Snapshot): SnapshotView {
  return {
    turn: snapshot.turn,
    formatVersion: snapshot.formatVersion,
    protocolVersion: snapshot.protocolVersion,
    sessionVersion: snapshot.sessionVersion,
    stateHash: snapshot.stateHash,
    uploadedByPlayerId: snapshot.uploadedByPlayerId,
    uploadedAt: snapshot.uploadedAt.toISOString(),
    body: snapshot.body,
  }
}
