# Multiplayer

Status: implemented server, client integration pending
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

The order document is deliberately generic: `{ schemaVersion: 1, ops: [{ op, args }] }` where `args`
is a flat map of scalars, bounded in count and size. It maps onto the authoritative operations the
core's replay recorder already stores; the server never interprets an op. Legality is judged by the
core on every client while applying the sealed set, exactly as a replay is verified, so an illegal
op is rejected identically everywhere and cannot cause a desync.

Numeric arguments are **safe integers only**, and `-0` is refused. The order digest is SHA-256 over
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
- **Bounded input everywhere**: body limits per route, a bounded op count and scalar sizes, an
  opaque settings blob capped at 8 KiB, snapshots capped at 1 MiB base64. Enums persisted as text
  are narrowed by the only writer, the service layer.
- **Integrity of the sealed set**: `orderSetHash` is SHA-256 over `slot:ordersHash` lines and
  each `ordersHash` is SHA-256 over the canonical JSON of that player's document, so a client can
  verify what it fetched against the digest that was announced on the stream.
- **What lockstep does not protect**: every client holds the full game state, so a modified
  client can reveal hidden gangs or peek at fog it should not see. The hash consensus catches any
  client that *changes* the outcome, not one that merely reads. Moving resolution server-side (a
  WebAssembly build of `Rechaos.Core` behind a `TurnResolver` port) would close that gap and is
  the one design change this layout leaves room for; the wire protocol would not change.

## Client integration contract

What the C# client (`Rechaos.Game`) has to do; `multiplayer/packages/client` is the reference:

1. Create or join, keep the token and the last event `seq`; open the stream with `Last-Event-ID`.
   Events are at least once: ignore one for a turn already applied, and treat the match view as the
   authority when the two disagree.
2. On `match.started`, build the match through `OriginalMatchFactory` from `seed` and the seated
   players (slot → human, the rest computer) using the stored `gameSettings`.
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

## Limitations and next steps

- Late joining into a running match (taking over a computer slot) is not offered; the lobby is
  the only door.
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
