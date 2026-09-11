# Game rules and evidence

Status: partial, active research
Last updated: 2026-09-11

This document separates intended rules stated by the original manual from
behavior verified against the fingerprinted version 1.1 executable. A manual
rule may be implemented provisionally, but it is not behavioral parity until a
controlled reference observation confirms its execution timing and edge cases.

## Evidence sources

- `MANUAL-GOG-1`: 30-page/56-numbered-page image scan, 6,229,841 bytes,
  SHA-256 `bdb1072848df95111cd014faaa7297d016b7c6e55cd7f2658dda67a167a0089d`.
- `EXE-GOG-1.1`: `Chaos Overlords.exe`, version 1.1, 664,576 bytes,
  SHA-256 `a1430159bbe20869e277a5000311344f4ec141ab77c96b385336617149e97d89`.
  No resolver finding below has yet been verified against this executable.

## Hiring

### RULE-HIRE-001 — Offer replacement and starting Force

- Source: `MANUAL-GOG-1`, numbered page 17.
- Observed statement: the Hire panel contains three gangs. Hiring a gang removes
  it from the panel and a replacement appears next turn. The player may instead
  fire one unwanted offer; its replacement likewise appears next turn. A hired
  gang begins with Force from 5 through 9 and may later be healed to 10.
- Interpretation: the panel uses three permanent slots and one mutually
  exclusive action selection: choosing a hire or snub clears any selection on
  the other two slots. Resolution negates the successful hire's or snub's offer
  in place. Its replacement is drawn into that same slot at the player's next
  planning entry, in ascending slot order, not during `FinishHire`. A recruit's
  initial Force is generated uniformly in the inclusive range 5–9 through the
  recovered bounded RNG wrapper. Initial and replacement offers use rejection
  sampling over definition IDs 1 through 89, rejecting other visible offers and
  the offer just removed. A later valid drop replaces or retargets the selection;
  Reject selects another slot, toggles an existing rejection off, or cancels a
  hire on that same slot. Cash is checked and charged only at resolution.
- Exact name modifier: in a fresh local game, the exact uppercase player name
  `SMGMILK` makes every later successful hire start at Force 10 and skips the
  normal three-call Force RNG sequence. Case variants do not match.
- Roster placement: successful resolution scans slots upward and reuses the
  first inactive slot among the original 80 usable gang records. The new gang
  receives a fresh stable match ID, and the reused AI family/action record is
  reset so no prior occupant's planning history leaks into it.
- Current exclusions: controlled runtime corroboration and behavior with
  modified or incomplete definition data remain pending.
- Confidence: High for the range, static call sites, rejection behavior and RNG
  ordering; runtime correlation remains pending.
- Implementation: `MatchState.SnubHireOffer`, `HireRules.ValidateSnub`,
  `HireResolver.Resolve`, and `ManualRules.MinimumHiredGangForce` /
  `MaximumHiredGangForce`.
- Tests: `HireAndEliminationTests` covers fixed middle-slot selection and
  tombstones, next-planning-entry same-slot refill, removed-ID exclusion,
  replacement/toggle actions, deferred payment, the recovered sector/cash/Force/
  global-capacity resolution order, inactive-slot reuse/reset, initial Force
  bounds, `SMGMILK` Force/RNG behavior, events, RNG consumption, and deterministic hashes. Save/replay tests
  cover slot/payment persistence and migration.
- Next experiment: record repeated Hire panels and new-gang Force values from a
  fixed reference save, then correlate offer order and RNG consumption.

## Instant commands

### RULE-HIDE-001 — Enter hidden state

- Source: `MANUAL-GOG-1`; Hide command and Stealth descriptions. Exact manual
  scan page locations still need transcription.
- Observed statement: Hide makes the acting gang hidden so opposing gangs must
  detect it before targeting it.
- Interpretation: the Instant resolver changes the acting gang's `Hidden` flag
  to true without an RNG roll; the state expires at the following Upkeep before
  repeat Hide resolves again. An attack against the hiding gang rolls 1–100
  against `clamp(50 + 5 × (attacker Detect − defender Stealth), 0, 100)` using
  the individual attacker's Detect. Failure means the target evades; success
  proceeds normally but cannot retaliate. Hide does not change whether the gang
  is visible in the sector.
- Current exclusions: binary confirmation of the five-point probability step,
  exact reveal timing, and interactions with police attacks.
- Confidence: High that Hide enters hidden state and successful hits prevent
  retaliation; Medium for the probability formula; Low for timing.
- Implementation: `CommandResolver.ResolveHide`, combat detection handling,
  `ManualRules.HiddenAttackHitPercent`, and `MatchGangState.Hidden`.
- Tests: `InstantResolutionTests.HideMarksGangHiddenAndRecordsTransition` and
  `CombatResolutionTests` evasion/hit cases.
- Next experiment: compare saves and target availability before Hide, directly
  after Instant, and after the hidden gang acts or is detected.

### RULE-INFLUENCE-001 — Cooperative site influence

- Source: `MANUAL-GOG-1`; Influence command, dice, site resistance, and site
  benefit descriptions, plus `BIN-RNG-002`, `BIN-RNG-003`, and
  `BIN-INSTANT-001`.
- Observed statement: participating gangs contribute total Force plus Influence;
  successes reduce site resistance; reaching zero influences the site and grants
  its listed benefits.
- Interpretation: scan player slots and persistent gang slots in ascending
  order. Each participating gang separately rolls
  `max(0, Force + effective Influence)` dice and immediately persists its
  successes against the remaining resistance. At zero, assign the player and
  add the site's Support value; later queued Influence commands skip their roll.
