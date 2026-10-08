# Multiplayer implementation review: open items

Status: open items only

What remains of the robustness and efficiency review of online play: the coordination server
under [`multiplayer/`](../multiplayer/README.md) and the game's client in
[`src/Rechaos.Multiplayer`](../src/Rechaos.Multiplayer/README.md) with its integration in
`Rechaos.Game`. The design is in [MULTIPLAYER.md](MULTIPLAYER.md).

The review's high and medium findings (desync repair, the client's give-up paths, the seal and
departure races, the sweep, fan-out and response-validation costs, reconnect replay, the game-thread
hashing and saves) have all been fixed, and each fix is explained in comments in the code where it
applies. Their write-ups were removed once they were checked against the current code; the git
history of this file still has them. Everything below was checked against the code as of the date
above and is still open. All of it is low severity: polish, efficiency, or tests that would stop an
earlier fix from regressing.

<!-- doc-index:begin toc depth=3 -->
- [Server](#server)
  - [A connection that carried an oversized body is dropped under the next request](#a-connection-that-carried-an-oversized-body-is-dropped-under-the-next-request)
- [Client](#client)
  - [Backoff jitter is not full jitter](#backoff-jitter-is-not-full-jitter)
  - [Every response is read three times](#every-response-is-read-three-times)
  - [Smaller allocations](#smaller-allocations)
- [Test coverage](#test-coverage)
<!-- doc-index:end -->

## Server

### A connection that carried an oversized body is dropped under the next request

The body caps refuse on `Content-Length` with 413 before reading the body. On the Node runtime,
`@hono/node-server` then gives the unread remainder half a second to drain and destroys the socket
when it has not, while the response it sent offered the connection for reuse. With a body of about a
megabyte the drain does not finish, so the next request the client queued on that connection hangs
for the half second and fails with the socket closed. Answering with `Connection: close` does not
help: the server then closes while the client is still writing, and the client reads a reset instead
of the 413. The game checks sizes before it sends, so a player only meets this after a bug of its
own, and the .NET handler retries a request that failed on a reused connection. The conformance case
for the 413 runs last for this reason.

## Client

### Backoff jitter is not full jitter

`RetryPolicy.Backoff` (`src/Rechaos.Multiplayer/Http/RetryPolicy.cs`) returns
`window × [0.5, 1.0]`, although its doc comment says "full jitter". When the Node server shuts
down it closes every stream at once, so every client comes back within the same half window.
Drawing from `[0, window]`, as the TypeScript client does, spreads that herd out.

### Every response is read three times

`MultiplayerClient.SendAsync` reads the body to a string. `WireJson.Read` then parses it to a
`JsonNode` tree, and `WireOrder.TagFirst` walks that tree before it is deserialised into records.
For the megabyte snapshot and a six-document sealed set, that is three copies and three passes.
`WireOrder.cs` already notes the fix: a converter per union that reads the discriminator wherever
it sits, then `DeserializeAsync` straight from the stream.

### Smaller allocations

- `OnlineTurnStatus()` (`ChaosGame.MultiplayerDraw.cs`) builds interpolated strings every frame.
  The countdown and the session lists are already cached; this one is not.
- `CanonicalJson` allocates a path string per property even on the success path. `OrderDigest`
  serialises a document to a string and re-parses it in order to canonicalise it. Both run only
  once per draft change and per sealed set. Build the path only when an exception is being raised,
  if it ever shows up in a profile.

## Test coverage

- **The keepalive frame on Cloudflare.** The conformance case for it runs where the harness can
  shorten the interval: in process (50 ms) and on the Node runtime (`sseHeartbeatMs`, 2 s). The
  Durable Object builds its hub with `DEFAULT_SERVER_CONFIG.sseHeartbeatMs`, twenty seconds, and
  nothing lets a test change it, so the worker pool skips the case rather than wait out a heartbeat.
- **End to end on a schedule.** `tools/OnlineSmoke` plays a match against the Node server and
  against the Worker under `wrangler dev` whenever the multiplayer workflow runs, but nothing runs
  it on a schedule to catch drift on a branch nobody touched. Running it on a schedule is tracked in
  [#459](https://github.com/kibertoad/chaos-overlords-new-chrome/issues/459).
