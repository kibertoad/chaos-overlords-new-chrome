# Architecture

Status: evolving implementation architecture
Last updated: 2026-09-09

## Dependency direction

```text
Rechaos.Game --------> Rechaos.Core
Rechaos.Extractor ----> Rechaos.Core
Rechaos.Tools --------> Rechaos.Core
Rechaos.Tests --------> Core + Extractor
```

`Rechaos.Core` owns portable definitions and simulation. It must never depend on
MonoGame. `Rechaos.Game` translates input to simulation commands and simulation
events to presentation. `Rechaos.Extractor` is an installation-time tool and is
not a runtime dependency of the game.

The current asset parser in Core performs file I/O because it validates original
tables during extraction. As the format layer grows, binary format readers will
move under `Rechaos.Formats`; the pure simulation will remain in Core.

## Projects

### `Rechaos.Core`

- `Assets/OriginalData.cs`: immutable decoded site, gang, item, and statistic
  definitions.
- `Assets/OriginalDataReader.cs`: strict fixed-record binary readers.
- `Assets/BundledOriginalData.cs`: embedded canonical gameplay definitions.
- `Assets/GameplayDataProvenance.cs`: pinned source and generated-data hashes.
- `GameModel/MatchBootstrap.cs`: explicit-layout, scenario-aware new-match initialization.
- `GameModel/TurnStructure.cs`: original five turn phases, six execution
  subphases, action IDs, and phase routing.
- `GameModel/TurnCoordinator.cs`: headless phase state machine and explicit
  transition records.
- `GameModel/GameCommands.cs`: bounded IDs, typed targets, deterministic
  per-gang command storage, replacement, cancellation, and repeat retention.
- `GameModel/MatchLimits.cs`: one source for board, player, gang, site, item,
  and hire-pool capacities.
- `GameModel/MatchState.cs`: explicit headless setup, player, sector, site, gang,
  research, inventory, hire, statistics, and equipment state.
- `GameModel/CommandValidation.cs`: data-driven action descriptors and typed,
  non-mutating command validation results.
- `GameModel/CommandOptionCatalog.cs`: deterministic expansion of declarative
  target shapes into validator-approved commands for UI and future AI use.
- `GameModel/GameEvents.cs`: monotonically ordered mechanical event records.
- `GameModel/CommandResolution.cs`: evidence-gated execution dispatcher and
  grouped phase dispatch and result codes; all Instant actions (Bribe, Heal,
  Hide, Influence, Research, and Snitch) now resolve.
- `GameModel/EconomyResolution.cs`: ordered Upkeep-phase sector/site income,
  gang upkeep, persistent negative balances, events, and notifications.
- `GameModel/ToleranceResolver.cs`: income/site-derived normal tolerance and
  one-point Upkeep restoration of temporary Bribe/Snitch changes.
- `GameModel/Notifications.cs`: bounded per-player mechanical notification queues
  with presentation-independent payloads.
- `GameModel/Determinism.cs`: serializable recovered Visual C++ random step and
  three-sample range wrapper plus canonical little-endian SHA-256 encoding.
- `GameModel/EffectiveStatistics.cs`: definition/equipment/influenced-site stat
  aggregation and deterministic six-sided dice rolls.
- `GameModel/Equipment.cs`: item-type slot mapping, replacement/unequip
  mutations, and manual half-price sale calculation.
- `Persistence/NativeSaveSerializer.cs`: bounded, versioned deterministic
  snapshots with definition/state fingerprints and complete runtime restoration.
- `Persistence/NativeSaveStore.cs`: atomic file promotion, previous-save backup,
  and corruption recovery.
- `Persistence/MatchReplay.cs`: mutation recorder plus bounded deterministic
  replay reader that checks validation results and state hashes after every step.

Target subdivisions:

- `Definitions`: immutable, source-controlled game definitions.
- `State`: match instances and bounded identifiers/value types.
- `Commands`: validated player/AI intent.
- `Resolution`: phase processors and exact formulas.
- `Events`: ordered facts emitted by resolution.
- `Scenarios`: setup, scoring, objectives, and victory.
- `Persistence`: native snapshots and authoritative-operation replays are
  implemented; migration and original-save compatibility remain separate workstreams.