- Current exclusions: cross-player simultaneous contests, already-influenced
  takeovers, cash/tolerance/stat benefit timing, site special behavior, and
  influenced-site modifiers in the dice pool. The recreation currently rejects
  commands against an already-influenced site until takeover rules are verified.
- Confidence: High for the base pool, success threshold, resistance reduction,
  Support value, per-gang scheduling, and completion guard; Low for takeover and
  benefit timing.
- Implementation: `CommandResolver.ResolvePhase`,
  `CommandResolver.ResolveInfluence`, `ManualRules.InfluenceDiceCount`, and
  `ManualRules.ApplyInfluenceProgress`; validation requires player ownership of
  the target sector.
- Tests: `InstantResolutionTests` covers separate roster-ordered rolls,
  cumulative partial progress, post-completion RNG suppression,
  completion/Support, target rejection, notifications, deterministic replay,
  and phase hashes.
- Next experiment: queue one and multiple gangs for the same site, then opposing
  players for the same site, and diff resistance, ownership, Support, RNG, and
  event ordering.

### RULE-HEAL-001 — Heal dice and force restoration

- Source: `MANUAL-GOG-1`; Heal command description, plus `BIN-RNG-002` and
  `BIN-RNG-003` for random-number generation. Exact manual scan page location
  still needs transcription.
- Observed statement: Heal rolls a base four dice plus Heal skill; each roll of
  4-6 restores one Force, capped at 10.
- Interpretation: calculate gang-definition plus equipped-item Heal modifiers,
  roll `max(0, 4 + Heal)` six-sided dice, count results at least four, and add
  successes to Force with a maximum of ten. Heal cannot be assigned at maximum
  Force; a repeating Heal order clears as soon as Force reaches ten.
- Current exclusions: influenced-site and other contextual stat modifiers are
  not applied until their ownership/scope is verified.
- Confidence: High for dice threshold and Force cap; Medium for dice-pool and
  equipment aggregation; High for the recovered RNG step/range wrapper; Low for
  initial seed and complete RNG call order.
- Implementation: `EffectiveStatisticsCalculator`, `DiceRoller`,
  `CommandResolver.ResolveHeal`, and `ManualRules.RestoreForce`.
- Tests: deterministic roll/event/hash replay, effective Heal equipment, dice
  bounds, RNG consumption, and Force cap tests in `CommandResolutionTests`,
  `DeterminismTests`, and `ManualRulesTests`.
- Next experiment: execute Heal from an identical save across base/item/site
  modifiers and correlate visible rolls plus Force deltas with predicted RNG.

### RULE-RESEARCH-001 — Research dice and persistent progress

- Source: `MANUAL-GOG-1`; Research command and item-table descriptions, plus
  `BIN-RNG-002` and `BIN-RNG-003` for random-number generation. Exact manual
  scan page locations still need transcription.
- Observed statement: Research rolls dice equal to the gang's Force plus its
  Research skill; each success reduces the item's remaining research number.
- Interpretation: calculate gang-definition plus equipped-item Research
  modifiers, roll `max(0, Force + Research)` six-sided dice, count results at
  least four, persist unfinished progress, and mark the item researched at zero.
  A repeating Research order clears on completion.
- Tech restrictions: a gang cannot research above its own Tech. Without a local
  influenced special research site the ceiling is Tech 5; an influenced Science
  Center raises it to 8 and a Research Lab to 10. A sector must be controlled
  before its sites can be influenced, and authoritative match state rejects an
  influencer who is not the current sector owner.
- Current exclusions: the exact behavior of zero-difficulty items and runtime
  confirmation of special-site timing.
- Confidence: High for the formula, completion threshold, fixed roster order,
  and suppression of later rolls after same-phase completion; Medium for
  equipment aggregation and repeat-command rejection; Low for initial seed and
  complete RNG call order.
- Implementation: `MatchPlayerState.RemainingResearch`,
  `MatchPlayerState.ApplyResearch`, `ManualRules.ResearchDiceCount`, and
  `CommandResolver.ResolveResearch`; `SpecialSiteRules.ResearchTechLimit`
  enforces the gang/site ceiling during validation. Instant resolution snapshots
  effective statistics before any same-phase Influence acquisition.
- Tests: `ResearchResolutionTests` covers effective dice count, recorded rolls,
  progress, completion, later-roster roll suppression, repeat rejection, state
  invariants, RNG consumption, notifications, deterministic phase hashes, and a
  newly influenced Science Center not changing a concurrent Research pool.
- Next experiment: execute Research from identical saves across gang/item/site
  modifiers, tech-level boundaries, and near-completion values, then compare
  rolls, unlock state, repeat behavior, and save deltas.

### RULE-BRIBE-001 — Bribe tolerance adjustment

- Source: `MANUAL-GOG-1`; command formula summary, plus `BIN-BRIBE-001` for
  shipped behavior. Exact scan page location still needs transcription.
- Observed statement: the manual prints a $5 cost and maximum unmodified
  tolerance of 40. The executable instead checks and deducts $3, records $3
  spent, and directly adds 3 to effective tolerance without a 40-point clamp.
- Interpretation: compatibility resolution follows the executable. During
  Instant, cash below $3 emits an ordered failed result without mutation;
  otherwise deduct and record $3, then add 3 directly to sector tolerance.
- Confidence: High static evidence for execution-time affordability, cost,
  cash-spent accounting, direct tolerance delta, and failure branch; Low for
  exact failure notification wording.
- Implementation: `CommandResolver.ResolveBribe` and
  `ToleranceResolver.ApplyBribe`; `ManualRules.ApplyBribe` preserves only the
  printed capped formula for comparison.
