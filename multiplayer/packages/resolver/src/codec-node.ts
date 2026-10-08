import { brotliCompressSync, brotliDecompressSync, constants } from 'node:zlib'
import type { BrotliCodec } from './archive.js'

const DECOMPRESS_SLACK_BYTES = 64 * 1024

/**
 * Brotli from `node:zlib`: in Node, and in a Worker with the `nodejs_compat` flag (the coordination
 * Worker has it; the resolver Worker must not, see `worker/index.js`).
 *
 * Quality 5 rather than zlib's default 11: a payload is up to 16 MiB of JSON, 11 takes seconds on
 * that, and the archive's readers do not care which quality wrote it.
 */
export const nodeBrotliCodec: BrotliCodec = {
  compress: (bytes) =>
    plain(
      brotliCompressSync(bytes, {
        params: {
          [constants.BROTLI_PARAM_QUALITY]: 5,
          [constants.BROTLI_PARAM_SIZE_HINT]: bytes.length,
        },
      }),
    ),
  // workerd's `node:zlib` refuses ("Memory limit exceeded") a limit that the output reaches inside
  // its last 64 KiB chunk, so the limit carries one chunk of slack; `readSnapshotArchive` checks the
  // exact length.
  decompress: (bytes, maxBytes) =>
    plain(brotliDecompressSync(bytes, { maxOutputLength: maxBytes + DECOMPRESS_SLACK_BYTES })),
}

/** The bytes of a `Buffer` as a plain `Uint8Array`, which every host's structured clone carries. */
function plain(buffer: Buffer): Uint8Array {
  return new Uint8Array(buffer.buffer, buffer.byteOffset, buffer.byteLength)
}
