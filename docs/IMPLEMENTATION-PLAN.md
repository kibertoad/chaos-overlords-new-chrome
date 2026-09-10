# Complete implementation and migration plan

Status: active roadmap
Last updated: 2026-09-10
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
- Original save import/export is explicitly unsupported. Recreation-native save
  and replay compatibility before 1.0.0 is useful but is not a completion gate;
  versioning and migration machinery is retained for post-1.0 compatibility.
- Simulation behavior is covered by deterministic fixtures and a parity matrix.
- Every file format, inferred field, formula, state transition, and unresolved
  discrepancy is documented with evidence and a confidence rating.
- The game builds and runs on supported Windows, Linux, and macOS targets.

### Explicit non-goal: original network code and protocols

Reimplementing or interoperating with the original WinSock/IPX, modem, serial,
AppleTalk, or MacTCP networking is explicitly out of scope. Those paths were
buggy and insecure, are not required for completion, and will not be exposed,
ported, protocol-matched, or accepted as a release dependency. The recreation
supports local single-player and hot-seat play. Networked multiplayer of any
kind is not part of this implementation plan.

## 2. Current baseline

| Area | Present now | Remaining |
|---|---|---|
| Build | .NET 10 solution, MonoGame DesktopGL 3.8.5.1, xUnit v3 4.0.0; self-contained Windows/Inno, Linux/Debian, and macOS arm64/x64/pkg installers; manual-only release and cross-platform validation workflows; pull-request/manual zizmor gate; Windows directory deployment with native-platform smoke test, visible/retryable legal-asset import, and startup-error reporting; green cross-platform installer run `34403047147` and zizmor run `34403047115` | Signing/notarization and native interactive play tests |
| Original data | Embedded 22 sites, 90 gangs, and 64 items with pinned provenance | Semantic/formula validation, versioned generation tool |
| Extraction | Transactional/versioned full-pack SHA-256 validation; 215 repaired RGB555 PX16 images, 214 retained plus decoded PX08 resources, 28 WAVs, 8 Ogg tracks, 2 Smacker videos, help and opaque files; generated 685-output factual catalog | Transparency/color-key validation, video strategy, semantic role/owner resolution |
| Simulation | Deterministic city seed, six players, stable gang IDs, typed command queue, headless phase coordinator, Upkeep, all 14 command resolvers, simultaneous gang/Crackdown combat, 3–5-turn police duration/extension and three-in-five control loss, hidden attack/visibility checks, and local influenced-site stats | Crackdown notification/timing fixtures, special buildings, original RNG seeding/order and exact parity formulas |
| Client | Scaled 640x460 routed setup/handoff/city/sector/gang/finance/ranking/items/Give/combat-summary/search/commands/hire/events/endgame UI backed by authoritative `MatchState`; distinct whole-city and detailed-sector projections composite original ownership tiles, the latter as a clickable 3x3 neighborhood beside all three building portraits; normal planning turns auto-resolve internal phases while debug mode can step them; persistent original-art Hire dock supports portrait drag/drop from city or sector detail, split price/reject footers and `HIRED` stamping; `PX05010` automatically pages queued turn reports after handoff; `PX05012` presents paged combat results; recovered item-selected eight-frame attack/hit, retaliation, evasion and police animation playback; recovered setup; local controls; private handoff; Core-derived commands, projections, visibility, combat results, research/equipment transfer and notifications; mouse/keyboard, saves/replays | Full setup detail, remaining sprites/atlas and management panels/hit maps, remaining sound/music/video, accessibility |
| Tests | 667 tests covering parsers/provenance, extraction, scenarios, deterministic command and phase resolution, difficulty bands, AI strategic/planning state and complete action tuples, family dispatch and live boundary branches, sector selection and hire placement, original city/setup vectors, hire schedules/limits/ranking/equipment, combat, saves, replays, installers, and repository policy | Original-reference fixtures, visual tests, larger-player AI stress, and native interactive installer/play tests |
| Documentation | File/binary research, generated factual asset catalog, architecture, validation, parity matrix, roadmap and initial full-screen UI atlas/hit map | Complete sprite atlas, rules and remaining documents listed in section 4 |

The current game is a playable architectural slice, not evidence of rule parity.
Any provisional gameplay formula must be replaced or validated before its
workstream can be marked complete.

### Handover checkpoint - 2026-09-10

- The current implementation baseline includes the recovered sector Combat +
  Defense advantage hostility pass, exact isolated scenario/strategy family
  dispatch, exact hire-offer ranking/rejection, the isolated modes 1-5 sector
  selection kernels, live recovered hire-placement rules, and 667 passing tests. Native saves are
  v13, replay is v14, and the canonical hash is v16.
