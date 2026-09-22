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

type PathSegment = string | number

function canonicalize(value: unknown, path: PathSegment[] = []): unknown {
  if (value === null || typeof value === 'boolean' || typeof value === 'string') return value
  if (typeof value === 'number') {
    if (!Number.isSafeInteger(value) || Object.is(value, -0)) {
      throw new NonCanonicalValueError(
        describe(path),
        `${value} is not a safe non-negative-zero integer`,
      )
    }
    return value
  }
  if (Array.isArray(value)) return value.map((item, index) => descend(path, index, item))
  if (typeof value === 'object') {
    const source = value as Record<string, unknown>
    const sorted: Record<string, unknown> = {}
    for (const key of Object.keys(source).sort()) {
      sorted[key] = descend(path, key, source[key])
    }
    return sorted
  }
  throw new NonCanonicalValueError(describe(path), `unsupported type ${typeof value}`)
}

function descend(path: PathSegment[], segment: PathSegment, value: unknown): unknown {
  path.push(segment)
  try {
    return canonicalize(value, path)
  } finally {
    path.pop()
  }
}

/**
 * Renders the accumulated segments the way the C# mirror's `path` parameter spells them
 * (`CanonicalJson.WriteObject`/`WriteArray`): an index is always `[n]`, and a key is dotted unless
 * nothing has been rendered yet. The emptiness of what came before decides the dot, not the
 * segment's position -- an empty-string key renders as nothing, so `{ '': { y: 1.5 } }` reports
 * `y` rather than `.y`, matching both the mirror and the string-building version this replaced.
 *
 * Only a throw reaches here, so the allocation is off the hot path.
 */
function describe(path: readonly PathSegment[]): string {
  let rendered = ''
  for (const segment of path) {
    if (typeof segment === 'number') rendered += `[${segment}]`
    else rendered += rendered.length === 0 ? segment : `.${segment}`
  }
  return rendered
}