- Tests: `ManualRulesTests` keeps the printed rule distinct;
  `CommandResolutionTests.BribeSpendsThreeAndAddsThreeTolerance`, the exact-$3
  boundary, and
  `CommandResolutionTests.BribeFailureIsOrderedAndDoesNotMutateCashOrTolerance`.
- Next experiment: capture the original failure message and compare command
  retention after an insufficient-cash Bribe.

### RULE-SNITCH-001 — Snitch tolerance adjustment

- Source: `MANUAL-GOG-1`; Snitch command description, plus `BIN-SNITCH-001`.
  Exact scan page location still needs transcription into the evidence log.
- Observed statement: Snitch is free and lowers the acting gang's sector
  tolerance by 3, with a minimum unmodified tolerance of zero.
- Interpretation: during Instant, subtract 3 directly from effective sector
  tolerance regardless of player cash. After all Instant commands, clamp every
  sector below 1 to 1. The printed base-zero helper remains manual-only.
- Confidence: High static evidence for the direct delta, debt independence,
  post-Instant global floor, and timing.
- Implementation: `ToleranceResolver.ApplySnitch`,
  `ToleranceResolver.ClampAfterInstant`, and `CommandResolver.ResolveSnitch`.
- Tests: `ManualRulesTests` preserves the printed zero-floor formula;
  `CommandResolutionTests` covers phase-floor and debt behavior, while
  `ChaosResolutionTests` covers clamping before commandless Chaos evaluation.
- Next experiment: confirm the visible Snitch report when the intermediate
  result is below 1.

### RULE-TOLERANCE-001 — Return toward normal tolerance

- Source: `MANUAL-GOG-1`; sector and site statistic descriptions.
- Observed statement: a sector's normal tolerance is determined by its income,
  with influenced-site Tolerance values modifying that normal. Temporary
  tolerance changes move one point toward normal each turn.
- Interpretation: normal effective tolerance is
  `17 - sector Income + sum(Tolerance)` for currently influenced sites. Site
  adjustments apply immediately when influence is gained or lost, may move the
  effective value outside 0–40, and remain outside Bribe/Snitch's base caps.
  During Upkeep, every sector's current value moves exactly one point toward
  its normal effective value. The post-Instant global floor then raises every
  value below 1 to 1 before Combat and Chaos.
- Confidence: High for the formula and one-point adjustment; Medium for the
  exact turn boundary and whether an influence change applies immediately.
- Implementation: `ToleranceResolver`, invoked by `MatchState.FinishUpkeep`.
- Tests: `ToleranceResolverTests` covers movement from both directions, stable
  values, direct action deltas and the post-Instant floor; influence tests cover
  the immediate modifier, and `ChaosResolutionTests` covers the pre-Chaos clamp.
- Next experiment: compare saves before and after Upkeep around a Bribe or
  Snitch, then repeat while gaining or losing influence over modifier sites.

## Resolution safety policy

The headless resolver preflights every command in the current subphase. If any
action lacks an implemented evidence-backed resolver, the subphase remains in
place and no command in it mutates state. This is a recreation invariant that
prevents incomplete logic from silently consuming player commands; it is not a
claim about original-game behavior.

## Combat

### RULE-ATTACK-001 — Simultaneous attack and retaliation

