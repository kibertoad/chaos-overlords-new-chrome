# Complete implementation and migration plan

Status: active roadmap
Last updated: 2026-09-09
Target: a deterministic, cross-platform MonoGame recreation of the Windows 95
release of *Chaos Overlords*, requiring a user-owned original asset pack.

## 1. Definition of complete

The recreation is complete when it can reproduce a full original single-player
or hot-seat match from setup through the final score, with equivalent rules,
turn resolution, AI decisions, audiovisual feedback, and all ten objectives.
Given the same initial state and RNG stream, the simulation must produce the
same state transitions as the reference game except where a documented,
optional modernization is enabled.

Completion requires all of the following:

- The open-source distribution contains all executable logic and non-expressive
  gameplay data needed to run the game.
- The asset tool validates a supported, legally acquired original data pack and
  extracts every required copyrighted resource without using the original EXE.
- Every original screen and game command is present or explicitly classified as
  an intentionally unsupported legacy feature.
- Original saves can be imported. Export compatibility is required only after
  byte-level round-trip safety is demonstrated.
- Simulation behavior is covered by deterministic fixtures and a parity matrix.
- Every file format, inferred field, formula, state transition, and unresolved
  discrepancy is documented with evidence and a confidence rating.
- The game builds and runs on supported Windows, Linux, and macOS targets.

Network compatibility with the unsafe original protocol is not a default
requirement. A safe modern multiplayer transport may be added after local game
parity; original-protocol interoperability must remain isolated and opt-in if it
is ever attempted.

## 2. Current baseline

| Area | Present now | Remaining |
|---|---|---|
| Build | .NET 10 solution, MonoGame DesktopGL 3.8.5.1, xUnit v3 4.0.0 | CI, packaging, other OS smoke tests |
| Original data | Embedded 22 sites, 90 gangs, and 64 items with pinned provenance | Semantic/formula validation, versioned generation tool |
| Extraction | Transactional/versioned full-pack SHA-256 validation; 215 repaired RGB555 PX16 images, 214 retained plus decoded PX08 resources, 28 WAVs, 8 Ogg tracks, 2 Smacker videos, help and opaque files; generated 685-output factual catalog | Transparency/color-key validation, video strategy, semantic role/owner resolution |
| Simulation | Deterministic city seed, six players, stable gang IDs, typed command queue, headless phase coordinator, Upkeep, all 14 command resolvers, simultaneous gang/Crackdown combat, 3–5-turn police duration/extension and three-in-five control loss, hidden attack/visibility checks, and local influenced-site stats | Crackdown notification/timing fixtures, special buildings, original RNG seeding/order and exact parity formulas |
| Client | Scaled 640x460 routed setup/handoff/city/sector/gang/finance/ranking/items/Give/combat-summary/search/commands/hire/events/endgame UI backed by authoritative `MatchState`; original neutral/player city layers composited per sector; recovered setup; local controls; private handoff; Core-derived commands, projections, visibility, combat results, research/equipment transfer, hire/snub and notification panels; mouse/keyboard, saves/replays | Full setup detail, remaining sprites/atlas and management panels/hit maps, animation, sound/music, accessibility |
| Tests | Parser/header/provenance, asset verification, scenarios, manual rules, deterministic non-combat action resolution, command queue and phase coordinator | Reference fixtures, combat, AI snapshots, save compatibility, visual tests |
| Documentation | File/binary research, generated factual asset catalog, architecture, validation, parity matrix, roadmap and initial full-screen UI atlas/hit map | Complete sprite atlas, rules, original save map and remaining documents listed in section 4 |

The current game is a playable architectural slice, not evidence of rule parity.
Any provisional gameplay formula must be replaced or validated before its
workstream can be marked complete.

## 3. Engineering principles

1. **Simulation before presentation.** The core must run headlessly and contain
   no MonoGame types, wall-clock reads, file I/O, or ambient randomness.
2. **Commands in, events out.** Player and AI choices become validated commands;
   deterministic resolution emits events consumed by UI, audio, saves, replays,
   and tests.
3. **One explicit RNG stream.** Record the algorithm, seed, consumption order,
   and every call site. UI effects must use a separate cosmetic RNG.
4. **Original and modern behavior are separate.** Compatibility is the default.
   Fixes, widescreen, alternate controls, and balance changes are named options.
5. **No unmarked guesses.** Code based on an unverified interpretation must link
   to a research entry and carry a test or issue describing how it will be
   confirmed.
6. **Proprietary boundary stays enforceable.** CI and clean clones must compile
   and test without original art, audio, video, help, or narrative assets.
7. **Small vertical milestones.** Each milestone ends in a playable build and a
   documented parity gate, not just disconnected subsystems.

### 3.1 Manual-derived implementation checklist

The supplied 30-page/56-numbered-page manual is an image scan of 6,229,841
bytes with SHA-256
`bdb1072848df95111cd014faaa7297d016b7c6e55cd7f2658dda67a167a0089d`.
It is an authoritative source for intended rules, not proof of the shipped
binary's exact edge-case behavior. Each item below must be implemented and then
confirmed against controlled runs of the original executable.

