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
    if (match.status !== 'running' && match.status !== 'desynced' && match.status !== 'finished') {
      throw new ConflictError('The match is not in progress', { reason: 'match_not_running' })
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
    // A confirmed turn's state hash is settled consensus. Re-uploading the same bytes is fine (a
    // reconnecting client may need them); contradicting it is not, or the snapshot clients bootstrap
    // from would disagree with the turn they already agreed on.
    //
    // The verdict's own hash is the thing to hold it to, not the snapshot beside it: a turn that
    // confirmed on unanimity alone has no snapshot yet, and the corroboration check below cannot
    // stand in for one, because it counts the reports of the players who are active NOW and every
    // reporter may have left since.
    const turn = await this.deps.storage.turns.get(match.id, request.turn)
    if (turn?.status === 'confirmed') {
      const settled =
        turn.stateHash ?? (await this.deps.storage.snapshots.get(match.id, request.turn))?.stateHash
      if (settled != null && settled !== request.stateHash) {
        throw new ConflictError('Turn already confirmed with a different state hash', {
          reason: 'turn_confirmed',
          stateHash: settled,
        })
      }
    }
    await this.requireCorroboration(match.id, request.turn, request.stateHash)
    const snapshot: Snapshot = {
      matchId: match.id,
      turn: request.turn,
      formatVersion: request.formatVersion,
      stateHash: request.stateHash,
      uploadedByPlayerId: player.id,
      uploadedAt: this.deps.clock.now(),
      body: request.body,
    }
    await this.deps.storage.snapshots.put(snapshot)
    await this.deps.storage.matches.updateRuntimeGameSettings(
      match.id,
      mergeSeatSummaries(match.settings.gameSettings, request.seatSummaries),
      this.deps.clock.now(),
    )
    await this.pruneOldSnapshots(match.id)
    // Confirmed-turn uploads are rolling autosaves. Only a desync repair asks live clients to
    // replace their state; announcing an ordinary autosave would make every client re-report it.
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
function mergeSeatSummaries(
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
    stateHash: snapshot.stateHash,
    uploadedByPlayerId: snapshot.uploadedByPlayerId,
    uploadedAt: snapshot.uploadedAt.toISOString(),
    body: snapshot.body,
  }
}
