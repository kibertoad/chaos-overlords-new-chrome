# Chaos Overlords multiplayer server

The coordination server for online play of *Chaos Overlords: New Chrome*. It runs as a plain
Node.js process (SQLite or Postgres) for anyone hosting a game for friends, and as a Cloudflare
Worker (D1 + Durable Objects) for a central public server. Both serve the same Hono application
and pass the same conformance suite.

The game's built-in central-service choice points to
`https://chaos-overlords.dinorefurb.com`. Its Online screen calls `GET /health` when opened; a
healthy deployment answers `{"ok":true}`. Custom/self-hosted origins use the same check.

The design (why the game core stays on the clients, why REST plus server-sent events, the turn
barrier, desync recovery, the security model) lives in [`../docs/MULTIPLAYER.md`](../docs/MULTIPLAYER.md).
This file is the operator and contributor manual.

## Layout

| Package | Role |
|---|---|
| `packages/contracts` | Wire contracts: valibot schemas for every request, view and event, plus one `defineApiContract` per endpoint. The source of truth the server mounts its routes from and the C# client's records are generated from. |
| `packages/kernel` | Runtime-neutral domain: entities, storage/runtime ports, pure turn logic, the lobby/turn/snapshot services, an in-memory storage for hermetic tests. |
| `packages/storage` | Drizzle schemas and repositories. One SQLite dialect serves better-sqlite3 and D1 from the same migration lineage; Postgres has its own. |
| `packages/bug-reports` | Bug report intake, in a database of its own: schema, migration lineage, repository, blob store and service. Shares nothing with the multiplayer storage — see "Bug reports" below. |
| `packages/server` | The Hono app: routes, bearer auth, error envelope, the SSE response builder, the in-process event hub. |
| `packages/client` | TypeScript client (REST + resumable SSE iterator). The conformance suite drives every runtime through it; the game's C# client in `src/Rechaos.Multiplayer` mirrors it. |
| `scripts/generate-csharp.mjs` | Regenerates the game's C# mirror of the contracts. See "Generating the C# client" below. |
| `packages/conformance` | Storage and HTTP behaviour suites every implementation and runtime runs. |
| `runtimes/node` | Node facade: `@hono/node-server`, SQLite or Postgres, timer-based deadlines plus a sweeper. |
| `runtimes/cloudflare` | Worker facade: D1, a `MatchHub` Durable Object per match for SSE fan-out and deadline alarms, a cron sweeper. |

## Run it

Prerequisites: Node.js 22+, pnpm 11 (`corepack enable`). The workspace pins the latest supported
11.x release and explicitly allowlists the native build scripts required by its runtime adapters.

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
| `PUBLIC_LISTING` | `true` | Serve `GET /api/v1/matches` so clients can browse public lobbies. Only matches whose host chose public visibility are ever listed; `false` turns the route off entirely and the game's Browse screen finds nothing on this server. |
| `RATE_LIMIT_PER_MINUTE` | `30` | Create/join attempts per client address per minute. Every per-minute budget is at least `1`: `0` is refused at startup rather than admitting one call a minute. |
| `MEMBER_RATE_LIMIT_PER_MINUTE` | `240` | Authenticated calls per player per minute. |
| `UPLOAD_RATE_LIMIT_PER_MINUTE` | `10` | Snapshot uploads per player per minute (a snapshot can be a megabyte). |
| `RETENTION_DAYS` | `30` | Delete finished, abandoned and never-started matches older than this, with everything they own. `0` keeps every match forever. |
| `ABANDONED_RETENTION_DAYS` | `90` | Delete a still-`running` match nobody is in any more once it has been silent this long. This is how most public matches actually end, and nothing else collects one. `0` keeps them forever. |
| `TRUST_PROXY` | `0` | How many trusted proxies sit in front. `0` reads the socket address, the only value a client cannot choose. `1` (or `true`) reads the last `X-Forwarded-For` entry, which is the one the trusted proxy wrote; a higher number skips that many more from the right. See the note below. |
| `BUG_REPORT_DATABASE_URL` | *(empty, intake off)* | A **second** SQLite file, for bug reports. Empty turns the intake off and `POST /api/v1/bug-reports` answers 404. The game posts its reports to the central service, so a lobby server has no reason to take them. |
| `BUG_REPORT_RETENTION_DAYS` | `90` | Delete a bug report and its archive once it is older than this. `0` keeps them forever. |
| `BUG_REPORT_DAILY_STATE_MB` | `512` | Attached journal megabytes accepted per rolling day across every reporter. Over budget, the report is still filed and only its journal is dropped. `0` lifts the ceiling. |
| `BUG_REPORT_BLOB_DIR` | *(unset)* | Directory for compressed match journals. Unset keeps archives under 256 KiB in the database row and drops larger ones (a `sqlite::memory:` bug report database gets an in-memory store instead, since it has no file to outlive). |
| `BUG_REPORT_RATE_LIMIT_PER_MINUTE` | `5` | Bug reports accepted per client address per minute. Its own budget, not the lobby's. |
| `SWEEP_INTERVAL_MS` | `15000` | How often the safety net runs: expired turn deadlines, interrupted seals, retention (the timers are the precise path for a deadline). At least `1000`; a pass still running when the next is due is not overlapped. |
| `SHUTDOWN_GRACE_MS` | `5000` | How long open event streams may delay shutdown before they are cut. |
| `MAX_EVENT_STREAMS` | `512` | Event streams this process holds at once, across every match; further opens answer 429. A stream lives until its client closes it and costs one read per published event, so this is what stops one member from holding thousands. Raise it and the file descriptor limit together. |
| `CORS_ORIGINS` | *(none)* | Comma-separated browser origins allowed to call the API. The game is not a browser and needs none; a web front end using `@chaos-overlords/client` lists its origin here, which also permits the preflighted `Authorization` and `Last-Event-ID` headers. |
| `LOG_LEVEL` | `info` | `debug`, `info`, `warn`, `error`. |

