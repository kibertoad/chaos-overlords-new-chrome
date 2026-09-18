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
    if (player.id !== match.hostPlayerId) {
      throw new ForbiddenError('Only the host uploads snapshots', { reason: 'host_only' })
    }
    const isBootstrap = request.turn === 0 && match.status === 'running' && match.currentTurn === 1
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
    // Bootstrap has no reports to corroborate. A repair does: only a hash the active players
    // reported most often can become the state every client is asked to adopt.
    await this.requireCorroboration(match.id, request.turn, request.stateHash)
    // Judged before anything is written: a refusal after the row landed would leave a stored
    // snapshot the caller was told was rejected, with no `snapshot.available` and no verdict.
    const gameSettings = mergeSeatSummaries(match.settings.gameSettings, request.seatSummaries)
    const snapshot: Snapshot = {
      matchId: match.id,
      turn: request.turn,
      formatVersion: request.formatVersion,
      protocolVersion: request.protocolVersion ?? 1,
      stateHash: request.stateHash,
      uploadedByPlayerId: player.id,
      uploadedAt: this.deps.clock.now(),
      body: request.body,
    }
    await this.deps.storage.snapshots.put(snapshot)
    await this.publishSeatSummaries(match.id, gameSettings)
    await this.pruneOldSnapshots(match.id)
    if (match.status === 'desynced') {
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
   * A recovery snapshot may only claim a state hash that the players themselves already computed
   * in the greatest number.
   *
   * This is what keeps the host from arbitrating a disagreement it is a party to. The snapshot for
   * a desynced turn becomes the hash every other client is told to converge on, so a host free to
   * name any hash could resolve a deliberate desync in favour of a doctored state. A hash that more
   * active players reported than any other cannot be minted by one of them. A tie leaves nothing to
   * count and the host breaks it; turns with no reports at all (a bootstrap snapshot for a
   * reconnecting client) are unconstrained, because there is no consensus to contradict yet.
   */
  private async requireCorroboration(
    matchId: string,
    turn: number,
    stateHash: string,
  ): Promise<void> {
    const [players, reports] = await Promise.all([
      this.deps.storage.players.listByMatch(matchId),
      this.deps.storage.turns.listReports(matchId, turn),
    ])
    const candidates = authoritativeCandidates(players, reports)
    if (candidates.length === 0 || candidates.includes(stateHash)) return
    throw new ConflictError(
      'A recovery snapshot must match a state hash the players reported most often',
      { reason: 'uncorroborated_state_hash', candidateStateHashes: candidates },
    )
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
    stateHash: snapshot.stateHash,
    uploadedByPlayerId: snapshot.uploadedByPlayerId,
    uploadedAt: snapshot.uploadedAt.toISOString(),
    body: snapshot.body,
  }
}
