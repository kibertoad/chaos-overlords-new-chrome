import { type BrotliCodec, readSnapshotArchive, writeSnapshotArchive } from './archive.js'
import type { BootstrapInput, MatchStatus, RestoreInput } from './core.js'

/** What a resolver build plays. */
export interface ResolverDescription {
  /** The session version the build plays; it referees only matches stored under this one. */
  sessionVersion: number
  /** The native save format of the snapshots it writes and reads. */
  snapshotFormatVersion: number
}

/**
 * The resolver as a host exposes it, with snapshots as bare native save payloads.
 *
 * The Node host (a worker thread) and the Cloudflare host (a Worker of its own, a Durable Object per
 * match) both implement it. Every call that names a match the host does not hold rejects with
 * `MatchNotHeldError`, and one whose input the build refuses with `ResolverRefusedError`; see
 * `errors.ts`.
 */
export interface PayloadResolver {
  describe(): Promise<ResolverDescription>
  bootstrap(matchId: string, input: BootstrapInput): Promise<MatchStatus>
  restore(
    matchId: string,
    savePayload: Uint8Array,
    stateHash: string,
    input: RestoreInput,
  ): Promise<MatchStatus>
  applyEvent(matchId: string, event: unknown, sealedOrders?: unknown): Promise<MatchStatus>
  status(matchId: string): Promise<MatchStatus | null>
  savePayload(matchId: string): Promise<{ payload: Uint8Array; status: MatchStatus }>
  release(matchId: string): Promise<void>
}

/** A snapshot as the server stores it: the archive body a client uploads, and its state hash. */
export interface StoredSnapshot {
  /** Base64 of the `RCHS` archive, or of a bare payload from an older client. */
  body: string
  stateHash: string
}

/**
 * The resolver as the coordination server calls it: snapshots in the archive form clients upload
 * and read, so a checkpoint the server writes is one every client can adopt.
 */
export interface MatchResolver {
  describe(): Promise<ResolverDescription>
  /** Builds a match from the facts of `match.started`, replacing any held under the id. */
  bootstrap(matchId: string, input: BootstrapInput): Promise<MatchStatus>
  /** Picks a match up from a stored snapshot, refused unless it hashes to `stateHash`. */
  restore(matchId: string, snapshot: StoredSnapshot, input: RestoreInput): Promise<MatchStatus>
  /**
   * Folds one event of the match's log, as the server stores it, in log order: the client's own
   * fold of the log (`MatchHistory` in `src/Rechaos.Multiplayer`). The facts that change the state
   * are `match.playerTakenOver`, `match.playerReturned` that replaced the computer,
   * `match.latePlayerJoined`, `turn.opened` and `turn.sealed`, which comes with its sealed set as
   * `GET /turns/:n/orders` answers it. Every other event is accepted and ignored.
   */
  applyEvent(matchId: string, event: unknown, sealedOrders?: unknown): Promise<MatchStatus>
  /** Where a held match stands, or `null` when the host does not hold it. */
  status(matchId: string): Promise<MatchStatus | null>
  /** The held match as a snapshot every client can adopt. */
  snapshot(matchId: string): Promise<StoredSnapshot & { status: MatchStatus }>
  release(matchId: string): Promise<void>
}

/** A {@link MatchResolver} over a host's payload interface and a Brotli codec. */
export function withArchives(host: PayloadResolver, codec: BrotliCodec): MatchResolver {
  return {
    describe: () => host.describe(),
    bootstrap: (matchId, input) => host.bootstrap(matchId, input),
    restore: (matchId, snapshot, input) =>
      host.restore(matchId, readSnapshotArchive(snapshot.body, codec), snapshot.stateHash, input),
    applyEvent: (matchId, event, sealedOrders) => host.applyEvent(matchId, event, sealedOrders),
    status: (matchId) => host.status(matchId),
    snapshot: async (matchId) => {
      const { payload, status } = await host.savePayload(matchId)
      return { body: writeSnapshotArchive(payload, codec), stateHash: status.stateHash, status }
    },
    release: (matchId) => host.release(matchId),
  }
}
