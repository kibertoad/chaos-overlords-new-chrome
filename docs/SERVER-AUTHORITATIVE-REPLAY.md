# Server-authoritative turn replay

Status: design, not implemented

This document designs the change that [MULTIPLAYER.md](MULTIPLAYER.md) leaves room for and
names twice: moving turn resolution onto the coordination server behind a `TurnResolver` port.
Today the server is a turn barrier that stores whatever the host uploads and counts the hashes the
clients report. After this change it holds a full deterministic implementation of the game rules
in TypeScript, replays every sealed turn itself, persists the state it computed as the only
authoritative state of the match, and judges every client against it. Nothing about it can be
built safely unless the TypeScript engine and `Rechaos.Core` resolve every turn identically, so the
second half of this document designs the conformance suite that both engines run.

Nothing here is implemented. Every section says what would be built, why that shape, and what it
costs; the phased plan at the end says in what order.

<!-- doc-index:begin toc depth=3 -->
- [Why](#why)
- [Two engines, one set of rules](#two-engines-one-set-of-rules)
- [Current state of the pieces](#current-state-of-the-pieces)
- [Target architecture](#target-architecture)
  - [The engine package](#the-engine-package)
  - [The TurnResolver port](#the-turnresolver-port)
  - [What the server stores instead of snapshots](#what-the-server-stores-instead-of-snapshots)
  - [The resolution pipeline](#the-resolution-pipeline)
  - [Order validation at submission](#order-validation-at-submission)
  - [Snapshots, reconnect and late join](#snapshots-reconnect-and-late-join)
  - [Controller transfers and absence](#controller-transfers-and-absence)
  - [Engine version and session version](#engine-version-and-session-version)
  - [Runtime placement](#runtime-placement)
- [What this does and does not defend against](#what-this-does-and-does-not-defend-against)
- [Conformance between the engines](#conformance-between-the-engines)
  - [Design goals](#design-goals)
  - [Layout](#layout)
  - [Vector format](#vector-format)
  - [Vector kinds](#vector-kinds)
  - [Runners](#runners)
  - [The differential fuzzer](#the-differential-fuzzer)
  - [Coverage the suite has to reach before the server trusts the engine](#coverage-the-suite-has-to-reach-before-the-server-trusts-the-engine)
- [Interaction with the existing documents and rules](#interaction-with-the-existing-documents-and-rules)
- [Risks](#risks)
- [Phased plan](#phased-plan)
<!-- doc-index:end -->

## Why

The lockstep model in [MULTIPLAYER.md](MULTIPLAYER.md#security-model) is honest about what it does
not defend against. Three of its gaps are the reasons for this design.

- **The snapshot store is dumb.** A desync is repaired by a client uploading a base64 native
  snapshot that the server never decodes. The server checks only that the hash the uploader claims
  is the one the players reported most often, and that the uploader holds it. That is a count of
  clients, not a check of the state: one person with two of three seats reports a doctored hash
  twice and uploads the doctored state as the new truth, and the honest third player is told to
  converge. Bootstrap snapshots and the periodic checkpoints a reconnecting client starts from are
  accepted from the host on the strength of a hash alone. A megabyte of opaque bytes per snapshot
  is also the largest thing the server stores.
- **Nothing enforces that a stored match is valid.** The server stores the sealed order sets, which
  are the only thing needed to reconstruct a match, but it cannot read them. It cannot tell whether
  a sealed set was ever applied, whether the hash a client reports is the hash those orders produce,
  or whether a snapshot is a state the rules could reach. Every invariant of the match is enforced by
  the clients agreeing with each other, and agreement among clients is not validity.
- **Blame cannot be attributed.** When reports disagree the server names the hashes and the host
  decides socially who is the odd one out. A client that diverges deliberately can pause the match
  every turn; a client that is simply buggy looks the same.

The design closes all three with one move: the server computes the state itself. A desync then
means one named client disagrees with the rules, a snapshot is something the server produces rather
than accepts, and the stored match is valid by construction because every row in it was derived by
replaying validated orders through the rules.

The earlier code review reached the same gaps from the implementation side. Its resolved findings
are retained in the git history of [MULTIPLAYER-REVIEW.md](MULTIPLAYER-REVIEW.md); the current
[multiplayer design](MULTIPLAYER.md#turn-lifecycle) describes the resulting model. R1 made repair
possible by whoever holds the sole most-reported hash, and required a repair to name a turn that is
actually `desynced`; C1 added a host-uploaded checkpoint every ten confirmed turns, accepted only
at the hash the verdict already settled on; and S3 put the divergence and a snapshot summary on the
turn row so a verdict no longer reads a megabyte.
Each of those is the best answer available when the only source of state is a client: a repair is
still a client's bytes admitted on a count of reports, a checkpoint is still a client's bytes
admitted on a hash, and a reconnect still replays up to nine turns. With a checkpoint per resolved
turn written by the server itself, there is no uploader to trust, no count to take, every reconnect
starts from the previous turn, and a verdict is a comparison of two stored hashes. The review's
fixes are the right shape for the current model; this design is what makes them unnecessary.

What it deliberately does not close is stated in
[What this does and does not defend against](#what-this-does-and-does-not-defend-against): a modified
client still holds the full state and can still read fog it should not see. Removing that requires
per-seat filtered state on the wire, which this design leaves room for and does not build.

## Two engines, one set of rules

The core rules are C#: `src/Rechaos.Core/GameModel` is about 14,200 lines, of which about 6,800 are
the computer-player planners and about 7,400 the rules, plus about 2,700 lines of persistence. The
server is TypeScript on Node and on Cloudflare Workers. Three ways to put the rules on the server
were weighed.

| Option | Verdict |
|---|---|
| **A second implementation of the rules in TypeScript** (chosen) | The server gets a native engine that runs on both runtimes, is debuggable with the same tools as the rest of the workspace, and can be published as a package for a browser client later. The cost is the port itself and the permanent duty of keeping two engines identical, which is what the conformance suite is for. |
| **Compile `Rechaos.Core` to WebAssembly** | One implementation, no drift. But the .NET WebAssembly toolchain ships a runtime and garbage collector of several megabytes, cold-starts in seconds, and is not a supported target on Workers at all; on Node it would tie the server's process model to a .NET runtime nobody in the workspace operates. It was the idea floated in MULTIPLAYER.md and it is not the right shape for this server. |
| **A sidecar .NET process the TypeScript server calls** | Only works on Node. The central public server is a Worker, so this splits the deployment story the workspace was built to keep single. |

Choosing the port turns the identity of the two engines from an assumption into a tested property.
The rest of the design is arranged so that the property is cheap to test and impossible to skip.

## Current state of the pieces

What already exists and is reused, so the new work is stated against it.

- **The core is already a replayable state machine.** Every authoritative mutation goes through
  `MatchReplayRecorder`; a replay document is an initial native snapshot plus ordered operations,
  each carrying the canonical state fingerprint after it. `MatchReplaySerializer.LoadAndReplay`
  rejects a journal at the first fingerprint divergence. This is exactly the verification the server will perform,
  already specified and tested on one side.
- **The state fingerprint is a byte-exact binary encoding**, not JSON: `MatchStateHasher` writes a
  little-endian stream (`RCHS` magic, encoding version 1, the full definition tables, setup, the
  coordinator and PRNG state including its consumption count, AI tables, the event count and the
  running digest of the canonical event history, the phase-boundary count and the running digest
  of that history, outcome, players in id order with gangs in roster order, sectors, the command
  queue, notifications, Comlink inboxes) and reduces it to 128 bits of XxHash128, written as 32
  lowercase hex characters. The two histories only grow, so each is kept as a digest chained entry
  by entry (`Chain(previous, entry)` is XxHash128 over the previous digest followed by the entry's
  canonical bytes) and the fingerprint folds in the digest rather than the history; a fingerprint
  costs the same on turn 200 as on turn 1. A phase boundary records the fingerprint of everything
  except the boundary history it is about to join, so the history is not self-referential. It is
  a checksum against divergence, not a cryptographic digest, which is exactly the property the
  design needs: the server compares fingerprints and never has to trust one.
- **The PRNG is small and fully specified**: the MSVC linear congruential step with multiplier
  `0x343fd` and addend `0x269ec3`, `(state >> 16) & 0x7fff` per raw draw, and the game's
  three-sample inclusive wrapper that spends three raws per bounded call. City generation and
  headquarters placement use rejection sampling, so the number of draws is data-dependent.
- **The sealed-turn path is short and explicit.** `SealedTurnApplier.Apply` takes the state at a
  Command phase, applies every slot's document in slot order under the slot the seal attributed it
  to, plans every computer seat with `AiPolicyPlanner`, finishes Command for every seat, then
  `CommandPhase.Enter` runs Execution, Hire, Elimination and Upkeep and draws the next turn's hire
  offers for every seat at once. The result is the hash reported. The port reproduces this function
  and nothing about the wire order document changes.
- **Bootstrap is a pure function of the match row.** `MatchBootstrapFactory` builds the six-seat
  setup from the seed, the stored `gameSettings` and the roster (names projected to the ten-glyph
  original form with cheat names substituted, portraits from the roster or the settings) and
  `OriginalMatchFactory.Create` generates the city. The server already holds every input.
- **The gameplay data is JSON.** `src/Rechaos.Core/GameData/original-data.json` (22 sites, 90
  gangs, 64 items, 62 KiB) is the bundled form of the three original tables and is loadable as is by
  a TypeScript engine. Scenario tables are code (`Scenarios.cs`) and are ported.
- **The kernel already has the ports pattern.** Storage and runtime arrive as interfaces, the
  services are runtime-neutral, and the conformance package runs the same suites over every storage
  and every HTTP facade. The engine slots in the same way.
- **Canonical JSON and the order digest are already pinned on both sides**
  (`packages/kernel/test/logic.spec.ts` and `MultiplayerCanonicalJsonTests`). That is the pattern the
  engine conformance vectors generalise.

## Target architecture

### The engine package

A new workspace package, `@chaos-overlords/engine` under `multiplayer/packages/engine`, holds the
port. It depends on nothing in the workspace but `contracts` (for the order document types) and has
no runtime port of its own: it is pure computation over plain data, which is what lets the same code
run in a vitest process, a Node server, a Durable Object and, later, a browser.

Its module layout mirrors the C# core file for file, because the conformance work will be done by
people holding both open side by side and the debt of every divergence is paid at the file that
differs:

```text
packages/engine/src/
  data/            OriginalData types, loader for original-data.json, OriginalDataValidator
  model/           MatchState, MatchPlayerState, MatchGangState, MatchSectorState, MatchLimits,
                   MatchSetup, Scenarios, TurnCoordinator, TurnStructure, GameCommands
  random/          DeterministicRandom (raw step, inclusive wrapper, consumption count)
  rules/           CommandValidation, CommandResolution (+ Chaos, Transactions), EconomyResolution,
                   HireResolution, CrackdownResolver, ToleranceResolver, SectorControlResolver,
                   SectorIncomeResolver, SectorBenefitResolver, SiteControlRules, SpecialSiteRules,
                   EffectiveStatistics, ManualRules, OriginalResolutionRules, MatchOutcome,
                   EndgameRanking, EndgameAwards, Notifications, Comlink
  ai/              AiTurnPlanner and its twelve family partials, AiPolicyPlanner, AiPlanningState,
                   AiStrategicState, the OriginalAi*Rules tables
  city/            OriginalCityGenerator, MatchBootstrap, OriginalPlayerName, ReservedPlayerNames
  hash/            CanonicalEventWriter, MatchStateHasher (the state fingerprint and the boundary
                   fingerprint, the chained history digests), an XxHash128 and a BinaryWriter
                   equivalent
  replay/          MatchReplayRecorder, ReplayStep, the sealed-turn applier, CommandPhase.Enter
  snapshot/        NativeSaveSerializer (format 26) read and write
  index.ts         the TurnResolver implementation the kernel consumes
```

Three rules for the port, each of which the conformance suite can catch a breach of:

1. **Integer arithmetic is explicit.** C# `int`, `short`, `byte`, `uint` and `long` overflow and
   truncate; JavaScript numbers do not. Every field the C# types hold in a sized integer is wrapped
   at the same width in the port (`| 0`, `& 0xffff`, `Math.imul` for the PRNG step, `BigInt` only
   for the `long` consumption counter and cash statistics where 2^53 could be exceeded). The PRNG
   state is a `uint32` and its step is `Math.imul(state, 0x343fd) + 0x269ec3 >>> 0`.
2. **Iteration order is stated, never inherited.** Where the C# orders by `Id.Value`, roster slot,
   command sequence or ordinal string comparison, the port sorts explicitly with the same key. No
   `Map` or `Set` iteration order is relied on for anything hashed.
3. **Strings are compared and encoded as the C# does.** Names are hashed as UTF-8 with a 4-byte
   length prefix; ordinal comparison is UTF-16 code-unit comparison, which is JavaScript's default
   `<` on strings, and never `localeCompare`.

The port is of the current format only: fingerprint encoding 1, native save 26, replay 30. Since
the fingerprint moved to XxHash128 the C# itself reads no older format (a save or journal declaring
one is refused as `OlderFormat`), so there is no legacy hasher to leave unported and the two engines
start from the same clean slate. XxHash128 is not in the Node or Workers standard library; the port
carries its own implementation, which is a few hundred lines of 64-bit arithmetic over `BigInt` or
paired 32-bit words and is pinned by the `prng`-style vectors before anything else is built on it.

### The `TurnResolver` port

The kernel gains one port, defined beside the storage and runtime ports:

```ts
export interface TurnResolver {
  /** Builds turn 1's Command-phase state from what the match row holds. Deterministic. */
  bootstrap(input: BootstrapInput): EngineState
  /** Applies one sealed set and runs the turn to the next Command phase. */
  apply(state: EngineState, sealed: SealedTurnInput): TurnResolution
  /** Canonical state fingerprint of a state at a Command boundary, as every client reports it. */
  hash(state: EngineState): string
  /** The native snapshot (format 26 JSON, UTF-8) of a state, loadable by the game client. */
  snapshot(state: EngineState): Uint8Array
  restore(snapshot: Uint8Array): EngineState
}

export interface BootstrapInput {
  seed: number                 // signed 32-bit, MatchSetup.InitialSeed
  sessionVersion: number
  gameSettings: GameSettings   // scenario, duration, AI mentality and policy, seat portraits
  roster: ReadonlyArray<{ slot: number; displayName: string; portraitId: number }>
}

export interface SealedTurnInput {
  turn: number
  documents: ReadonlyArray<{ slot: number; orders: OrderDocument }>   // slot order, as sealed
  /** Controller transfers the event log places before this seal, in log order. */
  transfers: ReadonlyArray<{ slot: number; to: 'computer' | 'human' }>
}

export interface TurnResolution {
  state: EngineState
  stateHash: string
  finished: boolean
  /** Per-op verdicts, for the journal and for the event stream. */
  verdicts: ReadonlyArray<{ slot: number; index: number; accepted: boolean; code: number }>
}
```

`apply` is `SealedTurnApplier.Apply` plus the controller transfers, which today are applied by every
client at an exact event-log position. The server already owns that position, so it is folded into
the input rather than left as a second mutation path the resolver has to be told about.

`EngineState` is opaque to the kernel. The kernel stores it only through `snapshot` and reads it
only through `restore`; nothing in the kernel reaches into the engine's model.

### What the server stores instead of snapshots

The `snapshots` table, holding host-uploaded bytes, is replaced by a table the server writes from its
own resolver. The match becomes event-sourced at the level the rules already define: the sealed order
sets are the increments, and the server-derived states are checkpoints of applying them.

```text
matches            + engine_version, resolved_turn (the last turn the server has applied)
turns              + resolved_state_hash (server), verdict_source ('server')
turn_orders        unchanged: the sealed documents remain the increment
turn_reports       unchanged: what each client claims, judged against resolved_state_hash
turn_verdicts      NEW: (match_id, turn, slot, op_index, accepted, validation_code)
match_states       NEW: (match_id, turn, state_hash, format_version, body BLOB, resolved_at)
                   body is the server's native snapshot at the Command boundary after `turn` sealed;
                   turn 0 is the bootstrap. Kept for the newest N turns (N = 5, the current per-match
                   snapshot bound) plus every turn a client is still reporting on; older ones are
                   re-derivable from turn_orders. The host-uploaded ten-turn checkpoints the current
                   model takes are subsumed: every turn is a checkpoint and none is uploaded.
```

Two properties follow. The stored match is valid by construction: every row of `match_states` was
computed by the rules from a bootstrap the rules generated and orders the schema admitted. And the
store is self-verifying: `bootstrap` plus the `turn_orders` rows regenerate every checkpoint, so a
maintenance job can re-derive a match end to end and compare, which is the same verification the
replay serializer performs on a journal file.

The body column keeps the snapshot as a blob rather than as JSON text; SQLite and D1 hold it in one
row under the 2 MB row limit (a native save at format 26 is on the order of 100 KiB to 700 KiB of
UTF-8 depending on turn count, so the existing 1 MiB base64 cap already implied this fits, and the
blob is stored raw, not base64). Postgres uses `bytea`. Compression with the `ReplayArchive` codec
is an option the port makes available since the archive format is part of the replay package; it is
not required by the design.

### The resolution pipeline

The seal today is a compare-and-swap followed by idempotent steps that a sweep can finish. Resolution
is one more such step, placed after the participant set is frozen and before the next turn opens,
and like the others it is conditional on its predecessor so a process that dies mid-resolution is
resumed by the sweep rather than leaving the match stranded.

```text
seal CAS ──> freeze sealedSlots + orderSetHash ──> publish turn.sealed
         ──> resolve: restore match_states[n-1] → apply(sealed set, transfers) → hash
         ──> write match_states[n], turn_verdicts, turns.resolved_state_hash (CAS on resolved_turn)
         ──> publish turn.resolved { turn, stateHash }
         ──> open turn n+1
```

Ordering choices worth stating:

- **`turn.sealed` is published before resolution finishes.** Clients start applying the set the
  moment it seals, as now, and the server resolves in parallel. Neither waits on the other. The
  verdict on a report waits only for `resolved_state_hash`, and a report arriving before the server
  has resolved is held (stored as it is today) and judged when resolution lands.
- **The next turn opens after resolution**, not after the seal. Today it opens at the seal so that
  players plan turn n+1 while reports for turn n arrive. With a server that resolves in well under a
  second this costs nothing observable and buys a simple invariant: the open turn's predecessor is
  always resolved, so the state a late joiner or reconnecting client fetches is always current.
  If measurement on the Worker shows AI planning for six seats crossing into seconds on some turns,
  the open moves back before resolution and the invariant is weakened to "resolved or resolving".
- **Resolution runs on the server's own checkpoint**, never on anything a client sent. The server
  cannot be handed a state.
- **`resolved_turn` only moves forward, the way `currentTurn` does.** `openTurn` no longer writes
  `currentTurn` outright: it goes through `MatchRepository.advanceCurrentTurn`, a conditional write
  that a late sweep re-opening a turn the match has moved past cannot move backwards, and only the
  call that created the turn or advanced `currentTurn` onto it arms the deadline. Resolution follows
  the same rule. The `match_states` insert is keyed on `(match_id, turn)` and refused when the row
  exists; `resolved_turn` advances through one conditional write that is refused unless it moves
  forward; and `turn.resolved` is announced under its own claim on the turn row, as `turn.desynced`
  already is through `claimDesyncAnnouncement`, so a process that dies between the write and the
  announcement leaves it for the sweep and the event is never published twice. A sweep that finds a
  resolution already written skips straight to the announcement claim and the idempotent
  `openTurn`. Because the next turn now opens after resolution, its deadline is armed
  after resolution too, so the time the server spends resolving never comes out of a player's clock.

The verdict logic changes from a consensus to a comparison. A report equal to `resolved_state_hash`
confirms the seat; a report that differs marks that seat desynced, names it in `turn.desynced`, and
leaves every other seat and the match itself running. There is no longer a match-wide pause: the
disagreeing client is told the authoritative hash, fetches the server's snapshot for that turn,
restores it, and re-reports. The match seals its next turn on the schedule it always had. A seat that
is desynced at seal time contributes whatever document it submitted, as an absent seat does today,
because its orders were validated by the server against the server's state and are as good as
anyone's.

The `finished` flag stops being something reports agree on and becomes something the resolver
returns. A match ends when the server's state carries an outcome.

### Order validation at submission

Two distinct questions hide under "should the server refuse an invalid order", and they have
different answers.

**Integrity does not depend on it.** An op the validator rejects is applied by nobody: the core
records the rejection with its code and changes nothing. Once the server computes the state, a
rejected op has no effect on the truth whether it is refused at submission or rejected at the seal.
Refusal adds no integrity; the validator running on the server already guarantees that no illegal
op ever reaches the state.

**Refusal is still done, for every code, because an honest client never sends a rejectable op.**
The game client records an op into its order document only when the core accepted it on the
planning copy (`SpeculativeTurn.Submit`), and that copy is a clone of the same Command-boundary state
the seal will apply the document to. Nothing another seat does during Command changes what this
seat's validator sees: queued commands only resolve in Execution, every seat's hire offers were drawn
on entering Command, and a controller transfer only removes a seat. The client's own reconnect path
already treats a document that does not apply to that state as "a protocol contradiction, not a
partial draft" (`SpeculativeTurn.Restore`). A document carrying an op the validator rejects therefore
comes from a modified or broken client, which is exactly the signal to catch, and catching it at the
door is strictly better than recording it at the seal.

So at `PUT /turns/:n/orders` the server restores the checkpoint the turn opened on, applies any
controller transfers the log already carries, and dry-runs the submitter's document alone and in
order, as the planning copy did. The first op the validator rejects refuses the whole document with
`422 invalid_orders`, naming the op index and the `CommandValidationCode`; nothing is stored, so a
`ready` that arrived with it does not count, and the refusal is logged against the seat. A dry-run
costs one checkpoint restore and a handful of validator calls per submission, which is far below the
cost of the resolution it precedes.

**What does not change is the seal's treatment of a rejected op**, should one still reach it. The
sealed set is applied through the same recorder on the server and on every client, and the replay
format records a rejected op as a step with `accepted: false` and its code. That path stays exactly
as it is, because it is what keeps every client's replay of the sealed set identical to the server's.
After submission-time refusal it should never be taken: a rejection at the seal means the dry-run and
the seal disagreed, which is a server defect (or a transfer that landed between the two) to log and
investigate, not a fact of the match. Neither the refusal nor the logging changes the rules, so
neither moves the session version.

### Snapshots, reconnect and late join

`POST /snapshots` is removed. `GET /snapshots/latest` and `GET /snapshots/:turn` remain, with the same
response shape, and now serve `match_states`. The `uploadedByPlayerId` field is dropped from the view
and `formatVersion` is the native save version the server wrote. A client that reconnects, joins late,
or is told it is desynced fetches the server's snapshot, verifies its `stateHash` against the hash it
recomputes after restoring it, and continues. The client-side reconnect logic in MULTIPLAYER.md's
client contract is unchanged except that step 5 (the host uploads on desync) disappears and step 7
(reconnect from the newest snapshot) always finds one.

The `seatSummaries` the host wrote into `gameSettings` from its snapshot uploads for the public listing
are now derived by the server from its own state at every resolution.

On the client this fits the restore split that already exists. `RestoreAsync` is now two steps:
`RebuildFromHistoryAsync`, which rebuilds the state and announces `Resumed`, and
`ResolvePendingDesyncAsync`, which the pump retries on its own when it runs out of its retry window,
so a long outage never announces the match twice. Under this design the second step stops adopting a
repair somebody posted, or posting one, and becomes: fetch `GET /snapshots/:turn` for the seat's
desynced turn, restore it, verify the hash and re-report. The retry boundary stays where it is. The
best-effort bootstrap upload the restore path still makes goes away with the upload route.

The host role loses its data duties with this change. Today the host uploads the bootstrap, the
ten-turn checkpoints and the seat summaries, and a desync waits for whoever holds the most-reported
hash to post a repair, so a match whose repairers are away stays parked in `desynced`. After it the
host of a running match only kicks. The handoff rules stay as they are: the role moves to the first
active player when the host leaves, a `takeoverPending` host who leaves included, and an abandoned
match pauses when nobody is left to take it. They stop being on the path of a match's progress,
because nothing the match needs is waiting on the host.

### Controller transfers and absence

Nothing changes in the vote machinery. What changes is that the approved transfer, which every client
applies at its event-log position through `TransferPlayerToComputer`, is also applied by the server at
the same position as part of the next `apply`. The transfers are read from the event log between the
previous seal and this one, in sequence order, and passed in `SealedTurnInput.transfers`. This is
the one place the resolver depends on the log, and the log is already durable before it is published.

The events are the three MULTIPLAYER.md names as placing a transfer: `match.playerTakenOver` becomes
`to: 'computer'`, and `match.playerReturned` (for a seat that was computer controlled) and
`match.latePlayerJoined` become `to: 'human'`. The resolver applies the rule the clients apply: a
transfer is ignored once the state carries an outcome, so the server ignores exactly the transfers
every client ignores. Transfers are applied before any document, and the frozen participant set
guarantees what the resolver then sees: `sealedSlots` is drawn from the human participants at the
freeze, so a slot that carries a document is human controlled once the transfers before the seal
are applied. The resolver asserts that rather than assuming it, and a slot that has a document and
ends the transfers computer controlled refuses the resolution as a server defect. The takeover
repository now enforces the upstream half of this too: `TakeoverRepository.openPrompt` opens a
prompt only while the seat has one of the `ABSENT_HUMAN_STATUSES`, so a player who has returned
cannot be voted to the computer by a prompt that outlived their absence.

### Engine version and session version

The engine's exact behaviour is part of the session as stored, so it is covered by the existing
`MULTIPLAYER_SESSION_VERSION` rule in [AGENTS.md](../AGENTS.md#multiplayer-session-version): a rules
change bumps it, and a match records the version it was created under for its whole life. What the
server adds is that it can now enforce the rule rather than merely store the number. The match row
carries the session version it was resolved under, the server refuses to resolve a match whose
session version is not the one its engine implements, and a deployment that ships a new engine
retires the matches in progress exactly as a client build does today. The server does not carry
several engine versions side by side; the design keeps the one-version policy the workspace already
has.

Because the C# hasher already hashes the definition tables into every fingerprint, the server also
pins the SHA-256 of the `original-data.json` it loaded and refuses to start against a data file
whose hash is not the one the engine's conformance vectors were generated for.

### Runtime placement

- **Node**: the resolver runs in-process. A turn's resolution is CPU-bound for tens to a few hundred
  milliseconds, dominated by AI planning; it runs on the request that completed the seal or in the
  sweep, and a `worker_threads` pool is the escape hatch if a public Node deployment shows it blocking
  the event loop. The event loop it blocks is also the one holding every open event stream (up to
  `MAX_EVENT_STREAMS`, with a quarter of that reserved for lobbies), writing their 20-second
  keepalives and re-checking each stream's membership. The game's stream idle deadline is two and a
  half heartbeats, so a resolution blocking for a few hundred milliseconds is far inside it; what
  the pool guards against is many matches sealing at once on one process, which delays every
  stream's keepalive by the sum of them.
- **Cloudflare**: the `MatchHub` Durable Object already exists per match for fan-out and deadlines.
  Resolution moves into it, which serialises resolution per match for free and lets it keep the
  restored `EngineState` of the current turn in memory between seals so that a turn is resolved from
  memory and only the checkpoint is written to D1. The Worker CPU budget is the constraint to
  measure: the engine's per-turn cost has to be characterised in the conformance suite's benchmark
  vectors before the Worker path is committed to.

## What this does and does not defend against

Replacing the paragraph of the same name in MULTIPLAYER.md for the new model.

**Closed:**

- **A doctored state can no longer become the truth.** No client sends state. Nobody uploads
  anything. Snapshot corroboration, its tie-break and the multi-seat collusion it was vulnerable to
  are gone, because there is nothing to corroborate.
- **A stored match is valid.** Every checkpoint is derived by the rules from validated inputs; an
  invalid state cannot be written because nothing but the resolver writes one.
- **Divergence is attributed.** A report that differs from the resolved hash names one client, and
  only that client is told to resync. Griefing by deliberate desync stops working: the match does not
  pause and the other players are not asked to do anything.
- **Any order the rules reject is refused at the door**, with the op index and the validation code.
  Since an unmodified client only ever submits ops its own planning copy accepted, a refusal names a
  client that is broken or tampered with, on the turn it happened.
- **Every seat's orders are secret until the seal, including from the server operator's point of
  view of correctness**: the server reads them at submission only to dry-run them, and the dry-run
  result is returned to the submitter alone.

**Still open, and why:**

- **Hidden information is still held by every client.** Fog, hidden gangs and other players' cash
  are in the state every client restores and hashes. Closing it means the server sending each seat a
  filtered view and the clients no longer resolving turns locally at all, which is a different game
  client rather than a different server. This design is the necessary first step for that: a server
  that owns the state can filter it, and nothing here would have to be redone.
- **A client can still be modified to submit what a human would not.** Any op the rules accept is a
  legal move; a bot playing a seat is indistinguishable from a person, as it is in every game with
  a wire protocol.
- **The operator of a self-hosted server is trusted**, as they were: the resolver runs where they
  can change it. Only the central service's authority is worth more than its clients', and that is
  exactly what the central service is for.

## Conformance between the engines

The port is only as good as the evidence that it behaves identically to `Rechaos.Core`, and the
evidence has to be reproduced on every change to either engine, forever. That is the job of the
conformance suite: a set of executable vectors that both engines run, plus a differential fuzzer
that generates new vectors from random play.

### Design goals

1. **One vector file, two runners.** A vector is JSON data with no code in it. A C# runner and a
   TypeScript runner each read it, execute it, and compare their own result with the expectation. A
   vector that only one engine can run is not a conformance vector.
2. **Fail at the earliest boundary.** Every vector that runs more than one operation records the
   state hash after every operation, as replay steps do, so a divergence is located at the first
   operation that differs and not reported as a different final hash forty turns later.
3. **Produce a diffable state on failure.** Both runners can dump the state at the failing step
   as the native save JSON. The existing `Rechaos.Tools state-diff` and a TypeScript equivalent then
   name the first differing field with its label. The failure triage list in
   [VALIDATION.md](VALIDATION.md#failure-triage) applies unchanged.
4. **Sanitized by construction.** Vectors carry mechanical state and ids only; names in them are the
   derived seat names. No original text or media is in a vector, so vectors are committed.
5. **The C# is the oracle until the suite says otherwise.** Expected values in every committed vector
   are generated by `Rechaos.Core`, because it is the engine whose parity with the original game is
   documented. The TypeScript engine is conformant when it reproduces them. Once both engines pass
   every vector, either can generate new ones and the other confirms.

### Layout

```text
conformance/
  README.md
  schema/
    vector.schema.json          one JSON Schema, a tagged union on `kind`
  vectors/
    prng/                       …
    hash/
    city/
    validation/
    resolution/
    ai/
    turn/
    match/
    snapshot/
    bench/
  generated/                    output of the fuzzer, not committed; promoted into vectors/ by hand
```

The directory sits at the repository root, beside `docs/` and `multiplayer/`, because it belongs to
neither side: `tests/Rechaos.Tests` reads it through a `ConformanceVectors` test class, and
`multiplayer/packages/engine` reads it through a vitest suite. The existing
`multiplayer/packages/conformance` package keeps its meaning (storage and HTTP behaviour of server
implementations) and is not where engine vectors live.

### Vector format

Every vector is one JSON object:

```jsonc
{
  "schemaVersion": 1,
  "id": "RESOLUTION.COMBAT.0007",
  "kind": "resolution",
  "engine": { "fingerprint": 1, "nativeSave": 26, "replay": 30, "dataSha256": "e65f80e4…" },
  "provenance": { "generatedBy": "Rechaos.Core", "commit": "…", "generatedAtUtc": "…",
                  "source": "fuzz:seed=1977:match=12:turn=7" },
  "description": "Two-gang attack into a defended sector with a Crackdown pending",
  "input":  { … kind-specific … },
  "expect": { … kind-specific … },
  "notes": [ "…" ]
}
```

The `engine` block pins the format versions the expectations were produced under. A runner whose
engine implements a different version skips the vector with a named reason rather than failing it,
and the suite fails if any vector was skipped on the engine's own current versions, which is how a
session-version bump forces every vector to be regenerated in the same change.

`input` states are expressed in one of two ways, and each vector says which:

- **`seedSetup`**: a seed, scenario, duration, AI settings and six seat definitions, from which both
  engines bootstrap. Cheap, and exercises city generation on every run.
- **`snapshot`**: an inline native save document (format 26). Used when a vector needs a state
  that random play reaches rarely (a specific site combination, a near-eliminated player), and for
  the snapshot vectors themselves.

### Vector kinds

| Kind | Input | Expectation | What it pins |
|---|---|---|---|
| `prng` | seed (uint32), a list of calls (`raw`, `inclusive(max)`, `int(max)`) | each return value, the state and consumption count after each | the LCG step, the three-draw wrapper, the clamp at `max < 1`, `uint` wraparound |
| `hash` | a snapshot, or a list of byte strings to chain | the hex of the complete encoded byte stream and its XxHash128 fingerprint; separately the boundary fingerprint (the same stream without the phase history); for a chain input, the digest after every link | every byte of `MatchStateHasher`, including string prefixes, nullable flags, ordering of every collection, and the definition block, plus the port's XxHash128 and the `Chain` step in isolation. Dumping the bytes, not only the digest, is what makes a mismatch locatable |
| `city` | seed, scenario, seat definitions | every sector's income, tolerance, three site ids and resistances, headquarters assignment, landmark placement, the PRNG consumption count afterwards | `OriginalCityGenerator`, `AssignHeadquarters`' rejection sampling, `ApplyScenarioLandmarks`, `MatchBootstrap` |
| `validation` | a state and one `GameCommand` (or a cancellation) | the `CommandValidationCode` and, on acceptance, the event appended | `CommandValidator` in every branch: one vector per code per action that can produce it |
| `resolution` | a state at Command with queued commands, and a single phase transition (`finishCommand`, `finishExecutionPhase`, `finishHire`, `finishPlayerElimination`, `finishUpkeep`) | the state hash after, the events appended, the PRNG consumption delta | each resolver in isolation: combat, transactions, chaos, movement, control, economy, hire, crackdown, tolerance, elimination, outcome |
| `ai` | a state and a seat | the ordered commands `AiPolicyPlanner.Plan` returns, the hire choice or snub `PrepareAiHiring` returns, the AI planning tables after | the twelve planner families and both policies; this is the largest kind by count because the planners are half the core |
| `turn` | a state at Command, a sealed set (documents by slot), transfers | per-op verdicts, the state hash at every replay step, the final Command-boundary hash | `SealedTurnApplier.Apply` end to end, which is the function the server calls |
| `match` | `seedSetup` and a list of sealed sets, one per turn | the Command-boundary hash after every turn and the outcome | whole matches; the long-running tier |
| `snapshot` | a state | the native save JSON both engines write, byte for byte, and the hash after restoring it | `NativeSaveSerializer` in both directions, so a server snapshot loads in the game and a game save loads in the server |
| `orders` | an order document | its canonical JSON text and digest | already pinned in kernel and C# tests; moved here so every digest rule is in one place |
| `bench` | `seedSetup` plus a turn count | no expectation; the runner reports wall time per turn and the peak state size | the Worker CPU question, answered by numbers rather than estimates |

A `snapshot` vector deserves one more sentence. The C# writes JSON through `System.Text.Json` with
camel-case names and specific converters for `PlayerId`, `GangId` and `CommandTarget`; a byte-equal
expectation requires the TypeScript writer to reproduce key order and number formatting exactly. That
is a stronger property than the server needs (the game loader accepts any key order) but it is cheap
to hold and it turns "loads in the game" into a property tested without the game.

### Runners

**C#**: a `ConformanceVectorTests` class in `tests/Rechaos.Tests` discovers every file under
`conformance/vectors`, one xUnit theory case per vector, tagged by kind. `match` vectors above a turn
threshold are tagged `LongRunning`, keeping the fast gate's timing. A `Rechaos.Tools engine-conformance`
subcommand runs the same code outside xUnit with `--generate` (write `expect` from the engine's own
result, for authoring), `--verify` (the default), `--dump-on-failure <dir>` (write the state at the
failing step as native save JSON) and `--filter <glob>`.

**TypeScript**: `packages/engine/test/conformance.spec.ts` does the same with vitest, one `it` per
vector, and `pnpm --filter @chaos-overlords/engine conformance -- --generate|--verify|--dump-on-failure`
mirrors the tool. The two command lines are kept the same shape on purpose: a divergence is
investigated by running both against one vector and diffing two dumps.

**Diffing**: both dumps are native save JSON, so `Rechaos.Tools state-diff expected.json actual.json
--labels docs/schemas/state-labels.example.json` already names the first differing field. The engine
package gets the same 150-line differ so a TypeScript developer does not need the .NET toolchain to
read a failure.

### The differential fuzzer

Hand-written vectors pin known cases; the fuzzer finds unknown ones. It is a small driver, written
once in each language, that plays random matches and compares hashes at every replay step:

1. Draw a fuzz seed. From it derive, deterministically, a match seed, a scenario, a duration, which
   seats are human, and a per-turn stream of random *representable* order documents for the human
   seats (the same generator both drivers implement; representable means schema-valid, not
   rules-valid, so rejected ops are exercised too).
2. Bootstrap, then for each turn build the sealed set, `apply`, and record every replay step hash.
3. Emit a `match` vector with the recorded hashes.

The C# driver generates; the TypeScript driver verifies (and vice versa once both pass). A failing
fuzz seed is reduced by the driver to the shortest prefix of turns that still diverges, then to the
smallest sealed set on the diverging turn that still diverges, and the result is promoted into
`conformance/vectors` as a named regression vector with its provenance recorded. The reduction is
what keeps the committed corpus small and every vector in it meaningful.

The nightly AI campaign workflow gains a fuzz job: a fresh fuzz seed per night, a fixed budget of
matches, and a failure that uploads the reduced vector as an artifact and opens nothing on its own.
The fast gate runs the committed corpus only.

### Coverage the suite has to reach before the server trusts the engine

Passing vectors is necessary; the corpus also has to be known to cover the rules. Three coverage
statements are required before the resolver goes live, each recorded in the parity matrix:

- Every `CommandValidationCode` and every `GangAction` has at least one `validation` vector that
  produces it, on both engines.
- Every AI family and both policies have `ai` vectors drawn from at least ten distinct match states
  each, and every `OriginalAi*Rules` table has been hashed into a `hash`-style vector of its own so a
  transcription error in a constant is caught without a match reaching the branch that reads it.
- Every `ExecutionPhase` and every `TurnPhase` transition has `resolution` vectors covering the
  rules the corresponding `RULE-*` entries in [the spec](../spec/README.md) describe, listed by ID
  in the vector's `notes`.

Line coverage on the TypeScript engine, measured under the corpus, is the cheap proxy to watch
between those statements: a rule file below the engine-wide figure is a rule file the corpus does not
reach.

## Interaction with the existing documents and rules

- **AGENTS.md session version rule**: unchanged in wording; the server now enforces it. A change to
  the engine is a change to "the deterministic rules or the resolution of a turn" and bumps it, and
  the vector corpus is regenerated in the same change because the `engine` block on every vector will
  no longer match.
- **Protocol version**: this change moves the wire (a removed upload route, a changed snapshot view,
  a new `turn.resolved` event, a `422 invalid_orders` refusal, the changed shape of `turn.desynced`),
  so it bumps `MULTIPLAYER_PROTOCOL_VERSION`. It does not by itself change the rules, so the session
  version moves only if the engine port is found to differ from the C# in a way resolved by changing
  the C#.
- **NATIVE-SAVE-FORMAT.md**: gains a statement that the server writes the format and which fields
  the server's writer guarantees byte-equal; the compatibility policy is unchanged.
- **MULTIPLAYER.md**: the security model and the "does and does not defend against" section are
  rewritten from the section above once the resolver ships; the turn lifecycle diagram gains the
  `resolved` step; the limitations list loses the three entries about desync recovery by count.
- **VALIDATION.md**: the conformance corpus becomes a fifth fixture class, "Conformance vector",
  and the fast gate description gains the corpus; the failure triage list is reused as is.
- **PARITY.md**: no change. The vectors compare the engine with its own recorded output, and the
  matrix's Tests column lists only tests that compare the rebuild with evidence from the original.

## Risks

- **The port diverges in a way the corpus does not catch.** Mitigated by generating vectors from
  random play rather than only by hand, by hashing every replay step rather than turn boundaries,
  and by the coverage gates. Not eliminated: an untested rule is an untested rule, which is why the
  rollout below keeps clients resolving and reporting for a whole phase during which the server's
  verdict is advisory.
- **AI planning cost on the Worker.** Six computer seats through twelve planner families is the
  expensive path. The `bench` vectors answer it with numbers before the Worker path is committed;
  the fallback is to open the next turn before resolution completes, as discussed above.
- **Two engines to maintain.** Every rules change is now made twice, with the conformance suite as
  the referee. That is the accepted price of a server that runs everywhere the workspace already
  runs, and it is bounded: the rules layer is about 7,400 lines and the planners about 6,800, both
  stable since the parity work, and a change to either is already a documented `RULE-*` or
  `DECISIONS.md` event.
- **Snapshot byte-equality is brittle.** A `System.Text.Json` behaviour change on a .NET upgrade
  would fail every `snapshot` vector. The vectors say so at once, and the expectation is regenerated
  in the same upgrade, which is the correct outcome.

## Phased plan

Each phase is shippable on its own and none changes the rules a client plays.

1. **Corpus and C# runner.** The vector schema, the `conformance/` layout, the C# generator and
   verifier, and a first corpus generated from the existing test matches and the headless runner. No
   TypeScript yet. This is the phase that fixes the format the port is held to.
2. **Engine port, layer by layer against the corpus.** `random` and `hash` first, because every
   later vector depends on them; then `data`, `city` and `model`; then `rules` with `validation` and
   `resolution` vectors; then `ai`; then `replay` and `snapshot` with `turn` and `match` vectors. The
   fuzzer is written when `turn` vectors pass, and runs nightly from then on.
3. **Shadow resolution.** The kernel gains the `TurnResolver` port and the server resolves every
   sealed turn, stores its checkpoint and its verdicts, and *logs* disagreement with the client
   consensus without acting on it. Protocol unchanged. This phase runs on the central service long
   enough to see real matches with real players, which the corpus cannot generate.
4. **Authoritative verdicts.** The server's hash becomes the verdict, per-seat desync replaces the
   match-wide pause, and the snapshot routes serve `match_states`. `POST /snapshots` is removed and
   the protocol version moves. Submission-time refusal of rejected orders ships here.
5. **Retire the client-side path that no longer has a purpose.** The host's upload code and the
   corroboration logic are deleted from both the kernel and the game client; the storage migration
   drops the `snapshots` table once every live match has a server checkpoint. The match-wide
   `desynced` status goes with them, and so do the paths that exist only to get a match out of it:
   `resumeAfterDesync`, the verdict repair in `reevaluate` and the sweep's pass over paused matches.

What comes after, and is not planned here: per-seat filtered state, which is what finally closes
the hidden-information gap, and a browser client on the engine package.
