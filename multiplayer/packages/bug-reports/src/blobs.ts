import type { R2Bucket } from '@cloudflare/workers-types'
import type { BlobStore } from './ports'

/** What an archive is stored as, wherever it lands. */
export const ARCHIVE_CONTENT_TYPE = 'application/octet-stream'

/**
 * The prefix an archive the client did not scrub is filed under.
 *
 * `anonymized` is whatever the client said, and the client is the one piece of this path nobody
 * operates. A journal that was not scrubbed carries other players' names and their Comlink text, so
 * it is kept apart rather than turned away: the report is still worth having, and the person
 * triaging it is entitled to know what they are about to open before they open it. Triage tooling
 * filters on the prefix; nothing else treats the two differently.
 */
export const UNSCRUBBED_ARCHIVE_PREFIX = 'bug-reports-unscrubbed'

export const ARCHIVE_PREFIX = 'bug-reports'

/**
 * The key an archive is filed under.
 *
 * Sharded by date so a bucket listing stays browsable after a few thousand reports, and suffixed
 * with the report id, which is already a random UUID — nothing in the key is derived from the
 * report's contents, so a key cannot leak one.
 */
export function archiveKey(id: string, receivedAt: Date, anonymized = true): string {
  const day = receivedAt.toISOString().slice(0, 10)
  const prefix = anonymized ? ARCHIVE_PREFIX : UNSCRUBBED_ARCHIVE_PREFIX
  return `${prefix}/${day}/${id}.rchjournal`
}

/** Cloudflare R2, which is where a public deployment puts the archives. */
export function createR2BlobStore(bucket: R2Bucket): BlobStore {
  return {
    async put(key, bytes, contentType) {
      await bucket.put(key, bytes, { httpMetadata: { contentType } })
    },
    async get(key) {
      const object = await bucket.get(key)
      if (!object) return null
      return new Uint8Array(await object.arrayBuffer())
    },
    async delete(key) {
      await bucket.delete(key)
    },
  }
}

/** An in-process store, for tests and for a `sqlite::memory:` development server. */
export function createMemoryBlobStore(): BlobStore {
  const objects = new Map<string, Uint8Array>()
  return {
    async put(key, bytes) {
      objects.set(key, bytes.slice())
    },
    async get(key) {
      return objects.get(key) ?? null
    },
    async delete(key) {
      objects.delete(key)
    },
  }
}
