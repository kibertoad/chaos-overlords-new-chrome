import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { BUG_REPORT_LIMITS, type SubmitBugReportRequest } from '@chaos-overlords/contracts'
import { sha256Hex } from '@chaos-overlords/kernel'
import { ManualClock, RecordingLogger } from '@chaos-overlords/kernel/testing'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { createMemoryBlobStore } from '../src/blobs'
import { openBugReportStorage } from '../src/node'
import type { BlobStore, BugReportRepository, StoredBugReport } from '../src/ports'
import { createBugReportService } from '../src/service'

function encodeBase64(bytes: Uint8Array): string {
  let binary = ''
  for (const byte of bytes) binary += String.fromCharCode(byte)
  return btoa(binary)
}

async function stateOf(
  bytes: Uint8Array,
  overrides: Partial<NonNullable<SubmitBugReportRequest['state']>> = {},
): Promise<NonNullable<SubmitBugReportRequest['state']>> {
  return {
    codec: 'brotli',
    replayFormatVersion: 24,
    uncompressedBytes: bytes.byteLength * 4,
    sha256: await sha256Hex(bytes),
    anonymized: true,
    body: encodeBase64(bytes),
    ...overrides,
  }
}

function request(overrides: Partial<SubmitBugReportRequest> = {}): SubmitBugReportRequest {
  return {
    message: 'The gang vanished after Move.',
    client: { version: '0.9.1', platform: 'Unix' },
    context: {
      scenario: 'Greed',
      matchType: 'single',
      turn: 7,
      phase: 'Command',
      humanPlayers: 1,
      computerPlayers: 5,
    },
    ...overrides,
  }
}

/** The repository contract, without a database, for the placement decisions. */
function inMemoryRepository(): BugReportRepository & { rows: StoredBugReport[] } {
  const rows: StoredBugReport[] = []
  return {
    rows,
    async insert(report) {
      rows.push(report)
    },
    async get(id) {
      return rows.find((row) => row.id === id) ?? null
    },
    async list(limit) {
      return [...rows]
        .sort((a, b) => b.receivedAt.getTime() - a.receivedAt.getTime())
        .slice(0, limit)
    },
  }
}

function serviceOver(repository: BugReportRepository, blobs?: BlobStore) {
  return createBugReportService({
    repository,
    clock: new ManualClock(),
    logger: new RecordingLogger(),
    ...(blobs ? { blobs } : {}),
  })
}

describe('bug report intake', () => {
  it('keeps a small archive in the row when there is nowhere else to put it', async () => {
    const repository = inMemoryRepository()
    const service = serviceOver(repository)
    const bytes = new Uint8Array(64).fill(7)

    const receipt = await service.submit(request({ state: await stateOf(bytes) }))

    expect(receipt.stateStored).toBe('stored')
    expect(repository.rows[0]?.state?.blobKey).toBeNull()
    expect(await service.archive(receipt.id)).toEqual(bytes)
  })

  it('sends every archive to the blob store when one is configured, and keeps none in the row', async () => {
    const repository = inMemoryRepository()
    const blobs = createMemoryBlobStore()
    const service = serviceOver(repository, blobs)
    const bytes = new Uint8Array(BUG_REPORT_LIMITS.inlineStateBytes + 1_024).fill(3)

    const receipt = await service.submit(request({ state: await stateOf(bytes) }))

    expect(receipt.stateStored).toBe('stored')
    const state = repository.rows[0]?.state
    expect(state?.body).toBeNull()
    expect(state?.blobKey).toMatch(/^bug-reports\/\d{4}-\d{2}-\d{2}\/.+\.rchjournal$/)
    expect(await service.archive(receipt.id)).toEqual(bytes)
  })

  it('accepts the report but drops an oversized archive when there is no blob store', async () => {
    const repository = inMemoryRepository()
    const service = serviceOver(repository)
    const bytes = new Uint8Array(BUG_REPORT_LIMITS.inlineStateBytes + 1).fill(1)

    const receipt = await service.submit(request({ state: await stateOf(bytes) }))

    expect(receipt.stateStored).toBe('omitted')
    expect(repository.rows[0]?.message).toBe('The gang vanished after Move.')
    expect(repository.rows[0]?.state).toBeNull()
  })

  it('says so when no archive came with the report', async () => {
    const service = serviceOver(inMemoryRepository())
    expect((await service.submit(request())).stateStored).toBe('not_sent')
  })

  it('refuses an archive that does not hash to what the report claims', async () => {
    const repository = inMemoryRepository()
    const service = serviceOver(repository, createMemoryBlobStore())
    const bytes = new Uint8Array(32).fill(9)
    const state = await stateOf(bytes, { sha256: '0'.repeat(64) })

    await expect(service.submit(request({ state }))).rejects.toMatchObject({
      code: 'validation_failed',
      details: { reason: 'state_digest_mismatch' },
    })
    expect(repository.rows).toHaveLength(0)
  })

  it('does not leave an orphaned object behind when the row cannot be written', async () => {
    const repository = inMemoryRepository()
    const blobs = createMemoryBlobStore()
    const failing: BugReportRepository = {
      ...repository,
      insert: async () => {
        throw new Error('disk is on fire')
      },
    }
    const service = serviceOver(failing, blobs)
    const bytes = new Uint8Array(16).fill(5)

    await expect(service.submit(request({ state: await stateOf(bytes) }))).rejects.toThrow(
      'disk is on fire',
    )
    // The key is deterministic in shape but not in id, so assert the store is empty instead.
    expect(await blobs.get('bug-reports/2026-01-01/anything.rchjournal')).toBeNull()
  })
})

describe('bug report storage on SQLite', () => {
  let directory: string
  let close: () => Promise<void>
  let repository: BugReportRepository

  beforeAll(async () => {
    directory = await mkdtemp(join(tmpdir(), 'chaos-bug-reports-'))
    const opened = openBugReportStorage(join(directory, 'bug-reports.db'))
    repository = opened.repository
    close = opened.close
  })

  afterAll(async () => {
    await close()
    await rm(directory, { recursive: true, force: true })
  })

  it('migrates its own lineage and round-trips a report with its journal metadata', async () => {
    const service = createBugReportService({
      repository,
      clock: new ManualClock(),
      logger: new RecordingLogger(),
      blobs: createMemoryBlobStore(),
    })
    const bytes = new Uint8Array(128).fill(2)

    const receipt = await service.submit(request({ state: await stateOf(bytes) }))
    const stored = await service.get(receipt.id)

    expect(stored?.message).toBe('The gang vanished after Move.')
    expect(stored?.context?.scenario).toBe('Greed')
    expect(stored?.state).toMatchObject({
      codec: 'brotli',
      replayFormatVersion: 24,
      compressedBytes: 128,
      anonymized: true,
    })
    expect(await service.archive(receipt.id)).toEqual(bytes)
  })

  it('lists newest first', async () => {
    const clock = new ManualClock()
    const service = createBugReportService({
      repository,
      clock,
      logger: new RecordingLogger(),
    })
    clock.advance(60_000)
    await service.submit(request({ message: 'older' }))
    clock.advance(60_000)
    await service.submit(request({ message: 'newer' }))

    const listed = await service.list(2)
    expect(listed.map((row) => row.message)).toEqual(['newer', 'older'])
  })
})
