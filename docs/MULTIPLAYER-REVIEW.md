# Multiplayer implementation review: open items

Status: open items only
Last updated: 2026-09-23

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
  - [Lobby polling is the steadiest load a server sees](#lobby-polling-is-the-steadiest-load-a-server-sees)
- [Client](#client)
  - [Backoff jitter is not full jitter](#backoff-jitter-is-not-full-jitter)
  - [A recovery file from a newer build is overwritten](#a-recovery-file-from-a-newer-build-is-overwritten)
  - [Every response is read three times](#every-response-is-read-three-times)
  - [Smaller allocations](#smaller-allocations)
- [Test coverage](#test-coverage)
<!-- doc-index:end -->

## Server

### Lobby polling is the steadiest load a server sees

The game polls the lobby once a second per player (`LobbyPollInterval`,
`src/Rechaos.Game/ChaosGame.Multiplayer.cs`). Each poll is an authenticated request that runs
several queries. The design doc calls this deliberate, but it is still the server's steadiest
load. An `ETag` built from `(updatedAt, lastEventSeq)` and answered with `304` would cut an
unchanged poll to the auth lookup and one `lastSeq` read. Alternatively, the lobby could long-poll
`GET /events?after=`.

## Client

### Backoff jitter is not full jitter

`RetryPolicy.Backoff` (`src/Rechaos.Multiplayer/Http/RetryPolicy.cs`) returns
`window × [0.5, 1.0]`, although its doc comment says "full jitter". When the Node server shuts
down it closes every stream at once, so every client comes back within the same half window.
Drawing from `[0, window]`, as the TypeScript client does, spreads that herd out.

### A recovery file from a newer build is overwritten

`MultiplayerRecoveryStore.TryLoadAll` (`src/Rechaos.Game/MultiplayerRecoveryStore.cs`) returns an
empty list when the file's `FormatVersion` is outside the range this build reads. A file this build
cannot parse is set aside as `.corrupt`, but this one is not. So after a downgrade, the next save
overwrites the newer build's seats. The single `.bak` generation is gone after two saves. Set such a
file aside the same way, or refuse to write over it.

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

Fixes without a test that would fail if they were reverted:

- **The stream parser's caps.** No test references `MaximumFrameChars` or an unterminated line.
- **Clock skew.** No test feeds the session a deadline from a server whose clock differs, or reads
  the offset from a `Date` header.
- **The resolution watchdog.** `RequestResync` is tested, but the game's watchdog and its grace
  period (set above the stream idle detector plus a reconnect) are not.
- **Client refusals and re-handshakes.** Nothing covers `TakeoverVoteFailed` reaching the UI, or
  the handshake being re-established on reconnect.
- **Over HTTP**, in `multiplayer/packages/conformance/src/http.ts`, so that every runtime runs it:
  - password-gated create and join;
  - the no-echo rule with a wrong-typed secret (e.g. `{ "password": 123456 }`);
  - stream caps answering 429 (currently tested only on the hub itself);
  - the keepalive frame;
  - `Last-Event-ID` taking precedence over `?after=`;
  - malformed JSON answering 422;
  - an oversized snapshot answering 413;
  - `Retry-After` on every 429 and `X-Request-Id` on every response, the event stream included.
- **Cloudflare end to end.** `tools/OnlineSmoke` runs in the multiplayer workflow against the Node
  runtime. Nothing plays a match against the workerd runtime, and nothing runs on a schedule to
  catch drift on a branch nobody touched.
