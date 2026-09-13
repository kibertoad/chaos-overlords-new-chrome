import type { R2Bucket } from '@cloudflare/workers-types'
import type { BlobStore } from './ports'

/** What an archive is stored as, wherever it lands. */
export const ARCHIVE_CONTENT_TYPE = 'application/octet-stream'

/**
 * The key an archive is filed under.
 *
 * Sharded by date so a bucket listing stays browsable after a few thousand reports, and suffixed
 * with the report id, which is already a random UUID — nothing in the key is derived from the
 * report's contents, so a key cannot leak one.
 */
export function archiveKey(id: string, receivedAt: Date): string {
  const day = receivedAt.toISOString().slice(0, 10)
  return `bug-reports/${day}/${id}.rchjournal`
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
