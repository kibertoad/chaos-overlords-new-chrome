# @chaos-overlords/server

HTTP server for the [*Chaos Overlords: New Chrome*](https://github.com/kibertoad/chaos-overlords-new-chrome) multiplayer server.

The Hono application both runtimes serve: REST intents, the server-sent events stream, bearer
auth, rate limiting and the error envelope. It takes every port it needs through a container, so
hosting it is a matter of supplying storage, a clock and an event hub.

```ts
import { createApp } from '@chaos-overlords/server'

const app = createApp(container)
```

For a server you can just run, see `@chaos-overlords/node-server` or `@chaos-overlords/worker`.

## Install

```sh
npm install @chaos-overlords/server
```

Requires Node.js 22 or newer.

## Documentation

The operator and contributor manual for the whole multiplayer workspace is in
[`multiplayer/README.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/multiplayer/README.md); the design notes are in
[`docs/MULTIPLAYER.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/docs/MULTIPLAYER.md).

## Licence

GPL-3.0-only. See [`LICENSE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/LICENSE) and [`NOTICE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/NOTICE).
