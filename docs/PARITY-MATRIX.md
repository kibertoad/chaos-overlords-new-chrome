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
| City | 8x8 sectors, three sites each | Recovered 32x32 density field, exact income/tolerance rounding, uniform 0..20 proposal draws, staged duplicate/stat-balance rejection, and Armageddon exclusions | Static executable control flow, High; fixed recreation seed vectors protect call order, but original runtime fixture remains pending | Capture an original initial-city fixture and recover initial seed/setup context |
| Players | Six participating slots in local games; omitted local slots become Computers on Begin | Fresh-match factory completes omitted slots in ascending order with bounded unique portrait 0..14 draws, resource names, Computer controllers, and pre-AI/city RNG ordering | Static setup state machine `0x0040e0a0`, High; fixed recreation vector, runtime fixture pending | Capture an original setup fixture and complete setup-screen hit/alignment validation |
| Gang capacity | 80 per player, six friendly per sector | Implemented | Manual, High | Boundary reference fixture |
| Gang identity | Stable gang instance identity, owner, force, sector, equipment and command projection | Headless schema implemented | Recreation invariant, not an original-format claim | Correlate IDs with save slots |
| Command queue | One action per gang, replace/cancel/repeat | Structured validation, typed mutation results, ordered queue events, and terminal repeat cleanup implemented; completed Control/Influence/Research/Heal and other exhausted targets no longer remain assigned | Manual supports queued/repeat actions; replacement and exact terminal-action set Low | Binary ordering, cost rules, terminal-repeat set and resolution fixtures |
| Determinism state | Explicit RNG state/consumption and phase hashes | Recovered VS98 raw step and three-sample range wrapper, serializable state, canonical SHA-256 encoding | Static binary addresses/constants, High algorithm; seed/call order Low | Locate seeding and validate rolls/phase hashes against reference fixtures |
| Notifications | Bounded per-player ordered queues | Mechanical notification schema and deterministic overflow implemented | Recreation safety bound; original capacity/overflow unknown, Low | Recover original queue layout, capacity and delivery order |
| Starting state | Right Hands in controlled Headquarters sector; seven zero-difficulty technologies start researched; Armageddon starts with $500 and all items researched | Explicit-layout bootstrap plus recovered six-candidate HQ permutation, Force-10 Right Hands, $20 ordinary cash, exact `SMGFUNDAGE` $1,500 override, post-HQ exact `SMGISLANDS` neutral Chaos 100 override, and deferred fixed hire slots | Static executable setup path and decoded tables, High for stated placement/values; fixed recreation vectors, runtime fixture pending | Capture an original initial-state fixture |
| Hire pool | Three fixed offers; one mutually exclusive hire-or-snub action; same-slot replacement next planning turn; new gangs start at 5–9 Force | Fixed authoritative slots, exact replacement/retarget/Reject toggle state machine, deferred resolution-time payment, recovered failure/RNG order, in-place tombstones, delayed ascending-slot refill, rejection sampling, exact `SMGMILK` Force-10/no-RNG modifier, UI projection, hashes, saves and replays implemented | Manual page 17 plus executable arrays/resolver/refill/UI/name-cheat paths, High | Runtime corroboration |
| Cash/upkeep | Sector/site income then active-gang upkeep; negative balances restrict purchases, Bribe, Snitch, and paid hires | Ordered Upkeep-phase resolver persists debt; runtime command restrictions, zero-cost hire exception, component events, notifications and hashes implemented | Manual formula/restrictions High; phase boundary/desertion modifiers Medium/Low | Reference debt, desertion and multi-turn fixtures |
| Sector control | Grouped Force + Control minus income, defenders and site Support; zero margin has a 50% chance; unique highest group wins a neutral conflict; overthrow resets influence | Phase-snapshot neutral conflict resolver, deterministic recorded zero-margin draw, ownership/site reset, Support and Overthrow accounting implemented | Manual equation/zero chance/unique-highest rule High; equal-tie and owned-sector ordering Low | Equal-margin and owned-sector cross-player conflicts, income-definition and crackdown fixtures |
| Influence | Cooperative Force + Influence versus site resistance; ownership and local benefits at zero | Same-player grouped Instant resolver, persistent resistance, ownership/Support, shared local stat modifiers, notifications and hashes implemented | Manual base formula/local scope High; pooling Medium; conflict/benefit timing Low | Cross-player ordering, takeover and special-building fixtures |
| Chaos | Per-turn Force + Chaos + sector Income; controlled/uncontrolled payout; sector-wide crackdown suppression; negative effective tolerance auto-busts | Upkeep reset, phase-wide grouped resolver, cash/statistics, commandless negative-tolerance crackdown state and broadcast notifications implemented | Manual reset/base formula/auto-bust High; pooling Medium; accumulation/rounding/order Low | Controlled binary experiments at tolerance boundaries and police aftermath |
| Bribe | $5, tolerance +3, max 40 | Instant-phase resolver, cash statistic, ordered result and notification implemented | Manual formula table, High for formula; failure behavior Low | Binary timing and insufficient-cash edge cases |
| Snitch/tolerance | Free, base tolerance -3/min 0; site modifiers may exceed base bounds; each turn moves one point toward income/site-modified normal | Instant resolver, immediate influence modifiers, base-cap separation, and deterministic Upkeep normalization implemented | Manual, High for formulas; exact normalization boundary Medium | Binary timing, influence-loss boundary and golden-save fixtures |
| Heal | Base four dice plus effective Heal, success restores force to max 10 | Instant-phase resolver with deterministic rolls/results implemented; assignment at full Force is blocked and a repeating order clears at maximum | Manual formula Medium/High; recovered RNG algorithm High; seed/context Low | Reference fixture for dice pool, equipment/site scope, terminal repeat behavior and RNG order |
| Hide/detect | Hidden state, individual attack evasion and cooperative sector visibility | Hide lifecycle, recovered d20 evasion threshold (`Stealth + 14 - Detect` for bands 0/1, `Stealth + 10 - Detect` for band 2), no retaliation on an evaded hit, detection-band aggregation and visibility query implemented | Static binary formulas High; reveal timing and police edges Low | Fixed band-boundary, reveal-timing and police fixtures |
| Combat | Force + class-modified Combat − Defense dice; simultaneous damage and halved retaliation; Martial Arts exception | Phase-wide snapshot resolver coalesces reciprocal orders into one opening attack and one retaliation; recovered 0/1/2 bands drive defender Defense reduction, 6+/5+/4+ opening thresholds, positive-pool minimum damage, exact Hide/weapon/Martial-Arts retaliation eligibility, and 5+/5+/4+ halved retaliation; elimination, equipment loss, statistics, events and notifications implemented; police combat remains separate | Static binary formulas and retaliation gate High; supplied reciprocal-combat observation Medium; remaining order/reveal edges Low | Fixed formula/RNG matrix and police combat |
| Movement | Eight-neighbor (including diagonal) sector move with six-friendly-gang capacity | Ordered resolver, submission/runtime capacity checks, drag-to-neighbor planning, events and notifications implemented | Manual and supplied reference behavior, High for adjacency/capacity; collision ordering Low | Simultaneous swap/final-slot fixtures |
| Equipment | One weapon/armor/misc; separate melee/ranged browsing; research/tech gates; purchase, transfer, replacement loss, half-price sale | Equip/Give/Sell Transaction resolvers, typed slots, cash/statistics, validation, events and notifications implemented; influenced local Factory applies 30% purchase discount | Manual formulas/discount value and decoded category/unlock data High; replacement/swap and Factory locality Medium; discount rounding Low | Reference Factory locality/rounding, multi-item UI, swap and acquisition fixtures |
| Research | Force + Research dice, persistent progress/completion; seven zero-difficulty items initially complete; gang tech and base-5/Science-8/Lab-10 site caps | Instant-phase resolver with deterministic rolls, progress/completion state, initial unlocks, validation, gang/site cap enforcement, result notification and hashes implemented | Manual formula/caps and decoded initial set High; locality/equipment/repeat Medium; recovered RNG algorithm High; seed/context Low | Reference fixtures for cap locality/timing, unlock effects and RNG order; recover initializer code path |
| Police | Combat 20 minus Defense, ordinary stealth curve, hidden Detect 12 branch; 3–5 police Combat phases extended by another Crackdown; third occurrence within five turns neutralizes control; Control prohibited while present | Deterministic post-attack duration countdown/extension and two-entry occurrence history with v4 save migration, control/influence cleanup, displaced-owner notification, visible countdown, phase-wide attacks, hidden/non-hidden detection, damage, events, notifications, casualties, and submission/runtime Control lockout implemented | Manual/save layout High for formulas/duration/history/lockout; duration RNG, message wording and ordering Low | Binary fixtures for timing, RNG consumption, duration draw/extension and three-in-five boundary/notification |
| Player elimination | Remove players with neither a sector nor an active gang; Eliminate removes a player with no living Right Hands | End-of-turn status transition, Eliminate gang/sector/influence cleanup, events and notifications implemented | Manual, High for triggers/effects; cleanup ordering Medium | Binary multi-player elimination/timing fixture |
| Objectives | Ten named scenarios and objective thresholds | Provisional live end-of-turn evaluation, explicit Siege-important sector state, Big Man center-sector point accrual, Eliminate cleanup, authoritative projection, tied winners, events/notifications and outcome hashing | Manual pages 12–15, High for thresholds/weights/effects; timing and special edges Low | Siege setup/visual mapping and binary boundary/tie/ordering fixtures |
| Timers | 26/52/104/208 turns | Implemented as model | Manual, High | End-turn boundary fixtures |
| Endgame | Ranking, five awards, statistics | Timed score standings with competition ties; all five award projections, Hide count, retaliation exclusion and outcome/event/hash integration; routed summary renders on mapped PX00200 frame | Manual pages 46–47 and scoring tables, High mappings; presentation/tie order/zero threshold Low | Recover objective ranking; map exact awards/stats layouts; binary tie/no-award and golden-screen fixtures |
| AI | Objective/difficulty-aware computer players | Deterministic objective-aware planner with recovered reaction/attitude initialization and updates, all nine difficulty-band consumers, role/family assignment, action history, hiring and placement, every dispatcher family handler, and all family-1 continuations. Six-by-81 polymorphic family-2/7 focus/family-11 formation values and family-6 coverage sectors preserve inactive family records and are hashed/saved/replayed. Family 2 uses exact ten-scenario competition standings for mode-6 leader routing. No recovered dispatcher family falls through to provisional scoring; recovered dispatch, shared operations, and recreation-native fallback scoring have separate partials. | Strategic-state, family-handler, persistence, and resolution-band integration High; original-runtime fixture breadth and complete outer planning parity Low | Add fixed original-runtime family traces and larger-player stress cases |
| Original saves | Two historically documented magic/size variants | Explicit non-goal: no import or export support | Deliberate product boundary; historical notes retained for state research | None; exclusion is final |
| Native saves | Versioned safe recreation format | Version 16 snapshots preserve fixed hire slots/action identity, payment and maximum-Force state, AI placement anchors, action/target history, equipment cooldowns, family-2/7 focus/family-11 formation values, family-6 coverage sectors, first-planning flags, mentality, portraits, attitudes/reactions, family/role state, sector income, Crackdown state, and all authoritative runtime state; deterministic encoding, bounds, atomic replacement, backup recovery, F5/F9 quick saves, autosaves, and reusable migration machinery implemented | Recreation format; deterministic continuation and migration tests, High; pre-1.0 compatibility is not guaranteed | Keep current-format safety and reusable migration machinery; enforce migration or documented rejection after 1.0.0 |
| Replays | Deterministic recreation playback | Version 17 records a v16 initial snapshot plus every public match mutation, AI planning/hiring preparation, validation result, and resulting state hash. The bounded loader rejects divergence and out-of-band mutation; full-turn/client F6/F10 record/playback and development-schema migration coverage are implemented. | Recreation format; current full-turn deterministic and file-store tests, High; pre-1.0 compatibility is not guaranteed | Animated playback controls and post-1.0 compatibility policy enforcement |
| City/Sector UI | Original screen/panel workflow | Routed title/setup/handoff/city/sector/gang/finance/ranking/items/Give/combat-summary/search/commands/hire/events/endgame flow starts authoritative recovered cities; queued reports automatically open the paged `PX05010` Last Turn Events panel after handoff using native `PX060xx` illustrations, and Combat Summary uses paged `PX05012` results; whole-city and detailed-sector maps both mark every active-gang sector; the detailed-sector screen preserves the portrait strip, live right-console routes, 3-by-3 neighborhood, coordinate frames, status overlays and three buildings; gang cards have assigned-action strips, stacked repeat marks and green/red Force tracks, stationary double-click opens details, and drag-to-neighbor queues Move; Hire uses the original split price/reject footers and opens `PX05000`; Equip/Research use four category tabs on `PX05004`/`PX05007` and share `PX05001` Item Information; `PX05002` shows site details from sector/Influence; Influence uses `PX05005`; Attack uses `PX05003`; resolved combat uses the original `PX05014` sector/combatant/equipment/Force layout with recovered animation frames; all mutations are replay-recorded | Static executable evidence High for generator and mapped atlas identities; supplied panel captures High for workflow and Medium for exact hit edges; remaining interaction/coordinates Medium-Low | Complete setup alignment and golden screens |
| Input | Original mouse plus modern keyboard | Letterboxed virtual-coordinate mouse hit testing and keyboard navigation implemented; city sectors select on single-click and open detail on bounded same-sector double-click; Hire portraits drag as scaled tokens through controlled-sector validation from both city and detailed-sector modes, with a detailed-workspace drop targeting its centered sector; detailed gang portraits use movement-threshold disambiguation so stationary double-click opens details while drag/drop queues only validator-legal neighboring Move targets | Current client and original Hire/sector captures, Medium-High | Remaining original hit maps, right-click/cancel and configurable bindings |
| Audio | 28 WAV resources and triggers | All WAVs extracted; resolved equipped-weapon attacks retain and route the event-time item Sound index to `SND00500`-`SND00518` without replaying historical load/replay events or losing the cue when elimination clears equipment | File/table inspection High for format and item field; exact trigger/overlap mapping Medium-Low | Map bare-hand, command, UI, police, impact and notification cues; validate overlap/interruption |
| Combat animation | Item-selected attack/hit sequences, retaliation, evasion and police attacks | All 98 `PX07xxx` strips decoded as eight 64x64 frames; event-time weapons route normal and mirrored attack/hit pairs into `PX05014`, including question/evasion and police-car/beam extras; queued playback holds presentation input without exposing simulation phases | Asset dimensions/frame inspection, item table and original Combat capture High; exact cadence/color key Medium | Validate cadence, compositing/color key and bare-hand style selection against executable captures |
| Music | Original CD-audio soundtrack, represented by eight GOG Ogg tracks (`Track02`-`Track09`) | Recovered screen programs are wired: title/setup repeats Track 2, gameplay advances Tracks 3-8 then repeats, and endgame repeats Track 9; losing focus pauses and regaining focus resumes; an Options overlay applies the recovered level-5 default and exact 0-10 volume conversion and safely persists the recreation preference; missing or unreadable music disables itself without affecting gameplay | GOG pack and static MCI/aux-volume call graph, High for track ranges/order/repeat/focus/default-volume behavior; exact original menu restart and preference-persistence boundaries Medium | Capture original menu restart/persistence boundaries and validate native playback on Windows, Linux, and macOS |
| Video | Two Smacker v2 movies | Extracted only | Signature, Verified container | Playback/transcode decision |
| Hot-seat | Multiple humans on one machine | One-to-six local players with original PX00132 ready/handoff screen at active-player transitions | Manual/current rotation, Medium | Reference timing, private panel state and complete original turn workflow |
| Legacy network | WinSock/IPX/modem/serial/Mac transports | Explicit non-goal: no port, protocol interoperability, or production exposure | Deliberate security/product exclusion | None; exclusion is final |
| Modern network | Safe deterministic command transport | Not applicable until post-parity | Roadmap decision | Threat model after M8 |