#### Menus, setup, and views

- Recreate File commands: New Game, Open Scenario, Save, End Game, Host Game,
  Join Game, and Quit, including when each is enabled.
- Recreate Options for color depth, music, sound effects, base statistics,
  detailed combat, sliding panels, and warnings for idle gangs.
- Recreate the Comm disconnect and multiplayer-type flows.
- Implement 10 scenarios, four time limits (6 months = 26 turns, 1 year = 52,
  2 years = 104, and 4 years = 208), difficulty, player/portrait/name/color
  setup, computer/human assignment, and the begin/cancel flow.
- Recreate Game Info, City View, Sector View, 9-sector display, Overlord Bar,
  Main Control Panel, Last Turn Events, Conflict/email, Finance, Gang Upkeep,
  New Recruits, Equipment, City Officials, Sector Tax, Site Protection, Chaos
  Estimate, Cash Adjustment, Combat, Gangs, Ranking, and Done panels.
- Recreate city/sector icons for friendly, enemy, unidentified, hired-this-turn,
  target, importance, selected sector, sites, and crackdown.

#### Capacity and lifecycle rules

- A player starts with the Right Hands in a controlled sector.
- The hire panel offers three gangs; hiring pays initial cost and adds upkeep.
  Recruits are placed during the Hire phase, not immediately.
- A player may command at most 80 gangs; a sector may contain at most six of one
  player's gangs. Duplicate gang types may exist in a match even though one
  player's three current hire choices are distinct.
- Cash is updated by income minus upkeep. If projected cash is negative the
  player ends with zero, cannot equip, and may hire only zero-cost gangs.
- A gang at force zero is eliminated and loses all equipment.

#### Exact phase order from the manual

The top-level turn phases are:

1. Upkeep - collect income and pay gangs/sites.
2. Command - queue commands.
3. Execution - resolve commands for all players simultaneously by subphase.
4. Hire - place gangs bought this turn and replace hire-panel vacancies.
5. Player Elimination - remove players with neither a sector nor a gang.

Execution subphases are:

1. Instant: Bribe, Heal, Hide, Influence, Research, Snitch.
2. Combat: Attack.
3. Transaction: Equip, Give, Sell.
4. Chaos: Chaos.
5. Movement: Move, Terminate.
6. Control: Control.

The binary investigation must establish ordering within each subphase, how
simultaneous conflicts are broken, when queued repeat commands are cleared, and
the exact RNG consumption order.

#### Manual-defined commands and formulas to verify

- Dice are six-sided; unless stated otherwise, rolls of 4-6 are successes.
- Heal rolls `4d6 + Heal skill`; every success restores one force, capped at 10.
- Research rolls dice equal to gang Force + Research; successes reduce the
  item's remaining research number. Tech level and Science Center/Research Lab
  caps must be tested against the table's special values.
- Influence uses the total Force + Influence of participating gangs. Successes
  reduce site resistance; a site reaching zero becomes influenced and applies
  support, cash, tolerance, and stat modifiers.
- Control is a non-dice comparison of total Force + Control, including the
  manual's site-influence contribution and contested-sector rules.
- Bribe costs $5 and raises sector tolerance by 3, capped at 40. Snitch is free
  and lowers tolerance by 3, floored at zero. At Upkeep, tolerance moves one
  point toward `17 - Income` plus the tolerance values of influenced sites.
  Site modifiers remain outside the base cap and a negative effective tolerance
  triggers a crackdown even without a Chaos command.
- Chaos uses total Force + Chaos, earns cash subject to sector income/control,
  increases chaos, and may trigger a crackdown. Exact cash and chaos increments
  require binary fixtures.
- Attack is simultaneous. Attack dice derive from Combat plus the applicable
  weapon skill; defense is Defense plus item defense. Each positive success
  difference causes one damage, capped by weapon damage. Retaliation uses a
  halved attack roll; Martial Arts exceptions must match the binary.
- Hiding compares the attackers' Detect against the defender's Stealth, with
  the manual's chance table. Multiple friendly gangs improve detection; the
  hidden gang's individual detect value is used for its own search.
- Crackdown police attack all gangs in the sector at Combat 20. Detection starts
  at 100% through Stealth 5 and falls 5 percentage points per point above 5;
  Stealth 25 is therefore undetectable according to the manual. Police Detect
  is 12. Sector ownership and influenced-site effects must be validated.
- Bare hands use Strength/Fighting as documented; melee/blade/ranged weapons use
  their corresponding skills. Martial Arts adds its base bonus only for bare
  hands and suppresses retaliation unless the opponent also qualifies.
- A gang may equip one weapon, one armor, and one miscellaneous item; gang tech
  level must meet item tech level and never increases.
- Each controlled sector grants $1 sector tax. Influenced-site cash, site
  protection, city-official chaos estimates, and projected cash adjustment must
  be reproduced.

