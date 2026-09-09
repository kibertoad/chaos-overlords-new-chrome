# Gameplay and feature parity matrix

Status values are defined in `IMPLEMENTATION-PLAN.md`. `Manual` confidence means
the intended behavior has been inspected but not yet confirmed in the binary.

| Area | Original requirement | Recreation status | Evidence/confidence | Next parity gate |
|---|---|---|---|---|
| Source pack | Exact supported DATA/HELP/MUSIC set | Implemented | Full SHA-256, Verified | Add second-source-version test |
| Output pack | 685 outputs from 471 original resources | Implemented | Transactional promotion plus per-file size/SHA-256, Verified | Add stale-install cleanup |
| PX16 | Repair four missing BMP fields as RGB555 | Implemented | Payload arithmetic plus 12,065,806 paired-pixel comparison, High | Verify transparency/color keys in reference rendering |
| PX08 | 207 RLE8 and 7 uncompressed indexed graphics | Implemented | Strict decoder, full 214-file extraction and payload checks; High | Compare palettes/pixels with PX16 and reference rendering |
| Tables | 22 sites, 90 gangs, 64 items | Parity verified (values only) | Source/payload hashes, Verified | Semantic field-use fixtures |
| Turn phases | Upkeep, Command, Execution, Hire, Elimination | Headless coordinator implemented | Manual, High; player iteration provisional | Binary dispatcher confirmation |
| Execution phases | Instant, Combat, Transaction, Chaos, Movement, Control | Headless coordinator implemented | Manual, High | Binary within-phase ordering |
| Action IDs | None plus 14 original commands | Data-driven validation, typed queue, and headless resolution implemented for every command | Table/save notes + manual, High; resolver edge cases/order Low | Binary fixtures for every command and reference-derived edge costs |
| City | 8x8 sectors, three sites each | Provisional | Manual/data, High structure; Low algorithm | Match initial reference city |
| Players | Six slots | Implemented | Data/save/manual, High | Setup permutations |
| Gang capacity | 80 per player, six friendly per sector | Implemented | Manual, High | Boundary reference fixture |
| Gang identity | Stable gang instance identity, owner, force, sector, equipment and command projection | Headless schema implemented | Recreation invariant, not an original-format claim | Correlate IDs with save slots |
| Command queue | One action per gang, replace/cancel/repeat | Structured validation, typed mutation results, and ordered queue events implemented | Manual supports queued/repeat actions; replacement ordering Low | Binary ordering, cost rules and resolution fixtures |
| Determinism state | Explicit RNG state/consumption and phase hashes | Recovered VS98 raw step and three-sample range wrapper, serializable state, canonical SHA-256 encoding | Static binary addresses/constants, High algorithm; seed/call order Low | Locate seeding and validate rolls/phase hashes against reference fixtures |
| Notifications | Bounded per-player ordered queues | Mechanical notification schema and deterministic overflow implemented | Recreation safety bound; original capacity/overflow unknown, Low | Recover original queue layout, capacity and delivery order |
| Starting state | Right Hands in controlled Headquarters sector; Armageddon starts with $500 and all items researched | Explicit-layout bootstrap implements ownership, stable Right Hands creation and Armageddon overrides without mutating its inputs | Manual, High for stated lifecycle/modifiers; placement/Force Low | Recover exact city, placement, ordinary cash/Force and initial hire-pool algorithms |
| Hire pool | Three distinct offers; one offer may be rejected per turn and is replaced next turn; new gangs start at 5–9 Force | Deferred purchase/placement, one-offer snubbing, initial Force roll, deterministic vacancy refill, events and notifications implemented | Manual page 17, High for lifecycle/caps/Force range; refill and RNG ordering Low | Binary offer-selection, refill, Force-distribution and call-order fixtures |
| Cash/upkeep | Sector/site income then active-gang upkeep; negative balances restrict purchases, Bribe, Snitch, and paid hires | Ordered Upkeep-phase resolver persists debt; runtime command restrictions, zero-cost hire exception, component events, notifications and hashes implemented | Manual formula/restrictions High; phase boundary/desertion modifiers Medium/Low | Reference debt, desertion and multi-turn fixtures |
| Sector control | Grouped Force + Control minus income, defenders and site Support; overthrow resets influence | Non-dice grouped resolver, ownership/site reset, Support and Overthrow accounting implemented | Manual equation High; positive threshold Medium; simultaneous ordering Low | Cross-player/tie, income-definition and crackdown fixtures |
| Influence | Cooperative Force + Influence versus site resistance; ownership and local benefits at zero | Same-player grouped Instant resolver, persistent resistance, ownership/Support, shared local stat modifiers, notifications and hashes implemented | Manual base formula/local scope High; pooling Medium; conflict/benefit timing Low | Cross-player ordering, takeover and special-building fixtures |
| Chaos | Force + Chaos + sector Income; controlled/uncontrolled payout; sector-wide crackdown suppression | Phase-wide grouped resolver, cash/statistics, persisted Chaos, crackdown state and broadcast notifications implemented | Manual base formula High; pooling Medium; accumulation/rounding/order Low | Controlled binary experiments at tolerance boundaries and police aftermath |
| Bribe | $5, tolerance +3, max 40 | Instant-phase resolver, cash statistic, ordered result and notification implemented | Manual formula table, High for formula; failure behavior Low | Binary timing and insufficient-cash edge cases |
| Snitch/tolerance | Free, tolerance -3, min 0; each turn moves one point toward income/site-modified normal | Instant resolver plus deterministic Upkeep normalization implemented | Manual, High for formulas; exact normalization boundary Medium | Binary timing, influence-loss boundary and golden-save fixtures |
| Heal | Base four dice plus effective Heal, success restores force to max 10 | Instant-phase resolver with deterministic rolls/results implemented | Manual formula Medium/High; recovered RNG algorithm High; seed/context Low | Reference fixture for dice pool, equipment/site scope and RNG order |
| Hide/detect | Hidden state, individual attack evasion and cooperative sector visibility | Hide lifecycle, percentage attack check, no retaliation on hidden hit, detection-band aggregation and visibility query implemented | Manual formulas High/Medium; negative/timing edges Low | Binary probability distribution, reveal timing and police fixtures |
| Combat | Force + class-modified Combat − Defense dice; simultaneous damage and halved retaliation; Martial Arts exception | Phase-wide snapshot resolver, elimination, equipment loss, statistics, events and notifications implemented; detection/police excluded | Manual and contemporary FAQ, Medium/High; binary ordering Low | Binary formula/RNG matrix, detection and police combat |
| Movement | Adjacent-sector move with six-friendly-gang capacity | Ordered resolver, submission/runtime capacity checks, events and notifications implemented | Manual adjacency/capacity High; collision ordering Low | Simultaneous swap/final-slot fixtures |
| Equipment | One weapon/armor/misc; research/tech gates; purchase, transfer, replacement loss, half-price sale | Equip/Give/Sell Transaction resolvers, typed slots, cash/statistics, validation, events and notifications implemented; influenced local Factory applies 30% purchase discount | Manual formulas/discount value High; replacement/swap and Factory locality Medium; discount rounding Low | Reference Factory locality/rounding, multi-item UI, swap and acquisition fixtures |
| Research | Force + Research dice, persistent progress/completion; gang tech and base-5/Science-8/Lab-10 site caps | Instant-phase resolver with deterministic rolls, progress/completion state, validation, gang/site cap enforcement, result notification and hashes implemented | Manual formula/caps High; locality/equipment/repeat Medium; recovered RNG algorithm High; seed/context Low | Reference fixtures for cap locality/timing, unlock effects, zero-difficulty items and RNG order |
| Police | Combat 20, Detect 12, stealth detection curve | Provisional phase-wide crackdown attacks, detection, damage, events, notifications and casualties implemented | Manual, High for constants/curve; defense formula and ordering Low | Binary fixtures for timing, defense/damage formula, RNG consumption, duration and aftermath |
| Player elimination | Remove players with neither a sector nor an active gang; Eliminate removes a player with no living Right Hands | End-of-turn status transition, Eliminate gang/sector/influence cleanup, events and notifications implemented | Manual, High for triggers/effects; cleanup ordering Medium | Binary multi-player elimination/timing fixture |
| Objectives | Ten named scenarios and objective thresholds | Provisional live end-of-turn evaluation, explicit Siege-important sector state, Big Man center-sector point accrual, Eliminate cleanup, authoritative projection, tied winners, events/notifications and outcome hashing | Manual pages 12–15, High for thresholds/weights/effects; timing and special edges Low | Siege setup/visual mapping and binary boundary/tie/ordering fixtures |
| Timers | 26/52/104/208 turns | Implemented as model | Manual, High | End-turn boundary fixtures |
| Endgame | Ranking, five awards, statistics | Timed score standings with competition ties; all five award projections, Hide count, retaliation exclusion and outcome/event/hash integration; routed summary renders on mapped PX00200 frame | Manual pages 46–47 and scoring tables, High mappings; presentation/tie order/zero threshold Low | Recover objective ranking; map exact awards/stats layouts; binary tie/no-award and golden-screen fixtures |
| AI | Objective/difficulty-aware computer players | Deterministic objective-aware baseline plans validator-approved Command actions, respects cooperative attack detection, budgets spending, chooses valid hires, and runs through replay recording; all four timed scenarios complete repeatable tournaments and all six objective scenarios pass deterministic 20-turn windows, every run replay-verified; original difficulty policy unknown | Recreation behavior High; original parity Low | Recover difficulty branches/weights and information limits; add reference decision snapshots, larger-player tournaments and objective-completion stress cases |
| Save import | Two known magic/size variants | Documented | Reverse-engineering notes, Medium | Obtain and parse corpus |
| Native saves | Versioned safe recreation format | Version 2 snapshots preserve explicit sector income and all authoritative runtime state; v1 migration, deterministic encoding, 16 MiB bound, atomic replacement, backup recovery, F5/F9 quick saves and end-turn autosave implemented | Recreation format; deterministic continuation and migration tests, High | Add checked-in v1 fixture |
| Replays | Deterministic recreation playback | Version 2 records a v2 initial snapshot plus every public match mutation, validation result and resulting state hash; bounded loader rejects divergence and out-of-band recording mutations; atomic client F6/F10 record/playback flow implemented | Recreation format; full-turn deterministic and file-store tests, High | Add checked-in fixture/migration and animated playback controls |
| City/Sector UI | Original screen/panel workflow | Routed title/setup/handoff/city/sector/gang/finance/ranking/items/combat-summary/search/commands/hire/events/endgame flow starts authoritative recovered cities; the city composites each sector from original owner-colored layers; sector/hire/gang views use original portrait sheets; finance/ranking use authoritative projections; items queue validated Research/Equip/Sell; Combat Summary presents relevant attack/police results; Search respects cooperative detection; all mutations are replay-recorded | Static executable evidence High for generator and mapped atlas identities; interaction/coordinates Medium-Low | Complete UI atlas/setup detail, richer Give/target workflow, remaining original management panels, seed/context fixture and golden screens |
| Input | Original mouse plus modern keyboard | Letterboxed virtual-coordinate mouse hit testing and keyboard navigation implemented for title/setup/city shell | Current client, Medium | Original hit maps, drag/drop, right-click/cancel and configurable bindings |
| Audio | 28 WAV resources and triggers | All WAVs extracted; resolved equipped-weapon attacks route the item Sound index to `SND00500`-`SND00518` without replaying historical load/replay events | File/table inspection High for format and item field; exact trigger/overlap mapping Medium-Low | Map bare-hand, command, UI, police, impact and notification cues; validate overlap/interruption |
| Music | Eight Ogg tracks | Extracted only | GOG pack, High format | Track sequencing behavior |
| Video | Two Smacker v2 movies | Extracted only | Signature, Verified container | Playback/transcode decision |
| Hot-seat | Multiple humans on one machine | One-to-six local players with original PX00132 ready/handoff screen at active-player transitions | Manual/current rotation, Medium | Reference timing, private panel state and complete original turn workflow |
| Legacy network | WinSock/IPX/modem/serial/Mac transports | Intentional deviation candidate | Manual, High | Document protocol; keep disabled |
| Modern network | Safe deterministic command transport | Not applicable until post-parity | Roadmap decision | Threat model after M8 |