- Source: `MANUAL-GOG-1`, numbered pages 50–51; the
  [1997 unofficial FAQ 0.7.1](https://gamefaqs.gamespot.com/pc/196900-chaos-overlords/faqs/1684)
  records a developer-informed correction that the printed Attack Roll omitted
  current Force. `BIN-COMBAT-ORDER-001` supplies the static attack and police
  scan order.
- Observed statement: attack dice equal current Force plus modified Combat minus
  defender Defense, floored at zero. Rolls of 4–6 each cause one Force damage.
  Strength adds for bare hands, melee, and blade weapons; Blade adds for blade
  weapons, Range for ranged weapons, and Fighting plus Martial Arts for bare
  hands. Retaliation uses the same starting statistics but halves successful
  damage, rounded down. A bare-handed Martial Artist prevents retaliation unless
  the opponent is also a bare-handed Martial Artist.
- Interpretation: snapshot every gang at the Combat boundary, roll queued
  attacks and eligible retaliation in player/roster-slot order, then apply all
  damage together. Reciprocal orders between the same two gangs form one
  encounter: the first gang reached in the fixed scan supplies the opening
  attack and the reverse order is represented by that encounter's single
  retaliation, rather than creating a second attack/retaliation pair.
  Consequently, a gang eliminated by one result still completes
  attacks and retaliation calculated from its phase-start Force. Force is
  floored at zero; elimination clears equipment and Hidden state. Actual damage
  credit is allocated in stable result order when attacks overkill one target;
  retaliation is not credited to Damage Inflicted.
- Hidden target behavior: a target that became Hidden during Instant receives
  an individual Detect-versus-Stealth percentage roll. Evasion produces an
  ordered `TargetEvaded` result; a successful hit prevents retaliation.
- Static binary confirmation: the original eligibility predicate requires the
  target action not to be Hide and allows retaliation when the attacker has no
  positive effective Martial Arts, has a weapon equipped, or faces a defender
  who is also an unarmed positive-Martial-Arts gang.
- Current exclusions: original overkill-stat attribution and reveal-state timing.
- Confidence: High for weapon-skill associations, the complete Martial Arts /
  weapon / Hide retaliation gate, its damage formula, and player/roster roll
  ordering; Medium/High for the Force-corrected opening formula; Low for
  overkill accounting and hidden-state timing.
- Implementation: `ManualRules.CombatRating`, `ManualRules.AttackDiceCount`,
  `ManualRules.RetaliationDamage`, and `CommandResolver.ResolveCombatPhase`.
- Tests: `CombatResolutionTests` covers effective attack/defense pools,
  retaliation, phase-start simultaneity, Martial Arts, hidden targets,
  reciprocal-order coalescing, reversed-submission roster order,
  elimination/equipment loss, statistics, RNG consumption, notifications, and
  hashes; `ManualRulesTests` covers the formulas.
- Next experiment: reproduce a fixed unarmed matchup from the FAQ, then repeat
  with melee/blade/ranged weapons, Martial Arts, two attackers, and a target
  hidden during Instant while capturing force bars, damage, and RNG order.

### RULE-DETECT-001 — Cooperative sector visibility

- Source: `MANUAL-GOG-1`, numbered page 47.
- Observed statement: compare an enemy gang's Stealth with the highest friendly
  Detect in its sector. Every additional friendly gang contributes +1 at Detect
  0–10, +2 at 11–12, +3 at 13–14, +4 at 15–16, +5 at 17–18, and +6 at 19 or
  more. If one friendly gang can see the enemy, all friendly gangs there can.
- Interpretation: active same-sector gangs contribute effective Detect through
  `SectorDetectionStrength`; visibility succeeds at `Detect >= Stealth`. Negative
  Detect helpers contribute zero because the manual defines no negative band.
  A player always detects its own gang, and Hide does not affect visibility.
- Current exclusions: stale knowledge after movement, UI information masking,
  exact behavior below zero, and binary confirmation of site/item modifiers.
- Confidence: High for the table and threshold; Medium for negative values and
  visibility-query timing.
- Implementation: `ManualRules.SectorDetectionStrength` and
  `MatchState.CanPlayerDetectGang`.
- Tests: `ManualRulesTests` covers band arithmetic; `CombatResolutionTests`
  covers ownership, enemy visibility, and independence from Hide.

### RULE-SITE-STATS-001 — Influenced-site local modifiers

- Source: `MANUAL-GOG-1`; Influence, site tables, and Statistics/Skill Mods.
- Observed statement: an influenced site benefits or hinders the influencing
  player's gangs in that sector using its listed statistics.
- Interpretation: `EffectiveStatisticsCalculator` adds every influenced site's
  complete statistic vector to the owner's gangs located in that sector, after
  definition and equipment statistics. Enemy gangs receive no benefit.
- Special Science Center/Research Lab research caps and the Factory purchase
  discount follow the same controlled-sector and influenced-site ownership
  rule. Match construction/load rejects neutral-sector influence or an
  influencer different from the sector owner.
  Protection/upkeep behavior and exact negative-stat clamping remain excluded.
- Confidence: High for ownership and local scope; Medium for aggregation order.
- Tests: `CombatResolutionTests.InfluencedSiteStatisticsApplyOnlyToOwnersGangsInThatSector`
  plus existing Heal, Research, Influence, Chaos, Control, and Combat tests that
  all consume the shared effective-stat path.

## Equipment transactions

### RULE-EQUIP-001 — Purchase and equip

- Source: `MANUAL-GOG-1`; Equip, Equipment, Item Types, and Tech Level
  descriptions on numbered pages 43–44.
- Observed statement: Equip buys an item for the acting gang, deducts its listed
  cost, requires prior research for most items, and requires gang tech level at
  least equal to item tech level. A gang has one weapon, armor, and miscellaneous
  slot; melee, blade, and ranged items share the weapon slot.
- Interpretation: zero-research-difficulty items are immediately available;
  other items require completed player research. Successful purchase replaces
  and loses the previous same-slot item and records cash spent.
- Initial unlock set: ordinary scenarios begin with the seven non-placeholder
  zero-difficulty technologies already researched: Metal Pipe (0), Combat Knife
  (1), Combat Pistol (12), Leathers (24), Shock Pads (25), Cool Hats (39), and
  Boom Boxes (40). Melee and ranged weapons are separate browser categories,
  so the pistol does not appear beside the two initial melee choices. Armageddon
  continues to unlock every real item.
- Factory rule: an influenced Factory in the acting gang's controlled sector
  reduces purchase price to `Cost - trunc(Cost / 3)`. This is a one-third
  discount rounded toward the full price; for example, an $11 Katana costs $8.
- Confidence: High for cost, categories, research, tech gates, the decoded
  zero-difficulty set, Factory division/rounding, controlled/influenced locality,
  fixed player/roster-slot resolution order, and same-slot replacement; Medium
  for repeat commands.
- Implementation: `EquipmentRules`, `SpecialSiteRules.EquipmentCost`,
  transaction validation, and `CommandResolver.ResolveEquip`.
- Tests: `TransactionResolutionTests` covers purchase, replacement, cash and
  statistics, research/tech validation, insufficient funds, replay hashes, and
  a Factory acquired during Instant discounting a same-turn Transaction-phase
  replacement; all decoded item costs exercise the recovered division formula.

### RULE-GIVE-001 — Transfer equipped item

- Source: `MANUAL-GOG-1`; Give description on numbered pages 43–44.
- Observed statement: a gang may give one or all equipped items to one friendly
  gang; the recipient must meet item tech level, and an existing similar item is
  lost.
- Interpretation: one Give command transfers one, two, or all three selected
  equipped items to one friendly same-sector target, clears every selected
  source slot, and replaces the target's corresponding slots without a cash
  change. The binary defers incoming items until every gang in that player's
  roster has completed its transaction, preserving same-slot swaps and making
  a gift overwrite the recipient's same-turn purchase. Later roster-slot gifts
  win when several target the same recipient slot.
- Confidence: High for transfer, tech gate, replacement loss, fixed scan order,
  and deferred application.
- Implementation: transaction validation, grouped transaction-phase Give
  preparation, and `CommandResolver.ResolveGive`.
- Tests: transfer, three-item batching, same-slot swaps, replacement loss,
  possession validation, and tech validation in `TransactionResolutionTests`.

### RULE-SELL-001 — Half-price sale

- Source: `MANUAL-GOG-1`; Sell description on numbered page 44.
- Observed statement: selling returns half the original listed price, excluding
  factory discounts, rounded down.
- Interpretation: remove every selected equipped item. A single-slot sale adds
  `floor(Cost / 2)` to player cash. The original's fixed weapon/armor/miscellaneous
  branches overwrite one payout local instead of accumulating it, so a multi-slot
  sale pays only the highest selected slot's half-price (miscellaneous, else
  armor, else weapon). Preserve this binary quirk and record that one payout as
  cash earned.
