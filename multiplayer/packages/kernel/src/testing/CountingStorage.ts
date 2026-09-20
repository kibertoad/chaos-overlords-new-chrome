import type { MultiplayerStorage } from '../ports/storage'

/**
 * Counts the storage calls a code path makes, so a hot path can be pinned to a number.
 *
 * Nothing else notices when a request or a sweep pass grows an extra read: the behaviour is
 * identical and only the cost moves, which is how a verdict came to page an entire event log and a
 * response to a megabyte snapshot came to be parsed three times. A test that asserts the count of a
 * seal, a submission or a sweep fails the moment somebody adds a query to it.
 *
 * It wraps any storage, so the same assertions hold against the in-memory reference and against a
 * real repository: the methods are the port's, and counting them is counting statements.
 */
export class CountingStorage {
  readonly counts = new Map<string, number>()
  readonly storage: MultiplayerStorage

  constructor(inner: MultiplayerStorage) {
    this.storage = Object.fromEntries(
      Object.entries(inner).map(([name, repository]) => [
        name,
        this.wrap(name, repository as Record<string, unknown>),
      ]),
    ) as unknown as MultiplayerStorage
  }

  /** Calls to one port method, e.g. `turns.getOrders`. */
  get(method: string): number {
    return this.counts.get(method) ?? 0
  }

  /** Every method called at least once, with its count, for a readable failure message. */
  snapshot(): Record<string, number> {
    return Object.fromEntries([...this.counts.entries()].sort(([a], [b]) => a.localeCompare(b)))
  }

  total(): number {
    return [...this.counts.values()].reduce((sum, count) => sum + count, 0)
  }

  reset(): void {
    this.counts.clear()
  }

  private wrap(repository: string, inner: Record<string, unknown>): Record<string, unknown> {
    const wrapped: Record<string, unknown> = {}
    for (const [method, value] of Object.entries(inner)) {
      if (typeof value !== 'function') {
        wrapped[method] = value
        continue
      }
      const key = `${repository}.${method}`
      wrapped[method] = (...args: unknown[]) => {
        this.counts.set(key, this.get(key) + 1)
        return (value as (...a: unknown[]) => unknown).apply(inner, args)
      }
    }
    return wrapped
  }
}