#### Manual-defined scenario and endgame behavior

- Greed scores cash; Power scores controlled sectors; Acceptance scores support.
- Dominance combines cash, support, and controlled sectors with different
  time-limit weights: 6 months uses 1/10/30 points, 1 year uses 1/30/100,
  2 years uses 1/75/250, and 4 years uses 1/300/1000 for each cash/support/
  controlled-sector unit respectively. Verify score normalization and ties in
  the binary.
- Kill 'Em All requires sole survival.
- Big 40 ends when an Overlord first controls 40 sectors.
- Eliminate revolves around killing each player's Right Hands and has special
  neutralization behavior that must be observed precisely.
- Siege requires simultaneous control of the six designated important sectors.
- Big Man uses the four central important sectors and ends at 40 points; the
  per-turn point award needs binary confirmation.
- Armageddon ends at control of all 64 sectors and starts players with $500 and
  every item researched.
- Recreate endgame ranking, awards (Skull, Fist, Dollar Sign, Safe, Big Fat
  Chicken), and statistics (cash earned/spent, damage inflicted, casualties,
  overthrows), including ties and no-award cases.

#### Legacy multiplayer facts to document

The manual exposes WinSock/TCP-IP, WinSockX/IPX, modem, and direct serial on
Windows 95, plus AppleTalk, Communications Toolbox modem/serial, and MacTCP on
Macintosh. Host/join setup and protocol behavior must be documented from the
binary, but these transports must not be exposed by default in the recreation.

## 4. Required technical documentation

Documentation is part of the implementation and must be reviewed with the code.

### 4.1 Documents to maintain

| Document | Required content | Completion gate |
|---|---|---|
| `ORIGINAL-FILE-FORMATS.md` | Byte layouts, endianness, compression, dimensions, signatures, checksums, unknowns, evidence and confidence | Every consumed original byte is mapped or explicitly opaque |
| `ASSET-CATALOG.md` | Every source resource, output path, media type, dimensions/rate, semantic role, screen/action owner, transparency/palette rules | Extracted manifest and catalog have identical coverage |
| `ORIGINAL-INTERNALS.md` | Recovered modules, global state, turn phases, arrays, limits, object relationships, state mutation order, RNG and timing | Enough structure to explain every parity fixture |
| `GAME-RULES.md` | Exact formulas and preconditions for setup, actions, income, combat, research, police, scoring, elimination and victory | Every rule cites observation/test evidence |
| `SAVE-FORMAT.md` | Both magic variants, field offsets, valid ranges, unknown byte preservation, import/export behavior | Corpus imports successfully; safe round-trip demonstrated before export |
| `UI-ATLAS.md` | Screen inventory, PX resource mapping, sprite rectangles, fonts, palettes, hit regions, z-order, animations and transitions | Every visible original state can be rendered from the catalog |
| `AUDIO-VIDEO.md` | Sound IDs and triggers, music sequencing/looping, volume behavior, Smacker metadata and playback/transcode decision | All media has deterministic trigger/ownership rules |
| `AI-SPEC.md` | Difficulty modifiers, information available to AI, evaluation functions, action ordering, tie-breaking and RNG use | Reference scenarios reproduce observed AI choices |
| `PARITY-MATRIX.md` | Feature-by-feature original behavior, recreation behavior, evidence, confidence, fixture and status | No required feature remains unclassified |
| `ARCHITECTURE.md` | Project boundaries, dependency direction, command/event flow, persistence, rendering and extension points | New contributors can locate every responsibility unambiguously |
| `VALIDATION.md` | How to capture reference runs, create sanitized fixtures, compare state, render golden images and reproduce failures | Clean-room parity procedure works from a fresh checkout |
| `DECISIONS.md` | Dated decisions for compatibility deviations, security exclusions, libraries and format handling | Every intentional deviation is recorded |

### 4.2 Evidence rules

Every finding must record:

- a stable finding ID;
- the source file/version/hash or reference-game observation;
- byte offset, call site, screenshot, save-state delta, or experiment used;
- observed value versus interpretation;
- confidence: Verified, High, Medium, or Low;
- implementation and tests depending on it;
- contradictions and the next experiment, when unresolved.

“Bit-for-bit validated” is reserved for data whose canonical source hash is
known and whose decoder consumes every field at exact record boundaries. It
does not imply that the meaning or gameplay use of a decoded value is known.
Behavioral parity requires a separate black-box observation or deterministic
comparison test.

### 4.3 Original internal-structure research

Recover and document, without copying original executable code:

- top-level game modes and screen/state transitions;
- new-game configuration and scenario tables;
- the exact turn phase/state machine;
- authoritative state arrays, capacities, sentinels, and indexing conventions;
- action validation and resolution ordering;
- notification queue structure and delivery order;
- RNG algorithm, seeding, rejection/range behavior, and call order;
- AI decision phases and difficulty effects;
- UI resource lookup tables, sprite slicing, cursor modes, and hit testing;
- audio/video resource lookup and trigger points;
- persistence serialization order and version branching;
- original network message/state model, documented for history even if not
  implemented for security reasons.