- Confidence: High for clearing, fixed slot order, payout overwrite, formula,
  and statistics update.
- Implementation: `EquipmentRules.SaleValue` and `CommandResolver.ResolveSell`.
- Tests: odd-price rounding, multi-slot payout overwrite, equipment removal,
  cash/statistics accounting, and runtime possession failure in `TransactionResolutionTests` and
  `ManualRulesTests`.

### RULE-TERMINATE-001 — Remove gang and equipment

- Source: `MANUAL-GOG-1` and `BIN-MOVEMENT-001`; Terminate description
  and command sequence on numbered
  pages 44–45.
- Observed statement: Terminate removes the gang from play and all items it
  possesses; it executes during Movement.
- Interpretation: set Force to zero, clear Hidden and all three equipment slots,
  and emit an elimination notification. Resolve the complete player/roster
  Terminate pass before any Move.
- Confidence: High for gang/item removal, phase, and scheduling; Low for
  statistics and notification presentation.
- Implementation: `CommandResolver.ResolveTerminate`.
- Tests: `TransactionResolutionTests.TerminateRemovesGangAndAllEquipmentDuringMovement`
  and `BoardResolutionTests.TerminatePassPrecedesMovePassRegardlessOfSubmissionOrder`.

## Movement and sector control

### RULE-CHAOS-001 — Cooperative Chaos and crackdown

- Source: `MANUAL-GOG-1`; Chaos and Crackdown descriptions, including the
  Math of the Game section.
- Observed statement: each player rolls the Force plus Chaos skill of their
  participating gangs with sector Income. Each success earns $1 in a
  controlled sector and counts toward crackdown; activity outside a controlled
  sector earns half as much. When total Chaos exceeds Tolerance, a crackdown
  prevents all Chaos income in that sector.
- Interpretation: reset the prior turn's sector Chaos during Upkeep, then group
  one player's Chaos commands by sector. Each participating gang contributes
  `sector Income + Force + Chaos` dice and rolls in fixed player-slot then
  persistent roster-slot order, independent of submission order or intervening
  sectors. Generated sector Income is independent of the three
  sites' Cash benefits. Accumulate every
  player's successes into `MatchSectorState.Chaos` before paying anybody. A
  sector already in crackdown, or crossing the strict `Chaos > Tolerance`
  threshold during this phase, pays no group; otherwise controlled groups earn
  all successes and uncontrolled groups earn `floor(group successes / 2)` after
  all same-player gangs in that sector have been aggregated. A newly
  triggered crackdown notifies every active player.
- Current exclusions: exact binary notification/event order and controlled
  runtime corroboration.
- Confidence: High for the per-gang pool, generated-Income distinction, control
  multiplier, roster RNG order, grouped half payout, and suppression rule;
  Medium for notification presentation.
- Implementation: `CommandResolver.ResolveChaosPhase`,
  `ManualRules.ChaosDiceCount`, `ManualRules.ChaosIncome`, and
  `ManualRules.TriggersCrackdown`.
- Tests: `ChaosResolutionTests` covers turn-start reset, pooling, generated
  sector Income versus site Cash, fixed player/roster RNG order, grouped payout,
  statistics, sector-wide
  cross-player aggregation, existing/new crackdown behavior, notifications,
  RNG consumption, and phase hashes; `ManualRulesTests` covers arithmetic and
  the strict threshold.
- Next experiment: run controlled and uncontrolled identical saves at Chaos
  values immediately below/equal/above Tolerance, including two players in one
  sector, then compare cash, Chaos, police creation, RNG, and next-turn reset.

### RULE-POLICE-001 — Crackdown detection and combat

- Source: `MANUAL-GOG-1`; Crackdown and Math of the Game descriptions, plus
  `BIN-COMBAT-ORDER-001`.
- Observed statement: during a crackdown, police attack every gang in the
  sector with Combat 20. Police detection is certain through Stealth 5 and
  drops five percentage points per additional Stealth point, reaching zero at
  Stealth 25; the manual separately lists police Detect as 12. No gang may
  attempt to control the sector until the police leave.
- Observed statement: police remain for three to five turns. Triggering another
  Crackdown while they are present makes them stay longer. Three Crackdowns in
  a five-turn period make the controlling Overlord lose the sector.
- Interpretation: at the Combat phase boundary, snapshot every active gang in
  a crackdown sector in ascending player/roster-slot order. Roll one percentile
  detection check per gang. On detection, roll
  `max(0, 20 - effective Defense)` dice and
  apply one damage per success. Police attacks do not retaliate or credit a
  player's damage statistic. Gang-command and police damage are accumulated
  against the same phase-start snapshots before casualties and equipment loss
  are applied. Control is rejected at submission while a crackdown is already
  active and fails without changing ownership if police arrive before the
  later Control subphase.
- Interpretation: a gang that resolved Hide during Instant uses the ordinary
  hidden-hit formula with police Detect 12 (`50% + 5% * (12 - Stealth)`),
  clamped to 0–100, instead of the non-hidden police stealth table.
