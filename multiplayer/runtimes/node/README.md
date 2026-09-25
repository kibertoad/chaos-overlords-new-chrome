# @chaos-overlords/node-server

Node.js server for the [*Chaos Overlords: New Chrome*](https://github.com/kibertoad/chaos-overlords-new-chrome) multiplayer server.

A ready-to-run multiplayer server: the shared Hono app over `@hono/node-server`, backed by
SQLite or Postgres, with an in-process event hub and timer-driven turn deadlines. Migrations run at
boot.

```sh
npx @chaos-overlords/node-server
```

With no configuration it listens on `0.0.0.0:8787` and keeps its state in `./chaos-overlords.db`.
Set `DATABASE_URL` to point it at Postgres. The full environment variable table is in the
[workspace manual](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/multiplayer/README.md).

Put TLS in front of it: player tokens are bearer credentials.

## Install

```sh
npm install @chaos-overlords/node-server
```

Requires Node.js 22 or newer.

## Documentation

The operator and contributor manual for the whole multiplayer workspace is in
[`multiplayer/README.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/multiplayer/README.md); the design notes are in
[`docs/MULTIPLAYER.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/docs/MULTIPLAYER.md).

## Licence

MIT. See [`LICENSE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/LICENSE) and [`NOTICE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/NOTICE).
