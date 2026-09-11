# Architecture

Status: evolving implementation architecture
Last updated: 2026-09-11

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
- `GameModel/Notifications.cs`: bounded presentation-independent mechanical
  notification queues. The client filters those richer payloads into Last Turn
  Events; the separately recovered 16-entry bound belongs to Comlink messages.
- `GameModel/Determinism.cs`: serializable recovered Visual C++ random step and
  three-sample range wrapper plus canonical little-endian SHA-256 encoding.
- `GameModel/EffectiveStatistics.cs`: definition/equipment/influenced-site stat
  aggregation and deterministic six-sided dice rolls.
- `GameModel/Equipment.cs`: item-type slot mapping, replacement/unequip
  mutations, and manual half-price sale calculation.
- `GameModel/AiTurnPlanner.Dispatch.cs`: recovered family dispatch and the
  immutable sector/family snapshot shared across one ordered AI planning pass;
  family-specific branch order remains in separate partial files, while
  provisional command scoring is isolated in
  `AiTurnPlanner.ProvisionalFallback.cs`.
- `GameModel/AiTurnPlanner.RecoveredOperations.cs`: narrowly shared recovered
  operations whose ordering is identical across handlers. The human-weighted/
  full-pool projection remains shared by families 0, 4, and 6; the asymmetric
  draw/comparison primitive and authoritative Attack tuple write are shared by
  families 0, 2, 3, 4, 5, 6, 7, and 9. A separate focused-Attack operation makes
  the subset of those families that also persist the current sector explicit.
  Cost-based replacement equipment writes are likewise split into ordinary
  family-1/3/5 and focus-clearing family-0/2/4/6/7 operations; equipment choice
  and branch order remain family-local. Recovered Move destinations are also
  committed through ordinary and focus-clearing operations, while destination
  selection and family-specific formation/coverage side effects stay with the
  originating handler.
- `Persistence/NativeSaveSerializer.cs`: bounded, versioned deterministic
  snapshots with definition/state fingerprints and complete runtime restoration.
- `Persistence/NativeSaveStore.cs`: verified atomic file promotion,
  last-valid-generation backup, and corruption recovery.
- `Persistence/MatchReplay.cs`: mutation recorder plus bounded deterministic
  replay reader that checks validation results and state hashes after every
  step, with the same verified promotion and backup recovery policy.

Target subdivisions:

- `Definitions`: immutable, source-controlled game definitions.
- `State`: match instances and bounded identifiers/value types.
- `Commands`: validated player/AI intent.
- `Resolution`: phase processors and exact formulas.
- `Events`: ordered facts emitted by resolution.
- `Scenarios`: setup, scoring, objectives, and victory.
- `Persistence`: native snapshots and authoritative-operation replays are
  implemented. Pre-1.0 formats may change incompatibly; the migration framework
  is retained for post-1.0 compatibility. Original-save compatibility is an
  explicit non-goal.
- `Determinism`: original-compatible PRNG and state hashing.
- `MatchOutcome`: state projection and end-of-turn scenario completion.
- `EndgameAwards`: deterministic award projection from player statistics.
- `EndgameRanking`: timed-scenario score ordering and tied placements.
- `SpecialSiteRules`: controlled/local influenced-site research Tech ceilings
  and binary-recovered Factory pricing (`Cost - trunc(Cost / 3)`) without
  duplicating those rules in UI or AI. Match validation enforces that every
  site influencer owns its sector.
- `MatchSectorState.Income`: density-derived sector difficulty used by Control,
  Chaos, Tolerance, AI, and city presentation. It is deliberately distinct from
  influenced sites' Cash benefits, which belong to Upkeep/site protection.

### `Rechaos.Extractor`

- Validates canonical original tables and the whole source fingerprint.
- Repairs missing PX16 BMP header fields without modifying pixel bytes.
- Copies media/opaque resources and generates a per-output hash manifest.
- Decodes the supported user-owned WinHelp container and contents index into a
  bounded, versioned local JSON topic document; neither the source nor decoded
  copyrighted text is checked into or packaged with the project.