- Interpretation: each trigger draws an inclusive three-to-five-turn duration
  from the deterministic simulation RNG and adds it to any remaining police
  presence. Each Combat phase in which police are present consumes one remaining
  turn after their attacks, producing exactly three to five attack opportunities;
  the sector detail panel exposes the authoritative count. Each sector retains
  two fixed occurrence slots and expires a slot only when it is strictly older
  than `current turn - 5`. A third occurrence therefore counts an oldest trigger
  exactly five turns earlier, neutralizes the sector, resets its influenced
  sites, Support, Tolerance modifiers, and resistance just like an overthrow,
  then writes the current turn into both slots. Reacquired control can be lost
  again on another recent trigger. The displaced owner receives a distinct
  `ControlLost` notification in addition to the global Crackdown notification.
  History mutation and ownership cleanup precede the duration-extension draw.
- Current exclusions: whether 0%/100% checks consume RNG,
  weapon/damage-cap treatment, and exact original message wording.
- Confidence: High for the detection percentage table, hidden Detect 12 branch,
  Combat 20, defense subtraction, player/roster attack order, occurrence
  window/reset, and duration RNG order; Low for the listed exclusions.
- Implementation: `CommandResolver.ResolveCombatPhase`, exposed through
  `MatchState.LastPoliceAttackResolutions` and `PoliceAttackResolved` events;
  `CommandValidator` and `CommandResolver.ResolveControl` enforce the Control
  lockout at both relevant boundaries; `CrackdownResolver` owns duration and
  extension plus the two-turn history represented in the original save layout.
- Tests: `PoliceCombatResolutionTests` covers player/roster ordering,
  normal and hidden detection, effective Stealth/Defense, undetectability at
  Stealth 25, deterministic RNG consumption/hashes, notifications, casualties,
  and equipment loss. `ChaosResolutionTests` covers duration, extension,
  five-turn history boundaries, neutralization and cleanup. `BoardResolutionTests`
  covers submission-time and execution-time Control lockout without ownership
  mutation; save/replay tests cover migration and multi-turn continuation.
- Next experiment: capture otherwise identical pre-Combat saves spanning
  Stealth 5/6/24/25 and several Defense values, with and without a player
  Attack command, then compare detection, damage, ordering, and RNG deltas.

### RULE-MOVE-001 — Adjacent movement and friendly capacity

- Source: `MANUAL-GOG-1`; Move command and command sequence descriptions, plus
  `BIN-MOVEMENT-001`.
- Observed statement: Move relocates a gang to an adjacent sector during the
  Movement phase. The structural limit is six friendly gangs per sector.
- Interpretation: move to any of the eight neighboring sectors, including a
  diagonal neighbor, rejecting a target already at friendly capacity; commands
  that compete for the final slot resolve
  in ascending player/roster-slot order and later commands fail without moving.
- Confidence: High for adjacency, capacity, phase precedence, and final-slot
  ordering.
- Implementation: movement validation and `CommandResolver.ResolveMove`.
- Tests: `BoardResolutionTests` covers movement events, capacity at submission,
  reversed-submission runtime contention, roster result order, Terminate
  precedence, and notifications.

### RULE-CONTROL-001 — Cooperative sector control comparison

- Source: `MANUAL-GOG-1`; Control command and Math of the Game, numbered pages
  31 and 49–50.
- Observed statement: friendly participants total `Force + Control`. Neutral
  attempts subtract sector income. Enemy attempts also subtract every active,
  non-hiding defending gang's `Force + Control` and total influenced-site
  Support. Losing a sector loses all influenced sites, which return to full
  resistance; taking ownership directly from another player is an Overthrow.
- Interpretation: aggregate gangs by player/roster slot, then resolve sectors
  in ascending board order so tie-break RNG is submission-order independent.
  Group same-player Control commands by sector, calculate the signed margin
  without dice, and capture when it is positive. A unique positive
  leader captures directly; equal positive leaders are selected by one bounded
  random draw in ascending player-slot order. At best margin zero, select among
  a leading neutral candidate and every tied player, again in slot order. This
  gives one challenger the manual's 50-percent chance and gives each of `n`
  tied challengers a `1 / (n + 1)` chance. Record the one-based roll and full
  candidate count in each tied group's resolution event. Evaluate every group
  from the phase-start owner snapshot, whether the sector is neutral or already
  controlled, and permit at most one capture or overthrow.
  On an overthrow, increment the attacker's statistic, remove the former
  owner's site Support, clear influence, and restore table resistance. A
  repeating Control order clears once its player owns the sector; repeating
  Influence similarly clears when its target reaches zero resistance. Other
  terminal repeat targets (completed Move/transactions, eliminated Attack
  target, maximum Heal, zero-tolerance Snitch) are removed while ongoing
  behaviors such as Hide and Chaos remain repeatable across turns.
- Moving or terminating the last friendly gang does not abandon the sector;
  ownership changes only through a separate ownership-changing rule.
- Current exclusions: original crackdown ordering and negative-total edge
  behavior.
- Confidence: High for equation components, density-derived sector Income,
  influence loss, zero-margin neutral selection, and cross-player winner/order
  behavior.
- Implementation: `ManualRules.ControlStrength`, `ManualRules.ControlMargin`,
  grouped `CommandResolver.ResolveControl`, and site-reset handling.
- Tests: `BoardResolutionTests` covers neutral capture, board/roster ordering,
  pooled strength, generated sector Income versus site Cash, defended
  failure, recorded deterministic zero-margin chance, positive and zero-margin
  cross-player ties, unique-highest neutral conflicts, retained empty-sector
  ownership after Move/Terminate, a single phase-opening
  defense for several owned-sector challengers,
  execution-time Crackdown rejection, overthrow/statistics, influence reset,
  deterministic hashes, and Hide expiration;
  `ManualRulesTests` covers equation arithmetic.

