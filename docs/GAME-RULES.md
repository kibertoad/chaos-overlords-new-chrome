# Game rules and evidence

Status: partial, active research
Last updated: 2026-09-09

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
  benefit descriptions, plus `BIN-RNG-002` and `BIN-RNG-003`.
- Observed statement: participating gangs contribute total Force plus Influence;
  successes reduce site resistance; reaching zero influences the site and grants
  its listed benefits.
- Interpretation: pool same-player gangs targeting the same site, roll
  `max(0, sum(Force + effective Influence))` dice once, persist reduced
  resistance, and at zero assign the player and add the site's Support value.
- Current exclusions: cross-player simultaneous contests, already-influenced
  takeovers, cash/tolerance/stat benefit timing, site special behavior, and
  influenced-site modifiers in the dice pool. The recreation currently rejects
  commands against an already-influenced site until takeover rules are verified.
- Confidence: High for the base pool, success threshold, resistance reduction,
  and Support value; Medium for friendly pooling; Low for conflict ordering and
  benefit timing.
- Implementation: `CommandResolver.ResolvePhase`,
  `CommandResolver.ResolveInfluence`, `ManualRules.InfluenceDiceCount`, and
  `ManualRules.ApplyInfluenceProgress`; validation requires player ownership of
  the target sector.
- Tests: `InstantResolutionTests` covers friendly pooling, single RNG
  consumption, partial progress, completion/Support, target rejection,
  notifications, deterministic replay, and phase hashes.
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
  successes to Force with a maximum of ten.
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
- Current exclusions: tech-level restrictions, Science Center/Research Lab
  effects, influenced-site/contextual modifiers, ownership/unlock effects, and
  the exact behavior of zero-difficulty items.
- Confidence: High for the manual formula and completion threshold; Medium for
  equipment aggregation and repeat-command rejection; High for the recovered
  RNG step/range wrapper; Low for initial seed and complete RNG call order.
- Implementation: `MatchPlayerState.RemainingResearch`,
  `MatchPlayerState.ApplyResearch`, `ManualRules.ResearchDiceCount`, and
  `CommandResolver.ResolveResearch`.
- Tests: `ResearchResolutionTests` covers effective dice count, recorded rolls,
  progress, completion, repeat rejection, state invariants, RNG consumption,
  notifications, and deterministic phase hashes.
- Next experiment: execute Research from identical saves across gang/item/site
  modifiers, tech-level boundaries, and near-completion values, then compare
  rolls, unlock state, repeat behavior, and save deltas.

### RULE-BRIBE-001 — Bribe tolerance adjustment

- Source: `MANUAL-GOG-1`; Bribe command description. Exact scan page location
  still needs transcription into the evidence log.
- Observed statement: Bribe costs $3 and raises the acting gang's sector
  tolerance by 5, with a maximum tolerance of 40.
- Interpretation: during the Instant execution subphase, deduct $3 from the
  commanding player, record it as cash spent, and set sector tolerance to
  `min(40, tolerance + 5)`.
- Current insufficient-cash behavior: the recreation emits an ordered failed
  result and does not change cash or tolerance. This edge case is provisional.
- Confidence: High for cost/delta/cap; Low for execution-time affordability and
  failure notification behavior.
- Implementation: `ManualRules.ApplyBribe`, `CommandResolver.ResolveBribe`.
- Tests: `ManualRulesTests.BribeAddsFiveAndCapsAtForty`,
  `CommandResolutionTests.BribeSpendsThreeAndAddsFiveTolerance`, and
  `CommandResolutionTests.BribeFailureIsOrderedAndDoesNotMutateCashOrTolerance`.
- Next experiment: queue Bribe with $0–$3 in identical reference saves and
  compare command retention, cash, tolerance, and event ordering.

### RULE-SNITCH-001 — Snitch tolerance adjustment

- Source: `MANUAL-GOG-1`; Snitch command description. Exact scan page location
  still needs transcription into the evidence log.
- Observed statement: Snitch is free and lowers the acting gang's sector
  tolerance by 3, with a minimum tolerance of zero.
- Interpretation: during the Instant execution subphase, set sector tolerance
  to `max(0, tolerance - 3)` without changing cash.
