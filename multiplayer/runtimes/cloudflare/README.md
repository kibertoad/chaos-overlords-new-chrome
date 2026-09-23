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
`UPLOAD_RATE_LIMIT_PER_MINUTE`, `BUG_REPORT_RATE_LIMIT_PER_MINUTE`, `RETENTION_DAYS`,
`LOBBY_RETENTION_DAYS`, `ABANDONED_RETENTION_DAYS`, `SILENT_RETENTION_DAYS`, `RETENTION_BATCH_SIZE`,
`BUG_REPORT_RETENTION_DAYS` and `BUG_REPORT_DAILY_STATE_MB` are vars; unset, each retention window
takes the kernel's default for a shared public server (see the multiplayer README). The
`scheduled` handler expects a cron trigger; five minutes is the interval its sweeper is written for.

**A deployment without a cron trigger has no safety net.** The sweeper is what seals a turn whose
Durable Object alarm never fired, finishes a seal whose isolate died halfway through, re-runs the
verdict of a match left paused by an interrupted one, and collects retention. Nothing else does any
of it, and nothing fails loudly when it is missing: matches simply stop advancing for the players in
them. `wrangler.dev.toml` carries the trigger so `wrangler dev --test-scheduled` exercises the same
path, but it is not a deployment and triggers are not inherited — so the first thing to add to a
real one is:

```toml
[triggers]
crons = ["*/5 * * * *"]
```

The Worker logs `cron trigger has not fired` on a request once it has been up for an hour without
one, which is how a deployment that forgot finds out from its own logs rather than from a player.

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
