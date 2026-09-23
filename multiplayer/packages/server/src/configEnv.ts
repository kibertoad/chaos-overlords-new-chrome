/**
 * Shared environment parsing for settings exposed by both server runtimes.
 *
 * Each parser takes the raw value as the runtime hands it over. On Node that is always a string; a
 * Worker's `[vars]` entry written without quotes arrives as a number or a boolean instead, so the
 * value is read through `String` rather than trusted to be one. `name`, when given, is put in front
 * of a refusal so the log says which variable to fix.
 */
type RawVar = string | number | boolean | undefined

function refuse(name: string | undefined, message: string): never {
  throw new Error(name ? `${name}: ${message}` : message)
}

/**
 * A whole number of at least `minimum`, or `fallback` when the variable is unset or empty.
 *
 * A rate limit of `0` is not "unlimited" and not "closed" — the limiter admits one call per window
 * and refuses the rest, which nobody means — so every budget passes a floor of one, and the refusal
 * names it rather than letting a misconfiguration run. Only decimal digits are read: `Number` would
 * otherwise take a blank value as `0` and `0x10` or `1e3` as numbers nobody typed.
 */
export function configInteger(raw: RawVar, fallback: number, minimum = 0, name?: string): number {
  if (raw === undefined || raw === '') return fallback
  const text = String(raw).trim()
  const value = /^\d+$/.test(text) ? Number(text) : Number.NaN
  if (!Number.isSafeInteger(value) || value < minimum) {
    refuse(name, `Expected an integer of at least ${minimum}, got "${String(raw)}"`)
  }
  return value
}

export function configFlag(raw: RawVar, fallback: boolean, name?: string): boolean {
  if (raw === undefined || raw === '') return fallback
  switch (String(raw).trim().toLowerCase()) {
    case 'true':
    case '1':
    case 'yes':
    case 'on':
      return true
    case 'false':
    case '0':
    case 'no':
    case 'off':
      return false
    default:
      return refuse(
        name,
        `Expected a boolean (true/false, 1/0, yes/no, on/off), got "${String(raw)}"`,
      )
  }
}

export function configList(raw: RawVar): string[] {
  return String(raw ?? '')
    .split(',')
    .map((entry) => entry.trim())
    .filter((entry) => entry.length > 0)
}
