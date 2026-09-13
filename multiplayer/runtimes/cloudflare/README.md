# @chaos-overlords/worker

Cloudflare Worker for the [*Chaos Overlords: New Chrome*](https://github.com/kibertoad/chaos-overlords-new-chrome) multiplayer server.

The Cloudflare Workers facade: the shared Hono app over D1, with one `MatchHub` Durable Object
per match for SSE fan-out and turn-deadline alarms, and a cron sweeper.

This package ships TypeScript sources rather than a bundle, because wrangler bundles the entry
itself. A deployment re-exports both the handler and the Durable Object class:

```ts
export { MatchHub } from '@chaos-overlords/worker'
export { default } from '@chaos-overlords/worker'
```

It needs two D1 bindings (`DB`, `BUG_DB`), an R2 bucket (`BUG_BLOBS`) and the `MATCH_HUB` Durable
Object namespace, whose migration lineage starts at tag `v1` with `new_sqlite_classes = ["MatchHub"]`.
The D1 migration lineages ship in `@chaos-overlords/storage` and `@chaos-overlords/bug-reports`; point
`wrangler d1 migrations apply` at them rather than copying them.

`PUBLIC_LISTING`, `RATE_LIMIT_PER_MINUTE`, `MEMBER_RATE_LIMIT_PER_MINUTE`,
`UPLOAD_RATE_LIMIT_PER_MINUTE`, `BUG_REPORT_RATE_LIMIT_PER_MINUTE` and `RETENTION_DAYS` are vars. The
`scheduled` handler expects a cron trigger; five minutes is the interval its sweeper is written for.

## Install

```sh
npm install @chaos-overlords/worker
```

Requires Node.js 22 or newer.

## Documentation

The operator and contributor manual for the whole multiplayer workspace is in
[`multiplayer/README.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/multiplayer/README.md); the design notes are in
[`docs/MULTIPLAYER.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/docs/MULTIPLAYER.md).

## Licence

GPL-3.0-only. See [`LICENSE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/LICENSE) and [`NOTICE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/NOTICE).
