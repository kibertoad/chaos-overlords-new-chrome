/**
 * The snapshot archive clients upload and read: a 12-byte header (`RCHS`, the archive version and
 * the payload's length, both little-endian 32-bit) and the native save payload compressed with
 * Brotli. `MatchStateClone` in `src/Rechaos.Multiplayer` writes and reads the same bytes.
 *
 * The browser-wasm runtime has no Brotli codec, so the resolver hands out and takes the bare payload
 * and its host puts the archive around it with a codec of its own.
 */

/** Brotli, as the host has it. */
export interface BrotliCodec {
  compress(bytes: Uint8Array): Uint8Array
  /** Refuses output much beyond `maxBytes`; the caller checks the exact length. */
  decompress(bytes: Uint8Array, maxBytes: number): Uint8Array
}

const MAGIC = [0x52, 0x43, 0x48, 0x53] // "RCHS"
const HEADER_BYTES = 12
const ARCHIVE_VERSION = 1
/** `NativeSaveSerializer.MaximumSaveBytes`: no payload the game reads is larger. */
export const MAXIMUM_PAYLOAD_BYTES = 16 * 1024 * 1024

/** The archive a client uploads, base64-encoded, around a native save payload. */
export function writeSnapshotArchive(payload: Uint8Array, codec: BrotliCodec): string {
  if (payload.length > MAXIMUM_PAYLOAD_BYTES) {
    throw new Error('the snapshot exceeds the save size limit')
  }
  const compressed = codec.compress(payload)
  const archive = new Uint8Array(HEADER_BYTES + compressed.length)
  archive.set(MAGIC, 0)
  const view = new DataView(archive.buffer)
  view.setInt32(4, ARCHIVE_VERSION, true)
  view.setInt32(8, payload.length, true)
  archive.set(compressed, HEADER_BYTES)
  return toBase64(archive)
}

/**
 * The native save payload inside a snapshot body. A body without the archive header is an
 * uncompressed payload, which older clients uploaded, as `MatchStateClone.FromBase64` reads it.
 */
export function readSnapshotArchive(body: string, codec: BrotliCodec): Uint8Array {
  const bytes = fromBase64(body)
  if (!MAGIC.every((byte, index) => bytes[index] === byte)) {
    if (bytes.length > MAXIMUM_PAYLOAD_BYTES) {
      throw new Error('the snapshot exceeds the save size limit')
    }
    return bytes
  }
  if (bytes.length < HEADER_BYTES) throw new Error('the snapshot archive is truncated')
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength)
  const version = view.getInt32(4, true)
  if (version !== ARCHIVE_VERSION) {
    throw new Error(`the snapshot archive has version ${version}, which this host does not read`)
  }
  const declared = view.getInt32(8, true)
  if (declared < 0 || declared > MAXIMUM_PAYLOAD_BYTES) {
    throw new Error('the snapshot archive declares an unusable payload size')
  }
  const payload = codec.decompress(bytes.subarray(HEADER_BYTES), declared)
  if (payload.length !== declared) {
    throw new Error('the snapshot archive does not expand to its declared size')
  }
  return payload
}

interface BufferLike {
  from(
    bytes: Uint8Array | string,
    encoding?: string,
  ): Uint8Array & { toString(encoding: string): string }
}

const NodeBuffer = (globalThis as { Buffer?: BufferLike }).Buffer

export function toBase64(bytes: Uint8Array): string {
  if (NodeBuffer) return NodeBuffer.from(bytes).toString('base64')
  let text = ''
  for (let index = 0; index < bytes.length; index += 0x8000) {
    text += String.fromCharCode(...bytes.subarray(index, index + 0x8000))
  }
  return btoa(text)
}

export function fromBase64(text: string): Uint8Array {
  if (NodeBuffer) {
    const buffer = NodeBuffer.from(text, 'base64')
    return new Uint8Array(buffer.buffer, buffer.byteOffset, buffer.byteLength)
  }
  const binary = atob(text)
  const bytes = new Uint8Array(binary.length)
  for (let index = 0; index < binary.length; index++) bytes[index] = binary.charCodeAt(index)
  return bytes
}
