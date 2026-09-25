# @chaos-overlords/bug-reports

Bug report intake for the [*Chaos Overlords: New Chrome*](https://github.com/kibertoad/chaos-overlords-new-chrome) multiplayer server.

Bug report intake in a database of its own: schema, migration lineage, repository, blob store
and service. It deliberately shares nothing with the multiplayer storage — reports arrive
unauthenticated, outlive the matches they describe, and carry other players' game journals.

The migration lineage ships under `migrations/sqlite`. `better-sqlite3` is an optional peer.

## Install

```sh
npm install @chaos-overlords/bug-reports
```

Requires Node.js 22 or newer.

## Documentation

The operator and contributor manual for the whole multiplayer workspace is in
[`multiplayer/README.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/multiplayer/README.md); the design notes are in
[`docs/MULTIPLAYER.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/docs/MULTIPLAYER.md).

## Licence

MIT. See [`LICENSE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/LICENSE) and [`NOTICE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/NOTICE).