Put TLS in front of it (Caddy, nginx, a tunnel): player tokens are bearer credentials.

### Behind a proxy

`X-Forwarded-For` is appended to, not replaced, so everything left of the last entry is whatever
the client sent. `TRUST_PROXY` is therefore a count of the proxies in front rather than a yes or no,
and the server reads the chain from the right: with `1` it takes the entry the single trusted proxy
wrote and ignores the rest. Set it too high and the server reads an address the client chose, at
which point the rate limits stop binding — they are the only guard on the join-code door, the
password door and multi-megabyte bug-report uploads.

Set it too low — the usual mistake is leaving it at `0` behind Caddy or nginx — and every player
shares the proxy's own address and with it one budget: one stranger's thirty bad tokens lock
everybody out of create and join for a minute. The server logs a warning the first time a
forwarded header arrives while `TRUST_PROXY` is unset.

Configure the proxy to overwrite or strip `X-Forwarded-For` and `CF-Connecting-IP` on the way in if
it can. `CF-Connecting-IP` is trusted only by the Cloudflare runtime, where Cloudflare sets it;
behind anything else it is a header the client fills in itself and this server ignores it.

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

The Worker is published as `@chaos-overlords/worker` and deployed from a separate, private
deployments repository — which account it runs in, what its databases are called and how its vars are
tuned are that repository's business, not this one's. What this package defines is the interface a
deployment has to satisfy:

| Binding | Kind | Purpose |
|---|---|---|
| `DB` | D1 | Matches, players, turns, snapshots. Migrate from `packages/storage/migrations/sqlite` — the same lineage better-sqlite3 runs. |
| `BUG_DB` | D1 | Bug reports, from `packages/bug-reports/migrations/sqlite`. Its own database; see "Bug reports" below. Leave it unbound and `POST /api/v1/bug-reports` answers 404. |
| `BUG_BLOBS` | R2 | Compressed match journals. Leave it unbound and only journals under 256 KiB are kept. |
| `MATCH_HUB` | Durable Object | `MatchHub`, one per match: SSE fan-out and the turn deadline alarm. Its migration lineage starts at tag `v1`, `new_sqlite_classes = ["MatchHub"]`. |

`PUBLIC_LISTING`, `RATE_LIMIT_PER_MINUTE`, `MEMBER_RATE_LIMIT_PER_MINUTE`,
`UPLOAD_RATE_LIMIT_PER_MINUTE`, `BUG_REPORT_RATE_LIMIT_PER_MINUTE`, `RETENTION_DAYS`,
`ABANDONED_RETENTION_DAYS`, `BUG_REPORT_RETENTION_DAYS` and `BUG_REPORT_DAILY_STATE_MB` are vars,
with the same meanings as the Node environment variables above. A deployment also wants the cron
trigger the `scheduled` handler expects — five minutes is the interval the sweeper is written for —
and Cloudflare rate limiting rules on `/api/v1/matches`, `/api/v1/matches/join` and
`/api/v1/bug-reports`: the in-Worker limiter counts per isolate, so it softens abuse on one edge node
rather than globally.

For local work, `runtimes/cloudflare/wrangler.dev.toml` binds all four to throwaway local resources.
It is a development and test fixture, not a deployment.

```sh
cd runtimes/cloudflare
pnpm db:migrate:local
pnpm db:migrate:bugs:local
pnpm dev
```

## Bug reports

