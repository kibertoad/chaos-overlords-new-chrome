import {
  type ApiContract,
  type CreateMatchRequest,
  createMatchContract,
  type EventPage,
  getMatchContract,
  type JoinMatchRequest,
  type JoinRunningMatchRequest,
  joinMatchContract,
  joinRunningMatchContract,
  kickPlayerContract,
  type LobbyList,
  latestSnapshotContract,
  leaveMatchContract,
  listEventsContract,
  listLobbiesContract,
  type MatchDetail,
  type MatchEvent,
  type MatchSettings,
  type MembershipView,
  type OwnSubmissionView,
  ownSubmissionContract,
  rejoinMatchContract,
  reportTurnContract,
  resolveResponseEntry,
  type SealedOrdersView,
  type SnapshotView,
  type SubmitOrdersRequest,
  sealedOrdersContract,
  snapshotContract,
  startMatchContract,
  streamEventsContract,
  submitOrdersContract,
  type TakeoverVoteRequest,
  type TurnReportRequest,
  takeoverVoteContract,
  type UploadSnapshotRequest,
  updateMatchSettingsContract,
  uploadSnapshotContract,
  validate,
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
   * Abandons a request that has produced nothing for this long. Event streams are exempt once they
   * are open: they are expected to stay that way and carry their own keepalives, so the deadline
   * covers only getting them open. `0` (or any value below it) disables both.
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
  /**
   * A connection that carries nothing, not even the server's keepalive comment, for this long is
   * dropped and reconnected. The default is two and a half server heartbeats; `0` disables it.
   */
  idleTimeoutMs?: number
  /**
   * How long the stream may keep failing to deliver anything before it gives up. The clock starts
   * at the first failure and is reset by an event, or by a keepalive on a connection that has lasted
   * a server heartbeat. A server that accepts and then closes at once is still an outage. `0`
   * retries forever.
   */
  maxOutageMs?: number
}

export class StreamOutageError extends Error {
  constructor(
    readonly attempts: number,
    readonly outageMs: number,
    override readonly cause: unknown,
  ) {
    super(`event stream delivered nothing for ${outageMs} ms across ${attempts} attempts`)
    this.name = 'StreamOutageError'
  }
}

const DEFAULT_REQUEST_TIMEOUT_MS = 15_000
const DEFAULT_RECONNECT_DELAY_MS = 1_000
const DEFAULT_MAX_RECONNECT_DELAY_MS = 30_000
/** The server's default keepalive interval (`sseHeartbeatMs`). */
const SERVER_HEARTBEAT_MS = 20_000
/** Two and a half of the server's heartbeats. */
const DEFAULT_STREAM_IDLE_TIMEOUT_MS = SERVER_HEARTBEAT_MS * 2.5
const DEFAULT_MAX_OUTAGE_MS = 5 * 60_000

const API = '/api/v1'

