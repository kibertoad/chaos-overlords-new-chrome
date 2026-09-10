# Multiplayer

Status: implemented server, game client wired
Last updated: 2026-09-10

Online play for *Chaos Overlords: New Chrome* runs through a coordination server that any player
can host and that can also run as a central public service. The server code lives under
[`multiplayer/`](../multiplayer/README.md). Nothing about the 1996 protocol is reproduced; this is
a new design over HTTP.

## What the server is, and is not

The game is a simultaneous-turn strategy game: every player issues orders during the Command phase,
and when everyone is done (or the turn timer expires) all orders resolve together. `Rechaos.Core`
already resolves a turn deterministically from queued commands and a seeded PRNG, records every
authoritative operation for replay, and hashes the state after each transition.

The server therefore does not simulate. It is the **turn barrier and the source of truth for what
was ordered**:

1. It holds each player's orders privately until the turn seals.
2. It seals the turn exactly once, when every active player is ready or the deadline passes, and
   publishes the complete, slot-ordered order set with a digest.
3. Every client applies the same sealed set to the same deterministic core and reports the
   resulting state hash. The server confirms the turn on agreement and flags a desync otherwise.
4. On a desync the host uploads a native snapshot; the server judges further reports against it,
   so every client converges on one state before the next turn can seal.

Empty slots and departed players are computer players, planned by the deterministic AI on every
client identically, so their orders never cross the wire. The match seed and the slot assignment
come from the server at start, so every client bootstraps the same city.

This is the classic deterministic-lockstep model of turn-based strategy games. Its cost is
stated in the security section: a modified client can read hidden state. Its benefits are that the
server is tiny, cheap to host, has no game rules to keep in step with the C# core, and that any
divergence is detected the turn it happens.

## Transport: REST for intents, server-sent events for the log