- `Determinism`: original-compatible PRNG and state hashing.
- `MatchOutcome`: state projection and end-of-turn scenario completion.
- `EndgameAwards`: deterministic award projection from player statistics.
- `EndgameRanking`: timed-scenario score ordering and tied placements.
- `SpecialSiteRules`: gang/local-site research Tech ceilings and Factory-priced
  equipment purchases without duplicating those rules in UI or AI.

### `Rechaos.Extractor`

- Validates canonical original tables and the whole source fingerprint.
- Repairs missing PX16 BMP header fields without modifying pixel bytes.
- Copies media/opaque resources and generates a per-output hash manifest.
- Records manifest format and extractor versions, original-relative source,
  output hash/size/media type, and conversion method/geometry per asset.
- Stages and fully verifies a complete pack before rollback-safe directory
  promotion; `--force` explicitly rebuilds an already-valid matching pack.
- Verifies installed packs and skips extraction when the full pack already
  matches.
- Rejects malformed paths, missing assets, size changes, and hash changes.

CLI modes:

```text
--source <install> [--output <assets>] [--force]
                                                extract unless already complete
--verify-source --source <install>           validate original source only
--verify-output [--output <assets>]          validate every output hash
--verify-output [--output <assets>] --quick  validate paths and sizes only
--catalog [--output <assets>]                generate docs/ASSET-CATALOG.md
--analyze-px [--output <assets>]             compare PX08/PX16 color encodings
```

Target additions are semantic catalog ownership, stale-staging cleanup, additional
supported source fingerprints, transparency validation, video conversion or playback,
and machine-readable diagnostics.

### `Rechaos.Tools`

- `state-diff` recursively compares sanitized JSON states without depending on
  JSON property order.
- Differences have stable JSON paths and may use a checked-in path-to-label map
  so reports name known state fields while retaining their exact location.
- Exit code 0 means states match, 1 means differences were found, and 2 means
  the input or invocation was invalid.

### `Rechaos.Game`

The current client owns the MonoGame loop, point-scaled virtual canvas, asset
loading, title/setup/hot-seat-handoff/city/sector/gang/finance/ranking/items/Give/combat-summary/search/commands/hire/events/endgame routing,
keyboard and inverse-mapped mouse input,
prototype board renderer, and an internal pixel font. `UI-ATLAS.md` records the
first full-screen resource and hit-region mappings.
It reads original media only from the extracted asset directory.
The item workflow projects research/equipment state and submits Research,
Equip, Give, and Sell through the replay recorder and authoritative Core
validator; Give expands only legal same-sector recipients for the equipped item.
Computer Command/Hire turns use the deterministic baseline in `AI-SPEC.md` and
submit through that same recorder; its policy is not an original-parity claim.
The audio router consumes newly appended attack-resolution events and maps an
equipped item's original Sound field to `SND005xx`; it never feeds playback
state or timing back into the simulation.

Target presentation layers:

1. `ScreenRouter` - title, setup, city, sector, panels, modal and endgame states.
2. `InputMapper` - mouse/keyboard/controller actions in virtual coordinates.
3. `ViewModels` - presentation projections of simulation state.
4. `Renderer` - PX atlas, primitives, fonts, cursors, animations and scaling.
5. `MediaDirector` - consumes events to trigger effects/music/video.
6. `Settings` - compatibility and optional modern presentation behavior.

## Target command/event flow

```text
Input or AI
    -> GameCommand
    -> command validation
    -> queued command in MatchState
    -> phase resolver using deterministic RNG
    -> ordered GameEvent sequence
       -> state mutation/hash
       -> UI animation and messages
       -> audio triggers
       -> replay log
       -> parity tests
```

Commands describe intent and may be rejected without changing state. Events
describe what happened and are never used as hidden commands. UI animation may
lag behind resolved state but cannot change it.

## Original turn model

One turn contains Upkeep, Command, Execution, Hire, and Player Elimination.
Execution resolves all players in Instant, Combat, Transaction, Chaos, Movement,
and Control order. `TurnStructure` is the current executable specification of
that ordering. Within-subphase ordering and tie-breaking remain unverified.
Before an execution subphase mutates state, every queued action in that subphase
must have a supported resolver. Unsupported actions block advancement rather
than being silently consumed.