/**
 * Typed access to the server. Unauthenticated calls first; `withToken` binds a membership.
 *
 * Every method reads its method and path off the contract it calls, so this client and the server's
 * routes cannot disagree about either: the server mounts the same contracts through
 * `@toad-contracts/hono`, and the C# client's records are generated from their schemas.
 */
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

  listLobbies(): Promise<LobbyList> {
    return this.call(listLobbiesContract, listLobbiesContract.pathResolver())
  }

  createMatch(request: CreateMatchRequest): Promise<MembershipView> {
    return this.call(createMatchContract, createMatchContract.pathResolver(), request)
  }

  join(request: JoinMatchRequest): Promise<MembershipView> {
    return this.call(joinMatchContract, joinMatchContract.pathResolver(), request)
  }

  joinRunning(request: JoinRunningMatchRequest): Promise<MembershipView> {
    return this.call(joinRunningMatchContract, joinRunningMatchContract.pathResolver(), request)
  }

  match(matchId: string): MatchHandle {
    return new MatchHandle(this, matchId)
  }

  /** @internal */
  async call<T>(contract: ApiContract, path: string, body?: unknown): Promise<T> {
    const headers: Record<string, string> = { Accept: 'application/json' }
    if (this.token) headers.Authorization = `Bearer ${this.token}`
    if (body !== undefined) headers['Content-Type'] = 'application/json'
    const init: RequestInit = { method: contract.method.toUpperCase(), headers }
    if (body !== undefined) init.body = JSON.stringify(body)
    if (this.requestTimeoutMs > 0) init.signal = AbortSignal.timeout(this.requestTimeoutMs)
    const response = await this.fetchImpl(`${this.baseUrl}${API}${path}`, init)
    if (!response.ok) throw await MultiplayerApiError.fromResponse(response)
    const responseKind = resolveResponseEntry(
      contract.responsesByStatusCode,
      response.status,
      response.headers.get('content-type') ?? undefined,
      true,
    )
    if (!responseKind) throw new Error(`server response is not declared by the contract`)
    if (responseKind.kind === 'noContent') return undefined as T
    if (responseKind.kind !== 'json') {
      throw new Error(`expected a JSON contract response, received ${responseKind.kind}`)
    }
    return (await validate(responseKind.schema, await response.json())) as T
  }

  /** @internal */
  async openStream(
    contract: ApiContract,
    path: string,
    after: number,
    signal: AbortSignal | undefined,
  ): Promise<OpenStream> {
    const headers: Record<string, string> = {
      Accept: 'text/event-stream',
      'Last-Event-ID': String(after),
    }
    if (this.token) headers.Authorization = `Bearer ${this.token}`
    // A deadline on the CONNECT phase only, cancelled the moment the headers arrive.
    //
    // A stream is exempt from the request timeout because it is meant to stay open, but that
    // exemption used to cover getting it open too: against a host that accepts nothing and answers
    // nothing — a firewall that drops SYNs rather than refusing them — each attempt cost the
    // operating system's own connect timeout, often two minutes, so the five-minute outage budget
    // bought two attempts instead of the dozens it is sized for. The C# client races the header
    // phase against a timer for the same reason. A non-positive `requestTimeoutMs` disables the
    // deadline here exactly as it does for `call`, rather than aborting every connect at once.
    //
    // The controller stays attached to the body afterwards, with the caller's own signal forwarded
    // into it, so cancelling the stream still works exactly as before; only the TIMER is cleared.
    // That forwarding is a listener on a signal the caller owns and keeps across every reconnect,
    // so `release` takes it back off once the body is done with — one connection's listener must
    // not outlive its connection, or a long stream accumulates one per reconnect.
    const connect = new AbortController()
    const forward = (): void => connect.abort(signal?.reason)
    const release = (): void => signal?.removeEventListener('abort', forward)
    if (signal?.aborted) forward()
    else signal?.addEventListener('abort', forward, { once: true })
    const timer =
      this.requestTimeoutMs > 0
        ? setTimeout(() => {
            connect.abort(
              new Error(`the event stream did not open within ${this.requestTimeoutMs} ms`),
            )
          }, this.requestTimeoutMs)
        : undefined
    try {
      let response: Response
      try {
        response = await this.fetchImpl(`${this.baseUrl}${API}${path}`, {
          headers,
          signal: connect.signal,
        })
      } finally {
        if (timer !== undefined) clearTimeout(timer)
      }
      if (!response.ok) throw await MultiplayerApiError.fromResponse(response)
      const responseKind = resolveResponseEntry(
        contract.responsesByStatusCode,
        response.status,
        response.headers.get('content-type') ?? undefined,
        true,
      )
      if (responseKind?.kind !== 'sse')
        throw new Error('server response is not the contracted stream')
      if (!response.body) throw new Error('event stream response has no body')
      return { response, release }
    } catch (error) {
      // Nothing was handed back, so nothing will call `release`.
      release()
      throw error
    }
  }
}

