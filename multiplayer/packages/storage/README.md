# @chaos-overlords/storage

Storage adapters for the [*Chaos Overlords: New Chrome*](https://github.com/kibertoad/chaos-overlords-new-chrome) multiplayer server.

Drizzle schemas and repository implementations of the kernel's storage ports. One SQLite
dialect serves both better-sqlite3 and Cloudflare D1 from a single migration lineage; Postgres has
its own.

```ts
import { createSqliteStorage } from '@chaos-overlords/storage/sqlite'
import { createPostgresStorage } from '@chaos-overlords/storage/postgres'
```

The migration lineages ship in the package, under `migrations/sqlite` and `migrations/postgres`.
Point `wrangler d1 migrations apply` or the Node migrator at them rather than copying them.

`better-sqlite3` and `pg` are optional peers: install the one your dialect needs.

## Install

```sh
npm install @chaos-overlords/storage
```

Requires Node.js 22 or newer.

## Documentation

The operator and contributor manual for the whole multiplayer workspace is in
[`multiplayer/README.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/multiplayer/README.md); the design notes are in
[`docs/MULTIPLAYER.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/docs/MULTIPLAYER.md).

## Licence

GPL-3.0-only. See [`LICENSE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/LICENSE) and [`NOTICE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/NOTICE).
