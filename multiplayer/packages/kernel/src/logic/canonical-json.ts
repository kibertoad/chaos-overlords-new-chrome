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
  return canonicalize(value)
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

/**
 * Writes the text directly, in sorted order, rather than building a sorted object and handing it to
 * `JSON.stringify`. An object does not keep the order its keys were inserted in: every key that
 * reads as an array index (`"9"`, `"10"`) is enumerated first and numerically, so `{"10","9"}` came
 * out as `9, 10` where the code-unit order — and the C# mirror's `StringComparer.Ordinal` — is
 * `10, 9`. The two sides then hashed different text for the same document. Building the string also
 * keeps a `__proto__` key a key rather than a prototype assignment.
 */
function canonicalize(value: unknown, path: PathSegment[] = []): string {
  if (value === null) return 'null'
  if (typeof value === 'boolean') return value ? 'true' : 'false'
  if (typeof value === 'string') return JSON.stringify(value)
  if (typeof value === 'number') {
    if (!Number.isSafeInteger(value) || Object.is(value, -0)) {
      throw new NonCanonicalValueError(
        describe(path),
        `${value} is not a safe non-negative-zero integer`,
      )
    }
    return String(value)
  }
  if (Array.isArray(value)) {
    const items: string[] = []
    for (let index = 0; index < value.length; index += 1) {
      items.push(descend(path, index, value[index]))
    }
    return `[${items.join(',')}]`
  }
  if (typeof value === 'object') {
    const source = value as Record<string, unknown>
    // `sort()` with no comparator orders by UTF-16 code unit, which is `StringComparer.Ordinal`.
    const members = Object.keys(source)
      .sort()
      .map((key) => `${JSON.stringify(key)}:${descend(path, key, source[key])}`)
    return `{${members.join(',')}}`
  }
  throw new NonCanonicalValueError(describe(path), `unsupported type ${typeof value}`)
}

function descend(path: PathSegment[], segment: PathSegment, value: unknown): string {
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
