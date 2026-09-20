# Multiplayer implementation review: robustness and efficiency

Status: findings addressed
Last updated: 2026-09-20

A code review of online play as implemented: the coordination server under
[`multiplayer/`](../multiplayer/README.md) (kernel, storage, HTTP and SSE layer, both runtimes) and
the game's client in [`src/Rechaos.Multiplayer`](../src/Rechaos.Multiplayer/README.md) with its
integration in `Rechaos.Game`. Every path was asked the same two questions: what breaks it, and what
does it cost. The design is in [MULTIPLAYER.md](MULTIPLAYER.md) and is not re-argued here; the
findings are about the implementation of that design, and every one was checked against the source
before it was written down.

Each finding names the code, says what it did, gives the concrete failure or cost, and recommends a
change. Severity is about consequence to a running match, not about how hard the fix is. Line
numbers are as of the commit this review was written against.

**Every finding below has been addressed**, in the order the closing section recommends. The
findings are kept in the present tense they were written in, because they are the record of what was
wrong and why it mattered; what each one led to is summarised under [Resolution](#resolution), and
the code carries the reasoning at the place it applies. Two recommendations were not followed as
written, and both are named there.

<!-- doc-index:begin toc depth=3 -->
- [Verdict](#verdict)
- [What is already done well](#what-is-already-done-well)
- [Findings at a glance](#findings-at-a-glance)
- [High: paths that leave a match unplayable](#high-paths-that-leave-a-match-unplayable)
  - [R1. Desync repair only works on a live host inside a narrow window](#r1-desync-repair-only-works-on-a-live-host-inside-a-narrow-window)
  - [R2. The client gives up where it should resynchronise](#r2-the-client-gives-up-where-it-should-resynchronise)
- [Server: turn barrier and storage](#server-turn-barrier-and-storage)
  - [S1. The seal freeze is not a compare-and-swap on what it freezes](#s1-the-seal-freeze-is-not-a-compare-and-swap-on-what-it-freezes)
  - [S2. A departure overwrites a seat's status without a compare-and-swap](#s2-a-departure-overwrites-a-seats-status-without-a-compare-and-swap)
  - [S3. A paused match is re-judged every sweep, with a log scan and a megabyte read each time](#s3-a-paused-match-is-re-judged-every-sweep-with-a-log-scan-and-a-megabyte-read-each-time)
  - [S4. Readiness is republished on every ready submission](#s4-readiness-is-republished-on-every-ready-submission)
  - [S5. The sweep and the public listing scale with every live match](#s5-the-sweep-and-the-public-listing-scale-with-every-live-match)
  - [S6. Smaller storage-path items](#s6-smaller-storage-path-items)
- [Server: streams, HTTP and runtimes](#server-streams-http-and-runtimes)
  - [S7. Fan-out costs one database read and one validation per stream per event, and per heartbeat](#s7-fan-out-costs-one-database-read-and-one-validation-per-stream-per-event-and-per-heartbeat)
  - [S8. Every JSON response is serialised, cloned, re-parsed and re-validated](#s8-every-json-response-is-serialised-cloned-re-parsed-and-re-validated)
  - [S9. The bug-report journal budget is spent before the request is read](#s9-the-bug-report-journal-budget-is-spent-before-the-request-is-read)
  - [S10. Validation refusals echo the refused value for type mismatches](#s10-validation-refusals-echo-the-refused-value-for-type-mismatches)
  - [S11. Smaller HTTP and runtime items](#s11-smaller-http-and-runtime-items)
- [Client: session, stream and outbox](#client-session-stream-and-outbox)
  - [C1. A reconnect replays the whole match from turn 0, one round trip per turn](#c1-a-reconnect-replays-the-whole-match-from-turn-0-one-round-trip-per-turn)
  - [C2. Every command the player queues hashes the whole match twice on the game thread](#c2-every-command-the-player-queues-hashes-the-whole-match-twice-on-the-game-thread)
  - [C3. Every resolved turn does a save round trip and an fsync on the game thread](#c3-every-resolved-turn-does-a-save-round-trip-and-an-fsync-on-the-game-thread)
  - [C4. A server that accepts and immediately closes is reconnected to every second, forever](#c4-a-server-that-accepts-and-immediately-closes-is-reconnected-to-every-second-forever)
  - [C5. The countdown trusts the local clock against a server timestamp](#c5-the-countdown-trusts-the-local-clock-against-a-server-timestamp)
  - [C6. The handshake is taken once and never revisited](#c6-the-handshake-is-taken-once-and-never-revisited)
  - [C7. Smaller client items](#c7-smaller-client-items)
- [The TypeScript client](#the-typescript-client)
- [Test coverage gaps](#test-coverage-gaps)
- [Recommended order of work](#recommended-order-of-work)
- [Resolution](#resolution)
  - [High](#high)
  - [Server](#server)
  - [Client](#client)
  - [Where the review was not followed](#where-the-review-was-not-followed)
  - [Test coverage](#test-coverage)
<!-- doc-index:end -->

## Verdict

The implementation is unusually careful for a project of this size. The lockstep contract is narrow
and stated once, every state change on the server is a conditional statement whose row count decides
a race, the event log allocates its own gapless sequence, delivery is at-least-once with idempotent
handlers on both ends, inputs are bounded at every door, and the client retries the right things and
refuses the right things. Most of the review went into trying to break the turn barrier under
interleavings, and the barrier held.

What did not hold is the recovery story around it. Desync repair, the one mechanism that turns a
detected divergence back into a playable match, works only when the host is live, saw the event, and
has not yet applied the next turn (R1). And the client has three separate places where it destroys a
recoverable session instead of resynchronising it (R2). Everything below those two is either a
genuine but narrow race (S1, S2), server work that is out of proportion to the fact it establishes
(S3, S7, S8, C1, C2), or polish.

None of the findings requires a wire change, though R1 wants one server rule tightened. Several want
a storage-port addition, which by the workspace's own rule means both repositories, the in-memory
reference storage and the conformance suite move in the same change.

## What is already done well

Named so the findings are read in proportion.

- **Race handling without transactions.** `claimSeat`, `submitOrders`, `transition`,
  `transitionStatus`, `castVote`, `openPrompt` and `createLate` are each one statement that is also
  the check. `players.create` is an insert fed by a select over the match row, so a seat claimed a
  moment before `start` cannot land in a running match. `players.delete` returns whether a row went,
  so a doubled `leave` cannot release two seats. The SQLite and Postgres repositories differ only in
  dialect, and the in-memory reference storage runs the same conformance suite.
- **A gapless, self-allocating event log.** `append` takes `max(seq) + 1` inside the insert, retries
  on the primary key, and the publisher fans out only after the row is durable. A stream cursor can
  never skip an event and a lost notification costs one heartbeat. Every sweep query has an index
  that matches it, and every child table's primary key leads with `match_id`, so cascading deletes
  are indexed too.
- **Repairable seals and verdicts.** `completeSeal` and `settle` are written so that a process dying
  at any `await` leaves a state the sweep finishes, the sweep guards each match so one that throws
  cannot starve the pass, and the kernel tests cover the interrupted paths.
- **Bounded streams and bounded input.** Per-player, per-match and per-process stream caps; a kick
  that hangs up streams the revoke alone could not reach; backpressure-aware draining; body limits
  per route registered before the contract validator; a settings blob capped in bytes and depth;
  order documents held to a closed op vocabulary with every id range-checked and every op attributed
  to the submitter's own slot; safe integers only, `-0` refused.
- **The client's retry line is drawn once.** `TransientFailure.IsTransient` decides for the stream
  and for every call a fact leads to; a request deadline is told apart from caller cancellation; the
  outbox keeps only the newest whole-document replacement, cancels a superseded in-flight one, and
  requeues rather than drops a document when its window expires.
- **The event stream parser is bounded** in line length and frame size before any buffer grows, the
  idle timer is re-armed per line, keepalives are surfaced so the idle detector can work, and a
  frame's `id:` is reconciled with its payload.
- **Recovery is verified, not trusted.** A snapshot is refused unless it hashes to what it claims; a
  reconstructed history is checked against every `turn.confirmed` hash it passes; a saved draft is
  proven to still apply by replaying it onto the resumed state, off the game thread; the recovery
  file is written through a temp file and an atomic rename, with the token sealed on Windows.
- **Determinism is treated as a contract.** The state hash is a hand-written encoding with explicit
  ordering everywhere; computer-controlled seats are read from hashed state rather than the roster;
  hire offers are drawn once per Command entry for every seat; canonical JSON is pinned to the
  server's rules by a shared golden document.

## Findings at a glance

| ID | Severity | Area | Summary |
|---|---|---|---|
| [R1](#r1-desync-repair-only-works-on-a-live-host-inside-a-narrow-window) | High | Both, robustness | A desync can be repaired only by a host that is online, handled the live `turn.desynced`, and has not yet applied the following turn. A host restart, a desync detected after the next seal, or a host that is the outlier each leave the match paused for good. The server also accepts an uncorroborated snapshot for a sealed turn that has no reports yet. |
| [R2](#r2-the-client-gives-up-where-it-should-resynchronise) | High | Client, robustness | The 30-second resolution watchdog fires before the 50-second stream idle detector; the stream's five-minute window ends the session while the outbox on the same session retries forever; a status-only 4xx from a proxy is treated as a revoked membership. Each ends a recoverable session. |
| [S1](#s1-the-seal-freeze-is-not-a-compare-and-swap-on-what-it-freezes) | Medium | Server, robustness | Two concurrent `completeSeal` calls can both freeze a sealed turn; a departure between them publishes two `turn.sealed` events with different digests, which a client treats as a protocol failure. |
| [S2](#s2-a-departure-overwrites-a-seats-status-without-a-compare-and-swap) | Medium | Server, robustness | `remove` writes `left`/`kicked` with an unconditional `setStatus`, so it can overwrite a `computer` status the takeover tally just won and reopen a prompt for a seat every client already plays as AI. |
| [S3](#s3-a-paused-match-is-re-judged-every-sweep-with-a-log-scan-and-a-megabyte-read-each-time) | Medium | Server, efficiency | The desync verdict pages through the whole event log and loads the full snapshot body to read one hash; the sweep re-runs both every 15 seconds for every paused match. |
| [S4](#s4-readiness-is-republished-on-every-ready-submission) | Low-Med | Server, robustness | Every `ready: true` submission appends a `turn.readiness` event, so one member can grow a match's log at the member rate limit. |
| [S5](#s5-the-sweep-and-the-public-listing-scale-with-every-live-match) | Low-Med | Server, efficiency | `listStalledSeals` joins every live match every tick, including months of abandoned ones; `listDesynced` can starve; the public listing is two queries per running match. |
| [S6](#s6-smaller-storage-path-items) | Low | Server | Late-join capacity is not atomic; `submitOrders` loads the previous full document for one flag; a snapshot re-upload keeps stale version columns; `append` retries without backoff; the clock can restart behind a vote that is opening. |
| [S7](#s7-fan-out-costs-one-database-read-and-one-validation-per-stream-per-event-and-per-heartbeat) | Medium | Server, efficiency | Each subscriber drains the log with its own query and formats its own frame; each heartbeat also queries. A stalled consumer is never dropped, and a reconnect at the process cap is refused before the caller's own stale stream is closed. |
| [S8](#s8-every-json-response-is-serialised-cloned-re-parsed-and-re-validated) | Medium | Server, efficiency | Response validation stringifies, clones, parses and walks every JSON response, including the megabyte snapshot on every reconnect. |
| [S9](#s9-the-bug-report-journal-budget-is-spent-before-the-request-is-read) | Low-Med | Server, robustness | The per-address daily journal budget is charged before validation and for reports that carry no journal; the map it lives in is what the design doc says does not exist. |
| [S10](#s10-validation-refusals-echo-the-refused-value-for-type-mismatches) | Low | Server, security | valibot's default messages carry the received value for type, literal and picklist failures, against the stated "described, not echoed" rule. |
| [S11](#s11-smaller-http-and-runtime-items) | Low | Server | `X-Request-Id` is missing on the stream response; PBKDF2 at 120k iterations sits on the unauthenticated join door; Durable Object calls have no deadline; `openTurn` fails the seal on a scheduling error; lobby polling; a few more. |
| [C1](#c1-a-reconnect-replays-the-whole-match-from-turn-0-one-round-trip-per-turn) | Medium | Client, efficiency | Only turn 0 and desync repairs are ever snapshotted, so a reconnect at turn 80 is 80 sequential sealed-set fetches and 80 turn resolutions before the player sees anything. |
| [C2](#c2-every-command-the-player-queues-hashes-the-whole-match-twice-on-the-game-thread) | Medium | Client, efficiency | The speculative copy routes every click through the replay recorder, which serialises and SHA-256s the whole state twice per operation, on the render thread, for a journal that is thrown away. |
| [C3](#c3-every-resolved-turn-does-a-save-round-trip-and-an-fsync-on-the-game-thread) | Low-Med | Client, efficiency | The pump clones the state for the notice, the game thread clones it again to build the planning copy, and then fsyncs the recovery file, all in the frame the new turn appears. |
| [C4](#c4-a-server-that-accepts-and-immediately-closes-is-reconnected-to-every-second-forever) | Low-Med | Client, robustness | One keepalive frame resets the outage budget, so a proxy that accepts and drops is hammered at the first backoff step indefinitely. |
| [C5](#c5-the-countdown-trusts-the-local-clock-against-a-server-timestamp) | Low | Client, robustness | The turn countdown is server deadline minus local `UtcNow`; a skewed clock shows and warns at the wrong time. |
| [C6](#c6-the-handshake-is-taken-once-and-never-revisited) | Low | Client, robustness | A protocol mismatch after a server upgrade mid-session surfaces as unrelated request failures rather than the handshake's message. |
| [C7](#c7-smaller-client-items) | Low | Client | A failed vote is invisible; a stale refusal overwrites the new turn's status line; the outbox cancels under its lock; the `HttpClient` never recycles connections; a corrupt recovery file is discarded; three-pass JSON reads; per-frame allocations. |

## High: paths that leave a match unplayable

### R1. Desync repair only works on a live host inside a narrow window

**Where.**

- Client: `src/Rechaos.Multiplayer/Session/MultiplayerMatchSession.cs`, `HandleDesyncAsync`
  (around lines 566 to 597), `AdoptSnapshotAsync` (the `turn < Coordinator.Turn - 1` guard);
  `MultiplayerMatchSession.Restore.cs`, `ApplyHistoricalEventAsync` (no case for
  `TurnDesyncedEvent` or `SnapshotAvailableEvent`) and `AdoptResumeSnapshot` (adopts, never
  reports); `src/Rechaos.Game/ChaosGame.MultiplayerNotices.cs`, the `Desynced` case (lines 206 to
  228), which calls `ShowOnlineMatchFailure` for a host whose hash is not a candidate.
- Server: `multiplayer/packages/kernel/src/services/SnapshotService.ts`, `upload` and
  `requireCorroboration` (around lines 80 to 97 and 156 to 171); `logic/turn-logic.ts`,
  `authoritativeCandidates` returns `[]` when a turn has no reports.

**What the code does.** The repair is driven entirely by the live `turn.desynced` event on the
host's pump: the host hashes its *current* state, and uploads it labelled as the desynced turn if
that hash is among the candidates the event named. Nothing else ever uploads a repair, and the
restore path neither notices a `desynced` match nor replays the desync or snapshot events.

**Failure scenarios.** Each of these was traced through the code and each leaves the match
`desynced` until retention collects it:

1. **The desync is detected after the next turn sealed.** Reports for turn N arrive after turn
   N+1 has sealed (a timed match and one slow client is enough: N+1 seals on its deadline with the
   slow seat `takeoverPending`, and that seat's late report for N disagrees). Every client has
   applied N+1 by the time `turn.desynced(N)` arrives. The host's current hash is the hash after
   N+1, which is not among N's candidates, so `canRepair` is false and the host client shows a
   terminal failure. Even if it could upload, every other client is on N+2 and
   `AdoptSnapshotAsync` ignores a repair for a turn older than the one it last resolved.
2. **The host restarts during the pause.** `RestoreAsync` replays history to the same state, the
   `turn.desynced` event is behind `view.LastEventSeq` and is never re-delivered, `_unreportedSeals`
   re-reports N with the same hash, and nothing uploads.
3. **A non-host restarts after the repair was posted.** `AdoptResumeSnapshot` loads the snapshot
   but never reports its hash, so the server keeps waiting on that seat.
4. **The host is the odd one out.** Its client ends itself with the failure above; no other client
   is allowed to upload; reconnecting is scenario 2.

The server-side half: `upload` accepts a repair for any `turn < currentTurn` while the match is
`desynced`, and `requireCorroboration` returns early when there are no reports to count. In
scenario 1, turn N+1 is `sealed` with no reports, so a host may store any hash for it, after which
`evaluateConsensus` with an authoritative hash can only confirm or wait, never desync. That is the
"host arbitrates a disagreement it is party to" case the comment says the check prevents. The
honest client never does this, but the rule is meant to bind a dishonest one.

**Recommendation.**

- **Client, live path.** Make the repair independent of where the host's state currently is: the
  host keeps the hash and a clone (or the compressed snapshot bytes) of the last confirmed or
  sealed turn until `turn.confirmed` for it arrives, so `HandleDesyncAsync` can upload turn N's
  state after applying N+1. Cheaper alternative that reuses existing code: on `turn.desynced(N)`
  with `Coordinator.Turn > N + 1`, rebuild N's state by loading the latest snapshot at or below N
  and replaying the sealed sets up to N (this is what `RestoreAsync` already does), then upload.
- **Client, adopting an older repair.** Replace the `turn < Coordinator.Turn - 1` early return in
  `AdoptSnapshotAsync` with the restore path: adopt the snapshot, replay the sealed sets after it
  from the log, re-report every turn that is unsettled, and hand the interface a `Resynced` notice.
- **Client, restore path.** In `RestoreAsync`, when `view.Status == Desynced` (or a `previousTurn`
  is `desynced`), re-run the repair from the view: the host uploads if its reconstructed hash is a
  candidate; every client whose local hash differs from a posted snapshot's hash adopts it and
  re-reports; every unsettled turn whose hash the client holds is reported (extend
  `_unreportedSeals` to cover a turn adopted from a snapshot).
- **Server.** In `SnapshotService.upload`, a non-bootstrap upload must name a turn whose status is
  `desynced`, not merely one below `currentTurn`; equivalently, refuse when `candidates.length === 0`
  for any turn at or above 1. Add the case to `services.spec.ts`.
- **Escape hatch.** The outlier-host case has no client-only fix: either let any player whose hash
  is a majority candidate upload the repair (a small server rule change; the corroboration check
  already does the counting), or let the host adopt a peer's state. Until then, the game should
  keep the host's session alive with a "waiting for a repair" state rather than ending it.

### R2. The client gives up where it should resynchronise

Three independent policies on one session each end it over a condition the session could ride out.
The recovery record and `RestoreAsync` make the manual reconnect work, so nothing is lost for good;
what is lost is the city screen, the planning copy and the player's confidence, each time.

**(a) The resolution watchdog fires before the stream idle detector.**
`src/Rechaos.Game/ChaosGame.Multiplayer.cs` line 15 sets `OnlineResolutionGrace` to 30 seconds;
`ChaosGame.MultiplayerFailure.cs`, `CheckOnlineResolutionWatchdog`, calls `ShowOnlineMatchFailure`
when every seat is ready and no seal has arrived in that time. `MatchEventStream.DefaultIdleTimeout`
is two and a half heartbeats, 50 seconds. The server seals in the same request that completes the
roster, so the ready `PUT` succeeds on a fresh connection while `turn.sealed` goes out on a stream
that a suspended laptop or an expired NAT entry has silently killed. The stream would notice at
50 seconds and resume from `Last-Event-ID`; the watchdog tears the session down at 30. The
`Desynced` notice also never clears `ResolutionExpectedSince`, so a legitimately slow repair on the
previous turn can trip it too.

**(b) The stream's five-minute window ends the session; the outbox's does not.**
`Http/RetryPolicy.cs`: `Stream` and `Call` both carry `MaxElapsed: 5 minutes`.
`MatchEventStream.ReadAsync` throws `RetryExhaustedException` when the window closes, and
`MultiplayerMatchSession.RunPumpAsync` catches it as a generic failure and calls `Fail`. The outbox
on the same session (`MultiplayerMatchSession.Outbox.cs`, `DrainOutboxAsync`) catches the same
exception, reports it on its lane, requeues the document and starts another window. A six-minute
sleep or a slow redeploy therefore keeps the player's draft and loses their session.

**(c) A status-only 4xx is a revoked membership.** `Http/MultiplayerApiException.cs`,
`EndsTheStream`: every status below 500 except 408, 425 and 429 is terminal, whether or not the
body was the server's error envelope; `MultiplayerFailureText.IsMembershipRevoked` reads any 401
the same way. `FromResponseAsync` deliberately builds a typed exception from an HTML page or an
empty body, so a reverse proxy or tunnel answering 404 or 403 for every path while the backend
restarts ends the session. `src/Rechaos.Game/MultiplayerRecoveryReconciliation.cs` already states
the correct rule for the recovery file ("a refusal that names a membership reason came from this
server's own error handler and is definitive; anything else might be transient") and the match
session does not apply it.

**Recommendation.**

- Turn the watchdog into a resync: a session method that drops the stream connection and runs
  `RestoreAsync(_resumeAfterSeq)`, with the grace set above idle timeout plus first backoff (say
  75 seconds), and clear `ResolutionExpectedSince` in the `Desynced` case. Only fail the session if
  the resync itself fails permanently.
- Treat the retry window as the bound on *silent* waiting, not on the session: while the connection
  modal shows the attempt log and a way to stop, retry indefinitely at the same backoff ceiling,
  and let the five-minute mark change the modal's wording rather than destroy the session. Make it
  a session option so `OnlineSmoke` can keep a bounded window.
- Make `EndsTheStream` and `IsMembershipRevoked` require an envelope with a membership reason
  (`invalid_token`, `missing_token`, `unknown_match`, `unknown_player`, `not_active`); retry a
  status-only 4xx like a 5xx.

## Server: turn barrier and storage

### S1. The seal freeze is not a compare-and-swap on what it freezes

**Where.** `multiplayer/packages/kernel/src/services/TurnService.ts`, `completeSeal`, the
`orderSetHash === null || sealedSlots === null` branch (around line 141) and the `transition` that
writes the digest (around line 172). `listStalledSeals`
(`packages/storage/src/sqlite/repositories.ts` around line 504) returns every live match whose
current turn is not `open`, which is exactly the window between the seal's compare-and-swap and
`openTurn` advancing `currentTurn`.

**What the code does.** After the seal's compare-and-swap (`open` to `sealed`) the participant set
and digest are computed from the current roster and written with `turns.transition(matchId, number,
[turn.status], { status: turn.status, orderSetHash, sealedSlots })`. That statement is conditional
on the *status* only. The sweep deliberately runs `completeSeal` against a seal in flight (see the
comment above the loop in `sweep`), so two callers can be in this branch at once: both read a null
digest, both compute, both write, both publish `turn.sealed`.

**Failure scenario.** A `leave` or `kick` lands between the two computations
(`LobbyService.remove` sets the status without a compare-and-swap, see S2). `humanParticipants`
changes, the second caller computes a different `sealedSlots` and `orderSetHash` and overwrites the
first. Every client receives two `turn.sealed` events for one turn with different digests. A client
that fetches the set after the overwrite verifies it against the first announced hash, and
`FetchAndApplySealedTurnAsync` throws "the sealed-set digest for turn N does not match the event
log", which is terminal for that session. Without a departure the duplicate event is harmless, but
the design doc promises the set is immutable from the compare-and-swap onwards, and here it is not.

**Recommendation.** Make the freeze a real compare-and-swap: add `order_set_hash is null` to the
statement's `where` (a `freezeSeal` port method, or an `expectUnfrozen` option on `transition`) and
publish `turn.sealed` only from the caller whose statement changed a row. Port, both repositories,
`InMemoryStorage` and the storage conformance suite move together, and a kernel test should run two
`completeSeal` calls concurrently with a roster change between them and assert one event.

### S2. A departure overwrites a seat's status without a compare-and-swap

**Where.** `multiplayer/packages/kernel/src/services/LobbyService.ts`, `remove`, around line 540:
`await this.deps.storage.players.setStatus(target.id, reason)`, after `target.status` was read at
authentication. `tallyTakeoverVote` (around lines 407 to 415) concurrently compare-and-swaps
`takeoverPending` to `computer` and publishes `match.playerTakenOver`.

**Failure scenario.** The tally wins, then `remove` overwrites `computer` with `left` or `kicked`.
The seat every client already plays as AI is back in `ABSENT_HUMAN_STATUSES`, so the next `rejoin`
or vote opens a fresh prompt for it, pauses the clock, and a unanimous vote publishes a second
`match.playerTakenOver`, the regression the comment at the top of `remove` says was fixed.

**Recommendation.** `transitionStatus(target.id, ['active', 'takeoverPending', 'left'], reason)` and
act only when it won; `revokeToken` and the hang-up can stay unconditional for a kick.

### S3. A paused match is re-judged every sweep, with a log scan and a megabyte read each time

**Where.** `TurnService.ts`: `settle` reads `snapshots.get(matchId, number)` (around line 521), whose
row includes `body`, and uses only `stateHash`; the desynced branch calls `hasEvent(matchId,
'turn.desynced', number)` (around line 573), which pages the whole log from sequence 0; `sweep`
(around line 312) calls `reevaluate` for every match `listDesynced` returns, and `reevaluate`
calls `settle` for every unsettled turn. On Node that is every 15 seconds; on Cloudflare every cron.

**Cost.** Per paused match per sweep: a match read, `listUnsettled`, and per unsettled turn a
roster read, a reports read, up to a megabyte of base64 loaded to read one field, and a linear scan
of every event the match ever logged. A match parked in desync (the documented way a public match
ends when a host never uploads) pays this continuously for up to 90 days, on the synchronous SQLite
driver that also serves every request, or as billed D1 row reads. The comment on `hasEvent` says it
is only read on repair paths where the log is short; the sweep makes it a steady-state path.

**Recommendation.**

1. Add `SnapshotRepository.getSummary(matchId, turn)` (the `getLatestSummary` projection already
   exists) and use it in `settle`; the route that serves the body keeps `get`.
2. Record the announcement durably: write `desyncedAt` on the turn row in the same
   compare-and-swap that moves it to `desynced`, publish only from the caller that won, and drop
   `hasEvent` from the verdict. `sealedSlots` and `orderSetHash` are the precedent for keeping a
   fact beside the status. If the log has to stay the source, an `EventRepository.hasEvent` backed by
   an index on `(match_id, type)` is the fallback.
3. Skip a desynced match in `sweep` whose `updatedAt` is older than the previous pass: every report
   and every upload refreshes it, so an untouched match has nothing new to judge.

### S4. Readiness is republished on every ready submission

**Where.** `TurnService.ts`, `submitOrders`, around lines 76 to 84: the event is published when
`previous?.ready !== request.ready || (request.ready && previous?.ready === true)`. The second
clause is deliberate, to repair an event lost between the row write and its publish.

**Cost.** A member may `PUT` orders at the member rate limit (240 a minute). Each such request with
`ready: true` appends a durable event and wakes every subscriber of the match (S7). A healthy client
sends one or two; a misbehaving one can grow a match's log by hundreds of thousands of rows a day,
which also multiplies S3's scan.

**Recommendation.** Publish only on a change of `ready`, and cover the lost-first-publish case
narrowly: when `previous.ready === true` and the retry is an identical document, check whether the
log already carries a readiness event for `(turn, player)` at or after the row's `submittedAt`
before appending. Or accept the tiny window: the match view carries `readyPlayerIds` and the client
reconciles from it on reconnect anyway.

### S5. The sweep and the public listing scale with every live match

**Where.** `packages/storage/src/sqlite/repositories.ts`: `listStalledSeals` (around line 504)
left-joins `turns` for every `running` or `desynced` match on every tick; `listDesynced` (around
line 203) orders by `updatedAt` ascending with a limit of 100; `listPublicLobbies` (around line 77)
sorts every public lobby and running match by `createdAt`. `packages/kernel/src/services/MatchQueryService.ts`,
`listPublicLobbies` (around lines 74 to 118), then issues `players.listByMatch` and
`snapshots.getLatestSummary` per running listing.

**Cost.** Abandoned matches are deliberately kept `running` for 90 days (180 without the roster
test), so on a public server the stalled-seal join walks thousands of rows every 15 seconds to find
nothing. `updatedAt` does not move while a match stays desynced, so with more than 100 of them the
same 100 are re-judged every pass and newer ones are never visited; `listStalledSeals` orders by
`matches.id`, so a stall that keeps throwing pins the head of its page. The listing is
unauthenticated, rate limited at 30 a minute per address, and costs up to two queries per running
match on top of the join.

**Recommendation.** Restrict the stalled-seal scan to matches with `updatedAt` inside a recent
window (a seal in flight is seconds old) and run the full scan on a much longer period; rotate the
desynced page (bump `updatedAt` on evaluation, or page by id after the last one seen). For the
listing, exclude running matches with no active player (they are unjoinable anyway) and fold the
active-player count and the snapshot existence into the one query with `count(*) filter` or
`exists` subselects; the reserved slots can come from one `players` query over all listed ids.

### S6. Smaller storage-path items

- **Late-join capacity is a read, not a claim.** `LobbyService.joinRunning` (around line 200)
  checks `existing.length >= maxPlayers` before `createLate`, whose insert guards only the slot. Two
  late joiners for different free slots both pass and the match exceeds `maxPlayers`. Add
  `(select count(*) from players where match_id = ?) < matches.max_players` to the
  `createLate` insert's select.
- **`submitOrders` loads the previous full document to compare one boolean.** `TurnService.ts`
  around line 66, `turns.getOrders`, up to `LIMITS.ordersBytes`, on the hottest write the server
  has (the client sends a whole-document replacement per command the player queues). Add
  `TurnRepository.getOrderSummary` or reuse `listOrderSummaries`; the stale-turn acknowledgement a
  few lines above needs only the hash too.
- **A snapshot re-upload keeps stale version columns.** `repositories.ts` around line 528 (Postgres
  identical): the `onConflictDoUpdate` set omits `protocolVersion` and `sessionVersion`. Add both.
- **`append` retries eight times with no backoff.** `constraints.ts` `APPEND_ATTEMPTS`,
  `repositories.ts` around line 689. Six players plus the sweeper appending to one match on Postgres
  can plausibly collide eight times in a row, and the throw lands mid-`completeSeal`. A few
  milliseconds of jittered delay per attempt is enough.
- **The clock can restart behind a vote that is opening.** `resumeAfterTakeoverVotes` checks
  `hasOpenPrompts` and then reschedules; `openTakeoverPrompt` inserts and then clears. The
  interleaving check, insert, clear-sees-null, reschedule leaves a live deadline under an open vote.
  Re-check `hasOpenPrompts` after `rescheduleDeadline` and clear again if one appeared.
- **A report can land after the verdict.** `report` reads the turn status and upserts in two
  statements, so a late report can be inserted after `settle` confirmed the turn, mildly against
  "the evidence is immutable". Make the upsert conditional on status in `(sealed, desynced)` if that
  matters; the client already treats the resulting 409 as success.
- **Duplicate events beyond readiness.** `start` and `repairInterruptedStart` can both publish
  `match.started` when a sweep tick lands between the `lobby` to `running` swap and `openTurn`; two
  reporters can both publish `turn.desynced`. The clients are idempotent on all of these, but the
  design doc promises set semantics for readiness only. Either document it for every event or gate
  each publish on a compare-and-swap win (S1 and S3 do that for the two that matter).

## Server: streams, HTTP and runtimes

### S7. Fan-out costs one database read and one validation per stream per event, and per heartbeat

**Where.** `multiplayer/packages/server/src/sse/LocalEventHub.ts` (`notify`, `wake`, `makeRoom`)
and `sse/createSseResponse.ts` (`drain`, the heartbeat, `send`, `formatEvent`).

**What the code does.** `notify(event)` discards the already-durable event and wakes every
subscriber of the match; each subscriber runs its own `events.listAfter(lastSeq, 200)` and its own
`formatEvent` (a valibot `validateSync` plus `JSON.stringify`) over the same rows. The heartbeat,
every 20 seconds per stream, also calls `wake()` so a lost notification is repaired, which means
every idle stream issues a query every 20 seconds whether or not anything happened. A seal emits
three to five events in a burst, each waking up to 18 streams per match.

Two smaller accounting issues sit beside it. `makeRoom` checks the process cap *before* closing the
caller's own stale streams, so at 512 open streams a player whose laptop woke up gets `429` although
closing their corpse would free the slot. And `send` enqueues heartbeats regardless of
`desiredSize`, so a half-open connection never fails on the server side: the drain parks correctly
on backpressure, but the slot, its heartbeat timer and its per-heartbeat query persist until the OS
retransmit timer gives up, typically 15 to 30 minutes on Linux.

**Cost.** Per published event: subscribers times (one query, one validation, one serialisation).
Trivial for a six-player match. The baseline is the larger number: at the default cap of 512 streams
an idle server issues about 25 queries a second to find nothing, synchronous statements on the Node
event loop that also seals turns, or D1 reads from the Durable Object.

**Recommendation.**

1. Have the hub remember the highest sequence it has been told about per match. On heartbeat, a
   stream whose `lastSeq` already equals it skips the query; a wake still drains. Keep an
   unconditional catch-up drain on a longer period (every fifth heartbeat) to bound a notification
   that never reached the hub at all.
2. Format each event once per wake and share the frame: the hub produces the `listAfter` page and
   its frames and hands them to each stream, which only enqueues. Validation of a stored row is per
   row, not per reader. A small per-match cache keyed by `seq` covers the burst.
3. Fast path in-band delivery: when `notify(event)` arrives and `event.seq === lastSeq + 1` for a
   stream, enqueue the cached frame without a read, keeping the drain as the catch-up.
4. Reorder `makeRoom` to close the caller's stale streams before the process check, and add a
   watchdog that shuts a stream whose drain has been parked for several heartbeats with no `pull`,
   so a stalled consumer is dropped and resumes from `Last-Event-ID`.

`MatchHub` on Cloudflare uses the same `LocalEventHub`, so one change covers both runtimes.

### S8. Every JSON response is serialised, cloned, re-parsed and re-validated

**Where.** `multiplayer/packages/server/src/http/responseValidation.ts`, around lines 32 to 35:
`body = await c.res.clone().json()` then `validate(responseKind.schema, body)`, after the handler's
`c.json(...)` has already stringified the object.

**Cost.** For `GET /snapshots/latest` (`views.ts`, `snapshotViewSchema`): stringify a megabyte,
clone and parse a megabyte, then run `base64BodySchema`'s regex and length walk over it, on every
reconnect and every repair adoption. For `GET /turns/:n/orders`: up to six documents of up to 512
ops each parsed and walked through `strictObject` and `variant` a second time. `GET /events`
(`routes/events.ts` around lines 22 to 29) does it a third time, since `tryFormatEvent` is run as a
readability probe and its string discarded before `c.json` stringifies again.

**Recommendation.** Validate the handler's *object* before serialisation: a thin wrapper around
`c.json` in `buildHonoRoute`, or expose the value on the context for the middleware, so the body is
stringified once and never re-parsed. Consider skipping the base64 regex on the outbound path, since
the value was validated on the way in. In the events route, replace the probe with a `safeParse`.

### S9. The bug-report journal budget is spent before the request is read

**Where.** `multiplayer/packages/server/src/http/middleware.ts`, `bugReportRateLimited` (around
lines 76 to 82): `bugReportState.take(...)` runs for every `POST /bug-reports` before the handler;
`routes/bugReports.ts` only reads the flag. `container.ts` sets the budget to five per address per
day; `RateLimiter.prune` runs at most once per window, so every distinct address lives in the map
for 24 to 48 hours.

**Failure scenario.** A player files five text-only reports, or five requests that answer 413 or
422, and the sixth report of the day, the one with the journal, is silently filed with
`stateStored: 'omitted'`. Everyone behind one NAT shares those five. The design doc also states the
opposite of what the code does: "there is deliberately no per-address daily counter — on an
unauthenticated route that map is itself unbounded memory".

**Recommendation.** Move the `take` into the handler, after contract validation, and only when
`request.state !== undefined`; or drop the per-address counter as the doc says and rely on the global
byte budget. If it stays, cap the map (an LRU) or prune on a timer independent of the window, and
update the doc.

### S10. Validation refusals echo the refused value for type mismatches

**Where.** `multiplayer/packages/server/src/http/errorHandler.ts`, `describeIssue` (around lines 57
to 74) strips valibot's `input` and `received` *fields* but forwards `issue.message` verbatim.
valibot 1.5's default message is `Invalid type: Expected string but received "<value>"` for every
schema step without a custom message (`string()`, `number()`, `literal()`, `picklist()`,
`minValue`/`maxValue`).

**Failure scenario.** `{ "password": 123456 }` answers `Invalid type: Expected string but received
123456`; `"visibility": "secret"` echoes `"secret"`; an unknown op kind echoes itself. A correctly
typed wrong password is not echoed (its `minLength` failure reports the length), which is why the
conformance test that checks the no-echo rule passes. The design doc's rule is "a refused request is
described, not echoed", and a mistyped value in a proxy log is what it is meant to prevent.

**Recommendation.** In `describeIssue`, rewrite the message when `issue.received !== undefined`
(keep `Invalid type: Expected string`), or configure valibot's global message to omit `received`.
Add a conformance case with a wrong-typed secret.

### S11. Smaller HTTP and runtime items

- **`X-Request-Id` is missing on the stream response.** `middleware.ts` sets it with `c.header()`
  before the handler; the stream route returns a raw `Response` from `createSseResponse`, which
  Hono 4.13 does not merge prepared headers into. A stream problem cannot be correlated. Return
  `c.newResponse(response.body, response)` or copy the id into the SSE headers, and assert the
  header on every response in the conformance suite.
- **PBKDF2 at 120,000 iterations on the unauthenticated join doors.** `kernel/src/logic/password.ts`,
  called from `join` and `joinRunning` before any seat is claimed. Public listings show
  `passwordProtected` and the join code, so a caller can drive 30 verifies a minute per address at
  tens of milliseconds of CPU each; on Node that is the libuv thread pool, on Workers CPU time per
  request. Add a small per-match failed-password budget beside the per-address one. Separately,
  confirm the Workers plan allows that iteration count; the free plan documents a lower ceiling.
- **Durable Object calls have no deadline, and a scheduling failure fails the seal.**
  `runtimes/cloudflare/src/kernel.ts` (around lines 61 to 80): notify, schedule and disconnect are
  awaited fetches with no `AbortSignal.timeout`. `TurnService.openTurn` publishes `turn.opened` and
  then awaits `scheduler.schedule`, so a hub failure turns a completed seal into a 500 for the
  submitter (the sweep still recovers the deadline). Treat a scheduling failure like a notification
  failure: log it and rely on the sweep.
- **The Cloudflare dev config carries no cron trigger**, and nothing warns a deployment that forgot
  one that its safety net is missing. Worth a README line and a startup assertion where possible.
- **The lobby is polled at one authenticated request per second per player**
  (`MultiplayerLobbySession.cs`), five queries each. Deliberate per the design doc, but the
  steadiest load a server sees. An `ETag` from `(updatedAt, lastEventSeq)` with `304` makes an
  unchanged poll cost the auth lookup and one `lastSeq`; or long-poll `GET /events?after=`.
- **Client-supplied `X-Request-Id` is adopted unvalidated** (any 64 characters into the envelope and
  the JSON logs). Restrict to `[A-Za-z0-9_-]` or always mint.
- **The member rate-limit key passes through the IPv6 masking regexes** on every request
  (`middleware.ts` `enforce` calls `rateLimitKey` on a player UUID). Harmless; skip masking for
  non-address tiers.
- **SQLite runs `synchronous = NORMAL`**, so "published after durable" holds against a process crash
  and not against power loss. A fine trade, documented in the code; the limitations section of the
  design doc could say so.

## Client: session, stream and outbox

### C1. A reconnect replays the whole match from turn 0, one round trip per turn

**Where.** Only two kinds of snapshot ever exist: the host's turn-0 bootstrap
(`MultiplayerMatchSession.Autosave.cs`, `UploadInitialSnapshotAsync`) and desync repairs; the test
`ConfirmedTurnDoesNotUploadAnotherFullSnapshot` pins this. `MultiplayerMatchSession.Restore.cs`,
`ReplayEventHistoryAsync` (around lines 192 to 227), walks the log in pages of 200 and, for every
`TurnSealedEvent` past the snapshot, does a sequential `SealedOrdersAsync` fetch and a full
`SealedTurnApplier.Apply`, which plans every computer seat and hashes the state through the
recorder on every op.

**Cost.** A reconnect at turn 80 of a six-player match is 80 sequential HTTP fetches, each under
its own 15-second deadline and five-minute retry window, plus 80 full turn resolutions, all inside
`RunPumpAsync` before the player sees anything. The design doc talks about matches of hundreds of
turns; that is minutes of reconnect, and every retry of it starts over.

**Recommendation.** Have the host upload a checkpoint snapshot every K confirmed turns (the server
already bounds a match to five snapshots and `getLatest` picks the newest), so a reconnect replays
at most K turns. Independently, prefetch the sealed sets for the pending turns concurrently while
applying sequentially: they are immutable and served with `Cache-Control: immutable`.

### C2. Every command the player queues hashes the whole match twice on the game thread

**Where.** `src/Rechaos.Core/Persistence/MatchReplay.cs`: `Submit`, `Cancel`, `QueueHire`,
`SnubHireOffer` and `TryDismissNotification` each call `EnsureSynchronized()` (line 286, which
computes `MatchStateHasher.ComputeSha256(State)` and compares) and then `CurrentHash()` (line 276,
which computes it again). `ComputeSha256` serialises the entire state into a stream and hashes it.
`Session/SpeculativeTurn.cs` (around lines 156 to 194) routes every interface action through this
recorder, on the game thread.

**Cost.** Two full-state serialisations and SHA-256s per click, per cancel, per hire. On a late-game
six-player city that is a visible hitch per action. The speculative copy's journal is never used:
the `OrderDocumentBuilder` beside it is the only record that matters, and the copy is thrown away
at the seal.

**Recommendation.** Give `MatchReplayRecorder` a non-verifying mode for speculative turns (skip
`EnsureSynchronized` and record steps without a hash), or replace the per-step hash with a cheap
mutation counter on `MatchState` and hash lazily when a journal is captured. The authoritative
recorder, whose journal becomes the bug-report archive, keeps the current behaviour.

### C3. Every resolved turn does a save round trip and an fsync on the game thread

**Where.** `MultiplayerMatchSession.ResolveSealedTurnAsync` enqueues `TurnResolved` with
`MatchStateClone.Of(_replay.State, ...)` from the pump; `src/Rechaos.Game/ChaosGame.Multiplayer.cs`
around line 432, `AdoptOnlineState`, then calls `SpeculativeTurn.For(authoritative, ...)`, which calls
`MatchStateClone.Of` again (a native save serialise and deserialise), builds a recorder (an initial
hash), and steps the coordinator past every preceding seat (each step hashed twice, C2). It then
calls `TouchOnlineRecovery`, and `MultiplayerRecoveryStore.TrySaveAll` writes the recovery file
with `Flush(flushToDisk: true)` before its atomic rename.

**Cost.** One redundant clone, one planning-copy build and one fsync on the render thread in the
frame the new turn appears. Each is small; together they are the only blocking work the game thread
does for online play, and they land at the moment the player is looking. The restore path already
does this the better way: `RestoreAsync` builds the `SpeculativeTurn` on the pump and hands it over
inside `Resumed`, and `AdoptOnlineState` already accepts a prebuilt turn.

**Recommendation.** Build the `SpeculativeTurn` on the pump for `TurnResolved` and `Resynced` as
`Resumed` does, and pass it through. Move the recovery write to a single-writer background task
with a last-write-wins queue (the same shape the outbox uses), or drop `flushToDisk`: the atomic
rename already prevents a torn file, and a lost last stamp costs only ordering in the history list.
The clean-exit marking at shutdown can stay synchronous.

### C4. A server that accepts and immediately closes is reconnected to every second, forever

**Where.** `src/Rechaos.Multiplayer/Http/MatchEventStream.cs`, `ReadAsync` (around lines 91 to 97):
the first frame, keepalive or event, sets `proven`, resets `attempt` to 0 and resets the outage
stopwatch. `AllowsAnother` is evaluated only against those.

**Failure scenario.** A backend that accepts, writes the `: connected` or `: keepalive` comment and
drops (a proxy with response buffering, a load balancer with a short idle cutoff) never exhausts the
budget: every attempt is "proven", every reconnect is `Backoff(1)`, half a second to a second, per
client, indefinitely. The design doc's intent is the opposite ("a server that accepts the connection
and closes it at once is an outage like any other").

**Recommendation.** Count a connection as proven only after it has survived some minimum time (one
heartbeat interval) or delivered an event; or keep a rolling cap on reconnects per window so a
tight loop still backs off.

### C5. The countdown trusts the local clock against a server timestamp

**Where.** `src/Rechaos.Game/ChaosGame.MultiplayerDraw.cs` line 57: `var remaining = deadline -
DateTimeOffset.UtcNow;`, with `DeadlineAt` parsed from the server's ISO instant in `turn.opened`
and `turn.deadlineExtended`.

**What the code does.** The countdown, and the ten-second and one-second warnings that play off it,
are computed against the client's wall clock. A client thirty seconds fast on a thirty-second timer
sees the turn expire before the server seals; one that is slow is sealed on while the screen still
shows time.

**Recommendation.** Estimate the server offset once per session and apply it: the `Date` header on
any response, or the difference between `turn.opened`'s `deadlineAt` minus `turnTimerSeconds` and
the local time the event was handled. Keep a smoothed offset on the session and compute `remaining`
from `deadline - (UtcNow + offset)`. The server stays the authority; this only makes the courtesy
countdown honest.

### C6. The handshake is taken once and never revisited

**Where.** `src/Rechaos.Multiplayer/Http/MultiplayerClient.cs`, `EnsureHandshakeAsync` and
`HandshakeState.Complete`. Nothing on a later request checks the version, and the server routes do
not carry it.

**Failure scenario.** The server is upgraded to a new protocol version during a match. The stream
reconnects (a 5xx or reset during the redeploy is transient) and every subsequent call is made under
the old contract. The failure arrives as a schema refusal on some later request, an unreadable event,
or a silently tolerant read of a field that changed meaning, where "Update your game to connect to
this server" was the truth.

**Recommendation.** Re-run the handshake on every stream reconnect and clear `Complete` when it
fails with a mismatch, so the pump's next call surfaces the handshake's message. Or have the server
add its protocol version as a response header and compare it on every response, which costs one
header and no round trip.

### C7. Smaller client items

- **A failed takeover vote is invisible.** `ChaosGame.MultiplayerTakeover.cs` around line 88:
  `Forget(_session.VoteOnTakeoverAsync(...))` followed by `_message = "VOTED TO ..."`; `Forget`
  writes a diagnostics entry on fault and nothing else. A `403 not_active` or an exhausted window
  leaves the modal saying the vote was cast. Route the outcome through the notice queue like every
  other background result and offer the buttons again.
- **A stale refusal overwrites the new turn's status line.** `ChaosGame.MultiplayerNotices.cs`
  `OrdersRefused` (lines 276 to 294) sets `TurnSyncError` and `_message` regardless of
  `refused.Turn`. The outbox and the pump are independent lanes, so the `409 turn_not_open` for
  turn N routinely arrives after `TurnResolved` N, and "TURN SYNC ERROR" sits over turn N+1 until
  the next draft is accepted. Ignore a refusal whose turn is not `PlanningTurn`, as `OrdersAccepted`
  already does.
- **The outbox cancels the in-flight request while holding its lock.**
  `MultiplayerMatchSession.Outbox.cs`, `QueueOrders`: `_inFlightOrders?.Cancel()` inside
  `lock (_outboxGate)`. `Cancel` runs registrations synchronously, and the continuation of the
  retry loop's `await Task.Delay` can run inline on the game thread inside the lock (reentrant, so
  no deadlock), through `Recovered()`, the `finally`, and if the semaphore already holds a count, into
  the next `SubmitOrdersAsync`'s synchronous prologue. Capture the source under the lock and cancel
  after leaving it, or use `CancelAsync`.
- **The shared `HttpClient` never recycles connections.** `MultiplayerClientOptions.CreateHttpClient`
  is `new HttpClient()` with the default infinite `PooledConnectionLifetime`. Right for socket
  exhaustion, wrong for a central service behind an edge whose addresses rotate: a dead pooled
  connection costs a timeout per call until the OS resets it. Use a `SocketsHttpHandler` with a
  lifetime of a few minutes.
- **A corrupt recovery file silently discards every saved token.** `MultiplayerRecoveryStore.cs`,
  `TryLoadAll` (around lines 169 to 193): any exception yields an empty list and the next save
  overwrites the file. Rename an unreadable file to `.corrupt` and keep a `.bak` of the last good
  write, so the seats can be recovered by hand or by a later build.
- **A restored host never re-checks the bootstrap snapshot.** `_uploadInitialSnapshot` is
  `IsHost && !isRestoring`, so a host that crashed before the turn-0 upload completed and reconnects
  never uploads it; the server then refuses every late join with `late_join_not_ready` for the life
  of the match. On restore, when no snapshot exists and the host is on turn 1, upload it.
- **Every response is read three times.** `MultiplayerClient.SendAsync` reads the body to a string,
  `WireJson.Read` parses it to a `JsonNode` tree, `WireOrder.TagFirst` walks it, then it is
  deserialised into records. For the megabyte snapshot and a six-document sealed set that is three
  copies and three passes; `WireOrder.cs` already notes the fix (a converter per union that reads the
  discriminator wherever it sits, then `DeserializeAsync` from the stream).
- **Per-frame allocations in the online UI.** `OnlineCountdown()` and `OnlineTurnStatus()` build
  interpolated strings every frame; `RecoverableOnlineSessions` and `FilteredOnlineListings()` are
  `Where(...).ToArray()` on every access from the screens. Cache on change (both only change through
  notices) and format the countdown when the second changes.
- **`CanonicalJson` allocates a path string per property on the success path**, and `OrderDigest`
  serialises the document to a string and re-parses it to canonicalise. Only hit per draft change and
  per sealed set, so small; build the path only when an exception is being raised if it ever shows
  up in a profile.
- **Shutdown disposes the shared `HttpClient` under still-running tasks.**
  `ChaosGame.MultiplayerShutdown.cs`: `Task.WaitAll(stopping, 2s)` then `_http.Dispose()`
  unconditionally, so tasks still winding down raise `ObjectDisposedException` nobody observes.
  Skip the dispose when the wait timed out, or attach the `Forget` continuation.

## The TypeScript client

`multiplayer/packages/client` is described as the reference the C# client mirrors. It is smaller than
that description suggests:

- `client.ts` `call()` has no retry at all; the retry contract in the design doc is implemented only
  in the C# client. Fine if intentional, but the README should say so.
- `openStream` has no connect-phase timeout: a SYN-blackholed server costs the OS connect timeout
  per attempt, often two minutes, so the five-minute outage budget is two attempts. Race the header
  phase against a timer, as the C# client does.
- `errors.ts` reads an error response with `await response.json()` and no size cap, where the C#
  client caps a receipt at 1 MiB.
- Backoff jitter is `[0.5, 1.0] × window`; after the Node server's `closeAll()` on shutdown every
  client reconnects within the same second. Full jitter over `[0, window]` spreads the herd better,
  on both clients.

## Test coverage gaps

The suites are thorough on single-request semantics and interrupted-process repair. What they do not
cover, and what the findings above would want:

- **Desync followed by a restart**, on either side (R1): no session test reconstructs a `desynced`
  match, and no server test uploads a repair for a sealed turn with no reports.
- **The watchdog against a dead stream** (R2a), **a session past the stream window** (R2b), and **a
  status-only 4xx** (R2c): `MultiplayerEventStreamTests` cover reconnect and idle detection; nothing
  asserts what the *session* does at each of those three boundaries.
- **Concurrent seal completion** (S1): `services.spec.ts` runs two `submitOrders` concurrently (around
  line 979) but never two `completeSeal` calls, and never a roster change between a seal's steps;
  nothing drives `sweep` against a request-path seal in flight.
- **Query counts.** Nothing asserts how many statements a `report`, a `submitOrders`, a stream wake or
  a sweep pass issues, so S3, S6, S7 and S8 regressed silently and would again. A counting wrapper
  around `InMemoryStorage` in the kernel tests, and around the hub's `listAfter` in `sse.spec.ts`,
  would pin each hot path to a number.
- **The stream parser's caps.** No test references `MaximumFrameChars` or an unterminated line.
- **One-frame-then-close** (C4): no test asserts the reconnect cadence when the server closes after
  a keepalive.
- **Clock skew** (C5): no test feeds the session a deadline from a server whose clock differs.
- **Over HTTP** (conformance): password-gated create and join and the no-echo rule with a wrong-typed
  secret (S10); stream caps answering 429 and a kicked player's open stream ending (only unit
  tested); the keepalive frame and a stream surviving 20 seconds idle; `Last-Event-ID` precedence
  over `?after=` and an out-of-range resume value restarting at 0; malformed JSON answering 422; an
  oversized snapshot answering 413 and the upload budget on `/snapshots`; `Retry-After` on every
  429; `X-Request-Id` on every response (S11); graceful shutdown seen from a client resuming with
  `Last-Event-ID`; on Cloudflare, a Durable Object stream ending when the edge client disconnects.
- **A live two-runtime match.** `tools/OnlineSmoke` plays a short match against a running server but
  is not in CI; the conformance suite exercises the HTTP surface without the C# client. A nightly
  smoke against the Node runtime in a container would catch a contract drift the generated records
  cannot.

## Recommended order of work

1. **R1**, in three commits: the server rule (a repair must name a `desynced` turn), the client's
   restore-path repair (host re-uploads, non-hosts adopt and re-report), then the live path that
   repairs a turn older than the current one. Each has a test from the gaps list.
2. **R2**, as one client change: resync instead of fail for the watchdog and the stream window, and
   envelope-gated terminality for 4xx. The recovery record already makes the manual path work, so
   this is removing the three places that force it.
3. **S1 and S2** together, because S2 is the departure that makes S1 visible: two compare-and-swaps
   and a concurrency test.
4. **S3 and S6's `getOrderSummary`**: two storage-port projections, the `desyncedAt` flag, the sweep
   skip, both repositories, `InMemoryStorage`, the conformance suite, and the query-count test that
   pins them.
5. **C2 and C3**: the non-verifying recorder for speculative turns and the pump-built planning copy,
   which together remove all blocking work from the game thread in online play.
6. **S7 and S8**: the hub's last-known sequence, the shared frame, and validating before
   serialisation, which are the changes that matter if the central server ever holds hundreds of
   matches.
7. **C1**: periodic checkpoint snapshots and concurrent sealed-set prefetch.
8. **S4, S5, S9, S10, C4** as ordinary hardening, then the remaining items in S6, S11 and C7 as the
   surrounding code is next touched.

## Resolution

Worked through in the order above. The wire moved twice — protocol version 10 to 12 — and the
session version did not: nothing about a stored match's shape or its resolution changed, so no match
in progress was retired for any of it.

### High

**R1** is answered on both sides. The server now requires a repair to name a turn whose status is
`desynced`, which closes the hole the sealed successor left: it carries no reports, so
`authoritativeCandidates` had nothing to count for it and the host could store any hash it liked,
after which the verdict could only confirm or wait. A repair may also be posted by whoever holds
the *sole* most-reported hash rather than by the host alone — the only thing that could ever fix a
desync the host is itself the outlier of — while a tie still belongs to the host, so a two-player
match is not handed to whoever uploads first.

The client no longer offers whatever hash it happens to be standing on. It works out the state as
it stood *after* the disputed turn, rebuilding it from the newest snapshot at or below that turn
plus the sealed sets on top when the match has moved past it. A restart during a pause carries the
divergence out of the event history and acts on it; a client that comes back after a repair was
posted adopts it *and reports the hash it adopted*, which the old path loaded but never said. A
client that cannot repair waits, and says so, rather than ending its session.

**R2**'s three places are all resynchronisations now. The resolution watchdog asks the session to
rebuild from the log rather than tearing it down, with its grace set above the stream's own idle
detector plus a reconnect — it used to fire twenty seconds before the mechanism that fixes the
thing it watches for. A closed retry window says how long it has been trying and opens another,
which is what the outbox on the same session always did; a caller that owns the whole match and has
somewhere to report to sets `StreamOutageBudget` to keep the old bound, and the headless smoke test
does. And terminality now requires the server's own error envelope, so a proxy answering 404 or 403
for every path while the backend restarts is an outage rather than a verdict.

### Server

**S1** and **S2** are compare-and-swaps: `turns.freezeSeal` is conditional on the set not being
frozen, not on a status that does not change across the window, and `remove` swaps a seat's status
instead of writing it. **S3** put the divergence announcement on the turn row beside the seal's
digest and gave the verdict a snapshot summary, so a paused match costs a few indexed reads rather
than a megabyte of base64 and a scan of its whole event log. **S4** publishes readiness on a change.
**S5** windows the sweep to matches something has happened to and folds the public listing into two
queries, which now asks the same question of a running match that the late-join door does: a seat
one missed deadline behind (`takeoverPending`) still counts as a human in it, and a match every
human has left is not offered because the door refuses it as `match_abandoned`. **S6**'s items each landed: capacity inside `createLate`'s insert, a conditional
`upsertReport`, the versions of a re-uploaded snapshot, jittered backoff in `append`, and a
re-check of the prompts after the clock restarts.

**S7** is `MatchLog`: the read, the validation and the serialisation of one event happen once for
the match rather than once per reader, the notification carries the event so a healthy stream never
reads at all, and a caught-up heartbeat asks nothing. **S8** validates the handler's own object
instead of cloning and re-parsing the body. **S9** spends the journal budget in the handler, after
validation and only for a report that carries a journal. **S10** takes the received value back out
of valibot's default message, which the stripped fields did not cover. **S11**'s items landed, from
the stream's `X-Request-Id` to a budget in front of PBKDF2 and a deployment that says so in its own
logs when it has no cron trigger. That budget is charged per caller and per match rather than per
match alone: a public listing carries the join code, so a match-wide counter charged on every
attempt was a lever a stranger could hold down to lock out everyone who knew the password. The
match-wide half is charged only by a failed verification and closes only a caller's second and
later attempts in a window, so a caller the match has not heard from still gets a first try while
an attack is running.

### Client

**C1** is checkpoints every ten confirmed turns plus a concurrent prefetch of the sealed sets a
replay needs. **C3** builds the planning copy on the pump and stops fsyncing the recovery file for
a stamp whose loss costs the order of a list. **C4** requires a connection to last a heartbeat
before a keepalive counts as proof. **C5** takes the countdown against the server's clock. **C6**
re-establishes the handshake on every reconnect. **C7**'s items each landed.

The TypeScript client got a connect-phase deadline, a size cap on the error body, full-window
backoff jitter, and a README that says what it does and does not implement. The deadline reads
`requestTimeoutMs: 0` as disabled, the way `call` does, and releases the listener it puts on the
caller's signal at the end of each attempt rather than once per stream.

### Where the review was not followed

Two recommendations were changed on the evidence:

- **C2** says the speculative copy's journal is never used and recommends dropping it. It is used:
  `MatchActions.Journal` hands that recorder to a bug report filed from an online match, because
  the turn being planned is the only history that client is the authority on. Dropping it would
  have stripped those reports of their most useful field without saying so. The verification half
  of the recommendation — skipping the per-step re-hash, which checks an aliasing invariant a
  single-owner copy cannot violate — is taken, and halves what a click costs.
- **S6**'s note that duplicate events beyond readiness should either be gated or documented is
  answered by gating the two that matter (S1's seal and S3's divergence) and leaving the rest as
  at-least-once delivery, which is what the design already promises and what every handler on both
  clients is written for.

### Test coverage

The gaps the review lists are covered, each by a test that fails against the code as it was: a
forced two-caller seal completion with a roster change between the steps, the four desync scenarios
end to end, the session's behaviour at each of R2's three boundaries, the one-frame-then-close
cadence, the sealed-set prefetch, and query-count budgets for a submission, a paused match's
re-judgement, the sweep, the public listing and the stream fan-out. `tools/OnlineSmoke` is still
not in CI, and a nightly two-runtime smoke remains the one item on that list left open.