- Records manifest format and extractor versions, original-relative source,
  output hash/size/media type, and conversion method/geometry per asset.
- Stages and fully verifies a complete pack before rollback-safe directory
  promotion; `--force` explicitly rebuilds an already-valid matching pack.
- Verifies installed packs and skips extraction when the full pack already
  matches.
- Rejects malformed paths, missing assets, size changes, and hash changes.

`Rechaos.Game` loads that optional topic document through a separate bounded
validator. F1 opens a cross-platform two-pane viewer whose navigation follows
the contents-listed topics in original order, omits unlisted internal fragments,
and falls back to every decoded topic only when no contents entries exist.
Contextual entry maps each implemented screen to its most specific available
original topic and normalizes legacy ellipsis styling. The mouse wheel scrolls
the topic list or article according to pointer position; keyboard topic
navigation and paging remain available. Help is presentation-only: opening it
pauses AI progression but never mutates authoritative match state, replay state,
or deterministic hashes. Missing or invalid help data degrades to an import
instruction instead of invoking the obsolete Windows WinHelp subsystem.

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
loading, title/setup/hot-seat-handoff/city/sector/sector-gangs/gang/finance/ranking/items/Give/combat-summary/search/commands/hire/events/endgame routing,
keyboard and inverse-mapped mouse input, including edge-triggered right-click
cancellation that delegates to each interaction's existing close/back operation,
prototype board renderer, and an atlas-backed renderer for the original
`PX00129` pixel font. `UI-ATLAS.md` records the
first full-screen resource and hit-region mappings.
The client shell is a partial class split by responsibility. `ChaosGame.cs`
retains the loop, shared client state, and top-level input/screen routing.
`ChaosGame.Assets.cs`, `ChaosGame.Setup.cs`, and `ChaosGame.Persistence.cs`
isolate media loading, setup, and snapshot/replay I/O. The testable
`PlanningTimer` state machine and its client integration live in
`ChaosGame.PlanningTimer.cs`; expiry uses the ordinary replay-recorded planning
completion path and never enters deterministic Core state. `ChaosGame.TurnFlow.cs`
owns planning handoff, its presentation, and computer-turn orchestration;
`ChaosGame.Endgame.cs` owns the single-player victory/elimination splash and
completed-match awards presentation; `ChaosGame.Hire.cs` owns
the hire dock, comparison screen, and hire interactions. `ChaosGame.City.cs`
owns city navigation, control-panel routing, board rendering, status projection,
and direct map-command assignment. `ChaosGame.Sector.cs` owns the detailed-sector
projection, gang/site interaction, drag/drop command assignment, and hover
target feedback. `ChaosGame.Management.cs` owns the Finance, site-search, and
Ranking presentations. `SiteSearchUi.cs` keeps the presentation-only site
filter and matching-sector projection outside authoritative state. Gang,
site, and item information modal navigation lives with its corresponding renderer in
`ChaosGame.GangDetails.cs`, `ChaosGame.SiteDetails.cs`, and
`ChaosGame.ItemDetails.cs`. `ChaosGame.Items.cs` owns the research/equipment
browser and Give workflow. `ChaosGame.Commands.cs` owns command-picker state
transitions and input handling, while the specialized attack picker remains in
`ChaosGame.AttackPicker.cs`. `ChaosGame.Input.cs` centralizes right-click
cancellation priority across transient edits/drags and nested panels. Combat
presentation, results, and turn events
likewise remain in their focused partials. Further screen groups should follow
these boundaries instead of growing the shell again.
`Directory.Build.targets` enforces a 1,000-line ceiling for every compiled C#
source file in every project, making oversized responsibilities a local-build,
test, CI, and release failure rather than a review-only convention. The limit
can be lowered with `MaximumSourceFileLines` for validation; disabling it
requires the explicit `DisableSourceFileLineLimit=true` MSBuild property.
It reads original media only from the extracted asset directory.
The item workflow projects research/equipment state and submits Research,
Equip, Give, and Sell through the replay recorder and authoritative Core
validator. Give first uses the original three-slot equipment selection panel,
then expands only recipients in the same sector who can accept every selected
item. The transaction resolver uses the binary's player/roster-slot scan,
reserves outgoing Give items, processes recipient transactions, and applies
incoming gifts afterward. This preserves two-gang swaps and the original rule
that an incoming gift overwrites a recipient's same-turn purchase. Multi-slot
Sell likewise preserves the original payout-local overwrite quirk.
Move uses `PX05006` and projects its 3-by-3 neighborhood directly from the same
native ownership tiles as the city map; command validation remains the authority
for edge, adjacency, and friendly-capacity availability.
Computer Command/Hire turns use the deterministic baseline in `AI-SPEC.md` and
submit through that same recorder; its policy is not an original-parity claim.
The audio router consumes newly appended combat-resolution events and maps
equipped items, unarmed attacks, and detected police attacks to their original
`SND005xx` cues while leaving evasion silent. Each cue travels with its Detailed
Combat clip and plays at that clip's first animation tick, including the
separately queued retaliation clip. It also owns the recovered
nine-entry general-effect slot table (`SND00200`-`SND00208`, with no slot 5);
named slot identities replace presentation magic numbers. Setup uses the
statically identified slot 3 accepted-input and slot 4 rejected-input cues,
while full local-setup push buttons use slot 2; an invalid pointer-driven player
count change follows its press cue with slot 4. The four setup push controls
retain the pressed identity and act only on release inside that same recovered
rectangle. Panel confirmation uses slots 0/1, a handoff into an unread Comlink
inbox uses slot 6, and the planning countdown uses slots 7/8. Slot 9 is loaded by the
original but has no gated-wrapper call site. None of these routes feeds playback
state or timing back into the simulation.
`SoundtrackCatalog` discovers the extracted `Track02`-`Track09` Ogg files and
encodes the recovered title/setup, gameplay, and endgame track programs, while
`ChaosGame.Media.cs` owns their optional streaming, screen transition, repeat,
focus pause/resume, and recovered 0-10 volume behavior. Effects use an
independent recovered 0-10 scale with a level-6 default and the same amplitude
conversion. `GamePreferencesStore`
loads and atomically replaces a bounded, recreation-versioned local preferences
file; malformed, unsupported, or out-of-range data falls back to the recovered
Music level-5, Effects level-6, enabled idle-gang-warning, and disabled planning
timer defaults. Version 5 also persists base/current gang-stat projection,
automatic Detailed Combat playback, and bounded panel motion while migrating
version-4 audio/warning/timer choices. Version 6 adds a persistent
windowed/borderless-fullscreen presentation choice, defaulting older preferences
to windowed mode without disturbing the 640x460 virtual coordinate system.
Legacy color depth is always enabled by the modern renderer. Playback and preference-write failures remain presentation-only;
media state never enters Core, saves, replays, commands, events, or deterministic
hashes.