The traffic pattern decides it. A player sends a handful of intents per turn (orders, ready, a
report), each of which is a state transition the server must validate and answer. The server sends
a low-rate, strictly ordered stream of facts ("Grace is ready", "turn 7 sealed", "turn 6
confirmed"). Nothing is latency-critical below a second.

| Option | Verdict |
|---|---|
| **REST + SSE** (chosen) | Intents get HTTP semantics for free: bearer auth, idempotent retries, `413`/`429`, immutable caching of a sealed order set. The event log is exactly what SSE models: ordered, resumable with `Last-Event-ID`, one-directional, over plain HTTP/1.1 or HTTP/2 through any proxy or tunnel a self-hoster already has. Works on Node and on Workers with the same code. |
| WebSocket | Bidirectional and lower overhead per message, neither of which this traffic needs. It brings a bespoke resume protocol, ping/pong, and on Cloudflare a hibernation dance; behind reverse proxies it is the thing that breaks. Reasonable later for lobby chat, never required for turns. |
| gRPC | No Workers support (HTTP/2 trailers), no browser path without grpc-web, and a code generator on the C# side for a dozen calls. |
| Polling | Kept as the fallback, not the design: `GET /events?after=N` reads the same log the stream serves, for networks that cannot hold a streaming response. |

The event log is persisted per match, and each event's sequence number is allocated **by its own
insert** (`max(seq) + 1` for that match, with the primary key settling a race). That matters more
than it looks: the number and the row become visible together, so a committed `seq` guarantees every
lower one is committed too. A counter handed out before the write could leave a hole that a cursor
moving forward would skip forever.

The fan-out (an in-process hub on Node, a per-match Durable Object on Cloudflare) is only a wake-up
hint: every wake drains the log from the last delivered sequence, so a lost notification costs
latency, never an event, and a reconnecting client resumes from the last `seq` it saw. Delivery is
therefore at least once — a resume, or a seal finished by the repair sweep, can repeat a fact a
client already holds — so every client handler must be idempotent. Events name facts and carry
references, not payloads: a sealed order set is fetched once over REST with `Cache-Control:
immutable`.

## Protocol

All paths are under `/api/v1`. Bodies are JSON; the schemas are in
`multiplayer/packages/contracts`. Authenticated calls send `Authorization: Bearer <token>`.

Each endpoint is one `defineApiContract` in `packages/contracts/src/contracts.ts`, carrying its
method, its path, the schema of every request target and the shape of every response. That is the
only statement of it anywhere: the server mounts those contracts through `@toad-contracts/hono`, so
a handler reads `c.req.valid(...)` rather than parsing again; the TypeScript client builds its URLs
from the same `pathResolver` the route pattern is derived from; and the C# client's records are
generated from the same valibot schemas (see "Two languages, one contract").

### Lobby

| Call | Who | Effect |
|---|---|---|
| `POST /matches` | anyone | Creates a lobby. Returns the host's token, the 8-character join code and the match view. `settings.gameSettings` is an opaque object the server stores for clients (scenario, portraits, difficulty); the server reads only `name`, `maxPlayers`, `turnTimerSeconds`, `visibility`. An optional `password` gates joining. |
| `GET /matches` | anyone | Public lobbies, when the server enables listing. |
| `POST /matches/join` | anyone | Joins by code (and password). Returns that player's token. Capacity is a single atomic seat claim. |
| `GET /matches/:id` | member | Match view: players, current and previous turn (who is ready, who reported), status, seed. |
| `POST /matches/:id/start` | host | Seats players (host slot 0, then join order), draws the seed, opens turn 1. |
| `POST /matches/:id/leave` | member | In the lobby: frees the seat (the host leaving abandons the lobby). Running: the slot becomes a computer player from the next turn; a leaving host hands the role to the lowest active slot. The token is revoked, so a departed player keeps no read access either. |
| `POST /matches/:id/players/:pid/kick` | host | Same as the target leaving. |

### Turn barrier

| Call | Who | Effect |
|---|---|---|
| `PUT /matches/:id/turns/:n/orders` | member | Replaces the caller's order document for the open turn and sets `ready`. The write is one statement conditional on the turn still being open, so an order landing after the seal is refused (`409 turn_not_open`), never silently folded in. When `ready` completes the roster, the turn seals in the same call. |
| `GET /matches/:id/turns/:n/orders/mine` | member | The caller's own submission (for a reconnecting client). |
| `GET /matches/:id/turns/:n/orders` | member | The sealed set: the documents of the players the seal froze, in slot order, plus `orderSetHash`. Refused while open (`409 turn_open`). |
| `POST /matches/:id/turns/:n/report` | member | `{ stateHash, finished }` after applying the sealed turn locally. |
| `POST /matches/:id/snapshots` | host | A base64 native snapshot for a sealed turn, with its `stateHash`. |
| `GET /matches/:id/snapshots/latest`, `/:turn` | member | Snapshot bodies for resync or reconnect. |
| `GET /matches/:id/events?after=N` | member | The log, paged. |
| `GET /matches/:id/stream` | member | The same log as SSE; `Last-Event-ID` or `?after=` resumes. |

The order document is `{ schemaVersion: 1, ops: [...] }`, where each op is one of the five
operations the core's replay recorder accepts as player intent, in the core's own vocabulary:

```jsonc
{ "op": "submitCommand", "player": 0, "gang": 12, "action": 10,
  "target": { "kind": "sector", "id": 27 }, "repeat": false, "secondaryTarget": null }
{ "op": "cancelCommand", "player": 0, "gang": 12 }
{ "op": "queueHire", "player": 0, "gangDefinitionId": 44, "sectorId": 27 }
{ "op": "snubHireOffer", "player": 0, "gangDefinitionId": 44 }
{ "op": "dismissNotification", "player": 0 }
```

These mirror `MatchReplayRecorder.Submit` / `Cancel` / `QueueHire` / `SnubHireOffer` /
`TryDismissNotification`. The phase transitions (`FinishCommand` and the rest) and the `Prepare*`
steps are driven by the turn structure on every client and are refused over the wire. Every id is
bounded by the capacity it indexes and every op must name the submitter's own slot; unknown ops and
unknown fields are refused. See "What the server does and does not defend against" for why this
stops short of judging legality, which stays with the core on each client while it applies the
sealed set, exactly as a replay is verified.

Numbers are **safe integers only**, and `-0` is refused. The order digest is SHA-256 over
canonical JSON, so a client in another language has to reproduce that text byte for byte, and a
float's shortest round-trip spelling is not portable (`1e+21` from JavaScript, `1E+21` from .NET).
Canonical JSON is: keys sorted by UTF-16 code unit (.NET's `StringComparer.Ordinal`), no whitespace,
standard JSON string escaping, integers written plainly. `packages/kernel/test/logic.spec.ts` pins a
golden document, its canonical text and its digest for the C# side to match.

### Turn lifecycle

```text
open ──(all ready | deadline)──> sealed ──(unanimous reports)──> confirmed
                                   │
                                   └──(reports disagree)──> desynced ──(reports match host snapshot)──> confirmed
```

Sealing opens the next turn immediately, so players plan turn n+1 while reports for turn n arrive.
The seal also **freezes its participant set** on the turn row, beside the digest taken over it. The
set a client fetches is therefore always the set the digest was computed from: a player who left
after submitting but before the seal is absent from both (their slot becomes a computer player),
and one who leaves after the seal stays in both.

A desync pauses the match (`match.status = desynced`): the open turn stays open but cannot seal
until every unsettled turn is confirmed. The host uploads the snapshot of the disputed turn;
clients load it, re-report, and the match resumes. Because orders are refused for the whole pause,
the open turn's clock **restarts** when the match resumes — otherwise a pause longer than the timer
would seal the next turn empty the moment it lifted — and `turn.deadlineExtended` announces the new
deadline. Once every active player reports `finished`, the match is finished.

Every transition is a compare-and-swap on the row's status. The last player's ready racing the
timer, two reports landing together, an alarm firing twice: each produces exactly one seal and one
verdict. There are no transactions anywhere, because D1 offers none. Sealing is consequently a
compare-and-swap followed by steps that are each conditional on the last, so a process that dies
mid-seal leaves a state the sweep can finish rather than a match with nothing to play: it looks for
a live match whose current turn is no longer open and completes it.

### Timer

`turnTimerSeconds` (0, or 30 to 86400) puts a `deadlineAt` on every opened turn. Node arms a timer
per open turn; Cloudflare sets a Durable Object alarm. Both runtimes also sweep the table for
expired open turns (a 15-second interval on Node, a cron on Cloudflare) so a lost timer costs at
most that interval. Sealing on the deadline includes whatever each player last submitted; a player
who submitted nothing contributes no orders.

## Retention

A coordination server accumulates rows with no second use: a finished match's order documents, a
megabyte of snapshot per desync, the lobby someone opened and never started. Matches in a terminal
or never-started state (`finished`, `abandoned`, `lobby`) are deleted once they have been untouched
for `RETENTION_DAYS` (30 by default, `0` to keep everything), with everything they own — every child
table cascades from the match row. A running or desynced match is never in scope at any age: a
desync pause is not abandonment.

## Security model

- **Identity is a capability token.** Create and join answer a 256-bit random bearer token that
  is shown once; the server stores its SHA-256 and looks it up by hash. There are no accounts,
  which is what makes self-hosting a one-command affair. A token is scoped to one player in one
  match; using it against another match answers 404, never 403, so match ids cannot be probed.
  Leaving or being kicked **revokes** it (the stored hash is cleared), so a departed player loses the
  event stream and the sealed order sets of later turns, not merely the right to act.
- **Secrecy of orders until the seal** is the property the design guarantees: nobody, the host
  included, can read another player's plan before the turn seals. There is no commit-reveal
  protocol because the server is the trusted holder; a self-hosted server is trusted by whoever
  chose to play on it.
- **Join codes** are 8 characters drawn uniformly (by rejection sampling, not a biased `%`) from a
  31-glyph alphabet, about 40 bits; an optional PBKDF2 hashed password gates the lobby.
- **Rate limits** come in three tiers: the unauthenticated doors per client address, every
  authenticated call per player, and snapshot uploads per player on a tighter budget, because a
  member is a cost too — order documents are a quarter of a megabyte and snapshots four times that.
  The windows are per process, which is what a self-hosted server needs; a public deployment puts its
  platform's rate limiting in front as the real gate.
- **Bounded input everywhere**: body limits per route, a bounded op count, an opaque settings blob
  capped at 8 KiB and bounded in nesting depth as well as bytes, snapshots capped at 1 MiB of
  base64 whose alphabet, padding and length are checked even though the server never decodes them.
  Enums persisted as text are narrowed by the only writer, the service layer.
- **Integrity of the sealed set**: `orderSetHash` is SHA-256 over `slot:ordersHash` lines and
  each `ordersHash` is SHA-256 over the canonical JSON of that player's document, so a client can
  verify what it fetched against the digest that was announced on the stream.
- **Recovery cannot be dictated by one player.** A snapshot that repairs a desynced turn becomes
  the hash every other client is told to converge on, so the host may only claim a hash that the
  players themselves already reported in the greatest number. Without that, a host could desync
  deliberately and upload a doctored state as the new truth. A genuine tie — above all the 1-1
  split of a two-player match — leaves nothing to count and the host breaks it; three or more is
  where this bites, and consistency is the goal, so converging on the majority is right even when
  the host's own client happens to be the correct one.
- **What lockstep does not protect**: every client holds the full game state, so a modified
  client can reveal hidden gangs or peek at fog it should not see. The hash consensus catches any
  client that *changes* the outcome, not one that merely reads. Nor does it attribute blame: a
  client that diverges deliberately can grief a match by desyncing it every turn, and the remedy is
  social — `turn.desynced` names every player's hash and the candidates, so the host can see who is
  the odd one out and kick them. Moving resolution server-side (a WebAssembly build of
  `Rechaos.Core` behind a `TurnResolver` port) would close both gaps and is the one design change
  this layout leaves room for; the wire protocol would not change.

## What the server does and does not defend against

The server does not simulate, so the line between what it can check and what it cannot is worth
stating exactly. `packages/contracts/src/orders.ts` is the enforcement point.

**Checked on every submission, by the one party all clients trust:**

- **The op vocabulary is closed.** An order document may only contain the five operations the game
  core records as player intent — `submitCommand`, `cancelCommand`, `queueHire`, `snubHireOffer`,
  `dismissNotification`. Phase transitions and the `Prepare*` steps are driven by the turn structure
  on every client and are refused over the wire; so is any op name the game does not have.
- **Every field is present, typed, and in range.** A sector is 0..63, a site 0..191, an item 0..63,
  a player 0..5, a gang action 0..14, a gang definition a signed 16-bit id — the capacities of
  `MatchLimits` and the C# types of `Rechaos.Core`. Unknown fields are refused rather than ignored,
  so nothing can be smuggled past a client that reads more of the document than it should.
- **Ops must act for the submitter's own slot.** The sealed set attributes orders to the slot they
  were submitted from; an op naming another player is refused, so the two attributions can never
  disagree and a client that trusts the `player` field cannot be steered by a peer.
- **Numbers must be portable.** Only safe integers, and never `-0`: the digest is taken over
  canonical JSON, and a value with no portable text form is a digest no C# client could reproduce.

**Not checked, because it is the rules and the rules are not here:** whether the player owns that
gang, can afford that hire, or may reach that sector. Each client judges legality while applying the
sealed set, through the same validator the replay reader uses, and a client that resolves differently
shows up as a desync. What the checks above buy is that the document reaching that point is always a
*representable* move — an out-of-range id or an unknown op can never crash or diverge a peer, and a
desync therefore means a genuine disagreement about the rules rather than malformed input.

## Two languages, one contract

The server is TypeScript and the game is .NET, so one of the two mirrors of every wire type has to
be derived from the other rather than typed twice. `pnpm codegen` in `multiplayer/` is that
derivation: [`@game-infra/valibot-to-csharp`](https://www.npmjs.com/package/@game-infra/valibot-to-csharp)
walks the valibot schemas and emits `src/Rechaos.Multiplayer/Generated/WireContracts.cs`, whose
records deserialize the same JSON; a second pass reads the endpoint contracts and emits
`RouteTemplates.cs`, which `MultiplayerApiRouteTests` holds the C# client's paths to. Both files are
committed, so building the game never needs Node, and CI runs `pnpm codegen:check` to fail if either
has drifted from the schemas.

Three things the schemas say exist for that crossing:

- **Every integer states its bounds.** A bound is what lets the generated C# hold a value in an
  `int` instead of a `long` or a `double`; JavaScript integers run to 2^53, so an unbounded one has
  no narrower type that could not refuse a legal value. The ceiling on turn numbers, sequence
  numbers and format versions is `int.MaxValue`, which is the bound the client already had.
- **No number is ever a float.** The order digest is taken over canonical JSON, and a float's
  shortest round-trip spelling is not portable: JavaScript writes `1e+21` where .NET writes `1E+21`.
- **`optional` and `nullable` are different.** The first says a key may be absent, the second that a
  value may be `null`, and a request that writes `null` where only absence is accepted is refused.
  The generated C# omits an optional field rather than writing a null for it.

What the generator cannot mirror, `Rechaos.Multiplayer` writes by hand and pins with a test:
canonical JSON. `packages/kernel/test/logic.spec.ts` and `MultiplayerCanonicalJsonTests` hold the
same golden document, the same canonical text and the same digest, on both sides of the wire.

## Client integration contract

What the C# client has to do. `multiplayer/packages/client` is the reference and
`src/Rechaos.Multiplayer` is the implementation:

1. Create or join, keep the token and the last event `seq`; open the stream with `Last-Event-ID`.
   Events are at least once: ignore one for a turn already applied, and treat the match view as the
   authority when the two disagree.
2. On `match.started`, build the match through `OriginalMatchFactory` from `seed` and the seated
   players (slot → human, the rest computer) using the stored `gameSettings`. The seed is a
   **signed 32-bit integer**, drawn to fit `MatchSetup.InitialSeed`: it can be negative, and a
   client that deserializes it into anything narrower than an `int` will reject half of all matches.
3. During Command, record the player's authoritative operations as the order document; `PUT` it
   whenever it changes, with `ready: true` when the player presses Done.
4. On `turn.sealed`, fetch the sealed set and verify the digest: SHA-256 over `slot:ordersHash`
   lines joined by `\n` in slot order, each `ordersHash` being SHA-256 of that player's canonical
   document. Apply every player's ops **attributed to the slot they arrived under** through the same
   validator the replay reader uses, plan every other slot (empty, departed, or computer) with the
   deterministic AI, run Execution, Hire, Elimination and Upkeep, then `POST` the state hash.
5. On `turn.desynced`, the host uploads the native snapshot for that turn (the same bytes as a
   quick-save); every other client loads it, recomputes the hash and re-reports.
6. On `turn.deadlineExtended`, replace the countdown for that turn: the match resumed after a
   desync pause and the turn's clock restarted.
7. On reconnect, fetch the match, load the latest snapshot if the local state is behind, replay
   sealed turns from there, and resume the stream. A token that answers 401 means the membership was
   revoked — the player left or was kicked.

### What a hot-seat core does not say

Two things the list above leaves implicit, which a client written against a turn-by-turn core gets
wrong by default. Both are settled in `src/Rechaos.Multiplayer/Session`.

**The interface plans on a copy.** Applying a queued command to the authoritative state as the
player queues it would put this client ahead of its peers, and the sealed set would then apply the
same command a second time. The authoritative state advances only by applying a sealed turn;
`SpeculativeTurn` is the copy the player plans on, and it is thrown away when the turn seals. The
copy also advances the coordinator to the local seat, because the core refuses a command from
anybody but the active player and in a simultaneous turn only one seat is ever that.

**Hire offers are drawn once, for every seat, on entering Command.** The game draws them lazily when
a player opens the dock, which is harmless with one state and not with six: drawing spends the
shared PRNG, so a client whose player never opened the dock would diverge from one whose player did,
and every later draw in the match with it. `MatchState.PrepareSimultaneousHireOffers` is the
simultaneous form — one ordered pass every client takes at the same point — and it is what makes the
dock a player plans against the dock the sealed turn grants.

## Limitations and next steps

- **One server process.** The Node runtime fans events out in memory, so two instances behind a
  load balancer would each wake only their own subscribers: a client on instance A would sit silent
  through everything written on instance B, with no error to show for it. Rate-limit windows
  fragment the same way. Postgres is offered for durability and operational familiarity, not as a
  way to scale out; running more than one instance needs a shared fan-out (the Cloudflare runtime's
  Durable Object is the worked example) before it is safe. The `GET /events?after=` fallback is the
  one path that does work under it, because it reads the log directly.
- Late joining into a running match (taking over a computer slot) is not offered; the lobby is
  the only door.
- **The lobby is polled, not streamed.** The game reads the match about once a second while the
  lobby is on screen and opens the event stream when the match starts. The stream carries the lobby
  facts too; opening it earlier would mean unwinding a session for every player who backs out.
- No chat. A WebSocket lane for lobby chat would sit beside the stream without touching turns.
- The turn timer is a whole-match setting; per-turn extensions are not offered beyond the restart
  that follows a desync pause.
- **Desync recovery trusts the host.** The snapshot the host uploads becomes the state every other
  client must match; there is no majority vote, and a host who never uploads leaves the match paused
  indefinitely. The escape is the ordinary one: players leave, and the match is abandoned when the
  last active player goes. A vote, or a quorum hash, is the obvious next step if public servers ever
  need it.
- An event is published after it is durable, so a process dying mid-publish can lose the
  notification but never the event. A process dying between persisting an event and its successor
  simply has no successor: clients reconcile from the match view on reconnect, which is always
  authoritative.