## Upkeep economy

### RULE-UPKEEP-001 — Base income, upkeep, and debt

- Source: `MANUAL-GOG-1`; Upkeep, Finance, Sector Tax, and Gang Upkeep
  descriptions. Exact scan page locations still need transcription.
- Observed statement: each controlled sector grants $1; influenced sites apply
  their listed cash values; active gangs charge their listed upkeep. Cash may
  become negative. The manual says equipment, Bribe, and Snitch are restricted,
  while the executable's Snitch resolver contains no debt gate.
- Interpretation: for each active player in stable ID order, calculate
  `cash + controlled sectors + influenced-site cash - active-gang upkeep` with
  checked integer arithmetic. Runtime affordability rejects equipment and
  Bribe; Snitch remains free and executable in debt. Hiring permits a zero-cost
  gang even while the balance is negative.
- Current exclusions: insufficient-funds desertion, site protection, cash
  adjustment, special gang/item/site modifiers, integer overflow behavior, and
  the exact statistics accounting boundary.
- Confidence: High for the component values and negative-cash restrictions; Medium for whether
  all components commit in one Upkeep boundary; Low for excluded edge cases.
- Implementation: `EconomyResolver.ResolveUpkeep` and
  `MatchState.FinishUpkeep`.
- Tests: `EconomyResolutionTests` covers component accounting, persistent debt,
  eliminated players, ordered events/notifications, and deterministic phase
  hashes. Command, transaction, and hire tests cover debt restrictions.
- Next experiment: prepare saves around zero projected cash with controlled
  combinations of sectors, positive/negative sites, and gangs, then compare
  Finance/Event panels and post-Upkeep saves.

## Turn timing

### RULE-TIMER-001 — Optional human planning limit

- Source: original Game Settings Help; `EXE-GOG-1.1` preference byte
  `0x00487854`, new-match mapping at `0x0046e766`, start helper `0x0041b8bc`,
  draw helper `0x0041b8fc`, input-pump divider at `0x00462f07`, sound wrapper
  `0x00464290`, and expiry helper `0x0041bdd5`.
- Observed statement: setup offers no limit, 30 seconds, 2 minutes, or 5 minutes
  to constrain planning turns, particularly in multiplayer games.
- Interpretation: the selected limit applies only to human Command phases.
  Expiry follows the ordinary Done path but bypasses the optional idle-gang
  confirmation. A 60-pixel bar shows remaining time. General sound slot 7 is
  requested below 10 seconds while more than 1 second remains, and slot 8 is
  requested for the final second. The bar truncates elapsed percent before
  converting that percentage to its 60-pixel width. Drawing and warning checks
  recur every sixth eligible input-pump call; the recreation mirrors that with
  every sixth fixed update.
- Current exclusions: exact wall-clock sound cadence, behavior while modal UI
  is open, and deactivation timing still need controlled reference capture.
- Confidence: High for choices, durations, human-only start, expiry ordering,
  bar scale/quantization, sound-slot boundaries, and update divider; Medium for
  wall-clock presentation cadence.
- Implementation: `PlanningTimerPolicy`, `PlanningTimer`, and the presentation-
  only integration in `ChaosGame.PlanningTimer.cs`. Timer expiry submits the
  normal replay-recorded `FinishPlanningTurn` operation; wall-clock state is not
  saved, hashed, or replayed.
- Tests: `PlanningTimerPolicyTests`, `GamePreferencesStoreTests`, and
  `UiNavigationTests` cover durations, boundaries, warning de-duplication,
  expiry, persistence validation, and original coordinates.
- Next experiment: record a 30-second original turn with and without an open
  planning modal, correlating bar widths and warning clip starts to wall time.

## Objectives and match completion

### RULE-SETUP-001 — Starting resources and `SMGFUNDAGE`

- Source: `MANUAL-GOG-1`; Armageddon scenario description; `EXE-GOG-1.1`
  fresh-game routine `0x0046e766` and name scan `0x0046dc10`.
- Observed statement: Armageddon starts every player with $500 and all equipment
  already researched.
- Interpretation: new-match bootstrapping replaces the ordinary-scenario cash
  input with 500 and marks every real item-table entry (excluding type-99 padding)
  as researched before turn one. Ordinary starting cash is $20. The exact
  uppercase player name `SMGFUNDAGE` then overwrites either starting amount with
  $1,500; the setup-only flag is transient and cash itself persists. Original
  local Begin also completes all empty slots as Computers before city generation;
  each receives a unique portrait 0..14 and its resource-defined name. Exact
  uppercase `SMGISLANDS` subsequently sets neutral non-HQ sectors to Chaos 100.
- Local setup starts with one human. Add/Remove changes the human count from one
  through six, and Begin fills every remaining slot with a Computer. Human names
  use the Help-specified 10-character name field; portrait 15 is the empty
  marker and cannot be selected as a human face.
- A face dragged to an empty color moves that local-human identity to the target
  slot. A face dropped on another human exchanges their colors. The resulting
  sparse human slots are preserved as player IDs, then every missing slot is
  filled in ascending order before AI initialization and city generation.
- Current exclusions: initial seed selection, the remaining setup call context,
  initial hire offers, and an original runtime fixture remain open.
- Confidence: High static evidence for ordinary/Armageddon cash, the name
  override, city/HQ generation and Right Hands Force; runtime correlation pending.
- Implementation: `OriginalMatchFactory.Create` and `MatchBootstrap.Create`.
- Tests: `MatchBootstrapTests` covers Armageddon resources, exact-case
  `SMGFUNDAGE` behavior in ordinary and Armageddon games, and verifies research
  state through the normal query API. `OriginalCityGeneratorTests` locks fixed
  city, Armageddon-rejection, HQ-permutation, empty-slot portrait/name ordering,
  `SMGISLANDS`, and RNG-continuation vectors.

