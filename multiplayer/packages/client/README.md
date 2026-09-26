# @chaos-overlords/client

TypeScript client for the [*Chaos Overlords: New Chrome*](https://github.com/kibertoad/chaos-overlords-new-chrome) multiplayer server.

Typed REST calls and a resumable server-sent events iterator for the multiplayer server. The
conformance suite drives every runtime through this client, and the game's C# client mirrors it.

**What this client does and does not do.** `stream()` reconnects: it resumes from the last sequence
it saw, backs off exponentially with full jitter, drops a connection that has carried nothing for
two and a half server heartbeats, and gives up after an outage budget. Ordinary `call()` requests do
NOT retry — each one either succeeds or throws, and deciding which failures are worth repeating is
left to the caller. The retry contract described in
[`docs/MULTIPLAYER.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/docs/MULTIPLAYER.md)
— one line drawn once, a request deadline told apart from caller cancellation, an outbox that keeps
only the newest whole-document replacement — is implemented in the game's C# client, which is the
one playing matches over hours. This package is the conformance driver and the reference for the
wire, and a caller that wants those semantics in TypeScript writes them around `call()`.

```ts
import { MultiplayerClient } from '@chaos-overlords/client'

const client = new MultiplayerClient({ baseUrl: 'https://example.invalid' })
```

## Install

```sh
npm install @chaos-overlords/client
```

Requires Node.js 22 or newer.

## Documentation

The operator and contributor manual for the whole multiplayer workspace is in
[`multiplayer/README.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/multiplayer/README.md); the design notes are in
[`docs/MULTIPLAYER.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/docs/MULTIPLAYER.md).

## Licence

MIT. See [`LICENSE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/LICENSE) and [`NOTICE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/NOTICE).
