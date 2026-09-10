import type {
  CreateMatchRequest,
  JoinMatchRequest,
  LobbyListing,
  MatchEvent,
  MatchView,
  MembershipView,
  OwnSubmissionView,
  SealedOrdersView,
  SnapshotView,
  SubmitOrdersRequest,
  TurnReportRequest,
  UploadSnapshotRequest,
} from '@chaos-overlords/contracts'
import { MultiplayerApiError } from './errors'
import { parseEventStream } from './sse'

export type FetchLike = (input: string, init?: RequestInit) => Promise<Response>

export interface ClientOptions {
  /** Server origin, e.g. `https://play.example.org` or `http://localhost:8787`. */
  baseUrl: string
  fetch?: FetchLike
  token?: string
  /**
   * Abandons a request that has produced nothing for this long. Event streams are exempt: they are
   * expected to stay open and carry their own keepalives.
   */
  requestTimeoutMs?: number
}

export interface StreamOptions {
  /** Resume after this sequence number (the last event seen). */
  after?: number
  signal?: AbortSignal
  /** First reconnect delay; it doubles up to `maxReconnectDelayMs`, with jitter. */
  reconnectDelayMs?: number
  maxReconnectDelayMs?: number
  /** Called for each dropped connection, so a caller can surface "reconnecting" to the player. */
  onReconnect?: (error: unknown, attempt: number) => void
}

const DEFAULT_REQUEST_TIMEOUT_MS = 15_000
const DEFAULT_RECONNECT_DELAY_MS = 1_000
const DEFAULT_MAX_RECONNECT_DELAY_MS = 30_000

export interface MatchDetail {
  match: MatchView
  joinCode: string
  /** The caller's own player id. */
  you: string
}

const API = '/api/v1'

/** Typed access to the server. Unauthenticated calls first; `withToken` binds a membership. */
export class MultiplayerClient {
  private readonly fetchImpl: FetchLike
  private readonly baseUrl: string
  private readonly token: string | undefined
  private readonly requestTimeoutMs: number

  constructor(options: ClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/+$/, '')
    this.fetchImpl = options.fetch ?? ((input, init) => fetch(input, init))
    this.token = options.token
    this.requestTimeoutMs = options.requestTimeoutMs ?? DEFAULT_REQUEST_TIMEOUT_MS
  }

  withToken(token: string): MultiplayerClient {
    return new MultiplayerClient({
      baseUrl: this.baseUrl,
      fetch: this.fetchImpl,
      token,
      requestTimeoutMs: this.requestTimeoutMs,
    })
  }

  listLobbies(): Promise<{ matches: LobbyListing[] }> {
    return this.call('GET', '/matches')
  }

  createMatch(request: CreateMatchRequest): Promise<MembershipView> {
    return this.call('POST', '/matches', request)
  }

  join(request: JoinMatchRequest): Promise<MembershipView> {
    return this.call('POST', '/matches/join', request)
  }

  match(matchId: string): MatchHandle {
    return new MatchHandle(this, matchId)
  }

  /** @internal */
  async call<T>(method: string, path: string, body?: unknown): Promise<T> {
    const headers: Record<string, string> = { Accept: 'application/json' }
    if (this.token) headers.Authorization = `Bearer ${this.token}`
    if (body !== undefined) headers['Content-Type'] = 'application/json'
    const init: RequestInit = { method, headers }
    if (body !== undefined) init.body = JSON.stringify(body)
    if (this.requestTimeoutMs > 0) init.signal = AbortSignal.timeout(this.requestTimeoutMs)
    const response = await this.fetchImpl(`${this.baseUrl}${API}${path}`, init)
    if (!response.ok) throw await MultiplayerApiError.fromResponse(response)
    if (response.status === 204) return undefined as T
    return (await response.json()) as T
  }

  /** @internal */
  async openStream(
    path: string,
    after: number,
    signal: AbortSignal | undefined,
  ): Promise<Response> {
    const headers: Record<string, string> = {
      Accept: 'text/event-stream',
      'Last-Event-ID': String(after),
    }
    if (this.token) headers.Authorization = `Bearer ${this.token}`
    const init: RequestInit = { headers }
    if (signal) init.signal = signal
    const response = await this.fetchImpl(`${this.baseUrl}${API}${path}`, init)
    if (!response.ok) throw await MultiplayerApiError.fromResponse(response)
    if (!response.body) throw new Error('event stream response has no body')
    return response
  }
}

export class MatchHandle {
  constructor(
    private readonly client: MultiplayerClient,
    readonly matchId: string,
  ) {}

  private path(suffix = ''): string {
    return `/matches/${encodeURIComponent(this.matchId)}${suffix}`
  }

  get(): Promise<MatchDetail> {
    return this.client.call('GET', this.path())
  }