The AI row includes all three handler-exact family-1 continuations in the live
planner: previous None/Chaos selects Heal, Move, or Chaos; previous Heal repeats
Heal or selects strict solo Control, then Move; and prior Control/Equip/Snitch
uses the recovered equipment gate before its crime-or-Move continuation.
Replay-recorded preparation uses exact item or mode-5 Move targets. The shared
selector also implements bounded objective modes 12–15, including nearest-ring
search, objective ownership/capacity filters, compounded hostile-human weight,
maximum-tie RNG, and x-then-y routing. The family-13/14 off-objective terminal
Move handlers are live, along with family 14's prior-Control Heal and family-13
transition on an objective. Both families' owned-objective Heal branch is also
live when no visible opponent is present. Their contested-objective parity,
visible-target pools, three-draw retry predicate, exact Attack target, and
Heal/Control fallbacks are live and replay-verified. On-objective weapon/armor
equipment and cooldowns, maximum-Chaos miscellaneous Equip, highest-Support
unfinished-site Influence, and the five-draw owned-visible Attack branch are
also live. Static inspection confirms neither handler assigns Research.
Only unavailable-command fallback remains
provisional for the live family-1 paths.

Family 3 now uses its complete recovered handler. Cash-site decisions after
previous None, Control, Equip, Heal, Influence, and Snitch include the
Force-8/effective-Heal-`-3` boundary, first-strict-maximum local Cash site,
previous-site retention, equipment gate/cooldowns, strict solo Control, and
mode-8 movement using summed unfinished positive Cash. After Attack, Hide, or
Move, the visible-opponent branch preserves the original human-pool target and
full-pool comparison-ordinal asymmetry. The terminal path applies the recovered
three-Move family transition and final-three-turn Greed Terminate override.
These decisions, targets, family changes, and RNG draws are replay-recorded.

