# Original executable internals research

Status: active clean-room research log
Reference executable SHA-256:
`a1430159bbe20869e277a5000311344f4ec141ab77c96b385336617149e97d89`

This research log records factual structure and testable interpretations of the
original executable. It does not contain copied decompiled source. Addresses are
valid only for the fingerprint above.

The findings themselves live in the per-subsystem documents under
[`original-internals/`](original-internals/). This page is the entry point to
them: it defines the finding format, indexes every `BIN-*` ID across the set,
and holds the remaining static-analysis queue.

<!-- doc-index:begin toc depth=2 -->
- [Finding format](#finding-format)
- [Finding documents](#finding-documents)
- [Finding index](#finding-index)
- [Remaining static-analysis queue](#remaining-static-analysis-queue)
<!-- doc-index:end -->

## Finding format

- **ID**: stable reference used by rules, code, tests, and the parity matrix.
- **Observation**: what is directly present in the executable or behavior.
- **Interpretation**: what the observation may mean.
- **Confidence**: Verified, High, Medium, or Low.
- **Next validation**: experiment needed before relying on semantics.

The ID is the handle everything else cites; no rule, test, or parity row cites a
document or a section. IDs are unique across the whole set and are never reused
or renumbered, so a finding can be regrouped without invalidating a reference.
Reach a finding by ID through the [finding index](#finding-index) below, or by
subsystem through the [topic index](README.md#topic-index).

ID history: `BIN-API-006` (audio and video) and `BIN-UI-036` (detailed-sector
site and gang meters) were originally recorded under the already-used IDs
`BIN-API-002` and `BIN-UI-034` and were renumbered on 2026-09-20. Nothing else
about those findings changed.

## Finding documents

New evidence goes into the document its subsystem lives in, under a new unique
`### BIN-<AREA>-<NNN>` heading.

| Document | What it holds |
|---|---|
| [executable-and-platform.md](original-internals/executable-and-platform.md) | PE layout and sections, the toolchain fingerprint, the Win32 and DirectDraw import boundaries per subsystem, and the data-path and help-resource literals |
| [randomness-and-turn-structure.md](original-internals/randomness-and-turn-structure.md) | Process seed, runtime random step and bounded wrapper, every classified caller, planning-slot order, and the recurring-command and end-of-turn boundaries |
| [new-game-setup.md](original-internals/new-game-setup.md) | City generation, headquarters and Right Hands, setup defaults, exact-name modifiers, local player-card geometry, legacy session flows, and hot-seat handoff |
| [commands-and-economy.md](original-internals/commands-and-economy.md) | Hire offers, Instant commands, movement and sector control, effective gang statistics, the equipment economy, and upkeep |
| [combat-and-police.md](original-internals/combat-and-police.md) | Attack, retaliation and detection order, exact police formulas, damage crediting, Chaos payouts, and Crackdown lifetimes |
| [objectives-and-awards.md](original-internals/objectives-and-awards.md) | Player-rail ranking positions, and endgame award thresholds, priority, ties, and visible slots |
| [computer-players.md](original-internals/computer-players.md) | The AI dispatcher and planning pass, family selection, hire ranking and placement, the Mentality byte, sector selection, attitude, and difficulty |
| [interface-and-options.md](original-internals/interface-and-options.md) | Copy modes and console geometry, city and sector panels, the alternate information panels, title, credits and menus, and registry-backed options |
| [reports-and-comlink.md](original-internals/reports-and-comlink.md) | The Last Turn report table and pager, the Comlink queue, navigation, composition and projection, and the Search panel |
| [audio-and-video.md](original-internals/audio-and-video.md) | CD track programs and lifecycle, and sound-effect slots, volume, and setup cues |

## Finding index

The tables below are generated from the `###` headings of the documents above by
`node tools/update-doc-indexes.mjs`; edit the headings, not the tables. Findings
are grouped by subsystem, each group names the document that holds it, and rows
are in ID order.

<!-- doc-index:begin finding-index -->
110 findings.

**Executable image** — [executable-and-platform.md](original-internals/executable-and-platform.md)

| ID | Finding |
|---|---|
| [BIN-PE-001](original-internals/executable-and-platform.md#bin-pe-001---executable-format) | executable format |
| [BIN-PE-002](original-internals/executable-and-platform.md#bin-pe-002---sections) | sections |
| [BIN-TOOL-001](original-internals/executable-and-platform.md#bin-tool-001---compilerruntime) | compiler/runtime |

**Platform boundaries visible in imports** — [executable-and-platform.md](original-internals/executable-and-platform.md)

| ID | Finding |
|---|---|
| [BIN-API-001](original-internals/executable-and-platform.md#bin-api-001---rendering) | rendering |
| [BIN-API-002](original-internals/executable-and-platform.md#bin-api-002---px-loading-palette-and-copy-modes) | PX loading, palette, and copy modes |
| [BIN-API-003](original-internals/executable-and-platform.md#bin-api-003---files-and-persistence) | files and persistence |
| [BIN-API-004](original-internals/executable-and-platform.md#bin-api-004---legacy-networking) | legacy networking |
| [BIN-API-005](original-internals/executable-and-platform.md#bin-api-005---configuration) | configuration |
| [BIN-API-006](original-internals/executable-and-platform.md#bin-api-006---audio-and-video) | audio and video |

**Resource lookup** — [executable-and-platform.md](original-internals/executable-and-platform.md)

| ID | Finding |
|---|---|
| [BIN-ASSET-001](original-internals/executable-and-platform.md#bin-asset-001---data-paths) | data paths |
| [BIN-ASSET-002](original-internals/executable-and-platform.md#bin-asset-002---winhelp-context-maps) | WinHelp context maps |
| [BIN-ASSET-003](original-internals/executable-and-platform.md#bin-asset-003---winhelp-styled-text-and-internal-hotspots) | WinHelp styled text and internal hotspots |

**Randomness and seeding** — [randomness-and-turn-structure.md](original-internals/randomness-and-turn-structure.md)

| ID | Finding |
|---|---|
| [BIN-RNG-001](original-internals/randomness-and-turn-structure.md#bin-rng-001---original-process-seed) | original process seed |
| [BIN-RNG-002](original-internals/randomness-and-turn-structure.md#bin-rng-002---runtime-random-step) | runtime random step |
| [BIN-RNG-003](original-internals/randomness-and-turn-structure.md#bin-rng-003---bounded-random-wrapper) | bounded random wrapper |
| [BIN-RNG-004](original-internals/randomness-and-turn-structure.md#bin-rng-004---ai-planning-callers) | AI planning callers |
| [BIN-RNG-005](original-internals/randomness-and-turn-structure.md#bin-rng-005---accepted-local-setup-through-initial-city) | accepted local setup through initial city |

**Turn structure and phase order** — [randomness-and-turn-structure.md](original-internals/randomness-and-turn-structure.md)

| ID | Finding |
|---|---|
| [BIN-COMMAND-ASSIGN-001](original-internals/randomness-and-turn-structure.md#bin-command-assign-001---recurring-menus-and-replacement-writes) | recurring menus and replacement writes |
| [BIN-ENDTURN-001](original-internals/randomness-and-turn-structure.md#bin-endturn-001---elimination-cleanup-reports-and-objective-order) | elimination cleanup, reports, and objective order |
| [BIN-HIDE-LIFECYCLE-001](original-internals/randomness-and-turn-structure.md#bin-hide-lifecycle-001---active-and-recurring-action-boundary) | active and recurring action boundary |
| [BIN-REPEAT-001](original-internals/randomness-and-turn-structure.md#bin-repeat-001---turn-start-terminal-recurring-command-cleanup) | turn-start terminal recurring-command cleanup |
| [BIN-TURN-PLAYER-ORDER-001](original-internals/randomness-and-turn-structure.md#bin-turn-player-order-001---fixed-ascending-planning-slots) | fixed ascending planning slots |

**New-game setup and city generation** — [new-game-setup.md](original-internals/new-game-setup.md)

| ID | Finding |
|---|---|
| [BIN-CITY-001](original-internals/new-game-setup.md#bin-city-001---density-derived-sector-income-and-tolerance) | density-derived sector income and tolerance |
| [BIN-CITY-002](original-internals/new-game-setup.md#bin-city-002---three-site-rejection-sampling) | three-site rejection sampling |
| [BIN-CITY-003](original-internals/new-game-setup.md#bin-city-003---headquarters-and-right-hands) | headquarters and Right Hands |
| [BIN-HOTSEAT-002](original-internals/new-game-setup.md#bin-hotseat-002---private-handoff-ordering-and-terminal-player-path) | private handoff ordering and terminal-player path |
| [BIN-SETUP-000](original-internals/new-game-setup.md#bin-setup-000---fresh-setup-defaults-to-kill-em-all) | fresh setup defaults to Kill 'Em All |
| [BIN-SETUP-001](original-internals/new-game-setup.md#bin-setup-001---smgfundage-starting-cash-override) | SMGFUNDAGE starting cash override |
| [BIN-SETUP-002](original-internals/new-game-setup.md#bin-setup-002---local-missing-slots-become-computer-players) | local missing slots become computer players |
| [BIN-SETUP-003](original-internals/new-game-setup.md#bin-setup-003---smgislands-permanent-neutral-sector-crackdown) | SMGISLANDS permanent neutral-sector Crackdown |
| [BIN-SETUP-004](original-internals/new-game-setup.md#bin-setup-004---extra-gang-and-global-visibility-name-modifiers) | extra-gang and global-visibility name modifiers |
| [BIN-SETUP-005](original-internals/new-game-setup.md#bin-setup-005---exact-local-player-card-interaction-geometry) | exact local player-card interaction geometry |
| [BIN-SETUP-006](original-internals/new-game-setup.md#bin-setup-006---legacy-session-lobby-resources-are-distinct-flows) | legacy session lobby resources are distinct flows |
| [BIN-SETUP-007](original-internals/new-game-setup.md#bin-setup-007---legacy-transport-progress-sheets) | legacy transport progress sheets |
| [BIN-SETUP-008](original-internals/new-game-setup.md#bin-setup-008---legacy-transfer-spinner-animation) | legacy transfer spinner animation |

**Hiring** — [commands-and-economy.md](original-internals/commands-and-economy.md)

| ID | Finding |
|---|---|
| [BIN-HIRE-001](original-internals/commands-and-economy.md#bin-hire-001---initial-and-replacement-offers) | initial and replacement offers |
| [BIN-HIRE-002](original-internals/commands-and-economy.md#bin-hire-002---hire-capacity-uses-only-the-current-players-roster) | hire capacity uses only the current player's roster |
| [BIN-HIRE-COMPARISON-001](original-internals/commands-and-economy.md#bin-hire-comparison-001---fixed-width-signed-values-in-the-three-offer-panel) | fixed-width signed values in the three-offer panel |

**Instant commands** — [commands-and-economy.md](original-internals/commands-and-economy.md)

| ID | Finding |
|---|---|
| [BIN-BRIBE-001](original-internals/commands-and-economy.md#bin-bribe-001---shipped-three-dollar-cost-and-direct-tolerance-delta) | shipped three-dollar cost and direct tolerance delta |
| [BIN-INFLUENCE-001](original-internals/commands-and-economy.md#bin-influence-001---influence-picker-targets-and-detail-entry) | Influence picker targets and detail entry |
| [BIN-INSTANT-001](original-internals/commands-and-economy.md#bin-instant-001---roster-order-actions-and-cumulative-influence) | roster-order actions and cumulative Influence |
| [BIN-RESEARCH-000](original-internals/commands-and-economy.md#bin-research-000---initial-progress-and-armageddon-completion) | initial progress and Armageddon completion |
| [BIN-RESEARCH-001](original-internals/commands-and-economy.md#bin-research-001---same-phase-completion-suppresses-later-rolls) | same-phase completion suppresses later rolls |
| [BIN-SNITCH-001](original-internals/commands-and-economy.md#bin-snitch-001---debt-independent-delta-and-post-instant-floor) | debt-independent delta and post-Instant floor |

**Movement and sector control** — [commands-and-economy.md](original-internals/commands-and-economy.md)

| ID | Finding |
|---|---|
| [BIN-CONTROL-001](original-internals/commands-and-economy.md#bin-control-001---cross-player-winner-and-zero-margin-neutral-candidate) | cross-player winner and zero-margin neutral candidate |
| [BIN-MOVEMENT-001](original-internals/commands-and-economy.md#bin-movement-001---terminate-pass-before-roster-ordered-move) | Terminate pass before roster-ordered Move |
| [BIN-MOVEMENT-002](original-internals/commands-and-economy.md#bin-movement-002---move-panel-neighborhood-target-mapping) | Move panel neighborhood target mapping |

**Gang statistics and equipment** — [commands-and-economy.md](original-internals/commands-and-economy.md)

| ID | Finding |
|---|---|
| [BIN-EFFECTIVE-STATS-001](original-internals/commands-and-economy.md#bin-effective-stats-001---gang-equipment-and-controlled-site-aggregation) | gang, equipment, and controlled-site aggregation |
| [BIN-EQUIP-001](original-internals/commands-and-economy.md#bin-equip-001---factory-price-division-and-rounding) | Factory price division and rounding |
| [BIN-EQUIP-002](original-internals/commands-and-economy.md#bin-equip-002---fixed-transaction-scan-deferred-gifts-and-sell-overwrite) | fixed transaction scan, deferred gifts, and Sell overwrite |
| [BIN-EQUIP-003](original-internals/commands-and-economy.md#bin-equip-003---give-item-selection-hit-targets) | Give item-selection hit targets |
| [BIN-EQUIP-004](original-internals/commands-and-economy.md#bin-equip-004---sell-item-toggle-hit-targets) | Sell item-toggle hit targets |
| [BIN-EQUIP-005](original-internals/commands-and-economy.md#bin-equip-005---equip-and-research-categorylist-targets) | Equip and Research category/list targets |
| [BIN-EQUIP-006](original-internals/commands-and-economy.md#bin-equip-006---cash-check-at-resolution-not-in-the-picker) | cash check at resolution, not in the picker |
| [BIN-GANG-DEFINITION-001](original-internals/commands-and-economy.md#bin-gang-definition-001---px05022-alternate-definition-panel) | PX05022 alternate definition panel |
| [BIN-GANG-RETIRE-001](original-internals/commands-and-economy.md#bin-gang-retire-001---death-and-terminate-preserve-inactive-record-payload) | death and Terminate preserve inactive record payload |
| [BIN-GANG-VALUES-001](original-internals/commands-and-economy.md#bin-gang-values-001---fixed-two-cell-gang-values-replace-template-padding) | fixed two-cell gang values replace template padding |

**Economy and finance** — [commands-and-economy.md](original-internals/commands-and-economy.md)

| ID | Finding |
|---|---|
| [BIN-FINANCE-001](original-internals/commands-and-economy.md#bin-finance-001---alternate-financial-panel-destination-and-close-face) | alternate Financial panel destination and close face |
| [BIN-UPKEEP-001](original-internals/commands-and-economy.md#bin-upkeep-001---flat-sector-tax-and-influenced-site-cash-share-one-byte) | flat sector tax and influenced-site Cash share one byte |

**Combat** — [combat-and-police.md](original-internals/combat-and-police.md)

| ID | Finding |
|---|---|
| [BIN-ATTACK-001](original-internals/combat-and-police.md#bin-attack-001---attack-picker-selector-and-target-hit-map) | Attack picker selector and target hit map |
| [BIN-COMBAT-ORDER-001](original-internals/combat-and-police.md#bin-combat-order-001---playerroster-attack-and-police-rolls) | player/roster attack and police rolls |
| [BIN-COMBAT-PRESENT-001](original-internals/combat-and-police.md#bin-combat-present-001---retaliation-lands-inside-the-attacks-clip) | retaliation lands inside the attack's clip |
| [BIN-COMBAT-RESULTS-001](original-internals/combat-and-police.md#bin-combat-results-001---results-pager-selection-map-and-bottom-control) | results pager, selection map, and bottom control |
| [BIN-COMBAT-STATS-001](original-internals/combat-and-police.md#bin-combat-stats-001---full-opening-damage-is-credited) | full opening damage is credited |
| [BIN-DETECT-001](original-internals/combat-and-police.md#bin-detect-001---cooperative-sector-visibility-aggregation) | cooperative sector visibility aggregation |

**Chaos and police** — [combat-and-police.md](original-internals/combat-and-police.md)

| ID | Finding |
|---|---|
| [BIN-CHAOS-001](original-internals/combat-and-police.md#bin-chaos-001---roster-order-rolls-and-grouped-uncontrolled-payout) | roster-order rolls and grouped uncontrolled payout |
| [BIN-POLICE-001](original-internals/combat-and-police.md#bin-police-001---occurrence-window-neutralization-and-duration-order) | occurrence window, neutralization, and duration order |
| [BIN-POLICE-002](original-internals/combat-and-police.md#bin-police-002---crackdown-report-recipients-and-ordering) | Crackdown report recipients and ordering |
| [BIN-POLICE-COMBAT-001](original-internals/combat-and-police.md#bin-police-combat-001---exact-detection-and-damage-formulas) | exact detection and damage formulas |

**Objectives, ranking, and awards** — [objectives-and-awards.md](original-internals/objectives-and-awards.md)

| ID | Finding |
|---|---|
| [BIN-AWARDS-001](original-internals/objectives-and-awards.md#bin-awards-001---thresholds-priority-ties-and-visible-slots) | thresholds, priority, ties, and visible slots |
| [BIN-RANKING-001](original-internals/objectives-and-awards.md#bin-ranking-001---player-rail-portrait-positions) | player-rail portrait positions |

**Computer players** — [computer-players.md](original-internals/computer-players.md)

| ID | Finding |
|---|---|
| [BIN-AI-001](original-internals/computer-players.md#bin-ai-001---per-gang-command-dispatcher-and-action-handlers) | per-gang command dispatcher and action handlers |
| [BIN-AI-002](original-internals/computer-players.md#bin-ai-002---scenario-sensitive-family-selection) | scenario-sensitive family selection |
| [BIN-AI-003](original-internals/computer-players.md#bin-ai-003---outer-ai-planning-pass-and-command-history) | outer AI planning pass and command history |
| [BIN-AI-003A](original-internals/computer-players.md#bin-ai-003a---strategic-hire-offer-ranking) | strategic hire-offer ranking |
| [BIN-AI-003B](original-internals/computer-players.md#bin-ai-003b---base-hire-role-schedule) | base hire-role schedule |
| [BIN-AI-003C](original-internals/computer-players.md#bin-ai-003c---ai-hire-destination-and-persistent-placement-anchor) | AI hire destination and persistent placement anchor |
| [BIN-AI-004](original-internals/computer-players.md#bin-ai-004---global-ai-mentality-byte-and-first-consumers) | global AI Mentality byte and first consumers |
| [BIN-AI-005](original-internals/computer-players.md#bin-ai-005---shared-weighted-sector-selector) | shared weighted sector selector |
| [BIN-AI-006](original-internals/computer-players.md#bin-ai-006---directional-attitude-and-hostility-matrix) | directional attitude and hostility matrix |
| [BIN-AI-007](original-internals/computer-players.md#bin-ai-007---per-player-difficulty-resolution-band) | per-player difficulty resolution band |

**Screens, panels, and hit geometry** — [interface-and-options.md](original-internals/interface-and-options.md)

| ID | Finding |
|---|---|
| [BIN-GAME-INFO-001](original-internals/interface-and-options.md#bin-game-info-001---alternate-panel-crop-and-field-origins) | alternate panel crop and field origins |
| [BIN-ITEM-INFO-001](original-internals/interface-and-options.md#bin-item-info-001---px05001-alternate-item-information-panel) | PX05001 alternate item-information panel |
| [BIN-NUMBER-HELPERS-001](original-internals/interface-and-options.md#bin-number-helpers-001---baseline-and-modifier-zero-glyphs) | baseline and modifier zero glyphs |
| [BIN-SECTOR-GANGS-001](original-internals/interface-and-options.md#bin-sector-gangs-001---compact-all-gangs-sector-roster) | compact all-gangs sector roster |
| [BIN-SITE-INFO-001](original-internals/interface-and-options.md#bin-site-info-001---px05002-alternate-site-information-panel) | PX05002 alternate Site Information panel |
| [BIN-UI-001](original-internals/interface-and-options.md#bin-ui-001---combat-animation-cadence) | combat animation cadence |
| [BIN-UI-016](original-internals/interface-and-options.md#bin-ui-016---last-turn-events-site-image-treatment) | Last Turn Events site-image treatment |
| [BIN-UI-031](original-internals/interface-and-options.md#bin-ui-031---px00129-uses-role-specific-copy-modes) | PX00129 uses role-specific copy modes |
| [BIN-UI-032](original-internals/interface-and-options.md#bin-ui-032---exact-main-console-hit-split-and-pressed-geometry) | exact main-console hit, split, and pressed geometry |
| [BIN-UI-033](original-internals/interface-and-options.md#bin-ui-033---exact-siege-and-big-man-objective-sector-pylons) | exact Siege and Big Man objective-sector pylons |
| [BIN-UI-034](original-internals/interface-and-options.md#bin-ui-034---native-pointer-is-stock-arrowwait-not-an-atlas-sprite) | native pointer is stock-arrow/wait, not an atlas sprite |
| [BIN-UI-035](original-internals/interface-and-options.md#bin-ui-035---sector-income-and-owner-only-cash-rows) | sector Income and owner-only Cash rows |
| [BIN-UI-036](original-internals/interface-and-options.md#bin-ui-036---detailed-sector-site-and-gang-meters) | detailed-sector site and gang meters |
| [BIN-UI-CREDITS-001](original-internals/interface-and-options.md#bin-ui-credits-001---blocking-publisherdeveloper-credits-presenter) | blocking publisher/developer credits presenter |
| [BIN-UI-MENU-001](original-internals/interface-and-options.md#bin-ui-menu-001---native-menu-resource-and-command-groups) | native menu resource and command groups |
| [BIN-UI-TITLE-001](original-internals/interface-and-options.md#bin-ui-title-001---title-canvas-and-dormant-demo-promotion) | title canvas and dormant demo promotion |

**Options and preferences** — [interface-and-options.md](original-internals/interface-and-options.md)

| ID | Finding |
|---|---|
| [BIN-OPTIONS-001](original-internals/interface-and-options.md#bin-options-001---registry-keys-initialized-defaults-and-idle-gang-warning) | registry keys, initialized defaults, and idle-gang warning |

**Turn reports and Comlink** — [reports-and-comlink.md](original-internals/reports-and-comlink.md)

| ID | Finding |
|---|---|
| [BIN-COMLINK-001](original-internals/reports-and-comlink.md#bin-comlink-001---per-player-message-queue-capacity-and-overflow) | per-player message queue capacity and overflow |
| [BIN-COMLINK-002](original-internals/reports-and-comlink.md#bin-comlink-002---view-navigation-and-hit-geometry) | View navigation and hit geometry |
| [BIN-COMLINK-003](original-internals/reports-and-comlink.md#bin-comlink-003---send-eligibility-controls-and-composition-cursor) | Send eligibility, controls, and composition cursor |
| [BIN-COMLINK-004](original-internals/reports-and-comlink.md#bin-comlink-004---view-record-fields-and-projection) | View record fields and projection |
| [BIN-EVENT-001](original-internals/reports-and-comlink.md#bin-event-001---last-turn-report-table-types-and-lifetime) | Last Turn report table, types, and lifetime |
| [BIN-EVENT-003](original-internals/reports-and-comlink.md#bin-event-003---cash-failure-report-illustration) | cash-failure report illustration |
| [BIN-EVENTS-002](original-internals/reports-and-comlink.md#bin-events-002---last-turn-events-pager-and-exit-control) | Last Turn Events pager and exit control |
| [BIN-SEARCH-001](original-internals/reports-and-comlink.md#bin-search-001---per-player-site-filters-and-city-markers) | per-player site filters and city markers |
| [BIN-SEARCH-002](original-internals/reports-and-comlink.md#bin-search-002---exact-search-panel-controls-and-row-targets) | exact Search panel controls and row targets |

**Audio and video** — [audio-and-video.md](original-internals/audio-and-video.md)

| ID | Finding |
|---|---|
| [BIN-MUSIC-001](original-internals/audio-and-video.md#bin-music-001---cd-track-programs-and-lifecycle) | CD track programs and lifecycle |
| [BIN-SOUND-001](original-internals/audio-and-video.md#bin-sound-001---effect-slots-volume-and-setup-cues) | effect slots, volume and setup cues |
| [BIN-SOUND-002](original-internals/audio-and-video.md#bin-sound-002---turn-start-cue-and-effect-interruption) | turn-start cue and effect interruption |
<!-- doc-index:end -->

## Remaining static-analysis queue

The first nine items in the former queue are complete: data-table loading, the
full action-phase dispatcher, PRNG and dice reduction, core resolvers, all
scenarios, city/HQ generation, and every live AI family now have address-level
findings and linked implementation/parity notes. The native PX loader, header
repair, indexed palette path, opaque/stretch, pattern, exact-white key, and
proven mixed-copy resource handling are also closed. A smaller semantic
rectangle/UI tail remains below.

Useful static work which remains is narrower:

1. Resolve remaining semantic PX rectangle roles where callers can distinguish
   them; the loader, palette conversion, copy modes, color keys, and embedded
   pattern masks are closed.
2. Continue exact UI geometry/hit-map work where it can be derived from draw and
   pointer call arguments for remaining panels. Full local-setup card, arrow,
   name, drag-threshold, token, clamp, and drop geometry is closed.
3. Match the linker/runtime fingerprints against a known compiler signature only
   if this becomes useful to interpret generated-code artifacts; it is not a
   gameplay-parity dependency.

The Equip affordability predicate and picker are closed statically in
`BIN-EQUIP-006`. Runtime boundary captures remain useful corroboration under
[RULE-EQUIP-001](GAME-RULES.md#rule-equip-001--purchase-and-equip).

The fixed original save/load envelope is closed in `BIN-API-003`. Every live AI
family, including family 1's unavailable-command policy and family 11's late
guards, is closed statically; reopen those areas only if a new contradiction
appears. The complete soundtrack-selector inventory also closes static
menu/program restart boundaries; remaining playback checks require native
runtime evidence.

Runtime captures listed elsewhere are corroboration work and deliberately are
not included in this static queue. Every completed static item must add its
address-level findings to the matching subsystem document, focused regression
coverage where behavior changes, and a linked parity-matrix update.