## Current blockers to parity claims

- The raw RNG and bounded wrapper match recovered binary control flow, but
  initial seeding and complete call order remain unknown.
- City generation selects sites uniformly instead of using verified frequency
  and class rules.
- Hire offer selection/refill and initial Force RNG ordering remain provisional,
  although purchase is deferred and resolved in the Hire phase.
- The client now drives the authoritative phase coordinator and replay recorder,
  but several original management workflows, hit regions, and visual layers are
  still incomplete or only provisionally mapped.
- Command replacement gets a new sequence number and resolution otherwise uses
  submission order inside a subphase; both are provisional recreation rules,
  not binary-validated behavior.
- AI difficulty behavior remains unrecovered; combat, crackdown, research,
  equipment, objective timing, original-save import, and audiovisual triggers
  retain the specific parity gaps listed above.

These blockers prevent describing the current playable slice as a faithful
gameplay recreation even though its decoded tables and asset pack are verified.

## Binary evidence status

Initial PE/import/string classification is recorded in `ORIGINAL-INTERNALS.md`.
It verifies the native x86/Win32 platform boundaries, source resource paths,
save/version strings, DirectDraw/GDI rendering, WINMM/Smacker media, and legacy
WinSock/TAPI/serial dependencies. No gameplay formula or RNG interpretation has
yet reached binary-verified status.