Influence, Chaos, and Control are grouped resolvers. Influence commands from one player
aimed at the same site pool Force and effective Influence into one deterministic
roll stream. Control commands from one player in the same sector pool Force and
effective Control into one non-dice comparison. Each group resolves at its
earliest queue position and emits one ordered result per participating command.
Cross-player conflicts remain an explicit parity gap.

Chaos is resolved across the entire subphase: one player's same-sector gangs
share a roll, every group in a sector contributes before its crackdown state and
payouts commit, and a new crackdown suppresses all groups in that sector. This
phase-wide barrier is deterministic and prevents queue order from letting an
earlier player escape suppression; accumulation/reset and original ordering are
still provisional pending binary fixtures.

Combat also uses a phase-wide barrier. It snapshots Force, effective statistics,
equipment class, and Hidden state for every gang; calculates all attacks and
eligible retaliation in queue order; then commits aggregate damage. This keeps
an eliminated gang's simultaneous response independent of event emission order.
Hidden attacks use an individual Detect-versus-Stealth roll and suppress
retaliation on a hit. Cooperative sector visibility is a separate deterministic
query shared by the Sector portrait strip and Search screen because Hide does
not affect whether a gang is displayed. Original police/gang ordering and
overkill attribution remain explicit binary-parity gaps.

All action resolvers consume the same effective-stat projection. It adds gang
definition, three equipment slots, and every same-sector site influenced by the
gang's owner, preventing research, social, Chaos, control, and combat paths from
silently disagreeing about local modifiers.

The Transaction resolver treats equipment as gang-owned: Equip purchases
directly into one of three slots, Give moves an equipped item between friendly
gangs, and Sell removes an equipped item for cash. The player inventory map is
validated state reserved for future acquisition workflows; it is not silently
used as a shop or overflow stash. Terminate clears all gang-owned equipment in
the Movement phase.

Move commits in stable queue order and enforces the six-friendly-gang capacity
both during submission and again during resolution. Control ownership changes
are atomic with former-owner influenced-site cleanup, Support adjustment, and
Overthrow statistics so phase hashes cannot observe a partially captured sector.

`MatchState.FinishUpkeep` resolves every active player in stable player-ID order
before entering Command. Each result separates sector tax, influenced-site
cash, and active-gang upkeep so reference fixtures can locate the first differing
component. Desertion and unverified special modifiers remain outside this slice.

Hiring uses the same deferred boundary as the manual: during the player's
planning turn, selecting one of three offers reserves the recruit, charges its
initial price, and removes that offer, but does not create a gang. A player may
also snub one offer during planning. `FinishHire` later places each recruit at a rolled 5–9 Force, assigns
a stable match-wide gang ID, and deterministically fills hired and snubbed
vacancies while preserving three distinct choices. Validation is an ordered set
of side-effect-free `IValidationRule` implementations. Each rule owns both its
typed failure code and user-facing message so UI previews, AI queries, and
authoritative submission cannot disagree about eligibility.

`GameplayTurnFlow` separates the player-facing turn from the diagnostic phase
machine. Normal play stops only in Command for each player's planning, where
commands, equipment, and hiring can be edited in any order. Done drains
Execution, deferred Hire placement, Player Elimination, and Upkeep through
replay-recorded transitions. `--debug-phases` retains boundary-at-a-time stepping.

The client keeps the whole-city board and detailed-sector presentation as
separate projections over the same authoritative sector collection. The detail
screen centers its selected sector in a clipped 3-by-3 neighborhood assembled
from the ownership sheets, then places the sector's three `PX02000` site images
beside it. Neighbor clicks only change the presentation cursor and never mutate
match state.

Player Elimination now runs at its named phase boundary. An active player with
neither an active gang nor an owned sector becomes eliminated; any remaining
site influence is cleared back to the site's base resistance, and ordered events
and notifications expose the transition. Exact original cleanup and simultaneous
ordering still require binary fixtures.

