import {
  type GameSettings,
  gameSettingsSchema,
  type SnapshotView,
  type UploadSnapshotRequest,
} from '@chaos-overlords/contracts'
import { safeParse } from 'valibot'
import type { Snapshot } from '../domain/entities'
import { ConflictError, ForbiddenError, NotFoundError } from '../domain/errors'
import { authoritativeCandidates } from '../logic/turn-logic'
import type { Principal } from './AuthService'
import type { KernelDeps } from './deps'
import type { EventPublisher } from './EventPublisher'
import type { TurnService } from './TurnService'

/**
 * Snapshots kept per live match. Enough to cover a desync being repaired while an earlier one is
 * still being fetched by a straggler, and far fewer than a long match would otherwise accumulate.
 */
const SNAPSHOTS_KEPT_PER_MATCH = 5

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
  ) {}

  async upload(principal: Principal, request: UploadSnapshotRequest): Promise<void> {
    const { match, player } = principal
    const isBootstrap = request.turn === 0 && match.status === 'running' && match.currentTurn === 1
    // The bootstrap snapshot is the host's alone: it is unconstrained (nothing has been reported
    // yet) and it is the match's starting state, which only the host has.
    if (isBootstrap && player.id !== match.hostPlayerId) {
      throw new ForbiddenError('Only the host uploads the starting snapshot', {
        reason: 'host_only',
      })
    }
    if (player.status !== 'active' && player.status !== 'takeoverPending') {
      throw new ForbiddenError('You are no longer part of this match', { reason: 'not_active' })
    }
    if (!isBootstrap && match.status !== 'desynced') {
      throw new ConflictError('Full snapshots are only accepted for bootstrap or desync repair', {
        reason: 'snapshot_not_required',
      })
    }
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
      // Nothing has been reported yet, so there is no consensus a bootstrap could contradict.
      await this.deps.storage.snapshots.put(await this.snapshotOf(match, player.id, request))
      await this.publishSeatSummaries(
        match.id,
        mergeSeatSummaries(match.settings.gameSettings, request.seatSummaries),
      )
      await this.pruneOldSnapshots(match.id)
      return
    }
    await this.requireDesyncedTurn(match.id, request.turn)
    await this.requireRepairAuthority(match, player, request.turn, request.stateHash)
    // Judged before anything is written: a refusal after the row landed would leave a stored
    // snapshot the caller was told was rejected, with no `snapshot.available` and no verdict.
    const gameSettings = mergeSeatSummaries(match.settings.gameSettings, request.seatSummaries)
    await this.deps.storage.snapshots.put(await this.snapshotOf(match, player.id, request))
    await this.publishSeatSummaries(match.id, gameSettings)
    await this.pruneOldSnapshots(match.id)
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

  private async snapshotOf(
    match: Principal['match'],
    uploadedByPlayerId: string,
    request: UploadSnapshotRequest,
  ): Promise<Snapshot> {
    // Re-running the settings schema over the merge is what holds the blob to its cap; it throws
    // before anything is stored, which is why it is called on both paths before the write.
    mergeSeatSummaries(match.settings.gameSettings, request.seatSummaries)
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
  private async publishSeatSummaries(matchId: string, gameSettings: GameSettings): Promise<void> {
    try {
      await this.deps.storage.matches.updateRuntimeGameSettings(
        matchId,
        gameSettings,
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
  private async requireDesyncedTurn(matchId: string, turn: number): Promise<void> {
    const row = await this.deps.storage.turns.get(matchId, turn)
    if (row?.status === 'desynced') return
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
   * no majority, nothing to count, and no third party to ask, so the host breaks it as before and a
   * peer may not. Letting either side of a tie impose its state would hand a two-player match to
   * whoever uploaded first.
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
    if (candidates.length === 1 || player.id === match.hostPlayerId) return
    throw new ForbiddenError('Only the host may break a tie between reported states', {
      reason: 'host_only',
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

  async get(matchId: string, turn: number): Promise<SnapshotView> {
    const snapshot = await this.deps.storage.snapshots.get(matchId, turn)
    if (!snapshot) throw new NotFoundError('No snapshot for that turn', { reason: 'no_snapshot' })
    return toView(snapshot)
  }
}

/**
 * Publishes the seat summaries into the settings blob, or refuses the upload.
 *
 * `createMatch` holds `gameSettings` to 8 KiB and to a nesting depth; this write goes in through
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
