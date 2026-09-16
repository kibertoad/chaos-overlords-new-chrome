# @chaos-overlords/client

TypeScript client for the [*Chaos Overlords: New Chrome*](https://github.com/kibertoad/chaos-overlords-new-chrome) multiplayer server.

Typed REST calls and a resumable server-sent events iterator for the multiplayer server. The
conformance suite drives every runtime through this client, and the game's C# client mirrors it.

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

GPL-3.0-only. See [`LICENSE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/LICENSE) and [`NOTICE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/NOTICE).