- The latest completed setup checkpoint is commit `10a3a9d`: fixed Greed and
  Armageddon city/HQ/RNG vectors now guard the statically recovered generator,
  stale frequency/class claims were removed, and exact uppercase `SMGFUNDAGE`
  now overrides ordinary or Armageddon starting cash with $1,500.
- Original local Begin now converts every omitted player slot to a computer,
  selecting a unique portrait 0..14 and its resource name in ascending slot
  order before AI initialization and city generation. Every fresh local match
  therefore enters with six participants. Exact uppercase `SMGISLANDS` applies
  Chaos 100 only to sectors still neutral after all six HQs are owned.
- The latest completed AI work implements exact fixed-six-player reaction and
  directional-attitude initialization, non-Homicidal recovery, combat/Control
  attitude changes, hostility-filtered attack candidates, and all nine known
  consumers of the recovered 0/1/2 resolution band. The scalar outer planner
  remains provisional and must not be presented as original-AI parity.
- The family-11 Equip selector, exact retaliation-eligibility compound
  operands, and modes 10 and 16 are now bounded. The latter are the family-11
  formation anchor/follower Move paths and include a local Attack gate; mode 6
  and the downstream Control use of `FUN_0040a1a7`'s pair flag are also
  bounded.
- Static kernels guard family-1's cash 50/51, Force 8/9, effective-Heal -3/-4,
  and Tolerance 3/4 terminal boundaries. The exact previous-None/Chaos
  Heal/Move/Chaos branch and previous-Heal Heal/Control/Move branch are wired
  into the live planner, including active-Crackdown selector `0x2a`, strict
  solo-Control selector `0x2c`, and exact mode-5 destinations with replayed tie
  RNG, all-zero post-filter fallback, and six-gang routing capacity. Capture
  controlled original decisions for the remaining selector contexts,
  then follow with larger-player and objective-completion AI stress traces.
- Static-analysis conclusions, addresses, confidence, and rejected hypotheses
  belong in `ORIGINAL-INTERNALS.md`; player-visible intended behavior belongs in
  `AI-SPEC.md`; implementation status and the next proof gate belong in
  `PARITY-MATRIX.md`. Never commit proprietary assets or decompiled original
  source text.
- Original networking remains a final, explicit non-goal. Do not analyze or
  recreate its transports, protocols, interoperability, or production paths.

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
8. **Bounded source modules.** Every compiled C# file is limited to 1,000 lines
   by `Directory.Build.targets`; exceeding the ceiling fails local builds, tests,
   CI, packaging, and releases.

### 3.1 Manual-derived implementation checklist

The supplied 30-page/56-numbered-page manual is an image scan of 6,229,841
bytes with SHA-256
`bdb1072848df95111cd014faaa7297d016b7c6e55cd7f2658dda67a167a0089d`.
It is an authoritative source for intended rules, not proof of the shipped
binary's exact edge-case behavior. Each item below must be implemented and then
confirmed against controlled runs of the original executable.

#### Menus, setup, and views

- Recreate File commands used by local play: New Game, Open Scenario, Save, End
  Game, and Quit, including when each is enabled. Host/Join are intentionally
  unsupported legacy-network entries.
- Recreate Options for color depth, music, sound effects, base statistics,
  detailed combat, sliding panels, and warnings for idle gangs.
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

#### Legacy multiplayer exclusion

The manual's WinSock/TCP-IP, WinSockX/IPX, modem, direct serial, AppleTalk,
Communications Toolbox, and MacTCP paths are historical context only. Their
implementation and protocol behavior are deliberately excluded from the
research and recreation scope.

## 4. Required technical documentation

Documentation is part of the implementation and must be reviewed with the code.

### 4.1 Documents to maintain

| Document | Required content | Completion gate |
|---|---|---|
| `ORIGINAL-FILE-FORMATS.md` | Byte layouts, endianness, compression, dimensions, signatures, checksums, unknowns, evidence and confidence | Every consumed original byte is mapped or explicitly opaque |
| `ASSET-CATALOG.md` | Every source resource, output path, media type, dimensions/rate, semantic role, screen/action owner, transparency/palette rules | Extracted manifest and catalog have identical coverage |
| `ORIGINAL-INTERNALS.md` | Recovered modules, global state, turn phases, arrays, limits, object relationships, state mutation order, RNG and timing | Enough structure to explain every parity fixture |
| `GAME-RULES.md` | Exact formulas and preconditions for setup, actions, income, combat, research, police, scoring, elimination and victory | Every rule cites observation/test evidence |
| `NATIVE-SAVE-FORMAT.md` | Current native snapshot/replay schemas, bounds, hashing, migration machinery, and compatibility policy | Current formats round-trip safely and the post-1.0 migration/rejection policy is enforced |
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