- Confidence: High for cost/delta/floor; Medium for execution timing; Low for
  automatic tolerance interactions in sectors without influenced sites.
- Implementation: `ManualRules.ApplySnitch`, `CommandResolver.ResolveSnitch`.
- Tests: `ManualRulesTests.SnitchSubtractsThreeAndFloorsAtZero` and
  `CommandResolutionTests.SnitchFloorsToleranceAtZeroWithoutCost`.
- Next experiment: execute Snitch at tolerance 0–4 with and without influenced
  sites and compare immediate plus next-turn save deltas.

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
  current Force.
- Observed statement: attack dice equal current Force plus modified Combat minus
  defender Defense, floored at zero. Rolls of 4–6 each cause one Force damage.
  Strength adds for bare hands, melee, and blade weapons; Blade adds for blade
  weapons, Range for ranged weapons, and Fighting plus Martial Arts for bare
  hands. Retaliation uses the same starting statistics but halves successful
  damage, rounded down. A bare-handed Martial Artist prevents retaliation unless
  the opponent is also a bare-handed Martial Artist.
- Interpretation: snapshot every gang at the Combat boundary, roll queued
  attacks and eligible retaliation in stable queue order, then apply all damage
  together. Consequently, a gang eliminated by one result still completes
  attacks and retaliation calculated from its phase-start Force. Force is
  floored at zero; elimination clears equipment and Hidden state. Actual damage
  credit is allocated in stable result order when attacks overkill one target.
- Hidden target behavior: a target that became Hidden during Instant receives
  an individual Detect-versus-Stealth percentage roll. Evasion produces an
  ordered `TargetEvaded` result; a successful hit prevents retaliation.
- Current exclusions: crackdown police attacks, animation/audio timing,
  original overkill-stat attribution,
  repeated mutual attacks, and binary confirmation of resolver/RNG order.
- Confidence: High for weapon-skill associations and Martial Arts exception;
  Medium/High for the Force-corrected formula and retaliation; Low for ordering,
  overkill accounting, and hidden failure behavior.
- Implementation: `ManualRules.CombatRating`, `ManualRules.AttackDiceCount`,
  `ManualRules.RetaliationDamage`, and `CommandResolver.ResolveCombatPhase`.
- Tests: `CombatResolutionTests` covers effective attack/defense pools,
  retaliation, phase-start simultaneity, Martial Arts, hidden targets,
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
- Current exclusions: special Science Center/Research Lab tech caps, Factory
  discount, protection/upkeep behavior, and exact negative-stat clamping.
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
- Confidence: High for cost, categories, research, and tech gates; Medium for
  same-slot purchase replacement behavior; Low for factory discounts and repeat
  commands.
- Implementation: `EquipmentRules`, transaction validation, and
  `CommandResolver.ResolveEquip`.
- Tests: `TransactionResolutionTests` covers purchase, replacement, cash and
  statistics, research/tech validation, insufficient funds, and replay hashes.

### RULE-GIVE-001 — Transfer equipped item

- Source: `MANUAL-GOG-1`; Give description on numbered pages 43–44.
- Observed statement: a gang may give one or all equipped items to one friendly
  gang; the recipient must meet item tech level, and an existing similar item is
  lost.
- Interpretation: one Give command transfers its selected equipped item to the
  friendly same-sector target, clears the source slot, and replaces the target's
  same slot without a cash change.
- Confidence: High for transfer, tech gate, and replacement loss; Medium for
  within-phase swap ordering.
- Implementation: transaction validation and `CommandResolver.ResolveGive`.
- Tests: transfer, replacement loss, possession validation, and tech validation
  in `TransactionResolutionTests`.

### RULE-SELL-001 — Half-price sale

- Source: `MANUAL-GOG-1`; Sell description on numbered page 44.
- Observed statement: selling returns half the original listed price, excluding
  factory discounts, rounded down.
- Interpretation: remove the selected equipped item, add `floor(Cost / 2)` to
  player cash, and record the proceeds as cash earned.
- Confidence: High for the formula; Medium for statistics timing and multi-item
  UI batching.
