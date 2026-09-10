import type {
  EventNotifier,
  EventRepository,
  EventStreamOpener,
  PersistedEvent,
} from '@chaos-overlords/kernel'
import { createSseResponse } from './createSseResponse'

const PAGE_SIZE = 200

/**
 * In-process fan-out: notifies every open stream of a match within this process. It is both the
 * notifier and the stream opener on Node, and the fan-out inside a Cloudflare Durable Object.
 */
export class LocalEventHub implements EventNotifier, EventStreamOpener {
  private readonly listeners = new Map<string, Set<() => void>>()

  constructor(
    private readonly events: EventRepository,
    private readonly heartbeatMs: number,
  ) {}

  async notify(event: PersistedEvent): Promise<void> {
    this.wake(event.matchId)
  }

  /** Wake every stream of a match; used when the notification arrives without the event body. */
  wake(matchId: string): void {
    for (const listener of this.listeners.get(matchId) ?? []) listener()
  }

  subscribe(matchId: string, wake: () => void): () => void {
    const set = this.listeners.get(matchId) ?? new Set()
    set.add(wake)
    this.listeners.set(matchId, set)
    return () => {
      set.delete(wake)
      if (set.size === 0) this.listeners.delete(matchId)
    }
  }

  connectionCount(matchId: string): number {
    return this.listeners.get(matchId)?.size ?? 0
  }

  async open(input: { matchId: string; afterSeq: number; signal: AbortSignal }): Promise<Response> {
    return createSseResponse(
      {
        listAfter: (afterSeq) => this.events.listAfter(input.matchId, afterSeq, PAGE_SIZE),
        subscribe: (wake) => this.subscribe(input.matchId, wake),
      },
      { afterSeq: input.afterSeq, heartbeatMs: this.heartbeatMs, signal: input.signal },
    )
  }
}