Platform distribution scripts publish self-contained game and extractor
payloads while forcing original assets out of every package. On Windows the
game uses a directory deployment so MonoGame's SDL2 and OpenAL libraries remain
adjacent to `Rechaos.Game.exe`; a platform-initialization smoke test enforces
that runtime boundary. The extractor remains a single-file utility.
`Build-WindowsInstaller.ps1` produces an Inno Setup `.exe`,
`Build-LinuxInstaller.ps1` produces an amd64 Debian package, and
`Build-MacInstaller.ps1` produces native x64/arm64 application-bundle `.pkg`
installers. Before publishing, `tools/Verify-Repository.ps1` enforces the
tracked-file legal boundary from `tools/repository-policy.json`.
`packaging/windows/RechaosOverlords.iss` detects a
legal GOG source through registry records and bounded conventional paths, then
optionally runs the extractor into the installed game's private `Assets`
directory. Import output is surfaced through Setup, invalid sources can be
reselected and retried, and a failed or incomplete import gives Setup a nonzero
exit code. Linux and macOS use a per-user writable asset root when an adjacent
pack is absent. The manual release workflow defaults to building and verifying
only the Windows installer, with an explicit all-platform option that requires
Windows, Linux, and both macOS architectures. It creates a requested version
tag only after every selected installer succeeds, so failed builds cannot
publish a tag. Validation is also manually dispatched and may upload short-lived
build artifacts, but never creates a tag or release.

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

