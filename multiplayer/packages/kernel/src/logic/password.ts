import { randomBytes, toHex } from './crypto'

const ITERATIONS = 120_000
const encoder = new TextEncoder()

/** PBKDF2-SHA256 over WebCrypto, so the same code runs on Node and workerd. */
export async function hashPassword(password: string): Promise<string> {
  const salt = randomBytes(16)
  const derived = await derive(password, salt)
  return `pbkdf2$${ITERATIONS}$${toHex(salt)}$${toHex(derived)}`
}

export async function verifyPassword(password: string, stored: string): Promise<boolean> {
  const [scheme, iterationsText, saltHex, hashHex] = stored.split('$')
  if (scheme !== 'pbkdf2' || !iterationsText || !saltHex || !hashHex) return false
  const derived = await derive(password, fromHex(saltHex), Number(iterationsText))
  return constantTimeEqual(toHex(derived), hashHex)
}

async function derive(
  password: string,
  salt: Uint8Array,
  iterations = ITERATIONS,
): Promise<Uint8Array> {
  const key = await crypto.subtle.importKey('raw', encoder.encode(password), 'PBKDF2', false, [
    'deriveBits',
  ])
  const bits = await crypto.subtle.deriveBits(
    { name: 'PBKDF2', hash: 'SHA-256', salt: salt as BufferSource, iterations },
    key,
    256,
  )
  return new Uint8Array(bits)
}

function fromHex(hex: string): Uint8Array {
  const bytes = new Uint8Array(hex.length / 2)
  for (let i = 0; i < bytes.length; i += 1)
    bytes[i] = Number.parseInt(hex.slice(i * 2, i * 2 + 2), 16)
  return bytes
}

function constantTimeEqual(a: string, b: string): boolean {
  if (a.length !== b.length) return false
  let diff = 0
  for (let i = 0; i < a.length; i += 1) diff |= a.charCodeAt(i) ^ b.charCodeAt(i)
  return diff === 0
}
