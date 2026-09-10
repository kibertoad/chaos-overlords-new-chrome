import { canonicalJson } from './canonical-json'

const encoder = new TextEncoder()

export async function sha256Hex(input: string | Uint8Array): Promise<string> {
  const bytes = typeof input === 'string' ? encoder.encode(input) : input
  const digest = await crypto.subtle.digest('SHA-256', bytes as BufferSource)
  return toHex(new Uint8Array(digest))
}

export function toHex(bytes: Uint8Array): string {
  let out = ''
  for (const byte of bytes) out += byte.toString(16).padStart(2, '0')
  return out
}

export function randomBytes(length: number): Uint8Array {
  const bytes = new Uint8Array(length)
  crypto.getRandomValues(bytes)
  return bytes
}

function toBase64Url(bytes: Uint8Array): string {
  let binary = ''
  for (const byte of bytes) binary += String.fromCharCode(byte)
  return btoa(binary).replaceAll('+', '-').replaceAll('/', '_').replace(/=+$/, '')
}

/** A bearer token: 256 random bits, prefixed so a leaked one is recognisable in logs. */
export const TOKEN_PREFIX = 'cop_'

export function generateToken(): string {
  return `${TOKEN_PREFIX}${toBase64Url(randomBytes(32))}`
}

export function hashToken(token: string): Promise<string> {
  return sha256Hex(token)
}

/** Crockford-ish alphabet without the glyphs players misread over voice chat (0/O, 1/I/L). */
const JOIN_CODE_ALPHABET = 'ABCDEFGHJKMNPQRSTUVWXYZ23456789'

export function generateJoinCode(length: number): string {
  const bytes = randomBytes(length)
  let code = ''
  for (const byte of bytes) code += JOIN_CODE_ALPHABET[byte % JOIN_CODE_ALPHABET.length]
  return code
}

/** A 32-bit seed for the deterministic game core. */
export function generateSeed(): number {
  const [value] = new Uint32Array(randomBytes(4).buffer)
  return value ?? 0
}

export function hashOrderDocument(orders: unknown): Promise<string> {
  return sha256Hex(canonicalJson(orders))
}

/** The digest every client can recompute from the sealed set it fetched, to verify integrity. */
export function hashOrderSet(
  entries: ReadonlyArray<{ slot: number; ordersHash: string }>,
): Promise<string> {
  const lines = [...entries]
    .sort((a, b) => a.slot - b.slot)
    .map((entry) => `${entry.slot}:${entry.ordersHash}`)
  return sha256Hex(lines.join('\n'))
}
