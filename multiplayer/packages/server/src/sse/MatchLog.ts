import type { EventRepository, PersistedEvent } from '@chaos-overlords/kernel'
import { tryFormatEvent } from './createSseResponse'

/** One event as it goes on the wire, formatted and validated exactly once. */
export interface EventFrame {
  seq: number
  /** The SSE frame, or null for a stored row this build's schema cannot read. */
  text: string | null
}

/**
 * Frames one match keeps in memory.
 *
 * Only the tail matters: every stream of a match sits within a few events of the others, so what
 * this covers is the burst a seal emits and the moment after a reconnect. A straggler further back
 * than this falls through to a query, which is what the durable log is for.
 */
const FRAMES_KEPT = 256

/** Events one query pages. */
const PAGE_SIZE = 200

/**
 * The shared read and the shared frame of one match's event log.
 *
 * Fan-out used to be per subscriber all the way down: waking eighteen streams for one event meant
 * eighteen `listAfter` queries over the same rows, eighteen schema validations of the same payload
 * and eighteen `JSON.stringify` calls producing the same string. The work is per EVENT, not per
 * reader, so it is done here once and handed out.
 *
 * Three things follow from that, in order of how much they save:
 *
 * 1. **The notification carries the event.** `notify` already holds the durable row, so its frame
 *    is formatted and kept here before any stream is woken. A stream whose cursor is exactly one
 *    behind — which is every healthy stream of the match — finds it and never reads at all.
 * 2. **Concurrent readers at the same cursor share one query.** A stream that does have to read
 *    joins the request already in flight for that cursor instead of issuing its own.
 * 3. **A heartbeat that has nothing to catch up on asks nothing.** The highest sequence this
 *    process has been told about is known here, so a caught-up stream skips its periodic re-read.
 *    At the default cap of 512 streams that was about twenty-five queries a second finding nothing,
 *    on the same event loop that seals turns.
 *
 * None of it changes the contract that the LOG is the truth: every fast path is a memo of rows that
 * were durable before they got here, and anything not covered falls through to a query.
 */
export class MatchLog {
  /** Ascending by seq; trimmed from the front. A Map iterates in insertion order. */
  private readonly frames = new Map<number, EventFrame>()
  private readonly inFlight = new Map<number, Promise<EventFrame[]>>()
  /**
   * The highest sequence a NOTIFICATION has carried, which is not the same as the highest this has
   * ever read.
   *
   * Only a notification proves that this process is being told about the match's appends, and only
   * that justifies answering "there is nothing further" without asking the database. A hub that is
   * woken without the event body — the Durable Object is notified by match id — never sets this, so
   * every one of its reads goes to the log exactly as before.
   */
  private notified = 0

  constructor(
    private readonly matchId: string,
    private readonly events: EventRepository,
  ) {}

  /**
   * Remember a published event and its frame.
   *
   * Formatting here rather than in each stream is what turns a seal's burst from "events times
   * subscribers" units of work into "events".
   */
  record(event: PersistedEvent): void {
    if (event.seq > this.notified) this.notified = event.seq
    if (this.frames.has(event.seq)) return
    this.frames.set(event.seq, { seq: event.seq, text: tryFormatEvent(event) })
    this.trim()
  }

  /**
   * Whether a stream at `lastSeq` holds every event this process has been told about.
   *
   * False whenever nothing has been (see `notified`), so an unsure answer is always the one that
   * does the work. Callers use it to skip a periodic re-read, never to decide what to deliver.
   */
  caughtUp(lastSeq: number): boolean {
    return this.notified > 0 && lastSeq >= this.notified
  }

  /**
   * The next page after `afterSeq`, from memory when the tail covers it and from the log otherwise.
   *
   * A page served from memory is contiguous by construction: it stops at the first gap, so a
   * caller can never be handed a frame whose predecessor it has not seen, which is the invariant
   * the stream cursor rests on.
   *
   * `force` bypasses memory entirely and asks the log. The caller that passes it is the periodic
   * catch-up, which exists precisely for what this process cannot be told: an append by another
   * process against the same database. Everything else may take the short answer, because the
   * events THIS process publishes are the events it is notified of.
   *
   * Bypassing the memo rather than only its "nothing further" answer is what makes the catch-up
   * happen at all: the drain spends the force on its FIRST page, so a page served out of memory
   * used it up without a read ever reaching the log, and the stream then waited another whole
   * catch-up period to try again. A tail hit is the common case on a woken stream, so that was
   * most of them.
   */
  async page(afterSeq: number, force = false): Promise<EventFrame[]> {
    if (!force) {
      const memoised = this.pageFromMemory(afterSeq)
      if (memoised.length > 0) return memoised
      if (this.caughtUp(afterSeq)) return []
    }
    // A read already in flight for this cursor is a read of the log, forced or not, so joining it
    // satisfies a forced caller too.
    const shared = this.inFlight.get(afterSeq)
    if (shared) return shared
    const request = this.read(afterSeq).finally(() => {
      this.inFlight.delete(afterSeq)
    })
    this.inFlight.set(afterSeq, request)
    return request
  }

  private pageFromMemory(afterSeq: number): EventFrame[] {
    const page: EventFrame[] = []
    for (let seq = afterSeq + 1; page.length < PAGE_SIZE; seq += 1) {
      const frame = this.frames.get(seq)
      if (!frame) break
      page.push(frame)
    }
    return page
  }

  private async read(afterSeq: number): Promise<EventFrame[]> {
    const rows = await this.events.listAfter(this.matchId, afterSeq, PAGE_SIZE)
    const page = rows.map((row) => {
      const known = this.frames.get(row.seq)
      if (known) return known
      const frame: EventFrame = { seq: row.seq, text: tryFormatEvent(row) }
      this.frames.set(row.seq, frame)
      return frame
    })
    this.trim()
    return page
  }

  private trim(): void {
    while (this.frames.size > FRAMES_KEPT) {
      const oldest = this.frames.keys().next()
      if (oldest.done) return
      this.frames.delete(oldest.value)
    }
  }
}