Use symbol-neutral names until semantics are demonstrated. Keep disassembly
addresses/version hashes in research notes, not as dependencies in production
code.

### 4.4 Full accuracy audit

Before any parity milestone can pass, perform a systematic static-analysis and
reference-observation audit of the fingerprinted GOG executable against the
recreation. This is not a one-off visual review: every result must be captured
under the evidence rules in section 4.2 and linked to the affected production
code, parity row, and regression fixture.

The audit must cover:

- screen geometry, hit regions, draw order, typography, colors, and resource
  selection for every non-network screen;
- setup, economy, command validation, phase ordering, combat, research,
  equipment, objectives, elimination, scoring, and persistence behavior;
- animation frame selection, frame durations, pauses, damage blinking, sound
  trigger points, interruption, and audiovisual sequencing;
- RNG seeding, range reduction, call order, rejection behavior, and tie breaks;
- AI information boundaries, difficulty branches, evaluation weights, action
  ordering, target choice, and end-turn behavior;
- all discrepancies between the current implementation, manual, controlled
  observations, saves, and binary-derived facts.

Use clean-room factual findings only; do not copy original implementation code.
Original WinSock/IPX, modem, serial, AppleTalk, MacTCP, or other networking code
and protocols are excluded from both this audit and the product as an explicit
non-goal. The audit is complete only when every in-scope parity-matrix row has a
reproducible comparison result and no unexplained discrepancy remains.

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
  in-scope gameplay paths, preserve the executable hash, and record
  OS/compatibility settings. Do not exercise legacy network paths.
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

- Implement title/intro flow and local new/load entry points.
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
  score/victory, save/load, preferences, and help. Legacy networking screens
  are intentionally excluded.
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

- Implement native recreation saves with schema versioning and migration.
- Add autosave, atomic writes, backups and corruption recovery.
- Add replay files as seed + initial configuration + ordered commands. A version
  1 authoritative-operation replay with an initial snapshot is implemented;
  replace the snapshot with compact setup inputs after exact city generation is recovered.
- Treat native saves and replays as changeable development formats before 1.0.0;
  retaining compatibility between those versions is not a release gate.
- Retain version discriminators, bounded readers, legacy hash selection, and
  migration structure so incompatible post-1.0 changes can migrate
  deterministically or reject safely. Original save import/export is excluded.

**Exit gate:** current native saves and replays round-trip deterministically,
corrupt or incompatible input fails safely, and the post-1.0 compatibility
policy is documented and testable.

### M. Hot-seat play

- Complete hot-seat support first, including hidden hand-off screens if needed.
- Do not port, analyze for interoperability, or expose the original network
  stack or protocols.

