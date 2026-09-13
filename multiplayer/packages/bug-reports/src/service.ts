import {
  BUG_REPORT_LIMITS,
  type BugReportReceipt,
  type SubmitBugReportRequest,
} from '@chaos-overlords/contracts'
import { type Clock, type Logger, sha256Hex, ValidationError } from '@chaos-overlords/kernel'
import { ARCHIVE_CONTENT_TYPE, archiveKey } from './blobs'
import type { BlobStore, BugReportRepository, StoredBugReport } from './ports'

export interface BugReportServiceDeps {
  repository: BugReportRepository
  clock: Clock
  logger: Logger
  /**
   * Where archives go. Omit it and a deployment keeps small ones in its database row and turns
   * large ones away — see {@link BugReportService.submit}.
   */
  blobs?: BlobStore
}

export interface BugReportService {
  submit(request: SubmitBugReportRequest): Promise<BugReportReceipt>
  /** Triage reads. Not reachable over HTTP; an operator calls them from a script or a console. */
  get(id: string): Promise<StoredBugReport | null>
  list(limit: number): Promise<StoredBugReport[]>
  /** The archive itself, wherever it was kept. */
  archive(id: string): Promise<Uint8Array | null>
}

/**
 * Intake for one bug report.
 *
 * The interesting decision is where the attached journal goes, and it is decided by one thing:
 * whether the deployment has a blob store.
 *
 * - **With one** (R2 on Cloudflare, a directory self-hosted) every archive goes to it, whatever its
 *   size, and the row keeps only the key and the metadata. This is the configuration a public
 *   server runs: a full-match journal is hundreds of kilobytes to a few megabytes compressed, D1
 *   refuses a row over 2 MB outright, and even the ones that fit would make every triage query drag
 *   megabytes of base64 through the query path to answer a question about a timestamp.
 * - **Without one** an archive up to {@link BUG_REPORT_LIMITS.inlineStateBytes} is kept in the row,
 *   which is what makes a `pnpm --filter node-server start` with no configuration work end to end.
 *   A larger one is dropped and the report is still accepted, because the player's description is
 *   worth having even when the journal is not storable — the receipt says `omitted` so they know.
 *
 * Nothing here decompresses or parses an archive. It is opaque bytes with a digest, and keeping it
 * that way is what makes accepting one from an unauthenticated stranger safe.
 */
export function createBugReportService(deps: BugReportServiceDeps): BugReportService {
  const { repository, clock, logger, blobs } = deps

  return {
    async submit(request) {
      const receivedAt = clock.now()
      const id = crypto.randomUUID()

      const stored = request.state
        ? await placeArchive(id, receivedAt, request.state)
        : { state: null, outcome: 'not_sent' as const }

      const report: StoredBugReport = {
        id,
        receivedAt,
        message: request.message,
        clientVersion: request.client.version,
        clientPlatform: request.client.platform,
        context: request.context ?? null,
        state: stored.state,
      }

      try {
        await repository.insert(report)
      } catch (error) {
        // The archive was written first, so a failed insert would otherwise leave an object nobody
        // can ever find a key for. Best effort: if this fails too the object is merely orphaned,
        // which is a cleanup job rather than a lost report.
        if (stored.state?.blobKey) await forget(stored.state.blobKey)
        throw error
      }

      logger.info('bug report received', {
        id,
        bytes: stored.state?.compressedBytes ?? 0,
        state: stored.outcome,
      })
      return {
        id,
        receivedAt: receivedAt.toISOString(),
        stateStored: stored.outcome,
      }
    },

    get: (id) => repository.get(id),

    list: (limit) => repository.list(limit),

    async archive(id) {
      const report = await repository.get(id)
      const state = report?.state
      if (!state) return null
      if (state.blobKey) return (await blobs?.get(state.blobKey)) ?? null
      return state.body === null ? null : decodeBase64(state.body)
    },
  }

  /**
   * Decodes, verifies and files the archive.
   *
   * The digest is checked before anything is written. The client computes it over the compressed
   * bytes it sent, so a mismatch is a truncated or mangled upload — and an archive that does not
   * hash to what its own report claims is worse than no archive, because somebody would spend an
   * afternoon replaying it before finding out.
   */
  async function placeArchive(
    id: string,
    receivedAt: Date,
    state: NonNullable<SubmitBugReportRequest['state']>,
  ): Promise<{ state: StoredBugReport['state']; outcome: BugReportReceipt['stateStored'] }> {
    const bytes = decodeBase64(state.body)
    const digest = await sha256Hex(bytes)
    if (digest !== state.sha256) {
      throw new ValidationError('The attached state does not match its declared digest', {
        reason: 'state_digest_mismatch',
      })
    }

    const common = {
      codec: state.codec,
      replayFormatVersion: state.replayFormatVersion,
      uncompressedBytes: state.uncompressedBytes,
      compressedBytes: bytes.byteLength,
      sha256: digest,
      anonymized: state.anonymized,
    }

    if (blobs) {
      const key = archiveKey(id, receivedAt)
      await blobs.put(key, bytes, ARCHIVE_CONTENT_TYPE)
      return { state: { ...common, blobKey: key, body: null }, outcome: 'stored' }
    }

    if (bytes.byteLength > BUG_REPORT_LIMITS.inlineStateBytes) {
      logger.warn(
        'bug report state dropped: no blob store and the archive is too large for a row',
        {
          id,
          bytes: bytes.byteLength,
          limit: BUG_REPORT_LIMITS.inlineStateBytes,
        },
      )
      return { state: null, outcome: 'omitted' }
    }
    return { state: { ...common, blobKey: null, body: state.body }, outcome: 'stored' }
  }

  async function forget(key: string): Promise<void> {
    try {
      await blobs?.delete(key)
    } catch (error) {
      logger.warn('could not remove the archive of a report that failed to store', {
        key,
        error: String(error),
      })
    }
  }
}

/**
 * Base64 to bytes.
 *
 * `atob` rather than `Buffer`, which the Workers runtime only has under `nodejs_compat`; the
 * contract has already held the string to the standard alphabet and a length that is a multiple of
 * four, so this cannot be handed something it has to guess about.
 */
export function decodeBase64(value: string): Uint8Array {
  const binary = atob(value)
  const bytes = new Uint8Array(binary.length)
  for (let index = 0; index < binary.length; index++) bytes[index] = binary.charCodeAt(index)
  return bytes
}
