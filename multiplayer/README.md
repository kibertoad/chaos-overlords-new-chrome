# Chaos Overlords multiplayer server

The coordination server for online play of *Chaos Overlords: New Chrome*. It runs as a plain
Node.js process (SQLite or Postgres) for anyone hosting a game for friends, and as a Cloudflare
Worker (D1 + Durable Objects) for a central public server. Both serve the same Hono application
and pass the same conformance suite.

The design (why the game core stays on the clients, why REST plus server-sent events, the turn
barrier, desync recovery, the security model) lives in [`../docs/MULTIPLAYER.md`](../docs/MULTIPLAYER.md).
This file is the operator and contributor manual.

## Layout

| Package | Role |
|---|---|
| `packages/contracts` | Wire contracts: valibot schemas for every request, view and event, plus one `defineApiContract` per endpoint. The source of truth the server mounts its routes from and the C# client's records are generated from. |
| `packages/kernel` | Runtime-neutral domain: entities, storage/runtime ports, pure turn logic, the lobby/turn/snapshot services, an in-memory storage for hermetic tests. |
| `packages/storage` | Drizzle schemas and repositories. One SQLite dialect serves better-sqlite3 and D1 from the same migration lineage; Postgres has its own. |
| `packages/server` | The Hono app: routes, bearer auth, error envelope, the SSE response builder, the in-process event hub. |
| `packages/client` | TypeScript client (REST + resumable SSE iterator). The conformance suite drives every runtime through it; the game's C# client in `src/Rechaos.Multiplayer` mirrors it. |
| `scripts/generate-csharp.mjs` | Regenerates the game's C# mirror of the contracts. See "Generating the C# client" below. |
| `packages/conformance` | Storage and HTTP behaviour suites every implementation and runtime runs. |
| `runtimes/node` | Node facade: `@hono/node-server`, SQLite or Postgres, timer-based deadlines plus a sweeper. |
| `runtimes/cloudflare` | Worker facade: D1, a `MatchHub` Durable Object per match for SSE fan-out and deadline alarms, a cron sweeper. |

## Run it

Prerequisites: Node.js 22+, pnpm 10 (`corepack enable`).

```sh
cd multiplayer
pnpm install
pnpm build
```

### Self-hosted (Node.js)

With no configuration the server listens on `0.0.0.0:8787` and keeps its state in
`./chaos-overlords.db` (SQLite, WAL mode). Migrations run at boot.

```sh
pnpm --filter @chaos-overlords/node-server start
```

For Postgres, start the bundled compose stack and point the server at it:

```sh
docker compose up -d
DATABASE_URL=postgres://chaos:chaos@localhost:5432/chaos pnpm --filter @chaos-overlords/node-server start
```

| Variable | Default | Meaning |
|---|---|---|
| `PORT`, `HOST` | `8787`, `0.0.0.0` | Listen address. |
| `DATABASE_URL` | `sqlite:./chaos-overlords.db` | `sqlite:<path>`, `sqlite::memory:`, or `postgres://…`. |
| `PUBLIC_LISTING` | `false` | Serve `GET /api/v1/matches` so clients can browse public lobbies. |
| `RATE_LIMIT_PER_MINUTE` | `30` | Create/join attempts per client address per minute. |
| `MEMBER_RATE_LIMIT_PER_MINUTE` | `240` | Authenticated calls per player per minute. |
| `UPLOAD_RATE_LIMIT_PER_MINUTE` | `10` | Snapshot uploads per player per minute (a snapshot can be a megabyte). |
| `RETENTION_DAYS` | `30` | Delete finished, abandoned and never-started matches older than this, with everything they own. `0` keeps every match forever. |
| `TRUST_PROXY` | `false` | Read the client address from `X-Forwarded-For` / `CF-Connecting-IP`. Set it behind a reverse proxy, never otherwise. |
| `SWEEP_INTERVAL_MS` | `15000` | How often the safety net runs: expired turn deadlines, interrupted seals, retention (the timers are the precise path for a deadline). |
| `SHUTDOWN_GRACE_MS` | `5000` | How long open event streams may delay shutdown before they are cut. |
| `LOG_LEVEL` | `info` | `debug`, `info`, `warn`, `error`. |

