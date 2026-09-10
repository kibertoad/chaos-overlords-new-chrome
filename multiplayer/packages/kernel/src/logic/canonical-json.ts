/**
 * Deterministic JSON, the form every order digest is taken over: object keys sorted by UTF-16 code
 * unit (JavaScript's default string order, .NET's `StringComparer.Ordinal`), no whitespace, and
 * numbers restricted to safe integers.
 *
 * The restriction is the point: a client written in another language has to reproduce this text
 * byte for byte to verify `ordersHash`, and floating-point shortest round-trip text is not portable
 * (`1e+21` here, `1E+21` in .NET). Integers, booleans, null and JSON-escaped strings are. Anything
 * outside that is a programming error, not a runtime condition, so it throws rather than hashing
 * something a peer cannot reproduce.
 */
export function canonicalJson(value: unknown): string {
  return JSON.stringify(canonicalize(value))
}

export class NonCanonicalValueError extends Error {
  constructor(
    readonly path: string,
    detail: string,
  ) {
    super(`cannot canonicalize ${path || 'value'}: ${detail}`)
    this.name = 'NonCanonicalValueError'
  }
}

function canonicalize(value: unknown, path = ''): unknown {
  if (value === null || typeof value === 'boolean' || typeof value === 'string') return value
  if (typeof value === 'number') {
    if (!Number.isSafeInteger(value) || Object.is(value, -0)) {
      throw new NonCanonicalValueError(path, `${value} is not a safe non-negative-zero integer`)
    }
    return value
  }
  if (Array.isArray(value))
    return value.map((item, index) => canonicalize(item, `${path}[${index}]`))
  if (typeof value === 'object') {
    const source = value as Record<string, unknown>
    const sorted: Record<string, unknown> = {}
    for (const key of Object.keys(source).sort()) {
      sorted[key] = canonicalize(source[key], path ? `${path}.${key}` : key)
    }
    return sorted
  }
  throw new NonCanonicalValueError(path, `unsupported type ${typeof value}`)
}
