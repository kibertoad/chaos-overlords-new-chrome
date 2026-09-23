/** Shared environment parsing for settings exposed by both server runtimes. */
export function configInteger(raw: string | undefined, fallback: number, minimum = 0): number {
  if (raw === undefined || raw === '') return fallback
  const value = Number(raw)
  if (!Number.isInteger(value) || value < minimum) {
    throw new Error(`Expected an integer of at least ${minimum}, got "${raw}"`)
  }
  return value
}

export function configFlag(raw: string | undefined, fallback: boolean): boolean {
  if (raw === undefined || raw === '') return fallback
  switch (raw.trim().toLowerCase()) {
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
      throw new Error(`Expected a boolean (true/false, 1/0, yes/no, on/off), got "${raw}"`)
  }
}

export function configList(raw: string | undefined): string[] {
  return (raw ?? '')
    .split(',')
    .map((entry) => entry.trim())
    .filter((entry) => entry.length > 0)
}
