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
| Action IDs | None plus 14 original commands | Data-driven target/relationship validation and typed queue implemented; resolution pending | Table/save notes + manual, High; queue tie-break Low | Add reference-derived costs and resolve each command |
| City | 8x8 sectors, three sites each | Provisional | Manual/data, High structure; Low algorithm | Match initial reference city |
| Players | Six slots | Implemented | Data/save/manual, High | Setup permutations |
| Gang capacity | 80 per player, six friendly per sector | Implemented | Manual, High | Boundary reference fixture |
| Gang identity | Stable gang instance identity, owner, force, sector, equipment and command projection | Headless schema implemented | Recreation invariant, not an original-format claim | Correlate IDs with save slots |
| Command queue | One action per gang, replace/cancel/repeat | Structured validation, typed mutation results, and ordered queue events implemented | Manual supports queued/repeat actions; replacement ordering Low | Binary ordering, cost rules and resolution fixtures |
| Determinism state | Explicit RNG state/consumption and phase hashes | Recovered VS98 raw step and three-sample range wrapper, serializable state, canonical SHA-256 encoding | Static binary addresses/constants, High algorithm; seed/call order Low | Locate seeding and validate rolls/phase hashes against reference fixtures |
| Notifications | Bounded per-player ordered queues | Mechanical notification schema and deterministic overflow implemented | Recreation safety bound; original capacity/overflow unknown, Low | Recover original queue layout, capacity and delivery order |
| Starting state | Right Hands in controlled sector | Documented | Manual, High | Implement exact setup |
| Hire pool | Three distinct offers; placement in Hire phase | Provisional | Manual, High | Deferred-hire fixture |
| Cash/upkeep | Sector/site income then active-gang upkeep with zero floor | Ordered Upkeep-phase resolver, component events, notifications and hashes implemented | Manual formula High; phase boundary/desertion modifiers Medium/Low | Reference zero-cash and multi-turn fixtures |
| Sector control | Force + Control contest | Provisional | Manual formula, Medium | Neutral/enemy/tie fixtures |
| Influence | Force + Influence versus site resistance | Documented | Manual, High | Implement and diff saves |
| Chaos | Income/chaos/crackdown effects | Documented | Manual, Medium | Controlled roll experiment |
| Bribe | $3, tolerance +5, max 40 | Instant-phase resolver, cash statistic, ordered result and notification implemented | Manual, High for formula; failure behavior Low | Binary timing and insufficient-cash edge cases |
| Snitch | Free, tolerance -3, min 0 | Instant-phase resolver and ordered result/notification implemented | Manual, High for formula; automatic tolerance behavior Low | Binary timing and uninfluenced-site edge cases |
| Heal | Base four dice plus effective Heal, success restores force to max 10 | Instant-phase resolver with deterministic rolls/results implemented | Manual formula Medium/High; recovered RNG algorithm High; seed/context Low | Reference fixture for dice pool, equipment/site scope and RNG order |
| Hide/detect | Stealth probability and cooperative detect | Documented | Manual, Medium | Distribution fixtures |
| Combat | Simultaneous attack, defense, damage and retaliation | Documented | Manual, Medium | Binary formula matrix |
| Movement | Adjacent-sector move | Documented | Manual, High | Simultaneous collision ordering |
| Equipment | One weapon/armor/misc, tech gated | Documented | Manual/tables, High | Inventory model and fixtures |
| Research | Force + Research progress, tech/site caps | Documented | Manual/tables, High | Full tech-tree fixture |
| Police | Combat 20, Detect 12, stealth detection curve | Implemented as pure rules | Manual, High | Wire crackdown; exact aftermath |
| Objectives | Ten named scenarios and objective thresholds | Implemented as model | Manual, High | Wire match setup; binary edge/tie fixtures |
| Timers | 26/52/104/208 turns | Implemented as model | Manual, High | End-turn boundary fixtures |
| Endgame | Ranking, five awards, statistics | Documented | Manual, High | Implement ties/no-award cases |
| AI | Objective/difficulty-aware computer players | Unknown | Binary research required, Low | Decision snapshots |
| Save import | Two known magic/size variants | Documented | Reverse-engineering notes, Medium | Obtain and parse corpus |
| Native saves | Versioned safe recreation format | Unknown | Design required | Snapshot/replay schema |
| City/Sector UI | Original screen/panel workflow | Provisional | Manual/PX assets, Medium | UI atlas and golden screens |
| Input | Original mouse plus modern keyboard | Provisional | Manual/current client | Full hit-map/navigation |
| Audio | 28 WAV resources and triggers | Extracted only | File inspection, High format | Resource-to-event map |
| Music | Eight Ogg tracks | Extracted only | GOG pack, High format | Track sequencing behavior |
| Video | Two Smacker v2 movies | Extracted only | Signature, Verified container | Playback/transcode decision |
| Hot-seat | Multiple humans on one machine | Provisional | Manual/current rotation | Hidden handoff/full turns |
| Legacy network | WinSock/IPX/modem/serial/Mac transports | Intentional deviation candidate | Manual, High | Document protocol; keep disabled |
| Modern network | Safe deterministic command transport | Not applicable until post-parity | Roadmap decision | Threat model after M8 |

## Current blockers to parity claims

- The headless raw RNG and bounded wrapper match recovered binary control flow,
  but initial seeding and complete call order remain unknown; the prototype
  `System.Random` does not match the recovered algorithm.
- City generation selects sites uniformly instead of using verified frequency
  and class rules.
- Hiring happens immediately and uses provisional price selection.
- Control spends an invented cash cost and assigns ownership directly.
- The core exposes a headless match schema, data-driven command validation,
  deterministic command and notification queues, ordered events, phase hashes,
  and a phase coordinator, but the current client still uses its older direct
  per-player turn controls.
- Command replacement gets a new sequence number and resolution otherwise uses
  submission order inside a subphase; both are provisional recreation rules,
  not binary-validated behavior.
- AI, research, equipment, combat, police, scenarios, victory and saves are not
  implemented.

These blockers prevent describing the current playable slice as a faithful
gameplay recreation even though its decoded tables and asset pack are verified.

## Binary evidence status

Initial PE/import/string classification is recorded in `ORIGINAL-INTERNALS.md`.
It verifies the native x86/Win32 platform boundaries, source resource paths,
save/version strings, DirectDraw/GDI rendering, WINMM/Smacker media, and legacy
WinSock/TAPI/serial dependencies. No gameplay formula or RNG interpretation has
yet reached binary-verified status.