/**
 * A connected event stream and the teardown for the caller-signal forwarding it installed.
 *
 * `release` only detaches that listener; it never aborts. It is called once the body has been
 * finished with, by which point cancelling it is the body reader's own business.
 */
export interface OpenStream {
  readonly response: Response
  release(): void
}

export class MatchHandle {
  constructor(
    private readonly client: MultiplayerClient,
    readonly matchId: string,
  ) {}

  get(): Promise<MatchDetail> {
    return this.client.call(
      getMatchContract,
      getMatchContract.pathResolver({ matchId: this.matchId }),
    )
  }

  start(): Promise<void> {
    return this.client.call(
      startMatchContract,
      startMatchContract.pathResolver({ matchId: this.matchId }),
    )
  }

  rejoin(): Promise<void> {
    return this.client.call(
      rejoinMatchContract,
      rejoinMatchContract.pathResolver({ matchId: this.matchId }),
    )
  }

  updateSettings(settings: MatchSettings): Promise<void> {
    return this.client.call(
      updateMatchSettingsContract,
      updateMatchSettingsContract.pathResolver({ matchId: this.matchId }),
      settings,
    )
  }

  leave(): Promise<void> {
    return this.client.call(
      leaveMatchContract,
      leaveMatchContract.pathResolver({ matchId: this.matchId }),
    )
  }

  kick(playerId: string): Promise<void> {
    return this.client.call(
      kickPlayerContract,
      kickPlayerContract.pathResolver({ matchId: this.matchId, playerId }),
    )
  }

  voteOnTakeover(playerId: string, request: TakeoverVoteRequest): Promise<void> {
    return this.client.call(
      takeoverVoteContract,
      takeoverVoteContract.pathResolver({ matchId: this.matchId, playerId }),
      request,
    )
  }

  submitOrders(turn: number, request: SubmitOrdersRequest): Promise<OwnSubmissionView> {
    return this.client.call(
      submitOrdersContract,
      submitOrdersContract.pathResolver({ matchId: this.matchId, turn }),
      request,
    )
  }

  mySubmission(turn: number): Promise<OwnSubmissionView> {
    return this.client.call(
      ownSubmissionContract,
      ownSubmissionContract.pathResolver({ matchId: this.matchId, turn }),
    )
  }

  sealedOrders(turn: number): Promise<SealedOrdersView> {
    return this.client.call(
      sealedOrdersContract,
      sealedOrdersContract.pathResolver({ matchId: this.matchId, turn }),
    )
  }

  report(turn: number, request: TurnReportRequest): Promise<void> {
    return this.client.call(
      reportTurnContract,
      reportTurnContract.pathResolver({ matchId: this.matchId, turn }),
      request,
    )
  }

  uploadSnapshot(request: UploadSnapshotRequest): Promise<void> {
    return this.client.call(
      uploadSnapshotContract,
      uploadSnapshotContract.pathResolver({ matchId: this.matchId }),
      request,
    )
  }

  latestSnapshot(): Promise<SnapshotView> {
    return this.client.call(
      latestSnapshotContract,
      latestSnapshotContract.pathResolver({ matchId: this.matchId }),
    )
  }

  snapshot(turn: number): Promise<SnapshotView> {
    return this.client.call(
      snapshotContract,
      snapshotContract.pathResolver({ matchId: this.matchId, turn }),
    )
  }

  events(after = 0, limit = 200): Promise<EventPage> {
    const path = listEventsContract.pathResolver({ matchId: this.matchId })
    return this.client.call(listEventsContract, `${path}?after=${after}&limit=${limit}`)
  }