- Implementation: `EquipmentRules.SaleValue` and `CommandResolver.ResolveSell`.
- Tests: odd-price rounding, equipment removal, cash/statistics accounting, and
  runtime possession failure in `TransactionResolutionTests` and
  `ManualRulesTests`.

### RULE-TERMINATE-001 — Remove gang and equipment

- Source: `MANUAL-GOG-1`; Terminate description and command sequence on numbered
  pages 44–45.
- Observed statement: Terminate removes the gang from play and all items it
  possesses; it executes during Movement.
- Interpretation: set Force to zero, clear Hidden and all three equipment slots,
  and emit an elimination notification.
- Confidence: High for gang/item removal and phase; Low for statistics,
  notification presentation, and effects on simultaneous Movement.
- Implementation: `CommandResolver.ResolveTerminate`.
- Tests: `TransactionResolutionTests.TerminateRemovesGangAndAllEquipmentDuringMovement`.

## Movement and sector control

### RULE-CHAOS-001 — Cooperative Chaos and crackdown

- Source: `MANUAL-GOG-1`; Chaos and Crackdown descriptions, including the
  Math of the Game section.
- Observed statement: each player rolls the total Force plus Chaos skill of all
  their participating gangs, plus sector Income. Each success earns $1 in a
  controlled sector and counts toward crackdown; activity outside a controlled
  sector earns half as much. When total Chaos exceeds Tolerance, a crackdown
  prevents all Chaos income in that sector.
- Interpretation: group one player's Chaos commands by sector, add sector Income
  once per group, and roll each group in earliest queue order. Accumulate every
  player's successes into `MatchSectorState.Chaos` before paying anybody. A
  sector already in crackdown, or crossing the strict `Chaos > Tolerance`
  threshold during this phase, pays no group; otherwise controlled groups earn
  all successes and uncontrolled groups earn `floor(successes / 2)`. A newly
  triggered crackdown notifies every active player.
- Current exclusions: crackdown duration/reset, police detection and combat,
  special site modifiers, whether the original compares turn-only or persisted
  Chaos, exact half-dollar rounding, and binary within-phase RNG/event order.
- Confidence: High for the base pool, control multiplier, and suppression rule;
  Medium for friendly pooling; Low for accumulation, rounding, and ordering.
- Implementation: `CommandResolver.ResolveChaosPhase`,
  `ManualRules.ChaosDiceCount`, `ManualRules.ChaosIncome`, and
  `ManualRules.TriggersCrackdown`.
- Tests: `ChaosResolutionTests` covers pooling, income, statistics, sector-wide
  cross-player aggregation, existing/new crackdown behavior, notifications,
  RNG consumption, and phase hashes; `ManualRulesTests` covers arithmetic and
  the strict threshold.
- Next experiment: run controlled and uncontrolled identical saves at Chaos
  values immediately below/equal/above Tolerance, including two players in one
  sector, then compare cash, Chaos, police creation, RNG, and next-turn reset.

### RULE-POLICE-001 — Crackdown detection and combat

- Source: `MANUAL-GOG-1`; Crackdown and Math of the Game descriptions.
- Observed statement: during a crackdown, police attack every gang in the
  sector with Combat 20. Police detection is certain through Stealth 5 and
  drops five percentage points per additional Stealth point, reaching zero at
  Stealth 25; the manual separately lists police Detect as 12.
- Interpretation: at the Combat phase boundary, snapshot every active gang in
  a crackdown sector in sector/gang-ID order. Roll one percentile detection
  check per gang. On detection, roll `max(0, 20 - effective Defense)` dice and
  apply one damage per success. Police attacks do not retaliate or credit a
  player's damage statistic. Gang-command and police damage are accumulated
  against the same phase-start snapshots before casualties and equipment loss
  are applied.
- Current exclusions: original timing within Combat, whether 0%/100% checks
  consume RNG, weapon/damage-cap treatment, use of the separately documented
  Detect 12 value, crackdown duration/reset, ownership/site aftermath, and
  exact binary RNG/event order.
- Confidence: High for the detection percentage table and Combat 20; Medium
  for defense subtraction; Low for phase ordering and the listed exclusions.
