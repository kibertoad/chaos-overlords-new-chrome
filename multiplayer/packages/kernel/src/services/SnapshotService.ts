import type { SnapshotView, UploadSnapshotRequest } from '@chaos-overlords/contracts'
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
    if (match.status !== 'running' && match.status !== 'desynced') {
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
    const turn = await this.deps.storage.turns.get(match.id, request.turn)
    if (turn?.status === 'confirmed') {
      const existing = await this.deps.storage.snapshots.get(match.id, request.turn)
      if (existing && existing.stateHash !== request.stateHash) {
        throw new ConflictError('Turn already confirmed with a different state hash', {
          reason: 'turn_confirmed',
          stateHash: existing.stateHash,
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