Use symbol-neutral names until semantics are demonstrated. Keep disassembly
addresses/version hashes in research notes, not as dependencies in production
code.

## 5. Workstreams

### A. Evidence and validation harness

- Establish supported reference builds and record complete file inventories.
- Treat the fingerprinted original executable as a behavioral oracle. The
  executable is a research/validation dependency only: it is never copied by
  the extractor and is never required for end users to launch the recreation.
- Perform static clean-room analysis of the original binary to recover factual
  control flow, constants, lookup tables, phase order, data ownership, RNG call
  sites, AI branches, serialization, and resource IDs. Document facts and
  addresses without copying original implementation code.
- Run the original only in a controlled offline environment. Disable/avoid its
  legacy network paths, preserve the executable hash, record OS/compatibility
  settings, and never expose it to an untrusted network.
- Design one-variable black-box experiments: fixed setup, saved pre-state, one
  command or setting change, saved post-state, UI/result capture, and repeated
  runs to distinguish deterministic rules from probability.
- Map manual claims to binary evidence and observed state deltas. When manual,
  executable, and current port disagree, record all three in the parity matrix;
  compatibility follows the shipped executable unless an intentional deviation
  is approved.
- Add an oracle-run ledger containing executable/data hashes, experiment ID,
  initial state, input sequence, observed RNG-sensitive outcomes, final state,
  screenshots/media events, analyst conclusion, and confidence.
- Build a `verify` extractor command that reports missing, modified, extra, and
  already-installed resources without writing files.
- Create a reference-observation template for setup, action, RNG, state delta,
  animation, sound, and outcome.
- Add a headless scenario runner accepting seed, commands, and expected events.
- Define a sanitized fixture format that contains state/mechanical values but no
  copyrighted pixels, audio, video, or long narrative text.
- Capture golden reference saves at setup and before/after each action.
- Add binary/state diff tooling with known-field labels and unknown-byte spans.
- Add frame-capture comparison tooling with masks for nondeterministic regions.

**Exit gate:** one reference turn can be replayed headlessly and every state
difference is either matched or documented.

### B. Asset acquisition and catalog

- Make extraction transactional: stage, validate counts/hashes, then atomically
  promote a complete versioned pack.
- Add `verify`, `list`, `clean-stale`, and `--force` behavior; never overwrite a
  valid pack unnecessarily.
- Support install-root and direct `DATA` inputs consistently.
- Store extractor version, source fingerprint, per-file output hash, conversion
  parameters, and source-to-output mapping in the manifest.
- Maintain bounded PX08 RLE8/BI_RGB decoding and compare corresponding
  PX08/PX16 imagery as formats or supported source versions change.
- Confirm transparency/color-key behavior for PX16 against reference rendering;
  RGB555 channel layout is established with High-confidence paired-pixel data.
- Discover exact dimensions for every exceptional image without size guesses.
- Slice sprite sheets and record rectangles/animation sequences in a generated
  catalog, while retaining unsliced originals for audit.
- Decide Smacker handling: runtime decoder, or local lossless/visually lossless
  transcode with provenance. Do not distribute converted output.
- Inspect `CLT00002`, `DATA.Z`, and help resources; decode or label them opaque.
- Verify WAV metadata across all sounds and Ogg integrity across all tracks.
- Provide actionable errors for unsupported versions and an extension mechanism
  for additional legal releases.

**Exit gate:** every original file is fingerprinted and classified; every file
needed at runtime has a verified conversion and semantic owner.

### C. Core model and deterministic runtime

- Replace mutable prototype objects with explicit identifiers and bounded value
  types for player, gang, item, site, sector, turn, action and target.
- Separate immutable definitions from match instances.
- Define complete match state with no presentation-only values.
- Implement command validation, event records, error/result codes, and replay.
- Implement exact action queueing, repeat actions, target selection, cancellation
  and phase transitions.
- Implement a versioned deterministic PRNG only after identifying the original.
- Add invariants: ownership ranges, unique gang identity, item capacity, player
  liveness, cash bounds, valid targets, and phase legality.
- Add snapshot serialization independent of original save compatibility.

**Exit gate:** a complete match can run headlessly with deterministic hashes at
every phase boundary.

### D. New-game setup and city generation

- Implement title/intro flow and new/load/network entry points.
- Implement scenario selection and descriptions.
- Implement time limit, objective, difficulty, city/settings controls, player
  names, portraits, human/AI assignment, and starting order.
- Recreate site distribution/frequency, three sites per sector, resistance,
  tolerance, support, cash, police and special-building derivation.
- Recreate HQ placement and player starting state.
- Establish seed entry/display for reproducible testing as a modern option.

Ten objectives to implement and validate:

1. Greed - timed cash score.
2. Power - timed sector-control score.
3. Acceptance - timed support score.
4. Dominance - timed combined objective.
5. Kill 'Em All.
6. The Big 40.
7. Siege.
8. Eliminate.
9. Big Man.
10. Armageddon.

