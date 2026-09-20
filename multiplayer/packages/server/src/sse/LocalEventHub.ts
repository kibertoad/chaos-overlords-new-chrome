import { LIMITS } from '@chaos-overlords/contracts'
import {
  type EventNotifier,
  type EventRepository,
  type EventStreamOpener,
  type PersistedEvent,
  RateLimitedError,
  type StreamCloser,
} from '@chaos-overlords/kernel'
import { createSseResponse } from './createSseResponse'

const PAGE_SIZE = 200

/**
 * How many streams may be open at once, and against what.
 *
 * A stream is not a request: it lives until the client closes it, it holds a heartbeat timer and a
 * wake listener, and every event or heartbeat costs it one `listAfter` query. The
 * per-player rate limit counts the opening call and nothing after it, so without these one
 * authenticated member could hold thousands of streams and turn each published event into thousands
 * of database reads and thousands of response writes — the whole match stops sealing for everybody.
 *
 * `perPlayer` is generous enough for the one case a healthy client produces: a reconnect whose
 * predecessor is still being torn down. Reaching it closes that player's oldest stream rather than
 * refusing the new one, because the new one is the live client and the old one is the corpse.
 * `perMatch` and `perProcess` are the ceilings a client cannot talk its way past, and they refuse.
 */
export interface EventHubLimits {
  perPlayer: number
  perMatch: number
  perProcess: number
}

export const DEFAULT_EVENT_HUB_LIMITS: EventHubLimits = {
  perPlayer: 3,
  perMatch: LIMITS.maxPlayers * 3,
  perProcess: 512,
}

interface Subscription {
  playerId: string
  wake: () => void
  close: () => void
}

/**
 * In-process fan-out: notifies every open stream of a match within this process. It is both the
 * notifier and the stream opener on Node, and the fan-out inside a Cloudflare Durable Object.
 */
export class LocalEventHub implements EventNotifier, EventStreamOpener, StreamCloser {
  /** Insertion-ordered, which is what makes "close this player's oldest" a first match. */
  private readonly listeners = new Map<string, Set<Subscription>>()
  private open_ = 0

  constructor(
    private readonly events: EventRepository,
    private readonly heartbeatMs: number,
    private readonly limits: EventHubLimits = DEFAULT_EVENT_HUB_LIMITS,
  ) {}

  async notify(event: PersistedEvent): Promise<void> {
    this.wake(event.matchId)
  }

  /** Wake every stream of a match; used when the notification arrives without the event body. */
  wake(matchId: string): void {
    // Snapshot deliberately: waking a stream can close it, which mutates the set being walked.
    // oxlint-disable-next-line unicorn/no-useless-spread
    for (const subscription of [...(this.listeners.get(matchId) ?? [])]) subscription.wake()
  }

  /**
   * End every stream a membership holds.
   *
   * Revoking a token stops the next request; a stream that is already open is never authenticated
   * again, so without this a kicked player keeps being handed every sealed set, every desync report
   * (which names each player's state hash) and every host change until they choose to disconnect.
   */
  async close(input: { matchId: string; playerId: string }): Promise<void> {
    // Snapshot deliberately: `close()` removes the subscription from the set being walked.
    // oxlint-disable-next-line unicorn/no-useless-spread
    for (const subscription of [...(this.listeners.get(input.matchId) ?? [])]) {
      if (subscription.playerId === input.playerId) subscription.close()
    }
  }

  connectionCount(matchId: string): number {
    return this.listeners.get(matchId)?.size ?? 0
  }

  /** Streams open across every match in this process. */
  get openStreams(): number {
    return this.open_
  }

  async open(input: {
    matchId: string
    playerId: string
    afterSeq: number
    signal: AbortSignal
  }): Promise<Response> {
    this.makeRoom(input.matchId, input.playerId)
    return createSseResponse(
      {
        listAfter: (afterSeq) => this.events.listAfter(input.matchId, afterSeq, PAGE_SIZE),
        subscribe: (wake, close) => this.subscribe(input.matchId, input.playerId, wake, close),
      },
      { afterSeq: input.afterSeq, heartbeatMs: this.heartbeatMs, signal: input.signal },
    )
  }

  private subscribe(
    matchId: string,
    playerId: string,
    wake: () => void,
    close: () => void,
  ): () => void {
    const set = this.listeners.get(matchId) ?? new Set<Subscription>()
    const subscription: Subscription = { playerId, wake, close }
    set.add(subscription)
    this.listeners.set(matchId, set)
    this.open_ += 1
    return () => {
      if (!set.delete(subscription)) return
      this.open_ -= 1
      if (set.size === 0) this.listeners.delete(matchId)
    }
  }

  /**
   * Enforce the three caps before a stream is built.
   *
   * The process cap is checked first: it is the one that protects everyone else's matches, and a
   * refusal is cheaper than closing somebody's live stream to make room for a caller this cap is
   * about to turn away anyway. The caller's own stale streams go next, BEFORE the match cap is
   * read: a full match is exactly the state a reconnecting player finds when every seat holds its
   * quota, and refusing them there would turn the one legitimate reconnect into a 429.
   */
  private makeRoom(matchId: string, playerId: string): void {
    if (this.open_ >= this.limits.perProcess) {
      throw new RateLimitedError('This server is holding as many event streams as it can', {
        reason: 'too_many_streams',
        scope: 'process',
      })
    }
    const set = this.listeners.get(matchId)
    if (set) {
      const mine = [...set].filter((subscription) => subscription.playerId === playerId)
      for (const stale of mine.slice(0, mine.length - this.limits.perPlayer + 1)) stale.close()
    }
    if ((this.listeners.get(matchId)?.size ?? 0) >= this.limits.perMatch) {
      throw new RateLimitedError('This match is holding as many event streams as it can', {
        reason: 'too_many_streams',
        scope: 'match',
      })
    }
  }
}
