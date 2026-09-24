# Multiplayer

Status: implemented server, game client wired
Last updated: 2026-09-20

Online play for *Chaos Overlords: New Chrome* runs through a coordination server that any player
can host and that can also run as a central public service. The server code lives under
[`multiplayer/`](../multiplayer/README.md). Nothing about the 1996 protocol is reproduced; this is
a new design over HTTP.

The game offers the official central service at `https://chaos-overlords.dinorefurb.com` and a
custom/self-hosted choice. Opening the Online screen probes the selected service's unversioned
`GET /health` route and reports whether it is available before the player tries to host or join.
Hosts give each match a name and choose whether it is publicly discoverable or join-code-only;
publicly discoverable is the default, and a server lists those matches unless its operator turned
`PUBLIC_LISTING` off. The Online screen browses public waiting and ongoing matches and filters them
by status, scenario, and AI difficulty. An unfiltered browse shows every match the server returned,
including one whose settings blob this build cannot read — that session is listed without its
scenario and mentality rather than hidden, and only a scenario or AI filter drops it. Code-only
matches remain absent from discovery. A host still receives an eight-character code and can copy it
to the system clipboard; joiners have a bounded Paste button that reads at most the eight supported
characters without disturbing the player-name field.

<!-- doc-index:begin toc depth=3 -->
- [What the server is, and is not](#what-the-server-is-and-is-not)
- [Transport: REST for intents, server-sent events for the log](#transport-rest-for-intents-server-sent-events-for-the-log)
- [Protocol](#protocol)
  - [Two versions: one for talking, one for playing](#two-versions-one-for-talking-one-for-playing)
  - [Lobby](#lobby)
  - [Turn barrier](#turn-barrier)
  - [Turn lifecycle](#turn-lifecycle)
  - [Timer](#timer)
- [Bug reports: the same deployment, a different database](#bug-reports-the-same-deployment-a-different-database)
- [Retention](#retention)
- [Security model](#security-model)
- [What the server does and does not defend against](#what-the-server-does-and-does-not-defend-against)
- [Two languages, one contract](#two-languages-one-contract)
- [Client integration contract](#client-integration-contract)
  - [What a hot-seat core does not say](#what-a-hot-seat-core-does-not-say)
- [Limitations and next steps](#limitations-and-next-steps)
<!-- doc-index:end -->

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

Every seat no human took at the start is a computer player, planned by the deterministic AI on every
client identically, so its orders never cross the wire. A host may start with only themselves and
may opt into late joining. In that mode an incoming player can claim an AI seat that has never been
owned by a human. A historically human seat is permanently reserved for its original owner, even
while AI temporarily controls it. The match seed and slot assignment come from the server at start,
so every client bootstraps the same city.

A departure or a timed turn with no submitted document opens a takeover vote. Every currently
present player must choose `USE AI` before control changes; any `WAIT` choice keeps the seat human,
and there is no server-side timeout that approves takeover implicitly. Leaving does not decide the
seat either: while the vote on a departed seat is open, whether anyone has answered it yet or
somebody chose `WAIT`, the turn waits on that seat's readiness like any other, so it cannot seal
before the player returns and finishes it or the vote hands the seat to the computer. A kicked seat
is not waited on, since its player cannot come back. While any such prompt is
open, the open turn has no deadline, so time spent in the modal cannot consume planning time. The
clock restarts when the last prompt closes. Opening a prompt is what stops the clock, wherever the
prompt comes from — a departure, a seal that found an empty seat, a returning player being asked
about the seats they find absent, or a vote that opens the question itself — so no path can leave a
countdown running behind a modal nobody can plan through. Open prompts and their votes are rows of their own
(`takeover_prompts`, `takeover_votes`), not a replay of the event log: opening a turn and judging a
vote each cost one indexed read however long the match has run. A seat that goes quiet while nobody
is present to ask is put to the first player who returns, and a vote cast on an absent seat nobody
was ever asked about opens the question itself, so no seat can be left idle for the rest of the
match with no way to hand it to the computer. A player who reconnects
atomically returns to `active`, cancels a pending absence vote, and reclaims their seat from AI when
necessary. `match.playerTakenOver`, `match.playerReturned`, and `match.latePlayerJoined` place both
directions of a controller transfer at an exact event-log position, so every client records the same
change in the hashed match state. The first former player to return when no host is present becomes
host.

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
hint: every wake and heartbeat drains the log from the last delivered sequence, so a lost
notification costs at most one heartbeat, never an event, and a reconnecting client resumes from
the last `seq` it saw. Delivery is
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

### Two versions: one for talking, one for playing

`POST /protocol/handshake` is the first call a game makes, and the only thing
`MULTIPLAYER_PROTOCOL_VERSION` decides: whether this build and this server can exchange anything at
all. It moves whenever the wire moves — a route, a schema, the event stream, authentication — and a
mismatch is refused there, before any match data crosses.

`MULTIPLAYER_SESSION_VERSION` answers the other question. It describes the session as it is stored
— the match row, its turns, its orders and its snapshots — and it is what a client checks before
picking a match up: the match view and every snapshot carry the version they were written under,
and a client plays them only when that is the version it knows. The server stores both numbers and
judges neither; it holds the session as opaque history, which is why the two are not one number.

Keeping them apart is what lets a wire change ship without ending the matches already being played.
A client on protocol 6 resumes a match created by a client on protocol 5, because the session it
would carry on is the same session; what it refuses is a session whose rules, orders or state
hashing are not the ones it plays. `AGENTS.md` says when each number moves.

### Lobby

| Call | Who | Effect |
|---|---|---|
| `POST /matches` | anyone | Creates a lobby. Returns the host's token, the 8-character join code and the match view. `hostPortraitId` is the overlord face the host sits down under, stored on their roster row. `settings.gameSettings` is an object the server stores for clients (scenario, the portraits that dress the unclaimed seats, difficulty) and reads two keys of: `allowLateJoin` gates the late-join door, and `seatSummaries` is written back from the host's snapshot uploads and hash reports for the public listing. Everything else in it is opaque. The server also reads `name`, `maxPlayers`, `turnTimerSeconds`, `visibility`. An optional `password` gates joining. |
| `GET /matches` | anyone | Public waiting and ongoing matches, including filterable settings, each match's `sessionVersion`, and available late-join seats with current gang, site, and sector counts. `?sessionVersion=N` narrows the list to matches stored under that session version before the page limit applies; the desktop client always sends its own, so a public match it could not play is never listed. Served unless the deployment set `PUBLIC_LISTING=false`, which answers 404 `listing_disabled` instead. |
| `POST /matches/join` | anyone | Joins by code (and password), under the caller's chosen `portraitId`. Returns that player's token. Capacity is a single atomic seat claim. |
| `POST /matches/join-running` | anyone | Joins an ongoing late-join-enabled match in a selected never-human AI slot. The atomic claim prevents two callers taking the same seat. `portraitId` is the face that seat already wears, which the client reads out of `gameSettings`: the match was generated with it before the caller existed, so a latecomer inherits a face rather than choosing one. |
| `GET /matches/:id` | member | Match view: players, current and previous turn (who is ready, who reported), status, seed. Seals an open turn whose deadline has already passed before answering; see [Timer](#timer). |
| `PUT /matches/:id/settings` | host | Updates the named lobby's scenario, AI policy, timer, duration, visibility, and late-join policy before start. |
| `PUT /matches/:id/profile` | member | Changes the caller's own `displayName` and `portraitId` before start (`409 match_not_in_lobby` after it). The name is held to the same per-match uniqueness as a join (`409 display_name_taken`), against everyone but the caller. Announced as `lobby.playerUpdated`. |
| `POST /matches/:id/start` | host | Seats players (host slot 0, then join order), draws the seed, opens turn 1. |
| `POST /matches/:id/leave` | member | In the lobby: frees the seat (the host leaving abandons the lobby). Running: publishes the departure and opens a takeover vote; it does not transfer control. A leaving host hands the role to the lowest active slot. The durable membership token is retained for later rejoin. |
| `POST /matches/:id/rejoin` | former member | Reactivates the caller's durable seat, restores host authority when appropriate, and transfers an AI-controlled reserved seat back to its owner. |
| `POST /matches/:id/players/:pid/kick` | host | Same as the target leaving. |
| `POST /matches/:id/players/:pid/takeover-vote` | active member | `{ decision: "computer" | "wait" }`. The latest choice per voter counts. Computer control requires every currently active player to approve; one wait vote preserves the human controller. |

### Turn barrier

| Call | Who | Effect |
|---|---|---|
| `PUT /matches/:id/turns/:n/orders` | member | Replaces the caller's order document for the open turn and sets `ready`. The write is one statement conditional on the turn still being open, so a new order landing after the seal is refused (`409 turn_not_open`), never silently folded in. An exact retry of the persisted document is acknowledged even after the turn advances, covering a lost success response. Readiness is never taken back: a `ready: false` document for a seat that is already ready is a draft that arrived after the final one, so the same statement leaves the row alone and the call answers with the document that stands. When `ready` completes the roster, the turn seals in the same call. |
| `GET /matches/:id/turns/:n/orders/mine` | member | The caller's own submission (for a reconnecting client). |
| `GET /matches/:id/turns/:n/orders` | member | The sealed set: the documents of the players the seal froze, in slot order, plus `orderSetHash`. Refused while open (`409 turn_open`). |
| `POST /matches/:id/turns/:n/report` | member | `{ stateHash, finished }` after applying the sealed turn locally. |
| `POST /matches/:id/snapshots` | host | A base64 native snapshot for a sealed turn, with its `stateHash`. |
| `GET /matches/:id/snapshots/latest`, `/:turn` | member | Snapshot bodies for resync or reconnect. |
| `GET /matches/:id/events?after=N` | member | The log, paged. |
| `GET /matches/:id/stream` | member | The same log as SSE; `Last-Event-ID` or `?after=` resumes. Every frame is `id:` the sequence number, `event: message`, and `data:` the event JSON — one event name for the whole stream, so a browser's stock `EventSource` reads it from `onmessage` and branches on the `type` inside the payload. A `: keepalive` comment every 20 seconds is the only other frame. |

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
set a client fetches is therefore always the set the digest was computed from: a player who leaves
after marking ready, while the vote on their seat is still open, is in both, because the seal waited
on them; one whose seat was kicked or handed to the computer before the seal is absent from both;
and one who leaves after the seal stays in both. A slot absent from the set contributes no human document. The first wholly missed timed turn
marks an otherwise active seat `takeoverPending` and opens a vote. It remains idle and human while
players wait; only a later `match.playerTakenOver` makes it computer-planned.

The vote is put to everyone but the seat it is about. A player who merely let one timed turn pass is
still at their keyboard, is marked absent only until their client reports that turn, and cannot vote
on themselves — the server refuses a vote from a seat that is not active — so a client shows them
their own pending absence on the turn status line rather than as a modal that would name them and
take the input they need to get back into the match. A transfer is also ignored once the state
carries an outcome: the match is over, a finished match is not the clean Command boundary control
transfers at, and the decision is read from state every client has already agreed on, so all of them
ignore exactly the same transfers.

An open takeover vote also pauses the current turn clock. The server clears its deadline and emits
`turn.deadlineExtended` with a null deadline; after the last vote closes it starts a fresh full clock
and emits the replacement deadline. A stale scheduler callback sees the cleared or replacement
deadline and cannot seal the turn early.

The verdict counts every human seat, including one that merely missed a timed deadline
(`takeoverPending`): its client still applies the sealed turn, so its report is waited for like any
other, and the wait costs nothing the match was not already paying, since an open absence vote
pauses the clock. Confirming without it would refuse its later report as `turn_confirmed` and leave
a genuine divergence undetected. The vote that makes the seat computer controlled re-runs the
verdict without it.

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
a live match whose current turn is no longer open and completes it. A start that died after running
the match but before seating it or announcing it is finished the same way: the sweep seats whoever
is still unseated and publishes `match.started` if the log does not carry it, then opens turn 1.

### Timer

`turnTimerSeconds` (0, or 30 to 86400) puts a `deadlineAt` on every opened turn. Node arms a timer
per open turn; Cloudflare sets a Durable Object alarm. Both runtimes also sweep the table for
expired open turns (a 15-second interval on Node, a cron on Cloudflare) so a lost timer costs at
most that interval. A timer that fires before `deadlineAt` (a `setTimeout` a millisecond early, or a
Durable Object whose clock is behind the isolate that set the deadline) does not seal; it re-arms
itself for the deadline, at least 250 ms out, rather than leaving the turn to the sweep. On
Cloudflare the retry also lands at least 250 ms past the time the alarm fired at, since the alarm
scheduler keeps its own clock and would otherwise refire at once while the object's clock lags.
A member reading the match view is the third path: the read seals an open turn whose deadline has
already passed, then answers the match as it stands afterwards. A client whose countdown has been on
zero for ten seconds with no seal resynchronises, and that rebuild's read is what seals a turn the
timer lost and the sweep has not reached yet — on Cloudflare the cron can be minutes away. The read
never seals a turn before its deadline, and a seal that fails there is logged and left to the timer
and the sweep rather than failing the read.
Sealing on the deadline includes whatever each player last submitted; a player who submitted
nothing contributes no orders.

## Bug reports: the same deployment, a different database

The server also takes bug reports, at `POST /api/v1/bug-reports`. It is the same application and the
same deployment that hosts matches, and the game posts there regardless of which lobby a player is
in: a report goes to the people who maintain the game, and somebody self-hosting a lobby for three
friends is not them. The address is hardcoded in the client rather than read from the online-play
field, for exactly that reason.

What travels is what the player typed and — unless they unticked the box — the whole match as a
compressed event-sourced journal that replays from its first turn. The game anonymizes it first by
re-running the match with player names replaced by seat labels and Comlink text redacted, so every
state fingerprint in it is recomputed and the result is a valid journal rather than an edited one;
if the re-run diverges at any step, nothing is attached. Names the original reads as cheat codes are
game rules and stay, because substituting one would change how the match plays.

Everything else about it is kept apart from matches:

- **Its own database.** A second D1 instance (`BUG_DB`), or a second SQLite file when self-hosted,
  with its own migration lineage. Reports arrive unauthenticated, outlive the matches they describe,
  and carry other players' journals; they share no schema, no lock, no retention sweep and no blast
  radius with the database holding live matches. The kernel cannot reach the bug database and the
  intake cannot reach the match one.
- **Its own budget.** The route is unauthenticated on purpose — the reports worth having most come
  from a player who could not get into a match at all — so it carries a per-address limit well below
  the lobby's rather than sharing it.
- **Its own storage for the bytes.** A journal is hundreds of kilobytes to a few megabytes; D1
  refuses a row over 2 MB and even the ones that fit would be dragged through every triage query. It
  goes to R2 (`BUG_BLOBS`) and the row keeps the key, the digest and the size. A deployment with no
  object store keeps archives under 256 KiB inline and accepts the report without the journal above
  that, saying so in the receipt.

The server never decompresses or parses an archive. It verifies the SHA-256 the client took over the
compressed bytes — so a truncated upload is refused rather than filed — and stores opaque bytes.
The game also caps the corresponding receipt response at 1 MiB before parsing it; a report receipt
is tiny, and an unexpectedly large response must not consume the client's default multi-gigabyte
HTTP buffer.

## Retention

A coordination server accumulates rows with no second use: a finished match's order documents, a
megabyte of snapshot per desync, the lobby someone opened and never started. Every row the server
stores belongs to exactly one retention window, and deleting a match takes everything it owns with
it, because every child table cascades from the match row. The defaults are sized for a shared public
server; a self-hosted one raises them through configuration, and `0` switches a window off.

| Window | Collects | Default |
|---|---|---|
| `RETENTION_DAYS` | `finished` and `abandoned` matches untouched for this long | 14 days |
| `LOBBY_RETENTION_DAYS` | `lobby` matches nobody started, counted from creation or the last settings change | 3 days; 0 when `RETENTION_DAYS` is 0 |
| `ABANDONED_RETENTION_DAYS` | `running`/`desynced` matches with no `active` player, silent this long | 30 days |
| `SILENT_RETENTION_DAYS` | `running`/`desynced` matches silent this long, whatever the roster says | 3 × abandoned |
| `BUG_REPORT_RETENTION_DAYS` | bug reports and their archives | 90 days |

A `running` or `desynced` match is collected only under the two longer windows. The abandoned one
is the living-dead case and the ordinary end of a public match: everybody walked away from a match
that is deliberately kept running so anyone can rejoin, and nobody ever did. A desync pause is not
abandonment and neither is a weekend, which is why an active seat spares the match under that
window. The silent window drops the roster test, because an untimed match whose clients all died
without a `leave` keeps its `active` rows forever; it follows the abandoned window unless it is set,
so switching the abandoned window off keeps running matches forever. Both windows run from the
match's last activity, and a player joining or rejoining the match counts as activity: it restarts
them the same way a turn opening does.

The lobby window follows `RETENTION_DAYS` when it is unset: a server that keeps finished matches
forever (`RETENTION_DAYS=0`) keeps its lobbies too, as it did before lobbies had a window of their
own. A value that is set but not a whole number is refused at startup on Node; the Worker has no
startup to refuse it at and treats it as unset, so a derived window keeps being derived.

Bug reports have their own database and their own window, `BUG_REPORT_RETENTION_DAYS` (90 by
default); collecting one deletes its archive from the blob store as well, because nothing cascades
there. The intake also spends a whole-day byte budget across every reporter
(`BUG_REPORT_DAILY_STATE_MB`, 512 by default): over budget a report is still filed and only its
journal is dropped, which is what bounds a flood arriving from many addresses at a few a minute
each. A per-address daily counter sits beside it, spent by the handler only for a report that
actually carries a journal and only after the contract has accepted the body — a budget charged
before either would be spent by text-only reports and by bodies the validator refused, so the one
report somebody attached a journal to would be filed without it. The map that counter lives in is
bounded by a hard cap on the number of keys any limiter holds, because on an unauthenticated route
a map keyed by address is otherwise unbounded memory.

Node runs the turn sweep every `SWEEP_INTERVAL_MS` and the retention sweeps as a separate cleanup
job every `RETENTION_INTERVAL_MS` (a minute by default), each pass deleting at most
`RETENTION_BATCH_SIZE` matches per window. Cloudflare invokes the same sweeps from its cron trigger
(every five minutes). Durable Object storage holds only a match's pending deadline, which the alarm
deletes once nothing is left to fire for, and in-memory rate limiter windows are bounded by a key cap.
Most
passes visit only matches something has happened to in the last few minutes — a seal in flight and
an interrupted verdict are both seconds old — with an unbounded pass on a much longer period and one
at startup, so the standing population of a public server (matches kept `running` for a month
after everyone walked away, matches parked in `desynced` because nobody ever repaired them) is not
re-judged every tick for the whole of its retention.

Snapshot storage is independently bounded to the newest five snapshots per match: a bootstrap, a
checkpoint every ten confirmed turns, and the exceptional desync repairs. A checkpoint is what
bounds how far a reconnect has to replay; the server takes one only from the host, only for a turn
already CONFIRMED, and only at exactly the hash that verdict settled on, so it can restate the
match's own conclusion and nothing else. Nothing on the client waits for one: the host serialises
the state on the event pump and uploads it in the background, off the connection-health lanes, with
a few attempts over at most two minutes, so a checkpoint the server is slow to take or rate limits
neither holds the next seal nor raises the reconnect modal. A checkpoint given up costs the next
reconnect a longer replay from an older snapshot.

**SQLite runs with `synchronous = NORMAL`.** "Published after durable" therefore holds against a
process crash and not against losing power: a handful of the most recent writes can be lost with
the machine. It is a deliberate trade — the alternative is an fsync on the path that seals every
turn — and the sweeps are what pick a match back up afterwards. A deployment that wants the stronger
guarantee sets `synchronous = FULL` or runs Postgres.

## Security model

- **Identity is a capability token.** Create and join answer a 256-bit random bearer token that
  is shown once; the server stores its SHA-256 and looks it up by hash. There are no accounts,
  which is what makes self-hosting a one-command affair. A token is scoped to one player in one
  match; using it against another match answers 404, never 403, so match ids cannot be probed.
  Kicking **revokes** it (the stored hash is cleared) and hangs up the event streams that
  membership already holds, because a stream open before the revoke is never authenticated again.
  Explicitly leaving a running match preserves the capability for that original player alone,
  allowing the client to rejoin and reclaim the historically reserved seat later. A failed
  authentication is charged to the caller's address, so an unauthenticated stranger cannot drive
  token lookups at line rate.
- **Names are constrained and unique within a lobby.** Control and format characters are refused
  (a bidi override in a name rewrites how every name drawn after it reads), names are NFC
  normalised, and a join whose case-folded name is already on the roster is refused. The roster is
  the only thing players have to tell each other apart by, and a second copy of the host's name
  makes every roster decision a guess.
- **Event streams are bounded.** A stream is not a request: it lives until the client closes it and
  every event published to its match costs it one read. So they are capped per player (the oldest
  goes to make room for a reconnect, before the match cap is read, so a reconnect into a full match
  succeeds), per match, and per process, and the last two refuse with 429.
  Without that one member could hold thousands of streams and turn every sealed turn into thousands
  of database reads for everybody.
- **Secrecy of orders until the seal** is the property the design guarantees: nobody, the host
  included, can read another player's plan before the turn seals. There is no commit-reveal
  protocol because the server is the trusted holder; a self-hosted server is trusted by whoever
  chose to play on it.
- **Join codes** are 8 characters drawn uniformly (by rejection sampling, not a biased `%`) from a
  31-glyph alphabet, about 40 bits; an optional PBKDF2 hashed password gates the lobby.
- **Rate limits** come in three tiers: the unauthenticated doors per client address, every
  authenticated call per player, and snapshot uploads per player on a tighter budget, because a
  member is a cost too — order documents are a quarter of a megabyte and snapshots four times that.
  Match creation also has one process-wide budget shared by every caller, because a per-address
  budget does nothing against many addresses and every create is a stored lobby; only a create whose
  body validates spends it. The Node runtime also caps connections and sets header and request
  deadlines, so a client that never finishes sending a request cannot hold a socket for long. The
  windows are per process, which is what a self-hosted server needs; a public deployment puts its
  platform's rate limiting in front as the real gate.
- **A refused request is described, not echoed.** A validation failure names the field and the
  rule; the value the client sent (a mistyped password, an order document) is never written back
  into the response or, through it, into a proxy log.
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
- **Corroboration assumes one human per seat.** There are no accounts, so nothing stops one person
  holding several seats in a public lobby. A host with two of three seats can report a doctored
  hash twice and then upload a snapshot claiming it, and the honest third player is told to
  converge. Counting reports is a defence against one client, not against one person wearing three
  hats, and the server has no way to tell the two apart. It is sound among people who found each
  other elsewhere and it is not a guarantee to strangers; the real fix is the `TurnResolver` port
  above.
- **Which join codes exist is observable to somebody already scanning the code space.** An unknown
  code and a match that has already started answer the same 404, but a code-gated lobby answers 401
  rather than 404, so a caller who guesses a live code learns that it is live. The space is about
  40 bits against 30 attempts a minute per address, so this buys an attacker nothing in practice,
  and collapsing the password refusal into a 404 would tell a player who mistyped their password
  that their join code was wrong. The trade is made deliberately in that direction.

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

**Checked because a name is not only a name.** The original game reads two player names as cheat
codes: `SMGFUNDAGE` grants the maximum starting cash, and `SMGISLANDS` changes how the city is
generated. In a hot-seat match they are harmless — the player typing one is the only player affected,
and they chose to. Online they are neither. A name arrives from the server's roster, every client
reads the same one, and the rules fire on all of them: one player takes the cash bonus with every
opponent's client agreeing they earned it, and the islands name is read with `Any` over the whole
roster, so one player rewrites the map for everybody. Neither shows up as a desync, because nothing
about either is inconsistent — which is exactly why neither can be left to surface on its own. The
lobby refuses both (`displayNameInputSchema`), and the client's bootstrap substitutes the seat's
derived name for any that reach it, deterministically, so a server that let one through still plays a
fair match. `ReservedPlayerNames` in `Rechaos.Core` is the list; the TypeScript copy names it as the
source of truth.

The recovered list is now complete: `SMGSPANK`, `SMGHUBBLE`, `SMGMILK`, and
`SMGKICKASS` also alter the original setup, visibility/roster, or hiring. The
native input record contains at most ten upper-case printable glyphs, while the
modern lobby can show a 32-character display name. Every seeded online match
therefore uses the deterministic ten-character original-font projection, and
the input schema rejects a longer name such as `SMGFUNDAGE THE THIRD` when
that projection would become a cheat. An older or permissive server cannot
bypass this: the client bootstrap applies the same projection and substitutes
the derived `PLAYER n` name for every resulting cheat, so all clients still
seed one fair match.

**Checked because a face is not only a face.** A player chooses their overlord portrait when they
create or join, and it rides their roster row from there: `playerView.portraitId`, one of the
original atlas's sixteen. Every client builds its city from that roster, and the setup a city was
generated from is hashed into every turn verdict, so the face is as load-bearing as the name beside
it. Three consequences follow, and all three are enforced rather than assumed. A face is set by
the request that claims the seat, and only `PUT /matches/:id/profile` changes it — together with
the name, and only while the match is in the lobby. The write is conditional on the lobby in the
same statement, and `start` reads the roster it seats after its own transition, so a change either
makes it into the roster every client bootstraps from or is refused; a face that moved mid-match
would read as a desync on every client that had already bootstrapped. A seat nobody
claimed keeps the portrait the host's `gameSettings` dressed it in, because there is no player to
ask; a latecomer taking such a seat over sends that same face back rather than their own. And a
value outside the atlas stops the bootstrap (`MatchBootstrapFactory`) instead of being clamped to
something drawable: a client that quietly substituted one would be playing a city no peer agrees
with.

## Two languages, one contract

The server is TypeScript and the game is .NET, so one of the two mirrors of every wire type has to
be derived from the other rather than typed twice. `pnpm codegen` in `multiplayer/` is that
derivation: [`@game-infra/valibot-to-csharp`](https://www.npmjs.com/package/@game-infra/valibot-to-csharp)
walks the valibot schemas and emits `src/Rechaos.Multiplayer/Generated/WireContracts.cs`, whose
records deserialize the same JSON; a second pass reads the endpoint contracts and emits
`RouteTemplates.cs`, which `MultiplayerApiRouteTests` holds the C# client's paths to. Both files are
committed, so building the game never needs Node, and CI runs `pnpm codegen:check` to fail if either
has drifted from the schemas.

Four things the schemas say exist for that crossing:

- **Every integer states its bounds.** A bound is what lets the generated C# hold a value in an
  `int` instead of a `long` or a `double`; JavaScript integers run to 2^53, so an unbounded one has
  no narrower type that could not refuse a legal value. The ceiling on turn numbers, sequence
  numbers and format versions is `int.MaxValue`, which is the bound the client already had.
- **No number is ever a float.** The order digest is taken over canonical JSON, and a float's
  shortest round-trip spelling is not portable: JavaScript writes `1e+21` where .NET writes `1E+21`.
- **`optional` and `nullable` are different.** The first says a key may be absent, the second that a
  value may be `null`, and a request that writes `null` where only absence is accepted is refused.
  The generated C# omits an optional field rather than writing a null for it.
- **A display name may be relayed that a request may not set.** `displayNameSchema` is what a roster
  is read through and `displayNameInputSchema` is what a request is held to; the second refuses the
  names the original game reads as cheat codes. The distinction matters because reading is not
  choosing: a name already on a roster has to stay readable whatever the rules have since become.
  See "What the server does and does not defend against".

What the generator cannot mirror, `Rechaos.Multiplayer` writes by hand and pins with a test:
canonical JSON. `packages/kernel/test/logic.spec.ts` and `MultiplayerCanonicalJsonTests` hold the
same golden document, the same canonical text and the same digest, on both sides of the wire.

Readiness is monotonic for one open turn, on both sides of the wire. Once any
queued or in-flight order replacement says `ready: true`, later drafts for that
same turn continue sending `true` until the server seals it; document
replacement must not retract readiness merely because the earlier request has
left the outbox. The client alone cannot promise that, though: a superseded
draft is cancelled locally, and a timed-out attempt is retried, while the
request itself may already have reached the server. Such a draft used to land
after the final document, put the seat back to drafting with the older orders,
and leave the turn waiting on a player whose screen said they were done — the
next player's ready sealed nothing until the first submitted again. The server
therefore refuses to let a non-ready document replace a ready one, in the same
conditional write that checks the turn is open.

The one way readiness is taken back is a finished document the server refuses
outright — `validation_failed`, `payload_too_large` or `bad_request`. The server
recorded none of it and the turn still waits on the seat, so the session stops
carrying readiness for the turn (`OrdersRefused.ReadinessWithdrawn`) and the
client hands the player the turn they planned, to change and end again. A
refusal of the turn itself, such as `turn_not_open`, changes nothing.

Readiness reaches the interface as the seats that have finished, not as a count
of them. The city top bar marks every seat the turn is still waiting on with a
green `WAIT` under its portrait, so "waiting for the other players" says which
ones; the footer's tally is the same fact counted. The player's own seat is
marked too, until they end the turn, and it follows what this client did rather
than the server's echo, so the mark goes the moment the turn is sent. A seat the
turn does not seal against — a computer empire, a kicked player, a player who
left with no vote open on their seat, or one voted onto computer control — is
never marked. A departed seat whose vote is still open is waited on, and marked,
until the vote closes.

## Client integration contract

What the C# client has to do. `multiplayer/packages/client` is the reference and
`src/Rechaos.Multiplayer` is the implementation:

1. Create or join, keep the token and the last event `seq`; open the stream with `Last-Event-ID`.
   Events are at least once: ignore one for a turn already applied, and treat the match view as the
   authority when the two disagree.
2. On `match.started`, build the match through `OriginalMatchFactory` from `seed` and the seated
   players using the stored `gameSettings`. Each seated player's overlord wears their own
   `portraitId` from the roster; the `gameSettings` portraits dress only the seats nobody claimed. The seed is a **signed 32-bit integer**, drawn to fit
   `MatchSetup.InitialSeed`: it can be negative, and a client that deserializes it into anything
   narrower than an `int` will reject half of all matches. Seat by *whether a slot was assigned*, not
   by a player's current status: a slot is handed out once and never reassigned, so "has a slot" says
   the same thing whenever it is asked, while status changes over the life of a match. Reading status
   here makes the generated city depend on when the client bootstrapped it, and because the rules read
   player names, two clients that bootstrapped either side of a departure disagree from the first
   upkeep.
3. During Command, record the player's authoritative operations as the order document; `PUT` it
   whenever its digest changes, with `ready: true` when the player presses Done. A `ready: false`
   draft means a turn the clock seals still uses what the player planned. The outbox retains only
   the newest pending whole-document replacement while one request is in flight, so rapid edits
   cannot build a backlog of obsolete drafts. Drafts are paced to at most one a second, because the
   per-player rate limit they spend is the one the stream, the reports and every read share; the
   finished turn is never paced. A transient request is retried for the shared
   five-minute call window; if that window expires without an answer, the outbox retains the same
   idempotent document and starts another window. Only a server refusal, a revoked membership, or
   caller shutdown discards it, so a connectivity outage cannot silently turn a submitted draft
   into an empty sealed turn. A draft's failed attempts are retried as quietly as they are
   persistently: they put a line on the message bar, never the reconnect modal, which answers only
   for the stream, the pump's calls, the reporter and a finished turn. A draft is retried only
   while it can still matter: a newer draft cancels it, and so does the seal of its turn, since the
   server holds nothing a sealed turn's draft could still change.
4. On `turn.sealed`, fetch the sealed set and verify both the digest announced by that exact event
   and the set's internally recomputed digest: SHA-256 over `slot:ordersHash`
   lines joined by `\n` in slot order, each `ordersHash` being SHA-256 of that player's canonical
   document. Apply every player's ops **attributed to the slot they arrived under** through the same
   validator the replay reader uses; plan every slot that *the match state says the computer controls*
   and has no document with the deterministic AI; leave a human-controlled slot with no document
   idle; run Execution, Hire, Elimination and Upkeep, then `POST` the state hash. Read the controller
   out of the state rather than a roster fetched beside it — the state is the one answer every client
   is already guaranteed to agree on, and a slot the AI plans on one client and not another is a
   desync on the turn *after* the one that caused it.

   The `POST` is the turn's last act, not a gate in front of the next one: a turn whose sealed set
   verified and applied is one this client can already plan on, so the hash is handed to an ordered
   background reporter and the player gets their city back without paying a round trip. The
   reporter sends one turn at a time and in turn order — the server settles turns in order, and an
   earlier turn left unreported blocks every later one — and it captures each request as the turn
   resolves, so `finished` and the host's seat summary describe the state that produced that hash.
   An exhausted retry window is answered the way the outbox answers one: the report is an
   idempotent restatement, so another window opens rather than the match ending. A client leaving
   the match flushes whatever it has not sent within a short grace, because a turn settles only
   once every human seat has reported it; anything still unsent is replayed out of the history and
   reported again on the next reconnect. The reconstruction paths — a restore and a desync repair —
   still wait for their reports, because they must not run ahead of the barrier they are clearing.
5. The server retains each sealed order set as the turn increment; ordinary confirmed turns do not
   upload the whole state again. The host includes the small public seat summary in its state-hash
   report for late-join selection. On `turn.desynced`, the host uploads a compressed native snapshot
   as an exceptional repair (the same state as a quick-save), declaring the **native save** format
   version — the replay format's says nothing about those bytes. Every other client refuses a version
   newer than it reads, and otherwise loads it, recomputes the hash and re-reports.
6. On `turn.deadlineExtended`, replace the countdown for that turn. A null deadline pauses it for an
   absence vote; a later timestamp restarts it after that vote or a desync pause closes. Show that
   countdown and warn against it — the client runs no planning clock of its own online, so the
   server's deadline is the only one there is, and a player hears the last ten seconds and the last
   second of it as they would in a hot-seat match. Each warning sounds once per deadline, and a
   restarted clock is a new deadline that warns again. A turn that seals while the player was still
   planning says so: the sealed set names the seats it carried, which is the only honest answer to
   whether the draft they were still editing reached the server in time.
7. On reconnect, fetch the match, load the latest snapshot if the local state is behind, then read
   the durable event log gaplessly through the refreshed `lastEventSeq`. Replay approved takeover and seal
   events in order, skipping seals already represented by the snapshot, before restoring the current
   draft and resuming the stream. Historical confirmation hashes are checked after each reconstructed
   turn, so a cash or other rules divergence is refused at its first authoritative boundary instead
   of being shown as a plausible restored state. A restore also picks up a divergence it finds in
   the history: the `turn.desynced` that announced it is behind the view's sequence and will never
   be delivered again, so the client adopts a repair somebody has already posted, or posts one
   itself if it turns out to hold the state the others agreed on.

   A refusal means the membership is gone only when it is the SERVER saying so — an envelope naming
   `invalid_token`, `missing_token`, `unknown_match`, `unknown_player` or `not_active`. A bare 4xx
   is what a reverse proxy, a stale tunnel or another service on the port answers, and the client
   treats those as an outage to wait out rather than a verdict to end a session on.
8. Drop and reconnect a stream that carries nothing, not even the server's 20-second keepalive, for
   two and a half heartbeats: a half-open connection (a suspended laptop, an expired NAT entry)
   delivers neither an error nor an end, and without that deadline every seal after it is missed
   until the operating system notices. Reconnect attempts count against one outage window that
   only an arriving EVENT resets, or a connection that has lasted a heartbeat; a server that
   accepts the connection, writes its keepalive and closes it at once is an outage like any other,
   and the keepalive is not allowed to disguise it. Retry transient failures for up to five minutes rather than ending the match immediately. Every call a received fact leads to is idempotent — the reads
   plainly so, and the two writes by definition, since a report restates a hash the server already
   holds and an order document replaces what was held — so a server having a bad moment costs latency
   and nothing else. Draft submissions are whole-document replacements: an unsent older draft is
   discarded, and queuing a newer draft cancels retries of the superseded in-flight document so
   only the latest plan consumes server work. A retried call waits at least as long as a
   `Retry-After` asks, up to a minute, so a rate limit is not spent on attempts its window will
   refuse. While retrying, the status line says so from the first failed attempt; once the server
   has gone unanswered for five seconds the client shows a modal attempt log with the concrete timeout,
   HTTP status/request id, stream closure, or network exception and lets the player stop early. A
   rate limit is titled as the server limiting requests rather than as a lost connection. If
   the window expires, the terminal error reports the attempt count, elapsed time, and last failure.
   A refusal that will keep being refused (a revoked token, a body the server will never accept) or a
   payload that cannot be made sense of still ends immediately. The city screen distinguishes an
   in-flight ready submission from one explicitly acknowledged by the server. If the server has
   acknowledged every required seat but no sealed successor arrives within 30 seconds, the client
   writes the turn and tally to diagnostics and offers reconnection; it never leaves that state
   looking like an opponent is still deciding.
9. Read responses tolerantly and requests strictly. The server is deployed separately, self-hosted
   ones especially, so a client must skip a response field it has never heard of and one the server
   left out, or a single additive release locks out every client built before it. The exception is a
   payload whose digest the client recomputes — the order documents inside a sealed set, which have
   to round-trip byte for byte — where an unknown or missing field is refused by name instead of
   reported much later as a hash that would not match.

The C# client implements the reconnect portion of this contract at session startup. An advanced
match is refreshed before its stream opens, reconstructed from the newest verified compatible
snapshot (or from the deterministic seed when no snapshot exists), and advanced through a strictly
contiguous event-log history. Approved takeovers and later sealed sets are applied at their exact
relative positions; seals already represented by a snapshot and duplicate transfer facts are
harmless no-ops. The announced sealed-set digest is checked against the fetched set before its contents are
recomputed. Only after pairing the state with the caller's current whole-document submission does
the stream resume from the refreshed `lastEventSeq`.

The desktop client writes the server, match id, player id, join code, session password, and
session version, and membership token to an atomic local recovery record as soon as it takes a seat. It writes the
match's own name beside them, which reaches every member on the wire rather than only the host who
typed it, and stamps the record each time the client adopts a turn's authoritative state. A terminal
failure also retains its safe correlation context beside that membership: stage, local operation,
HTTP status/reason, request id, planning turn, and last applied event sequence. It intentionally
does not retain the response body, credentials, player names, settings, or order contents. That is
what the list of unfinished sessions is read by: each row names the game and says when it was last
played, so a player with seats in more than one match can tell them apart. A normal shutdown
marks that record clean; an unclean exit leaves it resumable, so the next launch points the player
to a Reconnect action. The previous-sessions browser leaves out a seat whose session version this build
does not play: the record stays on disk, so a build of that session version lists it again, but
this one neither offers it nor prefills its join code. Terminal online
errors are shown on the title screen and name that recovery path when the saved membership may
still be valid. A completed match or an explicit Leave retires
the recovery record, and a retired record is dropped rather than written back: the token is a full
capability for that seat, so keeping a spent one on disk buys nothing. On Windows the token is
sealed with DPAPI to the current user account, so another account on the same machine cannot read
it out of the file; macOS and Linux keep it in clear under the user's own data root, because their
keystores want a native dependency the game does not otherwise carry. Neither defends against
something already running as the player.

The session password is kept in clear on every platform. It opens one session's door to whoever the
player was going to read it out to anyway, where the token is that seat itself, and the player who
resumes has to be able to read it out again: the escape menu of a match in progress shows it beside
the join code, which is the reason it is kept at all.

The client also bounds what a server can hand it. Its `HttpClient` buffers at most eight megabytes,
so a hostile custom server cannot answer a call with a body large enough to take the game down
before any parser sees it; event streams are read without buffering and are unaffected. The Online
screen says `NOT ENCRYPTED` beside a custom server reached over plain `http` unless the address is
loopback, a private-range literal or an mDNS `.local` name, because nothing else in the interface
would say that the seat token and the lobby password are about to cross a network readable.

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
- **Late joining is offered, and narrowly.** A host may set `allowLateJoin`, and once the match
  has its bootstrap snapshot a newcomer can take a slot that never belonged to a human. A public match
  can be named by its id or its join code; a `private` one only by its code, because the id rides
  every event, the client's recovery file and any log line. The door reads the host's
  `maxPlayers`, so the lobby's limit is the running match's limit too.
- **An approved computer seat can be reclaimed, by the player whose seat it was.** Players may
  wait indefinitely while a temporarily absent member holds a human seat, and authenticated turn
  activity cancels the pending vote. Once everyone approves computer control the deterministic
  transfer stands, but the original member still holds their token and `rejoin` takes the seat
  back; the `match.playerReturned` event says whether it replaced a computer. A late joiner
  cannot take a seat that was ever human, which is what keeps the two paths from colliding.
- **The lobby is polled, not streamed.** The game reads the match about once a second while the
  lobby is on screen and opens the event stream when the match starts. The stream carries the lobby
  facts too; opening it earlier would mean unwinding a session for every player who backs out.
- No chat. A WebSocket lane for lobby chat would sit beside the stream without touching turns.
- **Comlink is closed in an online match.** The original's player-to-player messaging writes hashed
  state on both sides: a message lands in a recipient's inbox, and merely opening the view clears
  that inbox's read mark. Either done on one client alone is a desync rather than a lost message, so
  the door refuses with a reason instead. Carrying it needs an order kind on the wire that the server
  relays with the rest of the sealed turn and every client applies at the same point. It is another
  authenticated simultaneous operation and wants its own determinism run rather than a local
  interface mutation.
- The turn timer is a whole-match setting; per-turn extensions are not offered beyond the restart
  that follows a desync pause or the closing of an absence vote.
- **Desync recovery is decided by a count of reports, and the host breaks ties.** The snapshot a
  client uploads becomes the state every other client must match, so it may only claim a hash more
  active players reported than any other, and it must name the turn that actually diverged. Whoever
  holds the SOLE most-reported hash may post it, host or not — which is what makes a desync the host
  is itself the outlier of repairable at all. A genuine tie leaves nothing to count and the host
  breaks it, which is every two-player desync. A match where nobody ever uploads stays paused
  indefinitely, and the escape is the ordinary one: players leave. The match is not abandoned when
  the last active player goes — it stays `running` so anybody can rejoin, with its turn clock
  stopped — and retention collects it once it has been silent for long enough. The counting assumes
  one human per seat; see the security model.
- **A host who never presses ready stalls an untimed match.** Only the host can kick, and without a
  turn timer nothing seals on its own, so the other players' only remedy is to leave. A unanimous
  vote of the remaining active players, reusing the takeover machinery, is the obvious next step.
- **The host role moves only to fill an empty seat.** `rejoin` promotes the caller when the current
  host has `left`, been `kicked` or been voted to `computer`. A host who is merely
  `takeoverPending` (one missed timed deadline, still connected) keeps the role, or any former
  member could take it at that moment and then kick the real host, whose token a kick revokes for
  good.
- An event is published after it is durable, so a process dying mid-publish can lose the
  notification but never the event. The stream heartbeat rechecks the durable log even while its
  connection remains healthy. A process dying between persisting an event and its successor simply
  has no successor: clients reconcile from the match view on reconnect, which is always authoritative.
