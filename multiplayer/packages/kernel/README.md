# @chaos-overlords/kernel

Domain kernel for the [*Chaos Overlords: New Chrome*](https://github.com/kibertoad/chaos-overlords-new-chrome) multiplayer server.

The runtime-neutral half of the multiplayer server: entities, the storage and runtime ports,
pure turn logic, and the lobby, turn and snapshot services. It knows nothing about HTTP, SQL or the
runtime it is hosted in — those arrive as ports, which is what lets the same domain run on Node.js
and on Cloudflare Workers.

`@chaos-overlords/kernel/testing` carries an in-memory storage for hermetic tests.

## Install

```sh
npm install @chaos-overlords/kernel
```

Requires Node.js 22 or newer.

## Documentation

The operator and contributor manual for the whole multiplayer workspace is in
[`multiplayer/README.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/multiplayer/README.md); the design notes are in
[`docs/MULTIPLAYER.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/docs/MULTIPLAYER.md).

## Licence

GPL-3.0-only. See [`LICENSE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/LICENSE) and [`NOTICE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/NOTICE).
