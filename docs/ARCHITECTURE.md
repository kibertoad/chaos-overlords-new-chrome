# Architecture

Status: evolving implementation architecture
Last updated: 2026-09-07

## Dependency direction

```text
Rechaos.Game --------> Rechaos.Core
Rechaos.Extractor ----> Rechaos.Core
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
- `GameModel/GameState.cs`: current playable prototype match state.
- `GameModel/TurnStructure.cs`: original five turn phases, six execution
  subphases, action IDs, and phase routing.
- `GameModel/TurnCoordinator.cs`: headless phase state machine and explicit
  transition records.
- `GameModel/GameCommands.cs`: bounded IDs, typed targets, deterministic
  per-gang command storage, replacement, cancellation, and repeat retention.

Target subdivisions:

- `Definitions`: immutable, source-controlled game definitions.
- `State`: match instances and bounded identifiers/value types.
- `Commands`: validated player/AI intent.
- `Resolution`: phase processors and exact formulas.
- `Events`: ordered facts emitted by resolution.
- `Scenarios`: setup, scoring, objectives, and victory.
- `Persistence`: native snapshots and replay schema.
- `Determinism`: original-compatible PRNG and state hashing.

### `Rechaos.Extractor`

- Validates canonical original tables and the whole source fingerprint.
- Repairs missing PX16 BMP header fields without modifying pixel bytes.
- Copies media/opaque resources and generates a per-output hash manifest.
- Verifies installed packs and skips extraction when the full pack already
  matches.
- Rejects malformed paths, missing assets, size changes, and hash changes.

CLI modes:

```text
--source <install> [--output <assets>]       extract unless already complete
--verify-source --source <install>           validate original source only
--verify-output [--output <assets>]          validate every output hash
--verify-output [--output <assets>] --quick  validate paths and sizes only
```

Target additions are transactional staging/promotion, semantic asset catalogs,
additional supported source fingerprints, PX08 decoding, video conversion or
playback, and machine-readable diagnostics.

### `Rechaos.Game`

The current client owns the MonoGame loop, point-scaled virtual canvas, asset
loading, keyboard input, prototype board renderer, and an internal pixel font.
It reads original media only from the extracted asset directory.

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

## State ownership target

`MatchState` will own:

- immutable setup/scenario identity and initial seed;
- turn number, current top-level phase, and active command player;
- six player slots, status, cash, score, statistics and research;
- 64 sector instances, three sites each, ownership, influence, tolerance,
  support, income, chaos, police and crackdown history;
- at most 80 gang slots per player with stable IDs, force, position, equipment,
  queued/repeat action, targets, flags and effective stats;
- three-entry hire pool and pending hire placement per player;
- bounded notification queues;
- deterministic PRNG state and consumption counter.

Only resolvers mutate this state. Renderer/view models receive read-only
projections.

## Determinism boundary

Compatibility state may not depend on system time, thread scheduling, hash-map
iteration, locale, filesystem ordering, rendering frames, audio playback, or
platform floating-point differences. Collections that affect decisions use a
defined order. The original PRNG is still unknown; `System.Random` in the
prototype is explicitly provisional and must be removed before parity claims.

State hashes will be computed from a canonical binary encoding after every
phase. Replays store setup plus ordered commands and expected phase hashes.

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

- `GameState` combines cursor/UI messages with simulation state.
- Hiring is immediate instead of deferred to the Hire phase.
- Control and city generation formulas are placeholders.
- Gang instances have stable identity and ownership, but still lack mutable
  force, equipment, and an embedded read-only command projection.
- The prototype client still advances its older per-player turn directly rather
  than driving `TurnCoordinator`; within-subphase command ordering is explicitly
  provisional until the binary tie-breaker is recovered.
- Source extraction writes directly to the destination instead of staging.
- Runtime manifest checking validates version only.
- Media resources are extracted but not presented.

Each item must move to the parity matrix before replacement so behavior changes
remain traceable.
