# Documentation index

Status: maintained index
Last updated: 2026-09-20

This directory holds the technical documentation of *Chaos Overlords: New
Chrome*. This page is the entry point: it says what each document is for, which
one to open for a given task, and where a given game subsystem is documented
across the rule log, the executable research log, the parity matrix, and the
design documents. Player-facing material (installation, controls, project
status, acknowledgements) is in the repository [README](../README.md); agent
and contributor working rules are in [AGENTS.md](../AGENTS.md).

<!-- doc-index:begin toc depth=2 -->
- [Start here](#start-here)
- [Document catalog](#document-catalog)
- [Topic index](#topic-index)
- [Identifiers and conventions](#identifiers-and-conventions)
- [Canonical identities](#canonical-identities)
- [Maintaining this directory](#maintaining-this-directory)
<!-- doc-index:end -->

## Start here

| If you want to… | Open |
|---|---|
| Build, run, and test from source | [DEVELOPMENT.md](DEVELOPMENT.md), then [VALIDATION.md](VALIDATION.md) for the fast gate, the long-running tier, and fixture classes |
| Resume development at the current checkpoint | [HANDOVER.md](HANDOVER.md) for repository state and the latest work by area, then [IMPLEMENTATION-PLAN.md](IMPLEMENTATION-PLAN.md) for the roadmap |
| Know how faithful a system is and what proves it | [PARITY-MATRIX.md](PARITY-MATRIX.md); status values are defined in [the plan's completion conventions](IMPLEMENTATION-PLAN.md#8-completion-tracking-conventions) |
| Check whether a deviation from the original is deliberate | [DECISIONS.md](DECISIONS.md) |
| Look up an exact game rule | [GAME-RULES.md](GAME-RULES.md) by `RULE-*` ID, via its [rule index](GAME-RULES.md#rule-index) or the [topic index](#topic-index) below |
| Look up what the original executable does | [ORIGINAL-INTERNALS.md](ORIGINAL-INTERNALS.md) by `BIN-*` ID, via its [finding index](ORIGINAL-INTERNALS.md#finding-index) or the [topic index](#topic-index) below |
| Understand how the computer players decide | [AI-SPEC.md](AI-SPEC.md), then the `BIN-AI-*` findings and the [per-family parity notes](PARITY-MATRIX.md#computer-player-row-details) |
| Find your way around the code | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Work on original file formats or the asset pack | [ORIGINAL-FILE-FORMATS.md](ORIGINAL-FILE-FORMATS.md), [ASSET-CATALOG.md](ASSET-CATALOG.md), [UI-ATLAS.md](UI-ATLAS.md), [AUDIO-VIDEO.md](AUDIO-VIDEO.md) |
| Set up or run clean-room binary analysis | [GHIDRA.md](GHIDRA.md) for the toolchain, [REFERENCE-CAPTURE.md](REFERENCE-CAPTURE.md) for runtime captures, [the static research protocol](VALIDATION.md#static-binary-research-protocol) for what a finding must record |
| Change saves, replays, or the canonical hash | [NATIVE-SAVE-FORMAT.md](NATIVE-SAVE-FORMAT.md) |
| Host, operate, or extend online play | [MULTIPLAYER.md](MULTIPLAYER.md) for the design, [multiplayer/README.md](../multiplayer/README.md) for operating a server, [src/Rechaos.Multiplayer/README.md](../src/Rechaos.Multiplayer/README.md) for the game client; version rules are in [AGENTS.md](../AGENTS.md#multiplayer-protocol-version) |
| Cut or sign a release | [RELEASING.md](RELEASING.md) |

## Document catalog

Every document opens with a `Status:` line saying how settled it is and a
`Last updated:` date, except the generated asset catalog and the procedure
guides, which are current by construction or change rarely.

### Orientation, process, and status

| Document | What it holds | Kind |
|---|---|---|
| [DEVELOPMENT.md](DEVELOPMENT.md) | Running from source, extractor commands (verify, catalog, regenerate bundled tables), the fast validation gate, project list | Guide |
| [HANDOVER.md](HANDOVER.md) | Repository state, current format versions, the latest playable work grouped by area, the reference environment, and the next evidence batches | Living checkpoint |
| [IMPLEMENTATION-PLAN.md](IMPLEMENTATION-PLAN.md) | Definition of complete, baseline, engineering principles and the manual-derived checklist, documentation requirements and evidence rules, workstreams A–N, milestones M0–M8, test matrix, status conventions, source hierarchy | Roadmap |
| [PARITY-MATRIX.md](PARITY-MATRIX.md) | One row per original feature: requirement, recreation status, evidence and confidence, next parity gate; grouped by area, with per-family AI notes, current blockers, and binary evidence status | Status matrix |
| [DECISIONS.md](DECISIONS.md) | Dated product, compatibility, and scope decisions, newest first, with an index | Decision log |
| [VALIDATION.md](VALIDATION.md) | The four validation layers, `Invoke-Validation.ps1` modes, canonical identities, the original-binary oracle and static research protocols, fixture classes, parity record fields, failure triage | Procedure |
| [RELEASING.md](RELEASING.md) | `version.txt`, local package builds, the GitHub release workflow, Windows Authenticode and Linux OpenPGP signing, why macOS is unsigned, continuous integration | Procedure |

### Recreation design and formats

| Document | What it holds | Kind |
|---|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Dependency direction, the four projects and their responsibilities, command/event flow, the original turn model as implemented, state ownership, determinism and proprietary-content boundaries, online play, error and security model, known debt | Design |
| [GAME-RULES.md](GAME-RULES.md) | `RULE-*` entries: manual statement, verified executable behavior, interpretation, confidence, and implementing types for hiring, instant commands, combat, equipment, movement and control, upkeep, timers, objectives, and awards | Rule log |
| [AI-SPEC.md](AI-SPEC.md) | Original versus Advanced policy architecture, planner inputs and invariants, the current policy, and the parity work still required | Specification |
| [NATIVE-SAVE-FORMAT.md](NATIVE-SAVE-FORMAT.md) | Recreation save container and limits, the current save document, the compatibility policy, and the replay format | Format specification |
| [MULTIPLAYER.md](MULTIPLAYER.md) | What the coordination server is and is not, REST plus server-sent events transport, protocol and session versions, lobby, turn barrier and lifecycle, timers, bug-report intake, retention, security model and threat boundaries, the shared TypeScript/C# contract, client integration contract, limitations | Design |

### Original-game research

| Document | What it holds | Kind |
|---|---|---|
| [ORIGINAL-INTERNALS.md](ORIGINAL-INTERNALS.md) | `BIN-*` findings from static analysis of the fingerprinted executable: observation, interpretation, confidence, next validation; indexed by subsystem, with the remaining static-analysis queue | Research log |
| [ORIGINAL-FILE-FORMATS.md](ORIGINAL-FILE-FORMATS.md) | Confidence scale, installation identity and bundled-data fidelity, gameplay tables, `PX16`/`PX08` graphics, audio, video, help, other files, historical save-game notes, open questions | Format research log |
| [ASSET-CATALOG.md](ASSET-CATALOG.md) | Every extracted output with its source resource, media type, conversion, semantic role, screen owner, palette/transparency rule, and SHA-256; regenerated by `Rechaos.Extractor --catalog`, never edited by hand | Generated inventory |
| [UI-ATLAS.md](UI-ATLAS.md) | Full-screen resources, composite sheets and panels, the `PX00143` and `PX00128` hit maps, legacy session setup screens, every rendering rule recovered so far, and the next mapping work | Presentation map |
| [AUDIO-VIDEO.md](AUDIO-VIDEO.md) | Sound-effect slots and triggers, music programs, Smacker video metadata and the playback decision | Media map |
| [GHIDRA.md](GHIDRA.md) | Pinned Ghidra and JDK installation, the reference executable and manual, the headless script workflow under `tools/ghidra/`, and evidence discipline | Tooling guide |
| [REFERENCE-CAPTURE.md](REFERENCE-CAPTURE.md) | Operating the original executable and the window-capture helper for runtime evidence, and the evidence rules for captures | Procedure |

### Machine-readable

| Path | What it holds |
|---|---|
| [schemas/reference-fixture.schema.json](schemas/reference-fixture.schema.json) | JSON Schema for sanitized reference fixtures described in [VALIDATION.md](VALIDATION.md#fixture-classes) |
| [schemas/state-labels.example.json](schemas/state-labels.example.json) | Example state-label document for fixtures |

### Documentation outside this directory

| Path | What it holds |
|---|---|
| [README.md](../README.md) | Player-facing overview: quick start, implemented and missing features, scope boundaries, quality-of-life additions, headless AI tournaments, controls, crash reports, acknowledgements, license |
| [AGENTS.md](../AGENTS.md) | Working rules: push destination, validation scope, documentation upkeep, protocol and session version rules, orphan-process audit |
| [multiplayer/README.md](../multiplayer/README.md) | Operator and contributor manual for the coordination server: layout, running self-hosted or on Cloudflare, generating the C# client, bug reports, publishing, development |
| [multiplayer/packages/*/README.md](../multiplayer/packages) | One page per server package: `contracts` (wire schemas), `kernel` (domain logic and ports), `storage` (SQLite/Postgres adapters), `server` (the Hono app), `client` (TypeScript client), `conformance` (runtime-neutral suites), `bug-reports` (separate intake database) |
| [multiplayer/runtimes/*/README.md](../multiplayer/runtimes) | The Node.js server and the Cloudflare Worker facades |
| [src/Rechaos.Multiplayer/README.md](../src/Rechaos.Multiplayer/README.md) | The game's C# client for the coordination server and its layout |

## Topic index

Where each game subsystem is documented. Rules are the mechanics as the
recreation applies them; findings are the clean-room facts about the original
executable they rest on; the last column points at design, presentation,
status, and decision material. Parity links open the matrix section that holds
the subsystem's rows.

| Topic | Rules | Executable findings | Design, presentation, status |
|---|---|---|---|
| Randomness, seeding, and dice | — | [BIN-RNG-001](ORIGINAL-INTERNALS.md#bin-rng-001---original-process-seed), [BIN-RNG-002](ORIGINAL-INTERNALS.md#bin-rng-002---runtime-random-step), [BIN-RNG-003](ORIGINAL-INTERNALS.md#bin-rng-003---bounded-random-wrapper), [BIN-RNG-004](ORIGINAL-INTERNALS.md#bin-rng-004---ai-planning-callers), [BIN-RNG-005](ORIGINAL-INTERNALS.md#bin-rng-005---accepted-local-setup-through-initial-city) | [Determinism boundary](ARCHITECTURE.md#determinism-boundary); [parity: Determinism state](PARITY-MATRIX.md#turn-structure-and-match-state); [seed correlation blocker](PARITY-MATRIX.md#current-blockers-to-parity-claims) |
| Turn structure and phase order | — | [BIN-TURN-PLAYER-ORDER-001](ORIGINAL-INTERNALS.md#bin-turn-player-order-001---fixed-ascending-planning-slots), [BIN-ENDTURN-001](ORIGINAL-INTERNALS.md#bin-endturn-001---elimination-cleanup-reports-and-objective-order), [BIN-INSTANT-001](ORIGINAL-INTERNALS.md#bin-instant-001---roster-order-actions-and-cumulative-influence), [BIN-REPEAT-001](ORIGINAL-INTERNALS.md#bin-repeat-001---turn-start-terminal-recurring-command-cleanup), [BIN-COMMAND-ASSIGN-001](ORIGINAL-INTERNALS.md#bin-command-assign-001---recurring-menus-and-replacement-writes), [BIN-HIDE-LIFECYCLE-001](ORIGINAL-INTERNALS.md#bin-hide-lifecycle-001---active-and-recurring-action-boundary) | [Original turn model](ARCHITECTURE.md#original-turn-model); [Target command/event flow](ARCHITECTURE.md#target-commandevent-flow); [parity: Turn phases, Execution phases, Command queue](PARITY-MATRIX.md#turn-structure-and-match-state) |
| New-game setup and city generation | [RULE-SETUP-001](GAME-RULES.md#rule-setup-001--starting-resources-and-smgfundage) | [BIN-CITY-001](ORIGINAL-INTERNALS.md#bin-city-001---density-derived-sector-income-and-tolerance), [BIN-CITY-002](ORIGINAL-INTERNALS.md#bin-city-002---three-site-rejection-sampling), [BIN-CITY-003](ORIGINAL-INTERNALS.md#bin-city-003---headquarters-and-right-hands), [BIN-SETUP-000](ORIGINAL-INTERNALS.md#bin-setup-000---fresh-setup-defaults-to-kill-em-all), [BIN-SETUP-002](ORIGINAL-INTERNALS.md#bin-setup-002---local-missing-slots-become-computer-players), [BIN-SETUP-005](ORIGINAL-INTERNALS.md#bin-setup-005---exact-local-player-card-interaction-geometry), [BIN-RNG-005](ORIGINAL-INTERNALS.md#bin-rng-005---accepted-local-setup-through-initial-city) | [UI atlas: PX00143 hit map](UI-ATLAS.md#px00143-hit-map); [Legacy session setup screens](UI-ATLAS.md#legacy-session-setup-screens); [parity: City, Players, Starting state](PARITY-MATRIX.md#turn-structure-and-match-state); [workstream D](IMPLEMENTATION-PLAN.md#d-new-game-setup-and-city-generation) |
| Exact-name modifiers | [RULE-SETUP-001](GAME-RULES.md#rule-setup-001--starting-resources-and-smgfundage) | [BIN-SETUP-001](ORIGINAL-INTERNALS.md#bin-setup-001---smgfundage-starting-cash-override), [BIN-SETUP-003](ORIGINAL-INTERNALS.md#bin-setup-003---smgislands-permanent-neutral-sector-crackdown), [BIN-SETUP-004](ORIGINAL-INTERNALS.md#bin-setup-004---extra-gang-and-global-visibility-name-modifiers) | [README: online name projection refuses cheat names](../README.md#quality-of-life-additions) |
| Hot-seat play and handoff | — | [BIN-HOTSEAT-002](ORIGINAL-INTERNALS.md#bin-hotseat-002---private-handoff-ordering-and-terminal-player-path), [BIN-SETUP-002](ORIGINAL-INTERNALS.md#bin-setup-002---local-missing-slots-become-computer-players) | [parity: Hot-seat](PARITY-MATRIX.md#multiple-players-and-networking); [handover: reports and handoff](HANDOVER.md#turn-reports-and-hot-seat-handoff); [workstream M](IMPLEMENTATION-PLAN.md#m-hot-seat-play) |
| Hiring | [RULE-HIRE-001](GAME-RULES.md#rule-hire-001--offer-replacement-and-starting-force) | [BIN-HIRE-001](ORIGINAL-INTERNALS.md#bin-hire-001---initial-and-replacement-offers), [BIN-HIRE-COMPARISON-001](ORIGINAL-INTERNALS.md#bin-hire-comparison-001---fixed-width-signed-values-in-the-three-offer-panel), [BIN-AI-003A](ORIGINAL-INTERNALS.md#bin-ai-003a---strategic-hire-offer-ranking), [BIN-AI-003B](ORIGINAL-INTERNALS.md#bin-ai-003b---base-hire-role-schedule), [BIN-AI-003C](ORIGINAL-INTERNALS.md#bin-ai-003c---ai-hire-destination-and-persistent-placement-anchor) | [parity: Hire pool](PARITY-MATRIX.md#commands-and-economy); [decision: AI hire slot/role indexing](DECISIONS.md#2026-09-17--correct-the-original-ai-hire-slotrole-indexing-defect) |
| Movement | [RULE-MOVE-001](GAME-RULES.md#rule-move-001--adjacent-movement-and-friendly-capacity) | [BIN-MOVEMENT-001](ORIGINAL-INTERNALS.md#bin-movement-001---terminate-pass-before-roster-ordered-move), [BIN-MOVEMENT-002](ORIGINAL-INTERNALS.md#bin-movement-002---move-panel-neighborhood-target-mapping) | [parity: Movement](PARITY-MATRIX.md#commands-and-economy) |
| Sector control | [RULE-CONTROL-001](GAME-RULES.md#rule-control-001--cooperative-sector-control-comparison) | [BIN-CONTROL-001](ORIGINAL-INTERNALS.md#bin-control-001---cross-player-winner-and-zero-margin-neutral-candidate) | [parity: Sector control](PARITY-MATRIX.md#commands-and-economy) |
| Influence and sites | [RULE-INFLUENCE-001](GAME-RULES.md#rule-influence-001--cooperative-site-influence), [RULE-SITE-STATS-001](GAME-RULES.md#rule-site-stats-001--influenced-site-local-modifiers) | [BIN-INFLUENCE-001](ORIGINAL-INTERNALS.md#bin-influence-001---influence-picker-targets-and-detail-entry), [BIN-SITE-INFO-001](ORIGINAL-INTERNALS.md#bin-site-info-001---px05002-alternate-site-information-panel), [BIN-EFFECTIVE-STATS-001](ORIGINAL-INTERNALS.md#bin-effective-stats-001---gang-equipment-and-controlled-site-aggregation) | [parity: Influence](PARITY-MATRIX.md#commands-and-economy); [SITES table](ORIGINAL-FILE-FORMATS.md#sites) |
| Heal and Research | [RULE-HEAL-001](GAME-RULES.md#rule-heal-001--heal-dice-and-force-restoration), [RULE-RESEARCH-001](GAME-RULES.md#rule-research-001--research-dice-and-persistent-progress) | [BIN-RESEARCH-000](ORIGINAL-INTERNALS.md#bin-research-000---initial-progress-and-armageddon-completion), [BIN-RESEARCH-001](ORIGINAL-INTERNALS.md#bin-research-001---same-phase-completion-suppresses-later-rolls), [BIN-EQUIP-005](ORIGINAL-INTERNALS.md#bin-equip-005---equip-and-research-categorylist-targets) | [parity: Heal, Research](PARITY-MATRIX.md#commands-and-economy) |
| Bribe, Snitch, and tolerance | [RULE-BRIBE-001](GAME-RULES.md#rule-bribe-001--bribe-tolerance-adjustment), [RULE-SNITCH-001](GAME-RULES.md#rule-snitch-001--snitch-tolerance-adjustment), [RULE-TOLERANCE-001](GAME-RULES.md#rule-tolerance-001--return-toward-normal-tolerance) | [BIN-BRIBE-001](ORIGINAL-INTERNALS.md#bin-bribe-001---shipped-three-dollar-cost-and-direct-tolerance-delta), [BIN-SNITCH-001](ORIGINAL-INTERNALS.md#bin-snitch-001---debt-independent-delta-and-post-instant-floor) | [parity: Bribe, Snitch/tolerance](PARITY-MATRIX.md#commands-and-economy) |
| Chaos, police, and Crackdowns | [RULE-CHAOS-001](GAME-RULES.md#rule-chaos-001--cooperative-chaos-and-crackdown), [RULE-POLICE-001](GAME-RULES.md#rule-police-001--crackdown-detection-and-combat) | [BIN-CHAOS-001](ORIGINAL-INTERNALS.md#bin-chaos-001---roster-order-rolls-and-grouped-uncontrolled-payout), [BIN-POLICE-001](ORIGINAL-INTERNALS.md#bin-police-001---occurrence-window-neutralization-and-duration-order), [BIN-POLICE-COMBAT-001](ORIGINAL-INTERNALS.md#bin-police-combat-001---exact-detection-and-damage-formulas) | [parity: Chaos](PARITY-MATRIX.md#commands-and-economy); [parity: Police](PARITY-MATRIX.md#combat-and-police) |
| Combat, detection, and hiding | [RULE-ATTACK-001](GAME-RULES.md#rule-attack-001--simultaneous-attack-and-retaliation), [RULE-DETECT-001](GAME-RULES.md#rule-detect-001--cooperative-sector-visibility), [RULE-HIDE-001](GAME-RULES.md#rule-hide-001--enter-hidden-state) | [BIN-ATTACK-001](ORIGINAL-INTERNALS.md#bin-attack-001---attack-picker-selector-and-target-hit-map), [BIN-COMBAT-ORDER-001](ORIGINAL-INTERNALS.md#bin-combat-order-001---playerroster-attack-and-police-rolls), [BIN-COMBAT-STATS-001](ORIGINAL-INTERNALS.md#bin-combat-stats-001---full-opening-damage-is-credited), [BIN-DETECT-001](ORIGINAL-INTERNALS.md#bin-detect-001---cooperative-sector-visibility-aggregation), [BIN-HIDE-LIFECYCLE-001](ORIGINAL-INTERNALS.md#bin-hide-lifecycle-001---active-and-recurring-action-boundary), [BIN-UI-001](ORIGINAL-INTERNALS.md#bin-ui-001---combat-animation-cadence), [BIN-COMBAT-RESULTS-001](ORIGINAL-INTERNALS.md#bin-combat-results-001---results-pager-selection-map-and-bottom-control) | [Resolution safety policy](GAME-RULES.md#resolution-safety-policy); [combat sounds](AUDIO-VIDEO.md#sound-effects); [parity: Combat](PARITY-MATRIX.md#combat-and-police); [parity: Combat animation](PARITY-MATRIX.md#help-and-media); [decision: opponent view scoped to detection](DECISIONS.md#2026-09-18--scope-the-sector-workspaces-opponent-gang-view-to-detection) |
| Equipment, Give, Sell, Factory, and Terminate | [RULE-EQUIP-001](GAME-RULES.md#rule-equip-001--purchase-and-equip), [RULE-GIVE-001](GAME-RULES.md#rule-give-001--transfer-equipped-item), [RULE-SELL-001](GAME-RULES.md#rule-sell-001--half-price-sale), [RULE-TERMINATE-001](GAME-RULES.md#rule-terminate-001--remove-gang-and-equipment) | [BIN-EQUIP-001](ORIGINAL-INTERNALS.md#bin-equip-001---factory-price-division-and-rounding), [BIN-EQUIP-002](ORIGINAL-INTERNALS.md#bin-equip-002---fixed-transaction-scan-deferred-gifts-and-sell-overwrite), [BIN-EQUIP-003](ORIGINAL-INTERNALS.md#bin-equip-003---give-item-selection-hit-targets), [BIN-EQUIP-004](ORIGINAL-INTERNALS.md#bin-equip-004---sell-item-toggle-hit-targets), [BIN-EQUIP-005](ORIGINAL-INTERNALS.md#bin-equip-005---equip-and-research-categorylist-targets), [BIN-GANG-RETIRE-001](ORIGINAL-INTERNALS.md#bin-gang-retire-001---death-and-terminate-preserve-inactive-record-payload), [BIN-GANG-VALUES-001](ORIGINAL-INTERNALS.md#bin-gang-values-001---fixed-two-cell-gang-values-replace-template-padding), [BIN-GANG-DEFINITION-001](ORIGINAL-INTERNALS.md#bin-gang-definition-001---px05022-alternate-definition-panel), [BIN-ITEM-INFO-001](ORIGINAL-INTERNALS.md#bin-item-info-001---px05001-alternate-item-information-panel), [BIN-NUMBER-HELPERS-001](ORIGINAL-INTERNALS.md#bin-number-helpers-001---baseline-and-modifier-zero-glyphs) | [parity: Equipment](PARITY-MATRIX.md#commands-and-economy); [ITEMS table](ORIGINAL-FILE-FORMATS.md#items); [workstream G](IMPLEMENTATION-PLAN.md#g-items-equipment-and-research) |
| Economy, upkeep, and finance | [RULE-UPKEEP-001](GAME-RULES.md#rule-upkeep-001--base-income-upkeep-and-debt) | [BIN-UPKEEP-001](ORIGINAL-INTERNALS.md#bin-upkeep-001---flat-sector-tax-and-influenced-site-cash-share-one-byte), [BIN-FINANCE-001](ORIGINAL-INTERNALS.md#bin-finance-001---alternate-financial-panel-destination-and-close-face), [BIN-UI-035](ORIGINAL-INTERNALS.md#bin-ui-035---sector-income-and-owner-only-cash-rows) | [parity: Cash/upkeep](PARITY-MATRIX.md#commands-and-economy); [handover: resolution and economy](HANDOVER.md#turn-resolution-commands-and-economy) |
| Objectives, elimination, ranking, and awards | [RULE-OBJECTIVE-001](GAME-RULES.md#rule-objective-001--end-of-turn-objective-evaluation), [RULE-AWARDS-001](GAME-RULES.md#rule-awards-001--endgame-performance-awards) | [BIN-ENDTURN-001](ORIGINAL-INTERNALS.md#bin-endturn-001---elimination-cleanup-reports-and-objective-order), [BIN-RANKING-001](ORIGINAL-INTERNALS.md#bin-ranking-001---player-rail-portrait-positions), [BIN-AWARDS-001](ORIGINAL-INTERNALS.md#bin-awards-001---thresholds-priority-ties-and-visible-slots), [BIN-UI-033](ORIGINAL-INTERNALS.md#bin-ui-033---exact-siege-and-big-man-objective-sector-pylons), [BIN-AI-002](ORIGINAL-INTERNALS.md#bin-ai-002---scenario-sensitive-family-selection) | [parity: Player elimination, Objectives, Endgame](PARITY-MATRIX.md#objectives-timers-and-endgame); [milestone M5](IMPLEMENTATION-PLAN.md#m5---objectives-and-full-hot-seat-game) |
| Planning timer | [RULE-TIMER-001](GAME-RULES.md#rule-timer-001--optional-human-planning-limit) | [BIN-OPTIONS-001](ORIGINAL-INTERNALS.md#bin-options-001---registry-keys-initialized-defaults-and-idle-gang-warning) | [parity: Timers](PARITY-MATRIX.md#objectives-timers-and-endgame); [online timer](MULTIPLAYER.md#timer) |
| Computer players | — | [BIN-AI-001](ORIGINAL-INTERNALS.md#bin-ai-001---per-gang-command-dispatcher-and-action-handlers), [BIN-AI-002](ORIGINAL-INTERNALS.md#bin-ai-002---scenario-sensitive-family-selection), [BIN-AI-003](ORIGINAL-INTERNALS.md#bin-ai-003---outer-ai-planning-pass-and-command-history), [BIN-AI-003A](ORIGINAL-INTERNALS.md#bin-ai-003a---strategic-hire-offer-ranking), [BIN-AI-003B](ORIGINAL-INTERNALS.md#bin-ai-003b---base-hire-role-schedule), [BIN-AI-003C](ORIGINAL-INTERNALS.md#bin-ai-003c---ai-hire-destination-and-persistent-placement-anchor), [BIN-AI-004](ORIGINAL-INTERNALS.md#bin-ai-004---global-ai-mentality-byte-and-first-consumers), [BIN-AI-005](ORIGINAL-INTERNALS.md#bin-ai-005---shared-weighted-sector-selector), [BIN-AI-006](ORIGINAL-INTERNALS.md#bin-ai-006---directional-attitude-and-hostility-matrix), [BIN-AI-007](ORIGINAL-INTERNALS.md#bin-ai-007---per-player-difficulty-resolution-band), [BIN-RNG-004](ORIGINAL-INTERNALS.md#bin-rng-004---ai-planning-callers) | [AI-SPEC.md](AI-SPEC.md); [parity: AI](PARITY-MATRIX.md#computer-players); [per-family notes](PARITY-MATRIX.md#computer-player-row-details); [decision: no substituted AI commands](DECISIONS.md#2026-09-17--do-not-substitute-rejected-recovered-ai-commands); [README: headless tournaments](../README.md#headless-ai-tournaments); [milestone M6](IMPLEMENTATION-PLAN.md#m6---ai-parity) |
| Last Turn Events, Comlink, and Search | — | [BIN-EVENT-001](ORIGINAL-INTERNALS.md#bin-event-001---last-turn-report-table-types-and-lifetime), [BIN-EVENTS-002](ORIGINAL-INTERNALS.md#bin-events-002---last-turn-events-pager-and-exit-control), [BIN-UI-016](ORIGINAL-INTERNALS.md#bin-ui-016---last-turn-events-site-image-treatment), [BIN-COMLINK-001](ORIGINAL-INTERNALS.md#bin-comlink-001---per-player-message-queue-capacity-and-overflow), [BIN-COMLINK-002](ORIGINAL-INTERNALS.md#bin-comlink-002---view-navigation-and-hit-geometry), [BIN-COMLINK-003](ORIGINAL-INTERNALS.md#bin-comlink-003---send-eligibility-controls-and-composition-cursor), [BIN-COMLINK-004](ORIGINAL-INTERNALS.md#bin-comlink-004---view-record-fields-and-projection), [BIN-SEARCH-001](ORIGINAL-INTERNALS.md#bin-search-001---per-player-site-filters-and-city-markers), [BIN-SEARCH-002](ORIGINAL-INTERNALS.md#bin-search-002---exact-search-panel-controls-and-row-targets) | [parity: Notifications, Comlink](PARITY-MATRIX.md#turn-structure-and-match-state); [handover: reports](HANDOVER.md#turn-reports-and-hot-seat-handoff) |
| Options, preferences, and the registry | — | [BIN-OPTIONS-001](ORIGINAL-INTERNALS.md#bin-options-001---registry-keys-initialized-defaults-and-idle-gang-warning) | [parity: Options](PARITY-MATRIX.md#interface-input-and-options); [decision: registry persistence defects](DECISIONS.md#2026-09-17--correct-the-original-registry-persistence-defects); [handover: Options](HANDOVER.md#input-options-and-panel-motion) |
| Screens, panels, hit geometry, and rendering | — | [BIN-API-001](ORIGINAL-INTERNALS.md#bin-api-001---rendering), [BIN-API-002](ORIGINAL-INTERNALS.md#bin-api-002---px-loading-palette-and-copy-modes), [BIN-UI-031](ORIGINAL-INTERNALS.md#bin-ui-031---px00129-uses-role-specific-copy-modes), [BIN-UI-032](ORIGINAL-INTERNALS.md#bin-ui-032---exact-main-console-hit-split-and-pressed-geometry), [BIN-UI-034](ORIGINAL-INTERNALS.md#bin-ui-034---native-pointer-is-stock-arrowwait-not-an-atlas-sprite), [BIN-UI-036](ORIGINAL-INTERNALS.md#bin-ui-036---detailed-sector-site-and-gang-meters), [BIN-SECTOR-GANGS-001](ORIGINAL-INTERNALS.md#bin-sector-gangs-001---compact-all-gangs-sector-roster), [BIN-GAME-INFO-001](ORIGINAL-INTERNALS.md#bin-game-info-001---alternate-panel-crop-and-field-origins), [BIN-UI-CREDITS-001](ORIGINAL-INTERNALS.md#bin-ui-credits-001---blocking-publisherdeveloper-credits-presenter), [BIN-UI-TITLE-001](ORIGINAL-INTERNALS.md#bin-ui-title-001---title-canvas-and-dormant-demo-promotion), [BIN-UI-MENU-001](ORIGINAL-INTERNALS.md#bin-ui-menu-001---native-menu-resource-and-command-groups), [BIN-SETUP-006](ORIGINAL-INTERNALS.md#bin-setup-006---legacy-session-lobby-resources-are-distinct-flows), [BIN-SETUP-007](ORIGINAL-INTERNALS.md#bin-setup-007---legacy-transport-progress-sheets), [BIN-SETUP-008](ORIGINAL-INTERNALS.md#bin-setup-008---legacy-transfer-spinner-animation) | [UI-ATLAS.md](UI-ATLAS.md); [architecture: Rechaos.Game](ARCHITECTURE.md#rechaosgame); [parity: City/Sector UI, Input, Pointer cursor](PARITY-MATRIX.md#interface-input-and-options); [handover: city and console](HANDOVER.md#city-sector-and-console-presentation); [handover: panels](HANDOVER.md#management-panels); [decision: bulk gang orders](DECISIONS.md#2026-09-19--order-a-ctrl-picked-selection-of-gangs-at-once); [workstream J](IMPLEMENTATION-PLAN.md#j-user-interface-and-input) |
| Audio, music, and video | — | [BIN-API-006](ORIGINAL-INTERNALS.md#bin-api-006---audio-and-video), [BIN-MUSIC-001](ORIGINAL-INTERNALS.md#bin-music-001---cd-track-programs-and-lifecycle), [BIN-SOUND-001](ORIGINAL-INTERNALS.md#bin-sound-001---effect-slots-volume-and-setup-cues) | [AUDIO-VIDEO.md](AUDIO-VIDEO.md); [Audio and music](ORIGINAL-FILE-FORMATS.md#audio-and-music); [Video](ORIGINAL-FILE-FORMATS.md#video); [parity: Audio, Music, Video](PARITY-MATRIX.md#help-and-media); [decision: Smacker decoding](DECISIONS.md#2026-09-13--decode-the-supported-smacker-subset-at-runtime); [decision: intro playback](DECISIONS.md#2026-09-13--stream-the-intro-once-then-keep-it-on-the-title-screen); [workstream K](IMPLEMENTATION-PLAN.md#k-audio-and-video) |
| Help (WinHelp import and viewer) | — | [BIN-ASSET-002](ORIGINAL-INTERNALS.md#bin-asset-002---winhelp-context-maps), [BIN-ASSET-003](ORIGINAL-INTERNALS.md#bin-asset-003---winhelp-styled-text-and-internal-hotspots) | [Help](ORIGINAL-FILE-FORMATS.md#help); [parity: Help](PARITY-MATRIX.md#help-and-media); [handover: Help](HANDOVER.md#help-audio-and-video) |
| Original file formats and the asset pack | — | [BIN-ASSET-001](ORIGINAL-INTERNALS.md#bin-asset-001---data-paths), [BIN-PE-001](ORIGINAL-INTERNALS.md#bin-pe-001---executable-format), [BIN-PE-002](ORIGINAL-INTERNALS.md#bin-pe-002---sections), [BIN-TOOL-001](ORIGINAL-INTERNALS.md#bin-tool-001---compilerruntime) | [ORIGINAL-FILE-FORMATS.md](ORIGINAL-FILE-FORMATS.md); [ASSET-CATALOG.md](ASSET-CATALOG.md); [architecture: Rechaos.Extractor](ARCHITECTURE.md#rechaosextractor); [Proprietary-content boundary](ARCHITECTURE.md#proprietary-content-boundary); [parity: source and output packs](PARITY-MATRIX.md#source-data-and-extraction); [extractor commands](DEVELOPMENT.md#run-from-source) |
| Saves, replays, and canonical hashing | — | [BIN-API-003](ORIGINAL-INTERNALS.md#bin-api-003---files-and-persistence) | [NATIVE-SAVE-FORMAT.md](NATIVE-SAVE-FORMAT.md); [original saves (unsupported)](ORIGINAL-FILE-FORMATS.md#save-games-historical-reference-only-unsupported); [decision: save compatibility scope](DECISIONS.md#2026-09-10--save-compatibility-scope); [parity: Persistence](PARITY-MATRIX.md#persistence); [handover: persistence](HANDOVER.md#saves-replays-and-canonical-hashing) |
| Online play | — | [BIN-API-004](ORIGINAL-INTERNALS.md#bin-api-004---legacy-networking) | [MULTIPLAYER.md](MULTIPLAYER.md); [multiplayer/README.md](../multiplayer/README.md); [Online play](ARCHITECTURE.md#online-play); [decision: networking scope](DECISIONS.md#2026-09-10--networking-scope); [parity: Legacy and Modern network](PARITY-MATRIX.md#multiple-players-and-networking); [protocol version rule](../AGENTS.md#multiplayer-protocol-version); [session version rule](../AGENTS.md#multiplayer-session-version); [handover: online play](HANDOVER.md#online-play-and-multiplayer) |
| Bug reports and diagnostics | — | — | [multiplayer: bug-report intake](MULTIPLAYER.md#bug-reports-the-same-deployment-a-different-database); [decision: replayable journals](DECISIONS.md#2026-09-13--bug-reports-carry-a-replayable-journal-stored-apart-from-matches); [operator manual: bug reports](../multiplayer/README.md#bug-reports); [README: crash reports](../README.md#crash-reports); [handover: diagnostics](HANDOVER.md#extraction-asset-verification-and-diagnostics) |
| Validation, fixtures, and evidence rules | — | — | [VALIDATION.md](VALIDATION.md); [4.2 Evidence rules](IMPLEMENTATION-PLAN.md#42-evidence-rules); [7. Cross-cutting test matrix](IMPLEMENTATION-PLAN.md#7-cross-cutting-test-matrix); [10. Source hierarchy](IMPLEMENTATION-PLAN.md#10-source-hierarchy); [Evidence discipline](GHIDRA.md#evidence-discipline); [capture evidence rules](REFERENCE-CAPTURE.md#evidence-rules); [Confidence scale](ORIGINAL-FILE-FORMATS.md#confidence-scale) |
| Packaging, signing, and releases | — | — | [RELEASING.md](RELEASING.md); [workstream N](IMPLEMENTATION-PLAN.md#n-platform-packaging-and-quality); [README: quick start](../README.md#quick-start) |

## Identifiers and conventions

| Convention | Meaning | Defined in |
|---|---|---|
| `BIN-<AREA>-<NNN>` | A clean-room finding about the original executable. IDs are stable and unique; a letter suffix (`BIN-AI-003A`) marks a finding split out of another. Never reuse or renumber an ID. | [Finding format](ORIGINAL-INTERNALS.md#finding-format) |
| `RULE-<AREA>-<NNN>` | A game rule as the recreation applies it, with its evidence and implementing types. | [Rule format](GAME-RULES.md#rule-format) |
| `YYYY-MM-DD — <title>` | A dated decision. New entries go at the top of the decision log. | [DECISIONS.md](DECISIONS.md) |
| `Verified` / `High` / `Medium` / `Low` | Confidence attached to every finding, rule, and format observation. | [Confidence scale](ORIGINAL-FILE-FORMATS.md#confidence-scale); [4.2 Evidence rules](IMPLEMENTATION-PLAN.md#42-evidence-rules) |
| `Unknown` … `Not applicable` | The seven parity status values. | [8. Completion tracking conventions](IMPLEMENTATION-PLAN.md#8-completion-tracking-conventions) |
| `Manual` | Evidence label for a behavior inspected in the manual but not yet confirmed in the binary. | [PARITY-MATRIX.md](PARITY-MATRIX.md) |
| `MANUAL-GOG-1`, `EXE-GOG-1.1` | Named evidence sources for rules. | [Evidence sources](GAME-RULES.md#evidence-sources) |
| Source hierarchy | Which source wins when they disagree. | [10. Source hierarchy](IMPLEMENTATION-PLAN.md#10-source-hierarchy) |
| `Status:` / `Last updated:` | Header lines at the top of each maintained document. | This directory |
| `<!-- doc-index:begin … -->` | A generated table of contents or index. Rebuild with `node tools/update-doc-indexes.mjs`; `--check` fails when a block is stale. | [tools/update-doc-indexes.mjs](../tools/update-doc-indexes.mjs) |

## Canonical identities

The fingerprints that every address, offset, and hash in this directory is
valid for. They are repeated here for lookup only; the linked document is the
authority for each.

| Identity | SHA-256 | Authority |
|---|---|---|
| Reference executable `Chaos Overlords.exe` (version 1.1, 664,576 bytes) | `a1430159bbe20869e277a5000311344f4ec141ab77c96b385336617149e97d89` | [ORIGINAL-INTERNALS.md](ORIGINAL-INTERNALS.md), [Evidence sources](GAME-RULES.md#evidence-sources) |
| Full `DATA` + `HELP` + `MUSIC` source pack | `ad958a934a691318f31a27a87252f420dd89a0ad03759457d8feaf49914d29e3` | [Current canonical identities](VALIDATION.md#current-canonical-identities), [ASSET-CATALOG.md](ASSET-CATALOG.md) |
| Original manual scan (`MANUAL-GOG-1`) | `bdb1072848df95111cd014faaa7297d016b7c6e55cd7f2658dda67a167a0089d` | [Evidence sources](GAME-RULES.md#evidence-sources) |
| Bundled gameplay JSON `src/Rechaos.Core/GameData/original-data.json` | `e65f80e4d9a99ceeffbbc7fb335ef7f57b368ef87c1c56af23bcd759cd4e8b3a` | [Current canonical identities](VALIDATION.md#current-canonical-identities) |
| Upstream `re-chaos` research executable (differs from the GOG build) | `0791e6209d573a79882675d1236737f5c9b369ea4af541a7dbd03cbadf4493d5` | [10. Source hierarchy](IMPLEMENTATION-PLAN.md#10-source-hierarchy) |

Current format versions (native save, replay, canonical hash, asset manifest,
extracted help, client preferences) change more often than hashes; read them
from [HANDOVER.md](HANDOVER.md#repository-state) and
[NATIVE-SAVE-FORMAT.md](NATIVE-SAVE-FORMAT.md) rather than from here.

## Maintaining this directory

- **New evidence goes where its family lives**: an executable finding is a
  new `### BIN-…` heading in ORIGINAL-INTERNALS.md; an intended mechanic is a
  `### RULE-…` heading in GAME-RULES.md; a deliberate deviation is a dated entry
  at the top of DECISIONS.md; a status change is a parity row. Then link them:
  a rule cites its findings, a parity row cites both, and this page's topic
  index gains the new IDs.
- **Regenerate, do not hand-edit, generated blocks**: after changing headings
  in a document with a `doc-index` block, run `node tools/update-doc-indexes.mjs`.
  ASSET-CATALOG.md is rewritten by `dotnet run --project src/Rechaos.Extractor -- --catalog`
  from a fully verified pack.
- **Adding a document**: give it `Status:` and `Last updated:` lines, add it to
  the catalog above and to the [documents-to-maintain table](IMPLEMENTATION-PLAN.md#41-documents-to-maintain)
  when it carries parity evidence, and link it from the topic index.
- **Keep evidence and claims apart**: what the executable does, what the manual
  says, and what the recreation currently does are recorded in different
  places on purpose; see [the evidence rules](IMPLEMENTATION-PLAN.md#42-evidence-rules)
  and [the static research protocol](VALIDATION.md#static-binary-research-protocol).
