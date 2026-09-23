import { LIMITS } from '@chaos-overlords/contracts'
import {
  type EventNotifier,
  type EventRepository,
  type EventStreamOpener,
  type PersistedEvent,
  RateLimitedError,
  type StreamCloser,
} from '@chaos-overlords/kernel'
import { abandonedSseResponse, createSseResponse } from './createSseResponse'
import { MatchLog } from './MatchLog'

/**
 * How many streams may be open at once, and against what.
 *
 * A stream is not a request: it lives until the client closes it, it holds a heartbeat timer and a
 * wake listener, and it is written to for every event of its match. The per-player rate limit counts
 * the opening call and nothing after it, so without these one authenticated member could hold
 * thousands of streams and turn each published event into thousands of response writes — the whole
 * match stops sealing for everybody. (What one event costs in READS is `MatchLog`'s business, and
 * the answer there is "once for the match", not once per stream.)
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

/** What the hub tells its runtime about, so an operator can find out it happened. */
export interface EventHubObserver {
  /** A stored event a stream had to skip because this build cannot read it. */
  unreadable?(matchId: string, seq: number): void
  /**
   * A stream request whose signal had already aborted by the time it reached the hub, answered
   * with an empty stream. Routine for a client that gave up during authentication; a steady run of
   * them for one player means the runtime is handing over signals that only look aborted, and
   * that client is reconnecting in a loop.
   */
  abandoned?(matchId: string, playerId: string): void
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
  /**
   * The shared read and shared frames of each match with a stream open; see `MatchLog`.
   *
   * Keyed by match and dropped with the last stream of it, so what this holds is bounded by the
   * stream caps rather than by how many matches the server has ever served.
   */
  private readonly logs = new Map<string, MatchLog>()
  private open_ = 0

  constructor(
    private readonly events: EventRepository,
    private readonly heartbeatMs: number,
    private readonly limits: EventHubLimits = DEFAULT_EVENT_HUB_LIMITS,
    private readonly observer: EventHubObserver = {},
  ) {}

  async notify(event: PersistedEvent): Promise<void> {
    // The notification carries the durable row, so its frame is formatted once here and every
    // stream one event behind — which is every healthy stream of the match — is served from it
    // without reading anything. See `MatchLog`.
    this.logs.get(event.matchId)?.record(event)
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

  /**
   * End every stream this process holds, for a graceful shutdown.
   *
   * An event stream never ends on its own, so `server.close()` cannot return while one is open.
   * Ending them here is what lets a SIGTERM finish at once instead of waiting out the grace period
   * and then cutting the sockets, which clients see as a reset rather than as a stream to resume
   * from their `Last-Event-ID`.
   */
  closeAll(): void {
    // Snapshot deliberately: `close()` removes the subscription from the set being walked.
    // oxlint-disable-next-line unicorn/no-useless-spread
    for (const subscriptions of [...this.listeners.values()]) {
      // oxlint-disable-next-line unicorn/no-useless-spread
      for (const subscription of [...subscriptions]) subscription.close()
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
    // Before the caps, not only inside the response: making room closes the caller's oldest
    // stream, and a request nobody is waiting on must not cost that player a live one (or be
    // refused with a 429 nobody reads).
    if (input.signal.aborted) {
      this.observer.abandoned?.(input.matchId, input.playerId)
      return abandonedSseResponse()
    }
    this.makeRoom(input.matchId, input.playerId)
    const log = this.logOf(input.matchId)
    return createSseResponse(
      {
        page: (afterSeq, force) => log.page(afterSeq, force),
        caughtUp: (lastSeq) => log.caughtUp(lastSeq),
        subscribe: (wake, close) => this.subscribe(input.matchId, input.playerId, wake, close),
      },
      {
        afterSeq: input.afterSeq,
        heartbeatMs: this.heartbeatMs,
        signal: input.signal,
        onUnreadable: (seq) => this.observer.unreadable?.(input.matchId, seq),
      },
    )
  }

  private logOf(matchId: string): MatchLog {
    const existing = this.logs.get(matchId)
    if (existing) return existing
    const log = new MatchLog(matchId, this.events)
    this.logs.set(matchId, log)
    return log
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
      if (set.size === 0) {
        this.listeners.delete(matchId)
        // Nothing reads this match any more, so its frames are only memory. The next stream of it
        // starts from the log, which is where the truth was all along.
        this.logs.delete(matchId)
      }
    }
  }

  /**
   * Enforce the three caps before a stream is built.
   *
   * The caller's own stale streams are closed FIRST, before either ceiling is read. Closing them is
   * not a concession to the caller, it is bookkeeping: a reconnect whose predecessor has not
   * finished being torn down is the one case that reaches these caps in healthy traffic, and the
   * corpse it is replacing occupies a slot in all three counts. Reading the process cap ahead of
   * that answered 429 to the player whose laptop had just woken up, on a server whose own count
   * would have been under the cap the moment their dead stream went.
   *
   * `perMatch` and `perProcess` then refuse whatever is still over, which is the case no client can
   * talk its way past.
   */
  private makeRoom(matchId: string, playerId: string): void {
    const set = this.listeners.get(matchId)
    if (set) {
      const mine = [...set].filter((subscription) => subscription.playerId === playerId)
      for (const stale of mine.slice(0, mine.length - this.limits.perPlayer + 1)) stale.close()
    }
    if (this.open_ >= this.limits.perProcess) {
      throw new RateLimitedError('This server is holding as many event streams as it can', {
        reason: 'too_many_streams',
        scope: 'process',
      })
    }
    if ((this.listeners.get(matchId)?.size ?? 0) >= this.limits.perMatch) {
      throw new RateLimitedError('This match is holding as many event streams as it can', {
        reason: 'too_many_streams',
        scope: 'match',
      })
    }
  }
}
