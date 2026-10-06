import { describe, expect, it } from 'vitest'
import {
  fromBase64,
  MAXIMUM_PAYLOAD_BYTES,
  readSnapshotArchive,
  toBase64,
  writeSnapshotArchive,
} from '../src/archive.js'
import { nodeBrotliCodec } from '../src/codec-node.js'

const payload = new TextEncoder().encode(
  JSON.stringify({ turn: 12, cells: Array(400).fill('gang') }),
)

function header(version: number, declared: number): Uint8Array {
  const bytes = new Uint8Array(12)
  bytes.set([0x52, 0x43, 0x48, 0x53])
  new DataView(bytes.buffer).setInt32(4, version, true)
  new DataView(bytes.buffer).setInt32(8, declared, true)
  return bytes
}

function concat(...parts: Uint8Array[]): Uint8Array {
  const bytes = new Uint8Array(parts.reduce((sum, part) => sum + part.length, 0))
  let offset = 0
  for (const part of parts) {
    bytes.set(part, offset)
    offset += part.length
  }
  return bytes
}

describe('the snapshot archive', () => {
  it('writes the RCHS header around a Brotli payload and reads it back', () => {
    const body = writeSnapshotArchive(payload, nodeBrotliCodec)
    const bytes = fromBase64(body)
    expect([...bytes.subarray(0, 4)]).toEqual([0x52, 0x43, 0x48, 0x53])
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength)
    expect(view.getInt32(4, true)).toBe(1)
    expect(view.getInt32(8, true)).toBe(payload.length)
    expect(bytes.length).toBeLessThan(payload.length)
    expect(readSnapshotArchive(body, nodeBrotliCodec)).toEqual(payload)
  })

  it('reads a body without the header as a bare payload, as older clients uploaded it', () => {
    expect(readSnapshotArchive(toBase64(payload), nodeBrotliCodec)).toEqual(payload)
  })

  it('refuses an archive version it does not read', () => {
    const body = toBase64(concat(header(2, payload.length), nodeBrotliCodec.compress(payload)))
    expect(() => readSnapshotArchive(body, nodeBrotliCodec)).toThrow(/version 2/)
  })

  it('refuses a declared size beyond the save limit before decompressing', () => {
    const body = toBase64(concat(header(1, MAXIMUM_PAYLOAD_BYTES + 1), new Uint8Array(4)))
    expect(() => readSnapshotArchive(body, nodeBrotliCodec)).toThrow(/unusable payload size/)
  })

  it('refuses a payload that expands to another size than it declares', () => {
    const body = toBase64(concat(header(1, payload.length - 1), nodeBrotliCodec.compress(payload)))
    expect(() => readSnapshotArchive(body, nodeBrotliCodec)).toThrow(/declared size/)
  })

  it('refuses a truncated header', () => {
    const body = toBase64(header(1, 10).subarray(0, 8))
    expect(() => readSnapshotArchive(body, nodeBrotliCodec)).toThrow(/truncated/)
  })

  it('refuses to write a payload beyond the save limit', () => {
    expect(() =>
      writeSnapshotArchive(new Uint8Array(MAXIMUM_PAYLOAD_BYTES + 1), nodeBrotliCodec),
    ).toThrow(/size limit/)
  })

  it('reads a payload of exactly the save limit, which needs the decompression slack in workerd', () => {
    const large = new Uint8Array(MAXIMUM_PAYLOAD_BYTES).fill(0x20)
    const body = writeSnapshotArchive(large, nodeBrotliCodec)
    expect(readSnapshotArchive(body, nodeBrotliCodec).length).toBe(MAXIMUM_PAYLOAD_BYTES)
  })
})