  start(): Promise<void> {
    return this.client.call('POST', this.path('/start'))
  }

  leave(): Promise<void> {
    return this.client.call('POST', this.path('/leave'))
  }

  kick(playerId: string): Promise<void> {
    return this.client.call('POST', this.path(`/players/${encodeURIComponent(playerId)}/kick`))
  }

  submitOrders(turn: number, request: SubmitOrdersRequest): Promise<OwnSubmissionView> {
    return this.client.call('PUT', this.path(`/turns/${turn}/orders`), request)
  }

  mySubmission(turn: number): Promise<OwnSubmissionView> {
    return this.client.call('GET', this.path(`/turns/${turn}/orders/mine`))
  }

  sealedOrders(turn: number): Promise<SealedOrdersView> {
    return this.client.call('GET', this.path(`/turns/${turn}/orders`))
  }

  report(turn: number, request: TurnReportRequest): Promise<void> {
    return this.client.call('POST', this.path(`/turns/${turn}/report`), request)
  }

  uploadSnapshot(request: UploadSnapshotRequest): Promise<void> {
    return this.client.call('POST', this.path('/snapshots'), request)
  }

  latestSnapshot(): Promise<SnapshotView> {
    return this.client.call('GET', this.path('/snapshots/latest'))
  }

  snapshot(turn: number): Promise<SnapshotView> {
    return this.client.call('GET', this.path(`/snapshots/${turn}`))
  }

  events(after = 0, limit = 200): Promise<{ events: MatchEvent[] }> {
    return this.client.call('GET', this.path(`/events?after=${after}&limit=${limit}`))
  }

  /** One connection's worth of events; ends when the server closes it. */
  async *streamOnce(options: StreamOptions = {}): AsyncGenerator<MatchEvent> {
    const response = await this.client.openStream(
      this.path('/stream'),
      options.after ?? 0,
      options.signal,
    )
    yield* parseEventStream(response.body as ReadableStream<Uint8Array>)
  }

  /**
   * Events forever: reconnects after a drop, resuming from the last sequence seen, until the
   * signal aborts. The seq is the only state a client needs to keep to never miss an event.
   *
   * Reconnects back off exponentially with jitter and a ceiling, so a server that is down or
   * restarting is not hammered once a second by every client at once, and a refusal the server will
   * keep repeating (a revoked token, a deleted match) ends the stream instead of being retried
   * forever. A refusal that is *about* this attempt rather than about the membership — being rate
   * limited, a timeout — is retried: it is the reconnect loop itself that spends the rate limit
   * budget, so treating 429 as fatal would make the recovery path destroy the thing it recovers.
   */
  async *stream(options: StreamOptions = {}): AsyncGenerator<MatchEvent> {
    let after = options.after ?? 0
    let attempt = 0
    const base = options.reconnectDelayMs ?? DEFAULT_RECONNECT_DELAY_MS
    const ceiling = options.maxReconnectDelayMs ?? DEFAULT_MAX_RECONNECT_DELAY_MS
    while (!options.signal?.aborted) {
      try {
        for await (const event of this.streamOnce({ ...options, after })) {
          after = Math.max(after, event.seq)
          attempt = 0
          yield event
        }
      } catch (error) {
        if (options.signal?.aborted) return
        if (error instanceof MultiplayerApiError && isFatalStreamError(error.status)) throw error
        options.onReconnect?.(error, attempt + 1)
      }
      if (options.signal?.aborted) return
      attempt += 1
      await sleep(backoff(base, ceiling, attempt), options.signal)
    }
  }
}

/**
 * Statuses that will not change on a retry, so the stream ends instead of reconnecting forever.
 *
 * Everything at 500 and above is the server having a bad moment. Below that, only a refusal about
 * the membership itself is permanent; 408 (timeout) and 429 (rate limited) describe this attempt,
 * and backing off is exactly the right response to both.
 */
const RETRYABLE_STREAM_STATUSES = new Set([408, 425, 429])

export function isFatalStreamError(status: number): boolean {
  return status < 500 && !RETRYABLE_STREAM_STATUSES.has(status)
}

/** Exponential with full jitter: every client picks a different point in the window. */
function backoff(baseMs: number, ceilingMs: number, attempt: number): number {
  const window = Math.min(ceilingMs, baseMs * 2 ** (attempt - 1))
  return Math.round(window * (0.5 + Math.random() / 2))
}

function sleep(ms: number, signal: AbortSignal | undefined): Promise<void> {
  return new Promise((resolve) => {
    const timer = setTimeout(finish, ms)
    signal?.addEventListener('abort', finish, { once: true })
    function finish(): void {
      clearTimeout(timer)
      signal?.removeEventListener('abort', finish)
      resolve()
    }
  })
}
