import {
  check,
  type InferOutput,
  integer,
  maxValue,
  minValue,
  number,
  picklist,
  pipe,
  record,
  strictObject,
  string,
  unknown,
} from 'valibot'
import { LIMITS } from './limits'
import { matchNameSchema } from './primitives'

/**
 * What the host chose when opening the lobby: the few knobs the server reads, plus the opaque blob
 * it only stores.
 */

/**
 * Settings the server stores verbatim for clients (scenario, portraits, difficulty). It never
 * looks inside, so the only rules are the two that protect it from being a weapon: how big it may
 * be, and how deep.
 *
 * The measurement is one iterative walk rather than a recursive schema. A recursive schema is the
 * obvious way to bound depth and the wrong one: the recursion overflows the stack on a body well
 * inside the size limit, and a `RangeError` is not a validation issue, so the caller gets a 500
 * where a 422 is the truth.
 */
export const gameSettingsSchema = pipe(
  record(string(), unknown()),
  // Size before shape. Both checks walk the whole blob, and the cheaper question to answer about an
  // oversized one is that it is oversized: measuring its depth first would mean doing the work on a
  // body that was never going to be accepted.
  check(withinBytes, `gameSettings exceeds ${LIMITS.gameSettingsBytes} bytes`),
  check(withinDepth, `gameSettings nests deeper than ${LIMITS.gameSettingsMaxDepth}`),
)

export const matchVisibilitySchema = picklist(['public', 'private'])

export const matchSettingsSchema = strictObject({
  name: matchNameSchema,
  maxPlayers: pipe(number(), integer(), minValue(2), maxValue(6)),
  /**
   * Seconds, or 0 for no timer. One piped number rather than a union of `literal(0)` and a range:
   * a union of a literal with a number has no C# type that holds both, so the generator falls back
   * to `JsonElement` and the client loses the field it has to render a countdown from.
   */
  turnTimerSeconds: pipe(
    number(),
    integer(),
    minValue(0),
    maxValue(86_400),
    check(
      (seconds) => seconds === 0 || seconds >= LIMITS.turnTimerMinSeconds,
      `a turn timer is 0 or at least ${LIMITS.turnTimerMinSeconds} seconds`,
    ),
  ),
  visibility: matchVisibilitySchema,
  gameSettings: gameSettingsSchema,
})

export type MatchSettings = InferOutput<typeof matchSettingsSchema>
export type MatchVisibility = InferOutput<typeof matchVisibilitySchema>
export type GameSettings = InferOutput<typeof gameSettingsSchema>

/**
 * Depth measured with an explicit stack, so a 20,000-deep array is refused rather than crashing
 * the process that refuses it.
 */
function withinDepth(value: Record<string, unknown>): boolean {
  const pending: Array<{ node: unknown; depth: number }> = [{ node: value, depth: 0 }]
  while (pending.length > 0) {
    const { node, depth } = pending.pop() as { node: unknown; depth: number }
    if (node === null || typeof node !== 'object') continue
    if (depth >= LIMITS.gameSettingsMaxDepth) return false
    for (const child of Array.isArray(node) ? node : Object.values(node)) {
      pending.push({ node: child, depth: depth + 1 })
    }
  }
  return true
}

/**
 * UTF-8 length of the serialized blob without a `TextEncoder`: this package is deliberately
 * environment-free, imported by the server, a Worker and a browser alike. Length in code units
 * would let three times the cap through for a body of CJK text, and the D1 row budget is in bytes.
 */
function withinBytes(value: Record<string, unknown>): boolean {
  let json: string
  try {
    json = JSON.stringify(value)
  } catch {
    // Nesting deep enough to overflow `JSON.stringify` is over budget by any measure, and the depth
    // check reports it in its own words. Both checks run: valibot finishes the pipe.
    return false
  }
  let bytes = 0
  for (const character of json) {
    const code = character.codePointAt(0) ?? 0
    bytes += code < 0x80 ? 1 : code < 0x800 ? 2 : code < 0x10000 ? 3 : 4
    if (bytes > LIMITS.gameSettingsBytes) return false
  }
  return true
}
