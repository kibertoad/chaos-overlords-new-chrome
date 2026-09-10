/**
 * Unique-constraint detection, the one place a driver's error shape is interpreted.
 *
 * The ports promise that a write a unique index can refuse answers `false` rather than throwing, so
 * services retry (a join code collision, two appends racing for the same sequence number) without
 * knowing which database they are on. Every layer reports the condition differently and Drizzle
 * wraps the driver's error in its own, so the whole `cause` chain is inspected: node-postgres
 * carries SQLSTATE 23505, better-sqlite3 an `SQLITE_CONSTRAINT…` code, and D1 only a message.
 */
const UNIQUE_MESSAGES = [
  'UNIQUE constraint failed',
  'PRIMARY KEY must be unique',
  'duplicate key value violates unique constraint',
]
const POSTGRES_UNIQUE_VIOLATION = '23505'
const SQLITE_CONSTRAINT_PREFIX = 'SQLITE_CONSTRAINT'
const MAX_CAUSE_DEPTH = 5

/** Attempts an append makes at claiming the next sequence number before giving up. */
export const APPEND_ATTEMPTS = 8

export function isUniqueViolation(error: unknown): boolean {
  let current: unknown = error
  for (let depth = 0; depth < MAX_CAUSE_DEPTH; depth += 1) {
    if (typeof current !== 'object' || current === null) return false
    const { code, message } = current as { code?: unknown; message?: unknown }
    if (code === POSTGRES_UNIQUE_VIOLATION) return true
    if (typeof code === 'string' && code.startsWith(SQLITE_CONSTRAINT_PREFIX)) return true
    if (typeof message === 'string' && UNIQUE_MESSAGES.some((needle) => message.includes(needle))) {
      return true
    }
    current = (current as { cause?: unknown }).cause
  }
  return false
}

/** Runs `write`, translating a unique-constraint refusal into `false`. */
export async function insertUnlessTaken(write: () => Promise<unknown>): Promise<boolean> {
  try {
    await write()
    return true
  } catch (error) {
    if (isUniqueViolation(error)) return false
    throw error
  }
}