The exact rules, thresholds, tie-breaking, time-limit applicability, and score
formulas for each must be captured in `GAME-RULES.md`; names alone are not
sufficient evidence.

**Exit gate:** identical setup inputs and RNG yield the same initial city,
players, hire pools, and objective state as the reference.

### E. Turn structure, economy, and world state

- Recover phase ordering: turn start, income/upkeep, hiring, command entry,
  simultaneous/sequential execution, notifications, elimination, scoring, and
  next-player/round transition.
- Implement income from sectors, sites, chaos and special gang/item modifiers.
- Implement upkeep, insufficient-funds behavior, desertion, negative cash/overflow,
  spending and earnings statistics.
- Implement site influence/support, control, tolerance/progress decay, ownership
  loss, sector takeover and special site effects.
- Implement police presence, crackdown probability/effects/history and bribery.
- Implement fog/information visibility if the reference restricts knowledge.
- Implement notification generation, queue capacity, ordering and UI dismissal.

**Exit gate:** economy and sector state match reference golden saves across at
least 20 turns, including insufficient cash and crackdown edge cases.

### F. Gang lifecycle and commands

- Implement three-choice hire pools, refresh timing, target sector, capacity,
  eligibility, force/cost semantics, and maximum gangs.
- Implement gang instance state: player, type, position, force/health, equipment,
  queued/repeat action, targets, flags and effective statistics.
- Implement movement legality, adjacency/path rules, occupancy and arrival order.
- Implement every command and its validation/cost/target UI:
  Attack, Bribe, Chaos, Control, Equip, Give, Heal, Hide, Influence, Move,
  Research, Sell, Snitch and Terminate.
- Recover action initiative/order, opposed checks, random rolls, modifiers,
  critical outcomes, partial progress, failure, interruption and retaliation.
- Implement casualties, force loss, healing, hiding/detection, gang death,
  dismissal/desertion and ownership transfer consequences.
- Implement repeated actions and exact behavior when a target becomes invalid.

**Exit gate:** every command has reference-derived success, failure, cancellation
and edge-case fixtures with matching state deltas and notifications.

### G. Items, equipment, and research

- Validate item categories, tech requirements, research difficulty, cost, stat
  modifiers, attack/hit animation IDs and sound IDs.
- Implement per-player research progress, research site bonuses, gang research
  contribution, item unlock, notification and duplicate-completion behavior.
- Implement inventory ownership/capacity and acquisition rules.
- Implement mutually exclusive equipment slots: weapon, armor and tool/misc.
- Implement equip, give, sell and loss-on-gang-removal behavior.
- Implement factory discounts and science/research tech caps exactly.
- Map all weapons to combat animations, impacts, audio and damage behavior.

**Exit gate:** the complete technology tree and every item can be acquired,
transferred, equipped, used and sold with reference-matching values.

### H. Combat and other opposed systems

- Determine how combat, defense, strength, blade, range, fighting and martial
  arts combine by range/action/equipment.
- Determine how stealth/detect, chaos/control, heal, influence and research are
  aggregated from gang, item and site modifiers.
- Implement target selection when several friendly/enemy gangs share a sector.
- Implement weapon range, attack/hit animation sequencing, sound timing, damage,
  casualties, retreat/removal and result messages.
- Test clamping, negative modifiers, zero force, maximum stats and ties.

**Exit gate:** a matrix of representative stat/equipment matchups reproduces the
reference result distribution and deterministic seeded outcomes.

### I. AI

- Determine exactly what state the AI can observe and whether it receives hidden
  information or resource bonuses.
- Recover difficulty-specific evaluation weights and modifiers.
- Implement hiring, economy, research, equipping, movement, targeting, action
  selection, threat response, objective strategy and end-turn decisions.
- Preserve action ordering, tie-breaking and RNG consumption.
- Build fixed-state AI decision snapshots and long-run statistical comparisons.
- Add turn-time budgets only as an optional modern behavior that does not alter
  deterministic compatibility mode.

**Exit gate:** AI decisions match reference fixtures and complete large automated
tournaments without invalid state, stalls, or nondeterminism.

### J. User interface and input

Functional UI is delivered incrementally with the gameplay milestones: the
shell and city workflow begin in M1, command and management screens land through
M2-M5, and every scenario must be locally operable at the M5 gate. M7 is the
separate visual-parity pass for exact sprites, animation, audio, interaction
polish, and golden screenshots; it is not the first appearance of a usable UI.

- Inventory every screen: logos/intro, title, menus, setup, scenario panel, game
  info, city view, sector view, gangs, sites, items/research, notifications,
  score/victory, save/load, preferences, help and multiplayer screens.
- Recreate original virtual resolution, layering, palettes, typography, cursors,
  tooltips, modal behavior, focus order, hit boxes and transitions.
- Map every PX sheet and sub-rectangle in `UI-ATLAS.md`.
- Implement mouse-first original interaction including drag-and-drop hiring,
  selection, targeting, buttons and right-click/cancel semantics.