Family 5 uses the same complete branch order for Support-focused Influence.
Mode 7 sums positive Support across unfinished sites in owned sectors and rejects
a destination when any same-sector gang record has previous Influence. Local
selection keeps the first strict maximum positive-Support unfinished site; the
remaining Heal, Equip, Control, Move, opponent, family-transition, Terminate,
and intentional None paths match the recovered family-3 shape and are live.

Family 9 is live with weapon-before-armor equipment, no existing-cooldown gate,
and cost-times-three replacement cooldowns. Without equipment it uses mode 3
to leave owned territory, attacks a weight-10 visible opponent through the
five-draw asymmetric target/comparison loop, moves after a previous Control,
and otherwise Controls in non-owned territory. Commands, targets, RNG, and
replay behavior are covered.

Family 10 is live with its strict-Defense armor upgrade and literal cooldown 2,
special researched Smoke Bombs Equip, Force-10/no-visible-opponent Heal gate,
and completed-positive-Stealth mode-9 routing. The handler preserves the
original's separate probe and destination calls, including two independent tie
draws, then chooses Chaos or Hide according to same-sector previous Chaos.

Family 12 is live with its no-visible-opponent weapon/armor/miscellaneous
priority, raw-cost weapon/armor cooldowns, Force-10 Heal gate, and encoded
current-sector movement. That encoded mode is deliberately cleared by the
shared selector's source exclusion and therefore exercises its all-zero random
tie and one-step route. The visible-opponent branch preserves the human-pool/
full-pool ordinal asymmetry, retries the combat comparison up to five times,
and still attacks its final selected target after five failures. Its exact
targets, RNG consumption, replay behavior, and final-three-turn Greed override
are covered.