- Implementation: `CommandResolver.ResolveCombatPhase`, exposed through
  `MatchState.LastPoliceAttackResolutions` and `PoliceAttackResolved` events.
- Tests: `PoliceCombatResolutionTests` covers stable sector/gang ordering,
  effective Stealth/Defense, undetectability at Stealth 25, deterministic RNG
  consumption/hashes, notifications, casualties, and equipment loss.
- Next experiment: capture otherwise identical pre-Combat saves spanning
  Stealth 5/6/24/25 and several Defense values, with and without a player
  Attack command, then compare detection, damage, ordering, and RNG deltas.

### RULE-MOVE-001 — Adjacent movement and friendly capacity

- Source: `MANUAL-GOG-1`; Move command and command sequence descriptions.
- Observed statement: Move relocates a gang to an adjacent sector during the
  Movement phase. The structural limit is six friendly gangs per sector.
- Interpretation: move to one orthogonally adjacent sector, rejecting a target
  already at friendly capacity; commands that compete for the final slot resolve
  in stable queue order and later commands fail without moving.
- Confidence: High for adjacency/capacity; Low for original simultaneous
  collision and final-slot ordering.
- Implementation: movement validation and `CommandResolver.ResolveMove`.
- Tests: `BoardResolutionTests` covers movement events, capacity at submission,
  runtime contention, stable result order, and notifications.

### RULE-CONTROL-001 — Cooperative sector control comparison

- Source: `MANUAL-GOG-1`; Control command and Math of the Game, numbered pages
  31 and 49–50.
- Observed statement: friendly participants total `Force + Control`. Neutral
  attempts subtract sector income. Enemy attempts also subtract every active,
  non-hiding defending gang's `Force + Control` and total influenced-site
  Support. Losing a sector loses all influenced sites, which return to full
  resistance; taking ownership directly from another player is an Overthrow.
- Interpretation: group same-player Control commands by sector, calculate the
  signed margin without dice, and capture only when it is strictly positive.
  On an overthrow, increment the attacker's statistic, remove the former
  owner's site Support, clear influence, and restore table resistance.
- Current exclusions: cross-player simultaneous tie/conflict ordering, precise
  definition of sector income, crackdown restrictions, abandoned-sector rules,
  and negative-total edge behavior. Groups currently resolve at their earliest
  stable queue position.
- Confidence: High for equation components and influence loss; Medium for the
  strict-positive threshold; Low for simultaneous ordering.
- Implementation: `ManualRules.ControlStrength`, `ManualRules.ControlMargin`,
  grouped `CommandResolver.ResolveControl`, and site-reset handling.
- Tests: `BoardResolutionTests` covers neutral capture, pooled strength, defended
  failure, overthrow/statistics, influence reset, deterministic hashes, and Hide
  expiration; `ManualRulesTests` covers equation arithmetic.

## Upkeep economy

### RULE-UPKEEP-001 — Base income, upkeep, and cash floor

- Source: `MANUAL-GOG-1`; Upkeep, Finance, Sector Tax, and Gang Upkeep
  descriptions. Exact scan page locations still need transcription.
- Observed statement: each controlled sector grants $1; influenced sites apply
  their listed cash values; active gangs charge their listed upkeep; projected
  cash below zero ends at zero.
- Interpretation: for each active player in stable ID order, calculate
  `max(0, cash + controlled sectors + influenced-site cash - active-gang upkeep)`.
- Current exclusions: insufficient-funds desertion, site protection, cash
  adjustment, special gang/item/site modifiers, integer overflow behavior, and
  the exact statistics accounting boundary.
- Confidence: High for the component values and zero floor; Medium for whether
  all components commit in one Upkeep boundary; Low for excluded edge cases.
- Implementation: `EconomyResolver.ResolveUpkeep` and
  `MatchState.FinishUpkeep`.
- Tests: `EconomyResolutionTests` covers component accounting, negative cash
  flooring, eliminated players, ordered events/notifications, and deterministic
  phase hashes.
- Next experiment: prepare saves around zero projected cash with controlled
  combinations of sectors, positive/negative sites, and gangs, then compare
  Finance/Event panels and post-Upkeep saves.