Instant commands resolve in fixed player/roster-slot order. Each Influence gang
rolls Force plus effective Influence separately and immediately reduces the
site's remaining resistance; later commands skip their rolls after completion.
Chaos and Control are grouped resolvers. Control commands from one player in the
same sector pool Force and effective Control into one non-dice comparison. Each
Control group emits one ordered result per participating command.
Cross-player groups in one sector evaluate against the same phase-opening owner,
visible defenders, influenced Support, and income. A unique positive leader
captures directly; equal positive leaders use one bounded draw in ascending
player-slot order. At best margin zero, neutral/no-capture is the first candidate
before every tied player. An owned sector can therefore be overthrown at most
once per phase, and an execution-time Crackdown rejects every group.

Instant resolution snapshots every acting gang's effective statistics before
any command mutates site influence. Heal, Research, and per-gang Influence rolls
therefore share the phase-opening view of equipment and local sites; acquiring a
Science Center or Research Lab cannot retroactively improve a concurrent roll.
Mutation and event emission follow the binary player/roster-slot scan.

Chaos is resolved across the entire subphase: gangs roll individually in binary
player/roster-slot order, one player's same-sector gangs share the aggregate
result, and every group in a sector contributes before its crackdown state and
payouts commit. A new crackdown suppresses all groups in that sector. This
phase-wide barrier prevents queue order from changing RNG or letting an earlier
player escape suppression; uncontrolled payout divides the completed
player-sector success total once.

Combat also uses a phase-wide barrier. It snapshots Force, effective statistics,
equipment class, and Hidden state for every gang; calculates all attacks and
eligible retaliation in binary player/roster-slot order; then commits aggregate
damage. This keeps
an eliminated gang's simultaneous response independent of event emission order.
Hidden attacks use an individual Detect-versus-Stealth roll and suppress
retaliation on a hit. Cooperative sector visibility is a separate deterministic
query shared by the Sector portrait strip and Search screen because Hide does
not affect whether a gang is displayed. The subsequent police roll pass also
uses player/roster-slot order; overkill attribution remains a binary-parity gap.

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

Movement resolution runs a complete player/roster-ordered Terminate pass before
a separate player/roster-ordered Move pass. Move enforces the six-friendly-gang
capacity both during submission and again during resolution. Control ownership
changes are atomic with former-owner influenced-site cleanup, Support adjustment, and
Overthrow statistics so phase hashes cannot observe a partially captured sector.
The binary owner-write inventory also makes ownership independent of garrison:
moving or terminating the last friendly gang does not neutralize the sector.

`MatchState.FinishUpkeep` resolves every active player in stable player-ID order
before entering Command. Each result separates sector tax, influenced-site
cash, and active-gang upkeep so reference fixtures can locate the first differing
component. Desertion and unverified special modifiers remain outside this slice.