`MatchBootstrap` converts an explicit city and player-placement layout into the
authoritative state graph. It remains useful for tests, imported scenarios and
future editors. It verifies a distinct neutral Headquarters sector for every player,
creates stable-ID Right Hands gangs there, transfers sector control, and leaves
the caller's reusable layout untouched. Armageddon setup applies its manual-only
$500 and all-items-researched overrides at this boundary. `OriginalMatchFactory`
uses the recovered density/site algorithm, fixed HQ candidates, Force 10 Right
Hands, $20 standard cash and deferred offer initialization. Original seed and
setup-mode selection remain provisional pending a reference fixture.

## State ownership target

`MatchState` now owns the schema for:

- immutable setup/scenario identity and initial seed;
- turn number, current top-level phase, and active command player;
- six player slots, status, cash, score, statistics and research;
- 64 sector instances, three sites each, ownership, influence, tolerance,
  support, income, chaos, police and crackdown history;
- at most 80 gang slots per player with stable IDs, force, position, equipment,
  queued/repeat action, targets, flags and effective stats;
- three-entry hire pool, pending hire placement and per-turn snub state per player;
- bounded notification queues;
- deterministic PRNG state and consumption counter.

The initial schema covers setup identity, phases, players, sectors/sites, gangs,
hire state, persistent research progress/completion, inventory, equipment,
statistics, command projections,
ordered events, bounded notification queues, deterministic PRNG state, and
phase-boundary hashes. Reference-derived resolvers remain to be added. Public
collection projections are read-only; renderer/view models must not receive
mutation paths.

## Determinism boundary

Compatibility state may not depend on system time, thread scheduling, hash-map
iteration, locale, filesystem ordering, rendering frames, audio playback, or
platform floating-point differences. Collections that affect decisions use a
defined order. The headless model reproduces the recovered Visual C++ 1998 raw
RNG step and the game's three-sample inclusive-range wrapper. Generated city,
headquarters and hire offers consume that explicit stream. Initial seeding and
the complete call-site order remain provisional; presentation effects must use
a separate cosmetic stream.

State hashes are computed from a versioned canonical little-endian binary
encoding after transitions made through `MatchState`. The encoding includes
definitions, setup, phase/RNG state, players, sectors, commands, and pending
notifications. Replays store an initial native snapshot, ordered authoritative
operations and expected state hashes.

## Proprietary-content boundary

Checked-in content includes code, numeric/mechanical tables, names and short
descriptions as agreed for this project. Original art, palettes, audio, music,
video, help, and opaque archives remain user-extracted and Git-ignored. A clean
clone must compile and run tests without them.

The original executable is a research oracle only. It is never copied,
redistributed, invoked by the shipped recreation, or required by the extractor.

## Error and security model

- Binary readers reject truncated/partial records and invalid signatures.
- Source validation uses exact known hashes before content is trusted.
- Manifest paths are canonicalized and must remain below the selected root.
- Every output length and SHA-256 is verifiable.
- Parsers must gain explicit size/allocation limits before accepting additional
  source versions or save files.
- Original network protocols remain disabled and isolated from production code.

## Known architectural debt

- The client now consumes authoritative `MatchState`, advances its real phase
  coordinator, submits validated Move/Control commands, and uses deferred Hire
  placement. F5/F9 expose atomic native quick-save/load with backup recovery in
  the user's local application-data directory; the same store writes an
  automatic recovery checkpoint after Player Elimination completes each turn.
  All client mutations pass through `MatchReplayRecorder`; F6/F10 atomically
  save and verify/play the current
  replay. New matches now use the recovered density/site generator, fixed HQ
  candidates, Right Hands setup and deferred initial offers; original seed
  selection and complete setup-mode context remain provisional.
- The client has a title/setup/city router and virtual-coordinate mouse input,
  original next-player privacy handoff, an event/notification viewer whose
  dismissal mutations are replay-recorded, plus a state-driven endgame summary
  on the mapped original frame, but still
  lacks the original setup detail, AI turn driver, notification presentation
  detail, animations and most original panels. Its command picker projects all
  currently legal commands from Core rather than maintaining parallel UI rules;
  its Hire panel exposes all three offers, selected-sector placement and snubbing,
  while sector/gang views project authoritative sites, influence, effective stats,
  equipment and queued commands.
- Exact control edges and within-subphase command ordering remain provisional.
- Runtime manifest checking validates version only.
- Media resources are extracted but not presented.

Each item must move to the parity matrix before replacement so behavior changes
remain traceable.
