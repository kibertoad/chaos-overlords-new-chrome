# Project decisions

Status: active
Last updated: 2026-09-24

This log records deliberate product and compatibility boundaries that affect the
implementation plan.

Entries are ordered newest first. Each records the decision, the evidence or
reasoning behind it, and what it rules in or out; a decision that changes a
parity row is also reflected in [PARITY-MATRIX.md](PARITY-MATRIX.md) with the
`Intentional deviation` status.

## Decision index

Generated from the `##` headings of this file by `node tools/update-doc-indexes.mjs`.

<!-- doc-index:begin decision-index -->
| Date | Decision |
|---|---|
| 2026-09-24 | [Refuse a hire drop on a sector already holding six friendly gangs](#2026-09-24--refuse-a-hire-drop-on-a-sector-already-holding-six-friendly-gangs) |
| 2026-09-24 | [Mark objective sectors on the detailed-sector minimap](#2026-09-24--mark-objective-sectors-on-the-detailed-sector-minimap) |
| 2026-09-24 | [Resolve cash transactions in player order](#2026-09-24--resolve-cash-transactions-in-player-order) |
| 2026-09-23 | [Do not animate the panel slide-out](#2026-09-23--do-not-animate-the-panel-slide-out) |
| 2026-09-22 | [Fold the definition set into a fingerprint as a digest](#2026-09-22--fold-the-definition-set-into-a-fingerprint-as-a-digest) |
| 2026-09-21 | [Fingerprint match state with XxHash128, not SHA-256](#2026-09-21--fingerprint-match-state-with-xxhash128-not-sha-256) |
| 2026-09-19 | [Order a ctrl-picked selection of gangs at once](#2026-09-19--order-a-ctrl-picked-selection-of-gangs-at-once) |
| 2026-09-18 | [Scope the Sector workspace's opponent gang view to detection](#2026-09-18--scope-the-sector-workspaces-opponent-gang-view-to-detection) |
| 2026-09-17 | [Do not substitute rejected recovered AI commands](#2026-09-17--do-not-substitute-rejected-recovered-ai-commands) |
| 2026-09-17 | [Correct the original registry persistence defects](#2026-09-17--correct-the-original-registry-persistence-defects) |
| 2026-09-17 | [Correct the original AI hire slot/role indexing defect](#2026-09-17--correct-the-original-ai-hire-slotrole-indexing-defect) |
| 2026-09-13 | [Decode the supported Smacker subset at runtime](#2026-09-13--decode-the-supported-smacker-subset-at-runtime) |
| 2026-09-13 | [Stream the intro once, then keep it on the title screen](#2026-09-13--stream-the-intro-once-then-keep-it-on-the-title-screen) |
| 2026-09-13 | [Bug reports carry a replayable journal, stored apart from matches](#2026-09-13--bug-reports-carry-a-replayable-journal-stored-apart-from-matches) |
| 2026-09-10 | [Save compatibility scope](#2026-09-10--save-compatibility-scope) |
| 2026-09-10 | [Networking scope](#2026-09-10--networking-scope) |
<!-- doc-index:end -->

## 2026-09-24 — Refuse a hire drop on a sector already holding six friendly gangs

**Decision.** Dropping a Hire offer on a sector where the player already has
six active gangs is refused on the spot with "Sector gang limit reached." The
offer stays unselected instead of being reserved as a hire. The count is the
current friendly count a Move into that sector is validated against, less any
gang the player has already ordered to Move away or Terminate: both resolve in
the Execution phase, before hires, so the room they leave is there when the
hire is placed.

**Original behavior.** `BIN-HIRE-001` establishes that the shipped drop handler
writes the destination without any capacity check, and the resolver only
counts the gangs in the target sector at resolution, where six fail the hire.
The recreation keeps that resolver check unchanged.

**Reasoning and compatibility.** A reserved hire into a full sector showed as
hired for the rest of the planning turn and could only fail. The refusal lives
in the client's drop handling, not in `HireRules`, so AI hiring, replay
validation and turn resolution are untouched; no session, save, replay or
fingerprint version moves.

## 2026-09-24 — Mark objective sectors on the detailed-sector minimap

**Decision.** The detailed-sector screen's 3-by-3 neighborhood minimap draws
the same exact-white-keyed `PX00129` objective pylons `(344,15,54,52)` that the
whole-city map draws, over every visible Siege landmark and Big Man center
sector 27, 28, 35, or 36. The crop is scaled into the minimap cell exactly as
the cell's city artwork is, so the pylons keep their city-map placement.

**Original behavior.** The recovered evidence places the pylon overlay only on
the whole-city map; no native copy of that crop into the detailed-sector
neighborhood has been identified.

**Reasoning and compatibility.** Big Man points accrue only in the center
sectors, and Siege landmarks decide that scenario, so a player working in the
detailed view should not have to return to the city to see which neighboring
sectors are objectives. The change is presentation only: rules, orders, saves,
replays, fingerprints, and the multiplayer protocol and session versions are
unaffected.

## 2026-09-24 — Resolve cash transactions in player order

**Decision.** Equip and Sell debit or credit cash in the order the player last
submitted those orders. Replacing an order moves it to the end. Transactions
still resolve by player slot, and Give retains its deferred roster-ordered
recipient writes. The city console shows current cash, the whole-cycle Delta,
and `UNSPENT = cash - sum(queued Bribe and Equip prices)` on one row as
`CASH 20 [18] (+1)`: cash, unspent cash in brackets, and the delta in
parentheses. Hovering the row explains each figure in its own section, breaks
the delta down by component, and shows every queued Bribe and Equip, numbered in resolution
order with its price: Instant Bribes first, then Equips in submission order.

**Original behavior.** `BIN-EQUIP-002` and `BIN-EQUIP-006` establish that the
shipped resolver instead scans fixed gang roster slots. An earlier-slot Sell
can fund a later-slot Equip regardless of which was queued first. The original
picker neither hides unaffordable researched items nor checks cash when an
item is chosen. The later Equip comparison is signed `cash < adjusted price`,
with equality permitted.

**Reasoning and compatibility.** A player can see and control submission order,
while the fixed roster index is hidden. Cash timing remains execution-time:
an earlier submitted Sell can fund Equip, but a later Sell, Chaos payout, or
next Upkeep income cannot. This deliberate rule deviation changes deterministic
turn outcomes, so multiplayer session version 9 retires sessions started under
version 8. Native saves and replay journals retain their format gates because
their schema and fingerprint encoding have not changed.

## 2026-09-23 — Do not animate the panel slide-out

**Decision.** With Slide Panels enabled, a panel slides in over the recovered
344- or 320-pixel travel, but the recreation does not animate the matching
slide-out. A closing panel disappears in the frame it closes. This is the only
panel-motion or audio behavior where the recreation deliberately departs from
the original. Every other effect cue keeps its recovered trigger, order and
interruption: one effect voice, and each new cue stops the one before it
(`BIN-SOUND-002`). Slide Panels stays off by default, a separate modern choice
recorded in the Options parity row.

**Reasoning.** The original's close helper `0x004196f5` plays slot 1 and then
runs a blocking copy loop of about a quarter second before the next handler can
open anything (see the interface-and-options panel slide evidence and
`BIN-SOUND-001`). That delay adds no information and holds input on every panel
change, including nested panel hops, and the entrance alone already shows where
the panel came from.

**Audio consequence.** The slide-out was also the gap between cues, so without
it a cue that the original separated in time now starts in the same frame and
cuts off the one before it. This is accepted rather than covered with delays or
with overlapping voices, which would add behavior the original never had.
Only the Slide Panels-gated cues are affected:

- a panel-to-panel change plays slot 1 and then slot 0, and the slot-1 close cue
  is not heard;
- confirming the idle-gang warning plays slot 1 and then the turn-start slot 9.

With Slide Panels off, the original plays neither slot 0 nor slot 1, and the
recreation matches it exactly.

**Rules out.** Adding a slide-out animation or a timed gap between cues to make
up for it, and letting effects overlap. Each would need its own decision.

## 2026-09-22 — Fold the definition set into a fingerprint as a digest

**Decision.** A state fingerprint folds in the gameplay definitions as a cached
128-bit digest of their canonical block rather than the block itself, and the
state-hash format moves to 2. Native save format 27 and replay format 31 carry
the new encoding and refuse every older format, and the multiplayer session
version moves to 6. The rule that these move together is written up under
[State fingerprint format](../AGENTS.md#state-fingerprint-format) and held by
`StateFingerprintVersionCouplingTests`.

**Reasoning.** The definition block is the same bytes on every call for a given
definition set, and a turn hashes 8 + 2P boundaries, so the whole of every site,
gang and item — names and descriptions included — was re-serialised and re-hashed
for each one. The digest is computed once per `OriginalData` instance and held
weakly, so a definition set the process stops using is still collectable. The
fingerprint stays a comparison, never a claim of authenticity, so a 128-bit
digest in place of the block costs nothing that matters.

**Compatibility.** A fingerprint under the new encoding is well-formed and
simply differs from the old one, and nothing in a save or a journal records
which encoding wrote it. Left to compare, an older file would be reported as
damage rather than as an older format — a save browser row drawn as playable,
then a failed verification that costs the intact backup generation, and a
journal reported as diverging on its first step. The format gates therefore move
with the encoding, and an older file is refused as `OlderFormat` before its
fingerprint is ever read. The game has not been released, so no player's file is
stranded; the session bump retires in-progress online matches for the same
reason, as [AGENTS.md](../AGENTS.md) requires when a state hash changes.

## 2026-09-21 — Fingerprint match state with XxHash128, not SHA-256

**Decision.** The canonical match-state fingerprint is 128 bits of XxHash128
over the canonical state encoding, with the event history and the
phase-boundary history folded in as running digests chained entry by entry.
Native save format 26 and replay format 30 carry it, refuse every older
format, and the multiplayer protocol and session versions move to 13 and 5.

**Reasoning.** The fingerprint is compared, never trusted: a replay step, a
save and an online turn report each carry one so that a divergence or a
corrupted file is noticed. Nothing depends on it being hard to forge, so a
cryptographic digest bought nothing, and it was the dominant cost of a match.
Every recorder step hashed the whole growing event history twice, so a
headless 200-turn match spent most of its time in SHA-256 and got slower with
every turn. Chaining the histories makes a fingerprint cost the same on any
turn; a 200-turn headless match dropped from 76 s to 5 s across the two
changes.

**Compatibility.** Older saves and journals were verified through preserved
projections of the SHA-256 of their day and cannot be checked under the new
fingerprint, so they are refused as `OlderFormat` rather than loaded on trust.
The game has not been released, so no player's file is stranded; the
multiplayer session bump retires in-progress online matches for the same
reason, as [AGENTS.md](../AGENTS.md) requires when a state hash changes.

## 2026-09-19 — Order a ctrl-picked selection of gangs at once

- Decision: the Sector workspace's gang cards take a ctrl-click as a pick. With
  more than one gang picked, an arrow on any picked card orders the whole
  selection, and dragging any picked card gives the selection the order that
  drop would have given the one gang. A bulk order is drawn from an allowlist —
  Attack, Control, Heal, Hide, Influence, Move — and is carried out by every
  picked gang the rules allow, skipping the rest rather than failing whole. The
  selection is per sector and per turn: another sector, a borrowed opponent
  roster, leaving the workspace, the order itself, or the end of the turn clear
  it. Information panels do not count as leaving, however deep they stack — a
  site inspected from the bulk influence picker stands two panels above the
  workspace and comes back to the same picks. A turn ends for this purpose
  whenever the turn on screen is replaced, including an online turn the
  authoritative clock seals without the player submitting it.
- Evidence: the original issued one order per gang and carried no multi-select
  affordance on the sector screen, so no native layout or handler constrains
  this addition.
- Reason: a player moving six gangs out of a falling sector, or healing whoever
  is hurt, is giving one decision six times. The allowlist is what stays
  unambiguous in bulk: equipping, researching, giving and selling read a single
  gang's inventory and purse, and bribing, snitching, raising chaos or
  terminating a whole selection in one click is a mistake nobody wants to make
  at speed. Partial execution follows from the same principle — the selection
  is a wish, not a promise, so a sector with room for two takes two and says so
  rather than refusing all six.
- Compatibility boundary: recreation-only input. Every picked gang goes through
  `CommandValidator` and queues its own ordinary `GameCommand`, so what reaches
  the queue is indistinguishable from the same orders given one at a time, and
  replays, saves, and the multiplayer protocol are unaffected. Move counts the
  selection against the destination's capacity before it queues, because the
  validator only refuses a sector that is already full and the surplus would
  otherwise be turned back during resolution.

## 2026-09-18 — Scope the Sector workspace's opponent gang view to detection

- Decision: the detailed-sector portrait strip marks an opponent with a red
  `GANGS` banner and lends the gang cards to that opponent's roster only for
  gangs the viewer already detects. The borrowed roster is per-visit state: the
  viewer's own portrait, moving the selected sector, or re-entering the screen
  restores their own gangs, and hovering an opponent's card no longer highlights
  what its queued command targets.
- Evidence: the original sector screen listed only the viewing overlord's gangs
  and carried no portrait-strip affordance for reading another overlord's, so no
  native layout or handler constrains this addition.
- Reason: the roster answers the question the sector screen already poses —
  who else is standing here — without widening what a player knows. Reusing
  `MatchState.CanPlayerDetectGang` keeps the strip, the cards, and attack
  targeting on one detection rule, and keeping the enemy action strip and
  queued-command highlight suppressed keeps orders private.
- Compatibility boundary: recreation-only presentation. It reads match state and
  queues nothing, so replays, saves, and the multiplayer protocol are unaffected.

## 2026-09-17 — Do not substitute rejected recovered AI commands

- Decision: once Original-policy preparation has produced a recovered family
  action and target, failure to find an equivalent modern legal command leaves
  that gang without a submitted command. It must not fall through to the
  recreation-native scalar scorer.
- Evidence: native family handlers write directly into the 16-byte planning
  records consumed by the resolver and contain no validator-rejection or
  alternate-command branch. The shared sector selector can legitimately return
  the source sector when capacity blocks every routed step, producing a native
  same-sector Move that the recreation's player-facing adjacency validator does
  not expose.
- Reason: inventing a different legal action changes strategy, RNG-independent
  outcomes, and later action history without original evidence. Retaining the
  exact prepared tuple while submitting nothing preserves its continuation and
  replay state without weakening validation for player commands.
- Compatibility boundary: this is a projection boundary, not a claim that the
  native game left the gang idle internally. The original retained and resolved
  its raw tuple; the recreation records that tuple in AI planning state but
  omits an unrepresentable command from the modern queue.

## 2026-09-17 — Correct the original registry persistence defects

- Decision: retain the recreation's validated, atomic, per-user preferences file
  rather than reproduce the executable's machine-wide registry implementation.
- Evidence: loader `0x0046439a` and writer `0x00464783` both open
  `HKLM\SOFTWARE\Stick Man Games\Chaos Overlords\1.0` with `0x20019`
  (`KEY_READ`). The writer then issues twelve `RegSetValueExA` calls without a
  handle carrying `KEY_SET_VALUE`; the loader attempts the same for a generated
  `serialNum`. Every result is ignored. The loader also reuses one DWORD across
  thirteen unchecked queries, allowing a missing later value to inherit stale
  data instead of its compiled default.
- Reason: silent non-persistence and cross-setting contamination are clear API
  misuse, not game design. Reproducing them would lose user choices and make
  malformed legacy state affect unrelated settings.
- Compatibility boundary: original compiled defaults and option semantics remain
  evidence for recreation defaults, except for separately documented modern
  choices such as Slide Panels off. Preference writes are reliable and bounded;
  malformed data falls back per field. The obsolete `serialNum` side effect is
  excluded from authoritative RNG, as documented in `BIN-RNG-001`.

## 2026-09-17 — Correct the original AI hire slot/role indexing defect

- Decision: when the AI hire scheduler considers its scenario-specific family-6
  slot, compare the previous hire role with role 4 in every scenario instead of
  copying the executable's comparisons with slot numbers 6, 5, 2, 10, and 5.
- Evidence: selector `0x8f` in `0x00402d70` reads `0x00482160`, and planner
  `0x00458fa0` fills that location from current-role array `0x00482128` before
  choosing the next role. Each guarded special slot writes role 4, which the
  recovered scenario/family table maps to family 6. Dominance's original
  comparison with 10 is impossible for the verified 0..6 role domain; the other
  values can only suppress unrelated roles by numerical coincidence.
- Reason: this is a clear slot-versus-role indexing defect rather than ambiguous
  game design. Preserving it would create arbitrary scenario-dependent repeated
  family-6 hiring. The corrected comparison implements the common apparent
  intent—do not immediately choose another family-6 hire—while leaving all
  schedule tables, quotas, availability tests, ranking, and RNG behavior intact.
- Compatibility boundary: the `Original` policy deliberately differs from the
  shipped executable at this guard. Static evidence and tests preserve both the
  original finding and the exact recreation exception; no claim of bit-for-bit
  AI decision parity includes this bug.

## 2026-09-13 — Decode the supported Smacker subset at runtime

- Decision: keep the two verified user-owned `.smk` files in the extracted
  pack and implement their validated Smacker-v2 subset in managed runtime code.
- Reason: this keeps asset import and playback cross-platform and deterministic
  without requiring an ambient FFmpeg executable, host codec registration, or
  thousands of derived frame files.
- Boundary: malformed/unsupported video or audio must skip presentation and
  continue to the title. Movie state is never authoritative simulation,
  persistence, replay, or multiplayer state.
- Status: bounded container, palette, packed-audio, and indexed-frame decoding
  plus streaming presentation are implemented. The recreation policy is tested;
  original trigger/skip evidence and native visual/audio fidelity remain pending.

## 2026-09-13 — Stream the intro once, then keep it on the title screen

- Decision: the logo and intro movies play unattended only until one run has
  reached the end of the queue. The recreation has no player profiles, so the
  record lives in the single local preferences file alongside the other client
  settings. Completing it records `IntroMoviesSeen` in
  the then-current client preferences format v8 (subsequently migrated through
  the current format), and the title screen gains an `INTRO`
  button that replays the same queue on demand.
- Reason: 135 seconds of startup video, even with skip input, is a toll on every
  launch of a recreation that players restart often, while the movies themselves
  are content worth keeping reachable.
- Boundary: the flag is presentation preference only. It never enters saves,
  replays, phase hashes, or multiplayer state, a failed preference write leaves
  playback unaffected, and an unreadable movie pack reports on the title screen
  instead of blocking it.

## 2026-09-13 — Bug reports carry a replayable journal, stored apart from matches

- Decision: the in-game Escape menu can file a bug report to a hardcoded
  central address, and by default attaches the whole match as an event-sourced
  journal that replays from its first turn. The journal is anonymized before it
  is compressed and sent: player names become seat labels and Comlink text is
  redacted, both by re-running the match and recomputing every state
  fingerprint, so what is sent is a valid journal rather than an edited one.
  Names the original reads as cheat codes are game rules and are kept.
- Reason: a described bug in a deterministic simulation is a guess, and a
  journal is the bug itself. Recording one costs nothing — the replay recorder
  already wraps every mutation — but it only became a session's history once
  saves carried it: a load used to start a fresh recorder, so the turns that
  produced a bug were exactly what a report filed afterwards did not have.
- Companion, not a format change: a save writes `<save>.rchjournal` beside
  itself and a load resumes it when it ends at that save's own state. An old
  save still loads, a missing or corrupt journal is never a failed load, and a
  slot saved without one loses the journal already there rather than pairing
  with another game's history.
- A load adopts the journal rather than replaying it. Re-deriving the state from
  the steps means re-running the whole match — every recorded operation plus a
  full-state fingerprint each — on the thread the player is waiting on: a
  30-turn match measured 147 ms, and it grows with the match, so the reward for
  a long session would be a load that visibly stops. The save already *is* that
  state, so what the journal supplies is the history, and the one thing worth
  proving is that the two belong together: the last step's fingerprint against
  the restored state's. That is the same equality the replay was reduced to at
  the end, and it is the recorder's own invariant, so a companion left by
  another game in the same slot is still refused. Adopting measures 7 ms. Where
  something needs every step to still reproduce — a bug report, which replays
  the journal to anonymize it — that check happens there, off the game loop and
  on the copy about to be sent.
- Compression is Brotli, not zstd, with the codec byte reserved for zstd. A
  journal is the opening snapshot plus every recorded command, each with the
  fingerprint of the state it produced; the commands are most of the bytes and
  almost none of the compressed size, and the fingerprints are the reverse. In a
  measured 27-turn match the step array went from 106 KB to 12.6 KB, of which
  12.3 KB was the fingerprints and 0.2 KB everything else — and 364 hashes carry
  11.6 KB of entropy, so Brotli is already within a few percent of the floor.
  That makes the codec a question of what each side already has rather than of
  ratio: Brotli ships in .NET, in Node and in a Cloudflare Worker under
  `nodejs_compat`, while zstd needs a package on the game side and has no
  Workers decoder. A new dependency in a game that must build offline is the
  larger cost.
- Storage: reports go to the same deployment that hosts multiplayer, over a
  route of its own, into a separate D1 instance (a separate SQLite file when
  self-hosted) with its own migration lineage. They arrive unauthenticated,
  outlive the matches they describe, and carry other players' journals, so they
  share no schema, no lock and no blast radius with live matches. The journals
  themselves go to R2: one is hundreds of kilobytes to a few megabytes, D1
  refuses a row over 2 MB, and even the ones that fit would be dragged through
  every triage query. A deployment with no object store keeps archives under
  256 KiB inline and accepts the report without the journal above that.
- Boundary: the server never decompresses or parses an archive. It verifies the
  digest the client computed over the compressed bytes and stores opaque bytes.
- Status: implemented and tested. The central address is a placeholder
  (`http://localhost:8787`) until the public deployment exists.

## 2026-09-10 — Save compatibility scope

- Importing or exporting original *Chaos Overlords* saves is an explicit
  non-goal.
- Recreation-native saves and replays may change incompatibly before version
  1.0.0. Compatibility between pre-1.0 development formats is useful but is not
  a release gate.
- Keep the existing version discriminator, bounded readers, legacy hash
  selection, and migration structure as infrastructure for post-1.0 evolution.
- Existing pre-1.0 readers and tests may be retained when inexpensive, but new
  schema work may remove or replace them rather than accumulate migration debt.
- Starting with 1.0.0, incompatible format changes must increment the format
  version and provide either a deterministic migration or an explicitly
  documented safe rejection path.

## 2026-09-10 — Networking scope

Original network code and protocols are outside the parity target and are never
reproduced. Modern online play is a new design: a coordination server under
`multiplayer/` that relays sealed orders between deterministic clients and
verifies state hashes, hostable by players or run centrally
([`MULTIPLAYER.md`](./MULTIPLAYER.md)). Hot-seat play remains the local mode
and the client-side wiring of online play is tracked as follow-up work.