Put TLS in front of it (Caddy, nginx, a tunnel): player tokens are bearer credentials.

## Generating the C# client

The game is a .NET client of this server, so its wire types are derived from these schemas rather
than typed a second time:

```sh
pnpm codegen        # rewrite src/Rechaos.Multiplayer/Generated/*.cs
pnpm codegen:check  # fail if the committed files no longer match the schemas
```

`WireContracts.cs` comes from the valibot schemas through
[`@game-infra/valibot-to-csharp`](https://www.npmjs.com/package/@game-infra/valibot-to-csharp);
`RouteTemplates.cs` comes from the endpoint contracts through the same `mapApiContractToPath` the
Hono routes are derived from. Both are committed, so building the game never needs Node.

The generator is fetched on demand rather than declared as a dependency — nothing about installing,
building or testing this workspace should wait on a maintenance tool. To run an unpublished one out
of a sibling `game-infra` checkout:

```sh
pnpm codegen --generator "npx tsx ../game-infra/packages/valibot-to-csharp/src/cli.ts"
```

Run `pnpm codegen` after changing anything under `packages/contracts/src`, and commit what it
writes; CI runs `pnpm codegen:check` and fails if you did not.

The generator is pinned to an exact version rather than a range, because this script both writes the
committed output and checks it: under a range, a generator release would turn CI red on whatever
unrelated pull request was open that day. Adopting a new one is a deliberate commit — bump the
version in `scripts/generate-csharp.mjs`, run `pnpm codegen`, and the diff says what changed.

### Central server (Cloudflare)

```sh
cd runtimes/cloudflare
wrangler d1 create chaos_overlords          # paste the id into wrangler.toml
pnpm db:migrate:remote
wrangler deploy
```

`wrangler.toml` declares the D1 binding, the `MatchHub` Durable Object and a five-minute cron. The
same `PUBLIC_LISTING`, `RATE_LIMIT_PER_MINUTE`, `MEMBER_RATE_LIMIT_PER_MINUTE`,
`UPLOAD_RATE_LIMIT_PER_MINUTE` and `RETENTION_DAYS` knobs are `[vars]` there. The in-Worker rate
limiter counts per isolate, so it softens abuse on one edge node rather than globally; add a
Cloudflare rate limiting rule on `/api/v1/matches` and `/api/v1/matches/join` for the real gate.

## Develop

```sh
pnpm lint                 # biome
pnpm typecheck
pnpm test                 # every package; Postgres lanes run only with TEST_DATABASE_URL set
TEST_DATABASE_URL=postgres://chaos:chaos@localhost:5432/chaos pnpm exec turbo run test:run --env-mode=loose
pnpm db:generate          # regenerate migrations after a schema change (both dialects)
```

The Cloudflare suite runs inside workerd with a real local D1 and the real Durable Object
(`@cloudflare/vitest-pool-workers`). The Node facade takes an injected clock in tests, so the
deadline route — expired turn, sweep, seal, next turn — runs over real HTTP against both SQLite and
Postgres; in workerd time cannot be moved, so there the deadline is covered by the alarm-arming test
and by `listExpiredOpen` in the D1 storage lane instead.

Rules that keep the two runtimes honest:

- A storage method is added to the kernel port, both repository files, and the storage
  conformance suite in the same change.
- Anything the Node facade wires (a scheduler, a notifier) has a Worker twin, asserted by the HTTP
  conformance suite running on both.
- **Run one process.** Events fan out in memory, so a second instance behind a load balancer would
  wake only its own subscribers and a client could sit silent through everything the other instance
  wrote — with no error to show for it. Postgres is for durability and familiar operations, not for
  scaling out; see "Limitations and next steps" in `docs/MULTIPLAYER.md`.
- No transactions: D1 has none. Every race is a single conditional statement whose row count says
  who won (see the port comments in `packages/kernel/src/ports/storage.ts`).
- A write a unique index can refuse returns `false` instead of throwing. Driver error shapes are
  interpreted in exactly one place, `packages/storage/src/shared/constraints.ts`.
- The in-memory reference storage runs the storage conformance suite too, so the fast kernel and HTTP
  tests cannot be passing against semantics the SQL repositories do not have.
