import {
  BUG_REPORT_LIMITS,
  type BugReportReceipt,
  type SubmitBugReportRequest,
} from '@chaos-overlords/contracts'
import { type Clock, type Logger, sha256Hex, ValidationError } from '@chaos-overlords/kernel'
import { ARCHIVE_CONTENT_TYPE, archiveKey } from './blobs'
import type { BlobStore, BugReportRepository, StoredBugReport } from './ports'

/**
 * What an intake may accumulate.
 *
 * The per-address, per-minute limiter in front of the route stops one client in a retry loop. It
 * does nothing about the other shape: reports arriving from many addresses, a few a minute each,
 * for as long as anybody cares to keep sending them. Nothing in the intake path ever deleted
 * anything, so that was unbounded object storage and unbounded rows on the central deployment.
 *
 * Two bounds and a sweep. The byte budget is a whole-day ceiling on attached journals across every
 * reporter, which is what stops a distributed flood without turning away the one player who files
 * three reports in an afternoon: over budget, the report is still accepted and the journal is
 * dropped, because the description is the part worth having. Retention then deletes the rows and
 * the objects once they are older than the window.
 */
export interface BugReportRetention {
  /** Attached journal bytes accepted per rolling day across all reporters. 0 means no ceiling. */
  dailyStateBytes: number
  /** Age after which a report and its archive are deleted. 0 keeps everything. */
  maxAgeMs: number
  /** Reports deleted per sweep, so one pass cannot monopolise the database. */
  batchSize: number
}

export const DEFAULT_BUG_REPORT_RETENTION: BugReportRetention = {
  dailyStateBytes: 512 * 1024 * 1024,
  maxAgeMs: 90 * 24 * 60 * 60 * 1000,
  batchSize: 100,
}

export interface BugReportServiceDeps {
  repository: BugReportRepository
  clock: Clock
  logger: Logger
  /**
   * Where archives go. Omit it and a deployment keeps small ones in its database row and turns
   * large ones away — see {@link BugReportService.submit}.
   */
  blobs?: BlobStore
  /** Overrides {@link DEFAULT_BUG_REPORT_RETENTION}; a runtime maps its configuration onto it. */
  retention?: BugReportRetention
}

export interface BugReportService {
  submit(request: SubmitBugReportRequest): Promise<BugReportReceipt>
  /** Triage reads. Not reachable over HTTP; an operator calls them from a script or a console. */
  get(id: string): Promise<StoredBugReport | null>
  list(limit: number): Promise<StoredBugReport[]>
  /** The archive itself, wherever it was kept. */
  archive(id: string): Promise<Uint8Array | null>
  /** Deletes reports past the retention window with their archives; returns how many went. */
  collect(): Promise<number>
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
  const retention = deps.retention ?? DEFAULT_BUG_REPORT_RETENTION

  return {
    async submit(request) {
      const receivedAt = clock.now()
      const id = crypto.randomUUID()

      const stored = await file(id, receivedAt, request.state)

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

    async collect() {
      if (retention.maxAgeMs <= 0) return 0
      const before = new Date(clock.now().getTime() - retention.maxAgeMs)
      const keys = await repository.deleteBefore(before, retention.batchSize)
      // The rows are already gone, so an object that cannot be removed is an orphan to sweep later
      // rather than a report that came back. Deleting the row first is the right order: the other
      // way round leaves a row pointing at an object that is not there.
      for (const key of keys) await forget(key)
      if (keys.length > 0) {
        logger.info('bug report retention deleted reports', {
          deleted: keys.length,
          before: before.toISOString(),
        })
      }
      return keys.length
    },

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
  /**
   * Where the attached journal came to rest, if it came with one and the day had room for it.
   */
  async function file(
    id: string,
    receivedAt: Date,
    state: SubmitBugReportRequest['state'],
  ): Promise<{ state: StoredBugReport['state']; outcome: BugReportReceipt['stateStored'] }> {
    if (!state) return { state: null, outcome: 'not_sent' }
    if (!(await withinDailyBudget(receivedAt, state))) return { state: null, outcome: 'omitted' }
    return placeArchive(id, receivedAt, state)
  }

  /**
   * Whether the day still has room for this archive.
   *
   * Measured against what is already filed rather than against a counter, so it survives a restart
   * and is shared by every isolate of a Worker deployment. A report that does not fit keeps its
   * description and loses its journal, and the receipt says `omitted` so the player knows.
   */
  async function withinDailyBudget(
    receivedAt: Date,
    state: NonNullable<SubmitBugReportRequest['state']>,
  ): Promise<boolean> {
    if (retention.dailyStateBytes <= 0) return true
    const since = new Date(receivedAt.getTime() - 24 * 60 * 60 * 1000)
    const spent = await repository.bytesSince(since)
    // The declared encoded length is what is checked, before anything is decoded: deciding after the
    // decode would mean the budget is only applied to bytes already in memory.
    const incoming = Math.ceil((state.body.length * 3) / 4)
    if (spent + incoming <= retention.dailyStateBytes) return true
    logger.warn('bug report state dropped: the daily archive budget is spent', {
      spent,
      incoming,
      budget: retention.dailyStateBytes,
    })
    return false
  }

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
      const key = archiveKey(id, receivedAt, state.anonymized)
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
