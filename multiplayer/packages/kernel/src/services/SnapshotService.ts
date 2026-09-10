import type { SnapshotView, UploadSnapshotRequest } from '@chaos-overlords/contracts'
import type { Snapshot } from '../domain/entities'
import { ConflictError, ForbiddenError, NotFoundError } from '../domain/errors'
import type { Principal } from './AuthService'
import type { KernelDeps } from './deps'
import type { EventPublisher } from './EventPublisher'
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