**Exit gate:** local hot-seat parity is complete. Networked multiplayer remains
an explicit non-goal.

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
| M1 | Foundation started | Ten scenario definitions and durations; recovered ascending empty-slot Computer completion with unique portrait/name RNG; 32x32 density-derived 8x8 city, balanced three-site rejection sampling, explicit sector income/tolerance, fixed-candidate HQ permutation, Right Hands Force 10, Armageddon exclusions/overrides, exact `SMGFUNDAGE` and `SMGISLANDS`; title/setup/city router with scenario/duration/local-player controls, original overlord portrait selection, global AI Mentality panel, virtual-coordinate mouse input, and mapped PX00130/PX00143/PX00128 frames | Original seed/setup fixture; remaining setup atlas/hit maps and golden screens |
| M2 | Foundation started | Centralized structural limits; explicit headless setup/player/sector/site/gang/research/inventory/hire/statistics schema; stable IDs; phase coordinator; common typed validation-rule pipeline; deferred hire purchase/placement, one-offer-per-turn snubbing, binary-derived 1–89 rejection refill and 5–9 initial Force; player elimination; declarative validation for all action target shapes; typed queue mutations and ordered events; bounded notification queues; recovered RNG; canonical phase hashes | Runtime offer/Force fixtures, within-phase ordering, and remaining reference-derived edge rules |
| M3 | Foundation started | Ordered base Upkeep economy; all Instant and Transaction actions; Move/Terminate; grouped Influence and Control; per-turn Chaos; Crackdown lifecycle; influenced-site stats; Research caps; Factory discount; recovered 0/1/2 difficulty pools and thresholds for Heal, Influence, Research, Chaos, plus high-band owned-sector Crackdown reduction | Binary police/call-order fixtures, Control conflict edges, remaining special buildings, Factory acquisition/swap fixtures, and M2 gate |
| M4 | Foundation started | Recovered raw RNG/range algorithms; serializable state; effective item/site stats; phase-wide Attack/retaliation and police snapshots; recovered difficulty-specific d20 hidden detection, band-0 Defense reduction, 6+/5+/4+ main rolls with quarter-pool damage floor, exact Hide/weapon/Martial-Arts retaliation eligibility, 5+/5+/4+ halved retaliation, elimination and recorded rolls | Resolve reveal timing, reference combat/Crackdown fixtures, animation/audio mapping, and M2-M3 gates |
| M5 | Foundation started | Scenario predicates, timed scores/standings with ties, live end-of-turn evaluation, explicit Siege-important sector state, Big Man accrual, Eliminate cleanup, state-derived winners, all five award projections, outcome events/notifications and hashing; recreation-native v13 snapshots with reusable v1-v12 migration machinery and v14 authoritative-operation replays plus client save/replay flows; equipment Give recipient workflow | Siege setup/visual mapping, objective ranking/tie fixtures, current-format persistence safety, remaining management UI deliverable and M4 gate |
| M6 | Foundation started | Deterministic objective-aware planner and replay driver; recovered outer planner, all family dispatch values, exact ten-scenario by seven-hire-role dispatch table, exact base turn schedules and three-offer role rankings, complete three-generation action tuples, selector inventory and strategic routing including mode-6 leader/hostility movement, pair-flag Control, and family-11 equipment/cooldown, local Attack, mode-10 anchor and mode-16 follower paths; the family table, every objective-specific hire-role adjustment, computed gang limit, hire-attempt gate, live post-command role update, exact live hire selector and failure-path snub, fixed three-slot offer tombstones/refill, ascending inactive roster-slot reuse/reset, exact family-11 weapon choice, first-planning lifecycle, duplicate Chaos/Influence cleanup, and live persistent-anchor placement refresh with ordinary, Big Man, visible-hostile, and Siege paths are implemented; verified current/previous hire roles, first-plan flags, action/target tuples, and six-by-81 family slots persist in authoritative state, with exact role rollover and active-gang family assignment during planning preparation; exact pre-city six-player reactions, directional attitudes, non-Homicidal recovery, combat/Control and sector Combat + Defense advantage hostility, hostility-aware targets, hashes/saves/replays; exact 0/1/2 per-computer resolution calibration implemented for all nine consumers | Capture reference boundaries and larger-player/objective-completion stress cases; M5 gate |
| M7 | Foundation started | Original city ownership layers and site/gang portraits are rendered; resolved equipped attacks route item-defined original weapon sounds | Complete atlas/event integration, animations, remaining audio/music/video, golden screens and M1-M6 dependencies |
| M8 | Foundation started | Windows local launcher; legal-copy extraction; self-contained Windows package and GOG-aware Inno installer with an always-visible New Chrome destination page, separate original-asset source page, visible import stages, retryable source selection, nonzero failure exit and runtime error dialog; Linux amd64 `.deb`; macOS arm64/x64 application-bundle `.pkg`; manually dispatched validation and selectable Windows-only (default) or all-platform GitHub Release workflow; pull-request/manual zizmor gate; clean-room run `34403047147` passed Windows, Linux, both macOS architectures, and all installer jobs; zizmor run `34403047115` passed | Signing/notarization, native interactive tests, accuracy audit, compatibility and full release gate |

Current automated baseline: the solution builds successfully, 667 tests
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

Deliver current native save/replay safety, the post-1.0 migration policy,
packaging, first-run asset UX, cross-platform CI, performance/accessibility
improvements and release docs. Original save import/export remains excluded.

Depends on: M7.
Gate: release checklist passes from clean installations on all target platforms.

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

1. Capture controlled original-turn fixtures for the now-guarded family-1 cash
   50/51, Force 8/9, effective-Heal -3/-4, and Tolerance 3/4 boundaries, plus
   fixed band 0/1/2 retaliation Hide and Martial Arts branches.
2. Capture persistent-anchor hire placement plus the family-11
   weapon-replacement cooldown and mode-10/mode-16 formation behavior across
   consecutive original turns.
3. Replace provisional AI scoring only where handler-exact evidence or fixed
   reference decisions support it; expand deterministic tournaments to larger
   player counts and objective-completion stress cases.
4. Capture an original new-game fixture to validate city generation,
   HQ/Right Hands placement, hire offers, initial Force, seeding, and complete
   setup RNG order.
5. Close the remaining economy, police, special-building, objective, and
   current-format persistence safety gates with binary/reference fixtures.
6. Complete setup alignment, remaining management hit maps, atlas semantics,
   transparency/color keys, combat cadence, audio/music/video triggers, and
   native-resolution golden screens.
7. Run the full section 4.4 accuracy audit across layouts, rules, animations,
   AI, RNG, media, and persistence; resolve or explicitly classify every
   in-scope discrepancy. Original networking remains excluded.
8. Finish signing/notarization, native interactive Windows/Linux/macOS
   validation, accessibility/performance work, and the complete release gate.

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