  /** One connection's worth of events; ends when the server closes it. */
  async *streamOnce(
    options: StreamOptions & { onActivity?: (connectionAgeMs: number) => void } = {},
  ): AsyncGenerator<MatchEvent> {
    const { response, release } = await this.client.openStream(
      streamEventsContract,
      streamEventsContract.pathResolver({ matchId: this.matchId }),
      options.after ?? 0,
      options.signal,
    )
    const connectedAt = Date.now()
    try {
      yield* parseEventStream(response.body as ReadableStream<Uint8Array>, {
        idleTimeoutMs: options.idleTimeoutMs ?? DEFAULT_STREAM_IDLE_TIMEOUT_MS,
        onActivity: () => options.onActivity?.(Date.now() - connectedAt),
      })
    } finally {
      // Runs on a `break` or a `return` from the consumer as well, so every connection gives its
      // listener back — including the ones `stream` abandons to reconnect.
      release()
    }
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
   *
   * Every other failure — a dropped socket, a mangled frame, a proxy answering with HTML, a
   * connection that went silent — is retried within `maxOutageMs`, after which the stream ends
   * with a `StreamOutageError` naming the last failure. An event resets the clock, and so does a
   * keepalive on a connection that has lasted a heartbeat; accepting and immediately closing
   * connections does not.
   */
  async *stream(options: StreamOptions = {}): AsyncGenerator<MatchEvent> {
    let after = options.after ?? 0
    let attempt = 0
    let outageStartedAt: number | null = null
    const base = options.reconnectDelayMs ?? DEFAULT_RECONNECT_DELAY_MS
    const ceiling = options.maxReconnectDelayMs ?? DEFAULT_MAX_RECONNECT_DELAY_MS
    const maxOutageMs = options.maxOutageMs ?? DEFAULT_MAX_OUTAGE_MS
    // A connection that is still carrying keepalives a heartbeat after it opened is a working one,
    // the same line the C# client draws (`MatchEventStream.ProvenAfter`). The heartbeat is read off
    // the idle timeout, which is two and a half of them, and capped so a short outage budget can
    // still be reset by a connection that lasts half of it.
    const idleMs = options.idleTimeoutMs ?? DEFAULT_STREAM_IDLE_TIMEOUT_MS
    const heartbeatMs = idleMs > 0 ? idleMs / 2.5 : SERVER_HEARTBEAT_MS
    const provenAfterMs = maxOutageMs > 0 ? Math.min(heartbeatMs, maxOutageMs / 2) : heartbeatMs
    while (!options.signal?.aborted) {
      // A connection the server closes cleanly without delivering anything is a failure of the
      // same kind as a dropped one for the purposes of the budget: nothing arrived.
      let failure: unknown = new Error('the server closed the event stream')
      try {
        for await (const event of this.streamOnce({
          ...options,
          after,
          onActivity: (connectionAgeMs) => {
            if (connectionAgeMs >= provenAfterMs) {
              attempt = 0
              outageStartedAt = null
            }
          },
        })) {
          after = Math.max(after, event.seq)
          attempt = 0
          outageStartedAt = null
          yield event
        }
      } catch (error) {
        if (options.signal?.aborted) return
        if (error instanceof MultiplayerApiError && isFatalStreamError(error.status)) throw error
        failure = error
      }
      if (options.signal?.aborted) return
      const now = Date.now()
      outageStartedAt ??= now
      if (maxOutageMs > 0 && now - outageStartedAt >= maxOutageMs) {
        throw new StreamOutageError(attempt + 1, now - outageStartedAt, failure)
      }
      options.onReconnect?.(failure, attempt + 1)
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

/**
 * Exponential with FULL jitter: a delay drawn uniformly from the whole window, not from its upper
 * half.
 *
 * The half-window form still clumps. The case that matters is the one the Node server produces on
 * purpose: `closeAll()` ends every stream in the process at once for a graceful shutdown, so every
 * client in every match starts its first backoff in the same millisecond, and half a window is a
 * narrow enough target that they all come back within the same second — at the moment the
 * replacement process is coldest. Over the whole window the herd spreads evenly, and the expected
 * delay halves as well, so a single client reconnects sooner on average rather than later.
 */
function backoff(baseMs: number, ceilingMs: number, attempt: number): number {
  const window = Math.min(ceilingMs, baseMs * 2 ** (attempt - 1))
  return Math.round(window * Math.random())
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