- Add complete keyboard navigation and configurable bindings.
- Add integer scaling, fullscreen/windowed modes, letterboxing and optional
  widescreen without disturbing compatibility coordinates.
- Add readable modern scaling/accessibility options outside compatibility mode.
- Ensure all visible game text comes from the correct bundled or extracted source.

**Exit gate:** all original workflows are operable using mouse and keyboard;
golden screenshots pass at native resolution for every major screen/state.

### K. Audio and video

- Map all sound IDs to commands, UI events, attacks, hits and notifications.
- Implement effect channel policy, interruption, overlap, volume and mute.
- Recreate music track selection, order, looping, transitions and preferences.
- Implement intro/logo Smacker playback or documented extractor-side conversion,
  with skip behavior and graceful failure when optional video is absent.
- Separate simulation timing from audiovisual playback so skipped/disabled media
  never changes results.

**Exit gate:** media trigger traces match reference observations and all volume,
mute, loop and skip paths are stable.

### L. Persistence and compatibility

- Obtain representative saves for both known magic values.
- Complete byte maps for all structures and unknown regions.
- Implement defensive import with version, size, magic, range and checksum checks.
- Preserve unknown bytes in an import/export sidecar model.
- Compare imported state with screenshots and continued reference play.
- Implement native recreation saves with schema versioning and migration.
- Enable original-format export only after no-op and edited round-trip tests prove
  that known and unknown data are preserved safely.
- Add autosave, atomic writes, backups and corruption recovery.
- Add replay files as seed + initial configuration + ordered commands. A version
  1 authoritative-operation replay with an initial snapshot is implemented;
  replace the snapshot with compact setup inputs after exact city generation is recovered.

**Exit gate:** the original corpus imports exactly; native saves migrate; any
enabled original export passes byte-aware round-trip validation.

### M. Multiplayer

- Complete hot-seat support first, including hidden hand-off screens if needed.
- Document the original Comm menu, transport, messages, synchronization,
  disconnect and resync behavior.
- Do not expose the original network stack to untrusted networks.
- Design a modern authenticated, versioned protocol transporting commands and
  periodic state hashes rather than arbitrary serialized objects.
- Add lobby, readiness, player assignment, reconnect, desync diagnostics and
  deterministic replay recovery.
- Threat-model malformed peers and impose strict bounds/timeouts.

**Exit gate:** hot-seat parity is complete; modern networking passes deterministic
multi-process and adversarial protocol tests. Legacy interoperability, if any,
has a separate security review and explicit opt-in.

### N. Platform, packaging, and quality

- Add CI for restore, build, test, formatting and clean-clone assetless startup.
- Test Windows x64 first, then Linux x64 and macOS arm64/x64.
- Package framework-dependent and self-contained builds without proprietary data.
- Add first-run asset selection, validation progress, actionable errors and an
  in-game asset-pack status screen.
- Add structured logs, crash reports stored locally, and privacy-respecting
  diagnostics export.
- Add unit, property, fuzz, integration, replay, soak, performance and visual
  regression suites.
- Fuzz every binary parser and enforce allocation/file-size limits.
- Benchmark turn resolution, AI, loading, extraction and rendering.
- Add licenses/notices for dependencies and a clear original-asset policy.
- Establish release versioning, changelog, reproducible build instructions and
  signed release artifacts.

**Exit gate:** release candidates install and complete games on all supported
platforms from clean machines using only user-supplied legal assets.

## 6. Milestone order and dependency gates

### Current milestone status (2026-09-08)

No milestone gate has passed yet. Work completed ahead of a dependency gate is
recorded as foundation work, not as completion of that milestone.

Local reference-oracle availability: the supported GOG installation includes
`C:\GOG Games\Chaos Overlords\Chaos Overlords.exe`, version 1.1, 664,576 bytes,
with SHA-256
`a1430159bbe20869e277a5000311344f4ec141ab77c96b385336617149e97d89`.
This path is machine-local research metadata: the executable remains outside
the repository, is never copied by the extractor or release, and is not an
end-user runtime dependency. If this exact binary becomes unavailable, binary-
dependent parity work must be reported as blocked rather than replaced by an
unmarked guess.

