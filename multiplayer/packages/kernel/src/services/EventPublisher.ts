import type { MatchEventBody } from '@chaos-overlords/contracts'
import type { PersistedEvent } from '../domain/entities'
import type { KernelDeps } from './deps'

/**
 * Appends an event to a match's log and fans it out. The sequence number comes from an atomic
 * counter on the match row, so two concurrent publishers never collide; a crash between the
 * counter and the insert leaves a harmless gap.
 */
export class EventPublisher {
  constructor(
    private readonly deps: Pick<KernelDeps, 'storage' | 'notifier' | 'clock' | 'logger'>,
  ) {}

  async publish(matchId: string, body: MatchEventBody): Promise<PersistedEvent> {
    const seq = await this.deps.storage.matches.allocateEventSeq(matchId)
    const event: PersistedEvent = {
      ...body,
      seq,
      matchId,
      createdAt: this.deps.clock.now().toISOString(),
    }
    await this.deps.storage.events.append(event)
    try {
      await this.deps.notifier.notify(event)
    } catch (error) {
      // The event is durable; a lost notification is recovered by the stream's next catch-up read.
      this.deps.logger.warn('event notification failed', { matchId, seq, error: String(error) })
    }
    return event
  }
}