Hiring uses the same deferred boundary as the manual: during the player's
planning turn, selecting one of three offers reserves the recruit without
charging or removing the offer. A player may also snub one offer during
planning. `FinishHire` validates capacity and cash, then charges and places each
recruit at a rolled 5–9 Force, assigns a stable match-wide gang ID, reuses the
first inactive roster slot while resetting its AI planning record, and leaves a
same-slot offer tombstone for next-turn refill. Validation is an ordered set
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
uses the recovered ascending empty-slot completion, unique portrait/name draws,
density/site algorithm, fixed HQ candidates, Force 10 Right Hands, $20 standard
cash, `SMGISLANDS` neutral-sector override and deferred offer initialization.
The setup client supplies a transient ordered sparse `MatchSetup` when a human
has moved to a non-contiguous color slot; the factory fills missing IDs in
ascending order and returns the ordinary contiguous six-player setup before any
authoritative state is constructed.
Original seed and the remaining pre-city call context remain provisional pending
a reference fixture.

## State ownership target

`MatchState` now owns the schema for:

- immutable setup/scenario identity and initial seed;
- turn number, current top-level phase, and active command player;
- six player slots, status, cash, score, statistics and research;
- 64 sector instances, three sites each, ownership, influence, tolerance,
  support, income, chaos, police and crackdown history;
- at most 80 active gang slots per player with ascending inactive-slot reuse,
  stable IDs, force, position, equipment,
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

## Online play

The multiplayer server under `multiplayer/` is a separate TypeScript workspace
(Hono on Node.js or Cloudflare Workers). It never simulates: it seals each
simultaneous turn, relays the order set, and verifies the state hash every
client reports after resolving that set through `Rechaos.Core`. Design,
protocol and the client contract: [`MULTIPLAYER.md`](./MULTIPLAYER.md).

## Error and security model

- `RuntimeDiagnostics` writes bounded JSON-lines session logs and separate
  exception reports under the user's local application-data directory. It
  retains five sessions and ten crashes, caps a session at 1 MiB, never uploads
  data, and deliberately excludes player names, commands, saves, and asset
  paths. Diagnostics are best-effort and disable themselves on I/O failure.
- Binary readers reject truncated/partial records and invalid signatures.
- Source validation uses exact known hashes before content is trusted.
- Manifest paths are canonicalized and must remain below the selected root.
- Every output length and SHA-256 is verifiable.
- Parsers must gain explicit size/allocation limits before accepting additional
  source versions or save files.
- Original network code and protocols are an explicit non-goal: they are not
  ported, exposed, or supported for interoperability.

## Known architectural debt

- The client now consumes authoritative `MatchState`, advances its real phase
  coordinator, submits validated Move/Control commands, and uses deferred Hire
  placement. F5/F9 expose atomic native quick-save/load with backup recovery in
  the user's local application-data directory; the same store writes an
  automatic recovery checkpoint after Player Elimination completes each turn.
  All client mutations pass through `MatchReplayRecorder`; F6/F10 atomically
  save and verify/play the current replay, recovering the previous verified
  generation when the primary is missing or corrupt. New matches now use the
  recovered density/site generator, fixed HQ
  candidates, Right Hands setup and deferred initial offers; omitted local slots
  are completed as Computers with the recovered pre-city portrait/name RNG, and
  original seed selection remains provisional. Local setup begins with one
  human and supports Add/Remove, 10-character names, portrait selection, and
  face-drag movement/exchange between the six color slots.
- Fresh Siege setup marks the six assigned starting HQ sectors as authoritative
  objective landmarks without consuming RNG. The city projection draws the
  manual-described pair of gray pylons over each landmark regardless of owner.
- The client has a title/setup/city router and virtual-coordinate mouse input,
  original next-player privacy handoff, an event/notification viewer whose
  dismissal mutations are replay-recorded, plus a state-driven endgame summary
  on the mapped original frame. Exact setup hit alignment and golden-screen
  comparison remain open. Its command picker projects all
  currently legal commands from Core rather than maintaining parallel UI rules;
  its Hire panel exposes all three offers, selected-sector placement and snubbing,
  while sector/gang views project authoritative sites, influence, effective stats,
  equipment and queued commands.
- Exact control edges and within-subphase command ordering remain provisional.
- Runtime manifest checking validates version only.
- Media resources are extracted but not presented.

Each item must move to the parity matrix before replacement so behavior changes
remain traceable.