| Milestone | Status | Implemented foundation | Work required before gate |
|---|---|---|---|
| M0 | In progress | Research templates; file/binary logs; architecture, validation and parity documents; source/output verification; rollback-safe staged installation; sanitized reference-fixture schema; labeled JSON state-diff tool; versioned 685-output manifest and generated factual asset catalog | One captured, fully documented reference observation |
| M1 | Foundation started | Ten scenario definitions and durations; recovered 32x32 density-derived 8x8 city, balanced three-site rejection sampling, explicit sector income/tolerance, fixed-candidate HQ permutation, Right Hands Force 10, Armageddon exclusions/overrides; title/setup/city router with scenario/duration/local-player controls, virtual-coordinate mouse input, and mapped PX00130/PX00143/PX00128 frames | Original seed/setup-mode fixture, full player/difficulty setup, remaining resource atlas/hit maps and golden screens |
| M2 | Foundation started | Centralized structural limits; explicit headless setup/player/sector/site/gang/research/inventory/hire/statistics schema; stable IDs; phase coordinator; common typed validation-rule pipeline; deferred hire purchase/placement, one-offer-per-turn snubbing, binary-derived 1–89 rejection refill and 5–9 initial Force; player elimination; declarative validation for all action target shapes; typed queue mutations and ordered events; bounded notification queues; recovered RNG; canonical phase hashes | Runtime offer/Force fixtures, within-phase ordering, and remaining reference-derived edge rules |
| M3 | Foundation started | Ordered base Upkeep economy; all Instant and Transaction actions; Move/Terminate; grouped Influence and Control with ownership/site reset, zero-margin chance and unique-highest neutral conflicts; per-turn grouped Chaos income/reset; full manual-backed Crackdown duration, extension, history, neutralization and lockout; local influenced-site stat projection; deterministic Research with gang and base-5/Science-8/Lab-10 Tech caps; local influenced Factory 30% equipment discount | Binary Chaos/police call-order fixtures, equal/owned-sector cross-player Influence/Control conflicts, remaining special-building behavior, Factory locality/rounding and acquisition/swap fixtures, and M2 gate |
| M4 | Foundation started | Recovered raw RNG/range algorithms; serializable state; effective item/site stats; phase-wide Attack/retaliation and Crackdown police snapshots; normal and hidden police detection; Combat-20-minus-Defense attacks; elimination and recorded rolls | Reveal timing, RNG seed/call-order validation, binary combat/Crackdown fixtures, animation/audio mapping, and M2-M3 gates |
| M5 | Foundation started | Scenario predicates, timed scores/standings with ties, live end-of-turn evaluation, explicit Siege-important sector state, Big Man accrual, Eliminate cleanup, state-derived winners, all five award projections, outcome events/notifications and hashing; recreation-native v4 snapshots with v1-v3 migration and v2 authoritative-operation replays plus client save/replay flows; equipment Give recipient workflow | Siege setup/visual mapping, objective ranking/tie fixtures, checked-in persistence fixtures, remaining management UI deliverable and M4 gate |
| M6 | Foundation started | Deterministic non-mutating objective-aware command planner, cash budgeting, validated hire choice, per-player Human/Computer setup and replay-recorded client driver; timed scenarios complete and objective scenarios run 20-turn deterministic replay-verified two-AI tournaments | Original difficulty branches/weights and visibility; reference decision snapshots; larger-player and objective-completion tournaments; M5 gate |
| M7 | Foundation started | Original city ownership layers and site/gang portraits are rendered; resolved equipped attacks route item-defined original weapon sounds | Complete atlas/event integration, animations, remaining audio/music/video, golden screens and M1-M6 dependencies |
| M8 | Not started | Windows local launcher and legal-copy extraction workflow only | Compatibility, CI, packaging and release gate |
| M9 | Not started | None | Frozen deterministic simulation after M8 |

Current automated baseline: the solution builds successfully, 286 tests
pass, and the inspected legal-copy output contains 685 size/SHA-256-verified
outputs from 471 original resources. This is implementation coverage, not
original-game behavioral parity.

### M0 - Research foundation

Deliver documentation templates, asset catalog generator, verification command,
reference fixture format, state diff tool and parity matrix skeleton.

Depends on: current baseline.
Gate: one fully documented reference observation with reproducible evidence.

### M1 - Faithful city setup and UI shell

Deliver all setup options, ten scenario definitions, exact deterministic city
generation, player/HQ initialization, screen router, mouse input and resource
atlas foundations.

Depends on: M0, asset dimension/palette resolution.
Gate: initial state and setup screens match reference fixtures.

### M2 - Complete command model

Deliver exact phases, gang lifecycle, hire pool, command queue and validation for
all actions, initially with textual resolution events.

Depends on: M1, save/state diff fixtures.
Gate: every legal/illegal command path is represented and deterministic.

### M3 - Economy, sites, research, and equipment

Deliver verified economy, site influence/control/chaos, special buildings,
police, research tree, inventory and equipment transfer/sale.

Depends on: M2.
Gate: non-combat golden scenarios match through multi-turn resolution.

### M4 - Combat and full turn resolution

Deliver attack/snitch/bribe/hide/detect/heal formulas, casualties, animation
events, notifications, elimination and exact RNG consumption.

Depends on: M2-M3, RNG discovery.
Gate: complete reference turns produce matching state hashes.

### M5 - Objectives and full hot-seat game

Deliver scoring, all victory conditions, endgame/statistics, save/load and every
required management screen. Finance, live ranking, and the combined research /
equipment workflow are implemented; remaining original management workflows
and exact presentation are still required.

Depends on: M4.
Gate: all ten scenarios can be completed locally with reference-equivalent rules.

### M6 - AI parity