### RULE-OBJECTIVE-001 — End-of-turn objective evaluation

- Source: `MANUAL-GOG-1`; scenario descriptions and scoring tables.
- Observed statement: Greed, Power, Acceptance, and Dominance end at their
  selected time limit; the other six scenarios end when their stated objective
  is achieved. A single-player game also ends when its human Overlord is
  eliminated; the elimination splash returns to the title instead of showing
  the Endgame Awards/Stats screen. Dominance uses the duration-specific weights recorded in
  `ScenarioCatalog`. In Big Man, each of the four center sectors grants one
  point per turn to its controller and the first player to 40 wins. In
  Eliminate, losing the Right Hands removes that player: all remaining gangs
  vanish and formerly controlled sectors become neutral.
- Observed statement: in Siege, each Overlord's starting controlled sector is
  designated important and identified by two gray pylons; one Overlord must
  control all six important sectors simultaneously.
- Interpretation: after Player Elimination and before advancing the turn,
  project cash, support, controlled sectors, active opponents, opposing active
  Right Hands, explicitly designated important sectors, and Big Man points from the
  authoritative match. Timed games end on turns 26/52/104/208 and preserve all
  players tied for the highest score. Objective games preserve all qualifying
  players in player-ID order. Emit one `MatchEnded` event and one Objective
  notification per player; include the outcome in canonical state hashes and
  prevent the following Upkeep phase from resolving. Timed outcomes include
  score-descending standings for every scenario; equal scores share a
  competition rank, with the next place skipped, and eliminated players follow
  the active ranking unranked in player-slot order. Fixed inactive slots retain
  the executable's -32,000 score sentinel during rank counting. Objective
  ranking uses the executable's scenario table: sectors for Big 40/Armageddon,
  owned HQ sectors for Eliminate, current center-sector control for Big Man, and the common
  inactive-player count for Kill 'Em All/Siege. Dominance divides its weighted
  numerator by ten before ranking. Big Man points are awarded in player-ID order
  at this boundary before its victory check. Eliminate cleanup also clears equipment,
  pending hires, and the eliminated player's site influence. After elimination
  resolution, a one-human match records `PlayerEliminated` immediately when that
  human is no longer active; a hot-seat match continues while its objective is
  unfinished.
- Current exclusions: tie-break presentation beyond stable slot order,
  exact Siege pylon artwork, exact Eliminate
  cleanup timing, and award edge-case parity.
- Confidence: High static evidence for end-boundary timing, thresholds,
  all-scenario scores, competition standings, active/inactive display order,
  durations, weights, and Siege setup mapping; Low for special objective edges.
- Implementation: `MatchOutcomeEvaluator`, scenario-specific elimination and
  Big Man accrual in `MatchState.FinishPlayerElimination`, `MatchState.Outcome`,
  and the canonical state hash.
- Tests: `MatchOutcomeTests` covers authoritative projection, objective event
  and notification emission, immediate single-player defeat versus continuing
  hot-seat play, Eliminate's Right Hands distinction, exact timed
  boundary ties/standings, objective standings, and outcome hashing;
  `EndgameRankingTests` covers recovered all-scenario scores, competition ties,
  and inactive ordering; `ScenarioLifecycleTests` covers Big Man accrual/event order and
  Eliminate cleanup/neutralization. `OriginalCityGeneratorTests` covers fresh
  Siege landmark assignment and one-important-sector-per-player starting state;
  `UiNavigationTests` bounds the paired pylon layout inside every city tile.
- Next experiment: capture the last two turns of each timed scenario and
  simultaneous-threshold states for objective scenarios, then compare event,
  ranking, tie, and next-screen behavior.

### RULE-AWARDS-001 — Endgame performance awards

- Source: `MANUAL-GOG-1`; numbered pages 46–47, Endgame Screen, Awards, and
  Stats. The page images were inspected directly from the fingerprinted GOG
  manual scan.
- Observed statement: Skull goes to most total combat damage; Fist to most
  Overthrows; Dollar Sign to most cash spent; Safe to least cash spent; and Big
  Fat Chicken to most hiding. Awards do not affect victory. Retaliatory damage
  is excluded from Damage Inflicted. The manual says no combat/hiding category
  award is given when that activity is not significant.
- Interpretation: calculate all five superlatives from authoritative player
  statistics at match completion. Preserve every tied recipient in player-ID
  order. Treat zero direct damage and zero Hide resolutions as not significant,
  omitting Skull and Big Fat Chicken respectively. Other zero-valued categories
  are retained because the manual states no equivalent exclusion for them.
- Current exclusions: the original significance threshold, tie presentation,
  eliminated-player eligibility, and whether a repeated Hide while already
  hidden increments the counter.
- Confidence: High for award/statistic mapping and retaliation exclusion;
  Medium for zero-activity omission; Low for ties and repeated Hide behavior.
- Implementation: `EndgameAwardEvaluator`, `MatchStatistics.TimesHidden`,
  direct-damage accounting in `CommandResolver`, and award snapshots in
  `MatchOutcome`, `MatchEnded` events, and canonical hashes.
- Tests: `EndgameAwardTests` covers every category, ties, zero combat/hiding,
  outcome/event integration and hashes; `CombatResolutionTests` verifies that
  retaliation is not credited; `InstantResolutionTests` verifies Hide counts.
- Next experiment: finish controlled games with tied and zero values for every
  statistic and repeat Hide commands across turns, then compare which icons and
  recipients the original Endgame Screen displays.