`POST /api/v1/bug-reports` takes what a player typed in the game's Escape menu and, if they left the
box ticked, the whole match as a compressed event-sourced journal that replays from its first turn.
The game posts to the official central-service address (`BugReportEndpoint` in
`src/Rechaos.Multiplayer/Http/BugReportSubmitter.cs`) rather than to whichever lobby a player is in:
a report goes to the people who maintain the game, and somebody self-hosting a lobby for three
friends is not them.

Three things about it are deliberate.

**It is unauthenticated.** A report is not a match and the player filing one has no seat. Requiring a
token would silence exactly the reports worth having most — the ones from a player who could not get
into a match at all. What stands in for it is a per-address budget far below the lobby's, and a body
limit sized for a compressed journal and nothing larger.

**Its database is separate.** A second D1 instance, a second SQLite file, its own migration lineage
under `packages/bug-reports/migrations/sqlite`. Reports arrive unauthenticated, outlive every match
they describe, and carry another player's game journal; they share no schema, no lock, no retention
sweep and no blast radius with the database holding live matches. Nothing in `Kernel` can reach
`BUG_DB` and nothing in the intake can reach `DB`.

**The journals are not in it.** A compressed whole-match journal is hundreds of kilobytes to a few
megabytes; D1 refuses a row over 2 MB and even the ones that fit would be dragged through every
triage query. With `BUG_BLOBS` (or `BUG_REPORT_BLOB_DIR` on Node) bound, every archive goes there and
the row keeps the key, the digest and the size. Without one, an archive under 256 KiB is kept inline
— which is what makes an unconfigured `node-server` work end to end — and a larger one is dropped
with the report still accepted and the receipt saying `omitted`.

The server never decompresses or parses an archive. It checks the SHA-256 the client computed over
the compressed bytes, so a truncated upload is refused rather than filed, and stores opaque bytes;
that is what makes taking one from a stranger safe. The archive format (`RCHJ`, Brotli, a reserved
codec byte for zstd) lives with the game in `src/Rechaos.Core/Persistence/ReplayArchive.cs`, and
`docs/DECISIONS.md` records why Brotli.

To read one back, `BugReportService` has `list`, `get` and `archive`; none of them are routes, so
triage is a script or a console against the deployment rather than an endpoint anyone can call.

## Publishing

Every package here is published to npm under `@chaos-overlords/`, GPL-3.0-only, so the server can be
self-hosted (`npx @chaos-overlords/node-server`) and deployed from elsewhere — a Cloudflare
deployment consumes `@chaos-overlords/worker` and the two migration lineages as ordinary
dependencies rather than as a checkout of this repository.

They share one release version. `workspace:*` is what the packages depend on each other by, and pnpm
rewrites it to that exact version as it packs, so every package must be bumped together. The release
workflow does that in its checkout and then runs `pnpm check-versions` before it builds anything;
release commits do not need manual package-version edits.

Release from *Actions → Publish multiplayer packages → Run workflow*, which runs
[`.github/workflows/multiplayer-publish.yml`](../.github/workflows/multiplayer-publish.yml). Enter the
version and choose a mode:

| Mode | What it does |
| --- | --- |
| `rehearse` (default) | Lints, builds, typechecks, tests, checks the C# codegen, and packs every tarball, then stops without contacting the registry. |
| `release` | The same checks, then publishes all nine packages, then tags the commit it published `multiplayer-v0.2.0`. |

Both cut from the tip of main, pin that commit, and apply the requested version to all nine package
manifests in the workflow checkout. The checks, tarballs, and tag therefore describe one commit even
if someone pushes to main mid-run. A release refuses to start if its tag already exists. The tag is
created last because it is the half that is cheap to redo by hand.

Pushing a `multiplayer-v*` tag still publishes, for a release that has to come from a commit other
than the tip of main:

```sh
git tag multiplayer-v0.2.0 && git push origin multiplayer-v0.2.0
```

Either way the workflow authenticates with **npm OIDC trusted publishing**: the job trades its
GitHub-issued `id-token` for a short-lived registry credential, so there is no npm token in this
repository to leak or rotate, and every tarball carries a provenance attestation. Each package has to
name the workflow as a trusted publisher on npmjs.com first (*Settings → Trusted publishers*: this
repository, workflow `multiplayer-publish.yml`), and the very first release of a new package name has
to be pushed by hand — a package that does not exist yet cannot have a trusted publisher. That
binding is to the workflow's filename, which is why releasing lives in the publishing workflow rather
than a second one that drives it.

`pnpm publish:dry-run` does the pack locally without a registry, and prints what each tarball would
contain. What goes into a tarball is `files` in each manifest; `prepublishOnly` builds the package,
copies the repository's `LICENSE` and `NOTICE` into it (npm ships one tarball per package, so each
needs its own), and fails the publish if anything `files` promises is missing.

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
