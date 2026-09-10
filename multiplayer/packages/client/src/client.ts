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
}

export interface StreamOptions {
  /** Resume after this sequence number (the last event seen). */
  after?: number
  signal?: AbortSignal
  /** Delay before reconnecting after a dropped stream, in milliseconds. */
  reconnectDelayMs?: number
}

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

  constructor(options: ClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/+$/, '')
    this.fetchImpl = options.fetch ?? ((input, init) => fetch(input, init))
    this.token = options.token
  }

  withToken(token: string): MultiplayerClient {
    return new MultiplayerClient({ baseUrl: this.baseUrl, fetch: this.fetchImpl, token })
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
   */
  async *stream(options: StreamOptions = {}): AsyncGenerator<MatchEvent> {
    let after = options.after ?? 0
    const delay = options.reconnectDelayMs ?? 1000
    while (!options.signal?.aborted) {
      try {
        for await (const event of this.streamOnce({ ...options, after })) {
          after = Math.max(after, event.seq)
          yield event
        }
      } catch (error) {
        if (options.signal?.aborted) return
        if (error instanceof MultiplayerApiError && error.status < 500) throw error
      }
      if (options.signal?.aborted) return
      await new Promise((resolve) => setTimeout(resolve, delay))
    }
  }
}