## Current blockers to parity claims

- The raw RNG and bounded wrapper match recovered binary control flow, but
  initial seeding and complete call order remain unknown.
- City generation and HQ placement match the verified static path, including
  their internal RNG ordering, but still lack an original runtime seed fixture.
  Empty local slots now consume recovered unique portrait/name draws before AI
  initialization and city generation; the complete path still needs an original
  runtime fixture.
- Hire slot selection and refill ordering match the recovered static control
  flow, including action replacement/toggling and resolution-time payment, but
  runtime corroboration and initial RNG seeding/call order remain pending;
  recovered persistent-anchor AI placement is wired while actual placement is
  deferred to the Hire phase.
- The client now drives the authoritative phase coordinator and replay recorder,
  but several original management workflows, hit regions, and visual layers are
  still incomplete or only provisionally mapped.
- Command replacement gets a new sequence number and resolution otherwise uses
  submission order inside a subphase; both are provisional recreation rules,
  not binary-validated behavior.
- AI Mentality's global, query selector, reaction/attitude state, sector Combat
  + Defense advantage hostility pass, and all nine resolution-band consumers
  are recovered and integrated; family 1's post-equipment equipment/cooldown/
  nearby-danger gate and owner/cash/Mentality/Tolerance continuation are live,
  including exact Equip and Move targets. Exact remaining-family planner
  outcomes and scoring weights remain incomplete. Combat ordering/reveal
  timing, police edges, equipment,
  objective timing, and audiovisual triggers retain the
  specific parity gaps listed above.

These blockers prevent describing the current playable slice as a faithful
gameplay recreation even though its decoded tables and asset pack are verified.

## Binary evidence status

Initial PE/import/string classification and continuing address-level gameplay
research are recorded in `ORIGINAL-INTERNALS.md`. Verified or high-confidence
findings now cover platform boundaries, source resource paths, save/version
strings, the original RNG step and bounded wrapper, city/site/HQ/hire setup,
and the outer AI command dispatcher. Exact AI planner policy, remaining resolver
ordering/reveal edges, and reference-trace parity retain their explicitly
listed gaps.
