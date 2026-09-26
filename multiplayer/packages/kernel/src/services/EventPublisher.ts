import type { MatchEventBody } from '@chaos-overlords/contracts'
import type { PersistedEvent } from '../domain/entities'
import type { KernelDeps } from './deps'

/**
 * Appends an event to a match's log and fans it out.
 *
 * The sequence number is allocated by the insert itself, so the log has no gaps: a client (or a
 * stream cursor) that holds `seq` is guaranteed to have been offered every event below it. That is
 * the property a separate counter could not give — a crash or a reorder between "take a number" and
 * "write the row" would publish a hole that a monotonic cursor then skips forever.
 *
 * Fan-out is only a wake-up hint. It happens after the event is durable and its failure is logged,
 * never propagated: the stream's heartbeat catch-up (or its next ordinary drain) delivers the
 * event anyway.
 */
export class EventPublisher {
  constructor(
    private readonly deps: Pick<KernelDeps, 'storage' | 'notifier' | 'clock' | 'logger'>,
  ) {}

  async publish(matchId: string, body: MatchEventBody): Promise<PersistedEvent> {
    const event = await this.deps.storage.events.append({
      ...body,
      matchId,
      createdAt: this.deps.clock.now().toISOString(),
    })
    await this.fanOut(matchId, event)
    return event
  }

  /**
   * `publish`, at most once per `key` for the match; null when the log already carries it.
   *
   * For an announcement that follows a compare-and-swap. The winner of the swap used to be the only
   * caller that ever published, so an append that threw after the swap lost the event for good and
   * every client waited on it forever. Keyed, the announcement can be repeated by anyone who finds
   * the state change unannounced, and racing repeats still log it exactly once. A repeat that finds
   * the event already there wakes nobody: whoever logged it did.
   */
  async publishOnce(
    matchId: string,
    key: string,
    body: MatchEventBody,
  ): Promise<PersistedEvent | null> {
    const event = await this.deps.storage.events.appendOnce(
      { ...body, matchId, createdAt: this.deps.clock.now().toISOString() },
      key,
    )
    if (event) await this.fanOut(matchId, event)
    return event
  }

  private async fanOut(matchId: string, event: PersistedEvent): Promise<void> {
    try {
      await this.deps.notifier.notify(event)
    } catch (error) {
      // The event is durable; a lost notification is recovered by the stream's next catch-up read.
      this.deps.logger.warn('event notification failed', {
        matchId,
        seq: event.seq,
        error: String(error),
      })
    }
  }
}