Deliver all difficulty levels and objective-aware AI with deterministic decision
fixtures and automated tournament coverage.

Depends on: M5.
Gate: single-player campaigns complete reliably and AI validation targets pass.

### M7 - Audiovisual parity

Deliver sprite/animation atlas, effects, music, video, original UI polish and
native-resolution visual baselines.

Current foundation: original city/site/gang imagery is routed, and resolved
equipped-weapon attacks use item-defined `SND005xx` cues. Remaining trigger,
animation, music, video, and exact visual behavior is still required.

Depends on: M1-M6 event model, complete asset catalog.
Gate: visual and media trigger comparisons pass across the entire game flow.

### M8 - Compatibility and cross-platform release

Deliver original save import, native save migration, packaging, first-run asset
UX, cross-platform CI, performance/accessibility improvements and release docs.

Depends on: M7.
Gate: release checklist passes from clean installations on all target platforms.

### M9 - Optional modern multiplayer

Deliver safe command-based networking after simulation parity is frozen.

Depends on: M8 deterministic state/replay stability.
Gate: threat model, desync recovery and multi-process tests pass.

## 7. Cross-cutting test matrix

Every subsystem must include:

- canonical happy-path fixture;
- minimum/maximum/zero/negative boundary cases where representable;
- invalid IDs, truncated input and corrupt-file cases;
- deterministic repeat and save/reload continuation tests;
- every human/AI player count and each relevant difficulty;
- state hash before and after resolution;
- notification/event order assertions;
- reference comparison and confidence update;
- proof that UI/audio options do not alter simulation state.

Release-level tests must cover all ten objectives, every action, all gangs, all
items, every site special, every major screen, both save variants, every media
family, at least 1,000 automated complete matches, and deterministic replay on
each supported OS.

## 8. Completion tracking conventions

Use these status values in `PARITY-MATRIX.md`:

- `Unknown` - behavior or data has not been investigated.
- `Documented` - evidence exists, but no production implementation.
- `Provisional` - implemented using one or more non-Verified assumptions.
- `Implemented` - code and internal tests exist.
- `Parity verified` - reference comparison passes with High/Verified evidence.
- `Intentional deviation` - approved and described in `DECISIONS.md`.
- `Not applicable` - original feature is outside scope with rationale.

A milestone is not complete while any required row is `Unknown`, `Documented`,
or `Provisional`. Percent-complete estimates should be derived from parity rows,
not lines of code or asset counts.

## 9. Immediate next implementation sequence

1. Finish M0 by capturing one reference observation with the fixture schema and
   labeled state-diff tool; transactional installation and output verification
   are complete.
2. Enrich the generated `ASSET-CATALOG.md` by resolving semantic owners,
   palette/transparency behavior and sprite rectangles.
3. Continue the headless match schema through reference-derived command costs,
   resolvers, and phase-boundary processing; notification queues, serializable
   provisional RNG state, canonical hashes, typed validation results, and ordered
   queue events are now in place.
4. Obtain original save/reference fixtures and connect them to state diffs and
   phase-boundary hashes.
5. Capture an original new-game fixture to validate the implemented city,
   HQ/Right Hands, initial-offer and RNG sequence, including seed/setup context.
6. Continue the M1 UI shell from its implemented title/setup/city router: map
   the first atlas slices, then add full player/difficulty setup and original hit maps.
7. Replace remaining provisional control and economy formulas with observed rules.
8. Implement each action vertically: validation, resolution, event, UI, media,
   documentation and reference fixture before starting the next.
9. Complete objectives/hot-seat, then AI, then audiovisual parity.
10. Finish persistence, cross-platform packaging and optional modern features.

## 10. Source hierarchy

When sources disagree, use this order and record the conflict:

1. Repeatable observation of a fingerprinted original build.
2. Byte/state changes in original save files from controlled experiments.
3. Original executable data/control-flow analysis expressed as clean-room facts.
4. The original manual supplied with the user's legal copy.
5. The local [`re-chaos`](C:/sources/re-chaos) format-research checkout,
   corresponding to upstream commit
   `3561d4122b4a7670105366b3edb16ddd74c2b932`, and its
   [upstream repository](https://github.com/wfr/re-chaos).
6. Contemporary reviews, strategy notes and community observations.

The manual describes intended behavior; the shipped executable defines
compatibility behavior. Neither undocumented assumptions nor the current
prototype implementation count as evidence.

Use `C:\sources\re-chaos` whenever its `README.md`, `structs.h`, or format tools
cover the file, save structure, image resource, or mechanical field under
investigation. Treat those materials as secondary research rather than copied
production code: cite the upstream commit and exact file/line or structure,
reimplement independently, and validate the result against this project's
fingerprinted GOG data and executable. The upstream research used executable
SHA-256 `0791e6209d573a79882675d1236737f5c9b369ea4af541a7dbd03cbadf4493d5`,
which differs from the locally available GOG executable, so a `re-chaos`
finding alone cannot establish behavioral parity for this build.
