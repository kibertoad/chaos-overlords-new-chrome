# Game rules and evidence

Status: partial, active research
Last updated: 2026-09-20

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

## Rule format

Each rule carries a stable `RULE-<AREA>-<NNN>` ID and is written as a bullet
list with, where they apply:

- **Source**: the evidence source above and its page, or the finding IDs in
  [ORIGINAL-INTERNALS.md](ORIGINAL-INTERNALS.md) that the rule rests on.
- **Observed statement**: what the manual or executable states or does.
- **Interpretation**: the rule as the recreation applies it, including timing,
  ordering, and edge cases.
- **Confidence**: Verified, High, Medium, or Low, with what it covers.
- **Implementation**: the types and tests that carry the rule.

Rule IDs are referenced from code comments, tests, [PARITY-MATRIX.md](PARITY-MATRIX.md),
and the topic index in [README.md](README.md#topic-index).

## Rule index

Generated from the `###` headings of this file by `node tools/update-doc-indexes.mjs`.

<!-- doc-index:begin rule-index -->
| ID | Rule | Section |
|---|---|---|
| [RULE-HIRE-001](#rule-hire-001--offer-replacement-and-starting-force) | Offer replacement and starting Force | Hiring |
| [RULE-HIDE-001](#rule-hide-001--enter-hidden-state) | Enter hidden state | Instant commands |
| [RULE-INFLUENCE-001](#rule-influence-001--cooperative-site-influence) | Cooperative site influence | Instant commands |
| [RULE-HEAL-001](#rule-heal-001--heal-dice-and-force-restoration) | Heal dice and force restoration | Instant commands |
| [RULE-RESEARCH-001](#rule-research-001--research-dice-and-persistent-progress) | Research dice and persistent progress | Instant commands |
| [RULE-BRIBE-001](#rule-bribe-001--bribe-tolerance-adjustment) | Bribe tolerance adjustment | Instant commands |
| [RULE-SNITCH-001](#rule-snitch-001--snitch-tolerance-adjustment) | Snitch tolerance adjustment | Instant commands |
| [RULE-TOLERANCE-001](#rule-tolerance-001--return-toward-normal-tolerance) | Return toward normal tolerance | Instant commands |
| [RULE-ATTACK-001](#rule-attack-001--simultaneous-attack-and-retaliation) | Simultaneous attack and retaliation | Combat |
| [RULE-DETECT-001](#rule-detect-001--cooperative-sector-visibility) | Cooperative sector visibility | Combat |
| [RULE-SITE-STATS-001](#rule-site-stats-001--influenced-site-local-modifiers) | Influenced-site local modifiers | Combat |
| [RULE-EQUIP-001](#rule-equip-001--purchase-and-equip) | Purchase and equip | Equipment transactions |
| [RULE-GIVE-001](#rule-give-001--transfer-equipped-item) | Transfer equipped item | Equipment transactions |
| [RULE-SELL-001](#rule-sell-001--half-price-sale) | Half-price sale | Equipment transactions |
| [RULE-TERMINATE-001](#rule-terminate-001--remove-gang-and-equipment) | Remove gang and equipment | Equipment transactions |
| [RULE-CHAOS-001](#rule-chaos-001--cooperative-chaos-and-crackdown) | Cooperative Chaos and crackdown | Movement and sector control |
| [RULE-POLICE-001](#rule-police-001--crackdown-detection-and-combat) | Crackdown detection and combat | Movement and sector control |
| [RULE-MOVE-001](#rule-move-001--adjacent-movement-and-friendly-capacity) | Adjacent movement and friendly capacity | Movement and sector control |
| [RULE-CONTROL-001](#rule-control-001--cooperative-sector-control-comparison) | Cooperative sector control comparison | Movement and sector control |
| [RULE-UPKEEP-001](#rule-upkeep-001--base-income-upkeep-and-debt) | Base income, upkeep, and debt | Upkeep economy |
| [RULE-TIMER-001](#rule-timer-001--optional-human-planning-limit) | Optional human planning limit | Turn timing |
| [RULE-SETUP-001](#rule-setup-001--starting-resources-and-smgfundage) | Starting resources and SMGFUNDAGE | Objectives and match completion |
| [RULE-OBJECTIVE-001](#rule-objective-001--end-of-turn-objective-evaluation) | End-of-turn objective evaluation | Objectives and match completion |
| [RULE-AWARDS-001](#rule-awards-001--endgame-performance-awards) | Endgame performance awards | Objectives and match completion |
<!-- doc-index:end -->

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
- Turn-boundary cost timing: the successful hire resolver charges only the
  initial contract cost. It installs an active gang before returning to the
  outer turn loop, so that gang pays its first ordinary Upkeep at the start of
  the immediately following turn. Normal play processes that next Upkeep before
  returning control, making both deductions visible together even though they
  occur in distinct phases. Static call evidence is `0x0046f706` ->
  `0x004726c0` -> (`0x00472750`) `0x00472775`; the contract debit is
  `0x00475cba`-`0x00475ce4`, while the separate active-roster Upkeep scan is in
  `0x0046e766` decompiler lines 194-215.
- Presentation: reserving a hire remains allowed regardless of current cash.
  The client shows a warning only when the Finance projection through Execution
  and the contract debit leaves a negative balance at the Hire boundary. It
  excludes the following turn's income and Upkeep; a queued Sell or projected
  Chaos payout can therefore fund a reservation without a warning.
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

- Source: `MANUAL-GOG-1`; Hide command and Stealth descriptions;
  `BIN-HIDE-LIFECYCLE-001` and `BIN-AI-007`.
- Observed statement: Hide makes the acting gang hidden so opposing gangs must
  detect it before targeting it.
- Interpretation: Hide becomes active as soon as it is assigned. At the next
  turn initialization, a gang's active action is replaced with its retained
  recurring action: one-off Hide therefore ends at Upkeep, while recurring Hide
  stays active until the player replaces or cancels it. An ordinary attacker
  hits a hidden target on an inclusive d20 roll at or above
  `Stealth + 14 - Detect`; an expert computer uses `Stealth + 10 - Detect`.
  Evasion skips the attack, while a hit proceeds normally but cannot retaliate.
  Police instead use
  `clamp(115 - 5 × effective Stealth - 20, 0, 100)%`. Hide does not change
  whether the gang is visible in the sector.
- Current exclusions: controlled native UI observation of the planning-time
  visibility transition.
- Confidence: High from the active/recurring action-byte writes, turn-start
  copy, combat and police consumers, and manual semantics.
- Implementation: `CommandResolver.ResolveHide`, combat detection handling,
  `ManualRules.HiddenAttackHitPercent`, and `MatchGangState.Hidden`.
- Tests: `InstantResolutionTests` covers immediate assignment, one-off expiry,
  recurring retention/counting, replacement and cancellation;
  `CombatResolutionTests` covers evasion/hit cases.
- Next experiment: capture target availability before assignment, after one-off
  Hide, and while replacing a recurring Hide in a fixed native turn.

### RULE-INFLUENCE-001 — Cooperative site influence

- Source: `MANUAL-GOG-1`; Influence command, dice, site resistance, and site
  benefit descriptions, plus `BIN-RNG-002`, `BIN-RNG-003`, and
  `BIN-INSTANT-001` and `BIN-EFFECTIVE-STATS-001`.
- Observed statement: participating gangs contribute total Force plus Influence;
  successes reduce site resistance; reaching zero influences the site and grants
  its listed benefits.
- Interpretation: scan player slots and persistent gang slots in ascending
  order. Each participating gang separately rolls
  `max(0, Force + effective Influence)` dice and immediately persists its
  successes against the remaining resistance. At zero, later queued Influence
  commands skip their roll, but ownership and benefits activate only in the
  next pre-planning sector rebuild. A completed site cannot be selected for
  Influence. There is no separate site-takeover action or persistent native
  influencer field: changing the sector owner clears all three sites' progress,
  after which the new owner may influence them again from full resistance.
  Headquarters is the zero-Resistance exception: clearing its native progress
  to zero also leaves it complete, and because completed benefits derive from
  the current sector owner, an overthrow immediately transfers Headquarters
  control to the new owner without a separate Influence action.
- Current exclusions: special-site behaviors beyond their separately recovered
  Research-cap and Factory effects.
- Confidence: High for the base pool, success threshold, resistance reduction,
  effective-stat aggregation, Support value, per-gang scheduling, completion
  guard and delayed benefit boundary, completed-site rejection, and
  ownership-change reset.
- Implementation: `CommandResolver.ResolvePhase`,
  `CommandResolver.ResolveInfluence`, `ManualRules.InfluenceDiceCount`, and
  `ManualRules.ApplyInfluenceProgress`; validation requires player ownership of
  the target sector and positive remaining resistance.
- Tests: `InstantResolutionTests` covers separate roster-ordered rolls,
  cumulative partial progress, post-completion RNG suppression,
  completion/Support, target rejection, notifications, deterministic replay,
  and phase hashes.
- Next experiment: capture one and multiple gangs influencing the same site and
  a post-overthrow reinfluence, then diff resistance, Support, RNG, and event
  ordering.

### RULE-HEAL-001 — Heal dice and force restoration

- Source: `MANUAL-GOG-1`; Heal command description, `BIN-EFFECTIVE-STATS-001`
  and `BIN-AI-007` for the shipped pool, plus `BIN-RNG-002` and `BIN-RNG-003`
  for random-number generation. Exact manual scan page location still needs
  transcription.
- Observed statement: Heal rolls a base four dice plus Heal skill; each roll of
  4-6 restores one Force, capped at 10.
- Interpretation: calculate effective Heal from the gang definition, all three
  equipped items, and every completed site in a sector controlled by the gang's
  player; roll `max(0, 4 + Heal)` six-sided dice, count successes at the
  difficulty-band threshold (5+ normally, 4+ for expert Computers), and add
  successes to Force with a maximum of ten. Heal cannot be assigned at maximum
  Force; the native turn-start scan clears a repeating Heal order at Force ten.
- Confidence: High static evidence for the pool, base/equipment/site
  aggregation, ownership scope, thresholds, Force cap, and repeat cleanup;
  High for the recovered RNG step/range wrapper and accepted-setup-to-city
  order; Low only for correlating a particular native launch's clock-derived
  state and subsequent dynamic branch path with a recreation fixture.
- Implementation: `EffectiveStatisticsCalculator`, `DiceRoller`,
  `CommandResolver.ResolveHeal`, and `ManualRules.RestoreForce`.
- Tests: deterministic roll/event/hash replay, effective Heal equipment, dice
  bounds, RNG consumption, and Force cap tests in `CommandResolutionTests`,
  `DeterminismTests`, and `ManualRulesTests`.
- Next experiment: execute Heal from an identical save and correlate the
  visible rolls and Force delta with the predicted later-match RNG stream.

### RULE-RESEARCH-001 — Research dice and persistent progress

- Source: `MANUAL-GOG-1`; Research command and item-table descriptions,
  `BIN-EFFECTIVE-STATS-001`, `BIN-AI-007`, `BIN-RESEARCH-000`, and
  `BIN-RESEARCH-001`, plus `BIN-RNG-002` and `BIN-RNG-003` for random-number
  generation. Exact manual scan page locations still need transcription.
- Observed statement: Research rolls dice equal to the gang's Force plus its
  Research skill; each success reduces the item's remaining research number.
- Interpretation: calculate effective Research from the gang definition, all
  three equipped items, and every completed site in a sector controlled by the
  gang's player; roll `max(0, Force + Research)` six-sided dice, apply the
  difficulty-band pool/threshold adjustment, persist unfinished progress, and
  mark the item researched at zero. The native turn-start scan clears a
  repeating Research order on completion.
- Tech restrictions: a gang cannot research above its own Tech. Without a local
  influenced special research site the ceiling is Tech 5; an influenced Science
  Center raises it to 8 and a Research Lab to 10. A sector must be controlled
  before its sites can be influenced, and authoritative match state rejects an
  influencer who is not the current sector owner.
- Initializer boundary: ordinary setup copies every item definition's Research
  Difficulty into each player's remaining-progress array, so zero-difficulty
  items start complete. Armageddon zeroes the complete array. The recreation
  omits inaccessible type-99 padding records from authoritative researched IDs.
  Runtime confirmation of special-site timing remains open.
- Confidence: High static evidence for initialization, the pool,
  base/equipment/site aggregation, ownership scope, difficulty adjustments,
  completion threshold, fixed roster order, later-roll suppression, and repeat
  cleanup; High for the accepted-setup-to-city RNG order and complete direct
  RNG-wrapper caller ownership; Low for the clock-derived state at Begin and
  original-runtime stream correlation.
- Implementation: `MatchPlayerState.RemainingResearch`,
  `MatchPlayerState.ApplyResearch`, `ManualRules.ResearchDiceCount`, and
  `CommandResolver.ResolveResearch`; `SpecialSiteRules.ResearchTechLimit`
  enforces the gang/site ceiling during validation. Instant resolution snapshots
  effective statistics before any same-phase Influence acquisition.
- Tests: `ResearchResolutionTests` covers effective dice count, recorded rolls,
  progress, completion, later-roster roll suppression, repeat rejection, state
  invariants, RNG consumption, notifications, deterministic phase hashes, and a
  newly influenced Science Center not changing a concurrent Research pool.
- Next experiment: execute Research from identical saves at tech-level and
  near-completion boundaries, then correlate rolls, unlock state, and save
  deltas with the predicted later-match RNG stream.

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
  `17 - density-derived base Income + sum(Tolerance)` for currently influenced
  sites. Newly completed sites apply their adjustment at the next pre-planning
  rebuild; losing influence removes it with control loss. The result may move
  outside 0–40 and remains outside Bribe/Snitch's base caps.
  During Upkeep, every sector's current value moves exactly one point toward
  its normal effective value. The post-Instant global floor then raises every
  value below 1 to 1 before Combat and Chaos.
- Confidence: High for the formula, one-point adjustment, and activation boundary.
- Implementation: `ToleranceResolver`, invoked by `MatchState.FinishUpkeep`.
- Tests: `ToleranceResolverTests` covers movement from both directions, stable
  values, direct action deltas and the post-Instant floor; influence tests cover
  delayed activation, and `ChaosResolutionTests` covers the pre-Chaos clamp.
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
  floored at zero. The executable marks a dead gang inactive without erasing
  its equipment bytes; the recreation retains those inaccessible values for
  parity and final-state inspection while clearing Hidden and live orders.
  Every opening attack credits its full computed damage to Damage Inflicted,
  even when that damage exceeds the target's Force or concurrent attacks
  collectively overkill it. Retaliation is not credited to Damage Inflicted.
- Hidden target behavior: a target that became Hidden during Instant receives
  an individual Detect-versus-Stealth percentage roll. Evasion produces an
  ordered `TargetEvaded` result; a successful hit prevents retaliation.
- Static binary confirmation: the original eligibility predicate requires the
  target action not to be Hide and allows retaliation when the attacker has no
  positive effective Martial Arts, has a weapon equipped, or faces a defender
  who is also an unarmed positive-Martial-Arts gang. The sole Damage Inflicted
  write adds the computed opening damage before accumulated damage is applied,
  with no remaining-Force cap.
- Current exclusions: controlled native combat fixture coverage.
- Confidence: High for weapon-skill associations, the complete Martial Arts /
  weapon / Hide retaliation gate, its damage formula, player/roster roll
  ordering, overkill statistic accounting, and hidden-state timing;
  Medium/High for the Force-corrected opening formula.
- Implementation: `ManualRules.CombatRating`, `ManualRules.AttackDiceCount`,
  `ManualRules.RetaliationDamage`, and `CommandResolver.ResolveCombatPhase`.
- Tests: `CombatResolutionTests` covers effective attack/defense pools,
  retaliation, phase-start simultaneity, Martial Arts, hidden targets,
  reciprocal-order coalescing, reversed-submission roster order,
  elimination/equipment retention, full overkill credit from multiple attacks,
  statistics, RNG consumption, notifications, and hashes; `ManualRulesTests`
  covers the formulas.
- Next experiment: reproduce a fixed unarmed matchup from the FAQ, then repeat
  with melee/blade/ranged weapons, Martial Arts, and a target hidden during
  Instant while capturing force bars, damage, and RNG order.

### RULE-DETECT-001 — Cooperative sector visibility

- Source: `MANUAL-GOG-1`, numbered page 47.
- Observed statement: the manual says to compare an enemy gang's Stealth with
  the highest friendly Detect in its sector, adding +1 at Detect 0–10, +2 at
  11–12, +3 at 13–14, +4 at 15–16, +5 at 17–18, and +6 at 19 or more for each
  additional friendly gang. If one friendly gang can see the enemy, all friendly
  gangs there can. The executable's rebuild differs at the helper boundaries.
- Interpretation: take the highest effective Detect as the sector base. Every
  other active same-sector gang adds 1, plus `(Detect - 8) / 2` when Detect is
  greater than 9. Thus negative through 9 adds 1; 10–11 adds 2; 12–13 adds 3;
  and the sequence remains unbounded above 19. Visibility succeeds at the
  resulting strength `>=` effective Stealth. A player always detects its own
  gang, and Hide does not affect cooperative visibility. The native Attack
  picker omits enemy records whose observer-specific visibility byte is zero;
  authoritative validation likewise rejects an undetected Attack target rather
  than trusting UI filtering alone.
- Current exclusions: exact UI refresh timing outside normal planning entry.
- Confidence: High static evidence for effective-stat fields, strongest-gang
  selection, exact helper arithmetic including negative/high values, active
  roster filtering, threshold, and the two turn-loop rebuild call sites.
- Implementation: `ManualRules.SectorDetectionStrength` and
  `MatchState.CanPlayerDetectGang`.
- Tests: `ManualRulesTests` covers band arithmetic; `CombatResolutionTests`
  covers ownership, enemy visibility, independence from Hide, and the distinct
  individual hidden-target evasion roll; `AttackTargetRosterTests` covers both
  picker filtering and authoritative rejection.

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
- Confidence: High static evidence for ownership, local scope, three-site scan
  order, all fourteen fields, and gang/equipment/site aggregation order.
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
- Initial unlock set: the static initializer confirms that ordinary scenarios
  begin with the seven non-placeholder
  zero-difficulty technologies already researched: Metal Pipe (0), Combat Knife
  (1), Combat Pistol (12), Leathers (24), Shock Pads (25), Cool Hats (39), and
  Boom Boxes (40). Melee and ranged weapons are separate browser categories,
  so the pistol does not appear beside the two initial melee choices. Armageddon
  continues to unlock every real item.
- Factory rule: an influenced Factory in the acting gang's controlled sector
  reduces purchase price to `Cost - trunc(Cost / 3)`. This is a one-third
  discount rounded toward the full price; for example, an $11 Katana costs $8.
- Confidence: High for cost, categories, research, tech gates, the statically
  verified zero-difficulty initialization, Factory division/rounding, controlled/influenced locality,
  fixed player/roster-slot resolution order, and same-slot replacement; Medium
  for repeat commands.
- Implementation: `EquipmentRules`, `SpecialSiteRules.EquipmentCost`,
  transaction validation, and `CommandResolver.ResolveEquip`.
- Tests: `TransactionResolutionTests` covers purchase, replacement, cash and
  statistics, research/tech validation, insufficient funds, replay hashes, and
  a Factory completed during Instant not discounting a same-turn
  Transaction-phase replacement; all decoded item costs exercise the recovered division formula.

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
- Observed statement: the manual says Terminate removes the gang from play and
  all items it possesses; it executes during Movement. The executable instead
  changes only the copied gang record's sector to inactive sentinel 100, leaving
  its equipment bytes as inaccessible stale state.
- Interpretation: set Force to zero and clear Hidden/live orders in the
  recreation, but retain all three equipment fields for native parity and
  final-state inspection. Resolve the complete player/roster Terminate pass
  before any Move and emit an elimination notification.
- Confidence: High for retirement field mutation, retained equipment, phase,
  and scheduling; Low for statistics and notification presentation.
- Implementation: `CommandResolver.ResolveTerminate`.
- Tests: `TransactionResolutionTests.TerminateRetiresGangButRetainsEquipmentRecordDuringMovement`
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
- Interpretation: group one player's Chaos commands by sector. Each
  participating gang contributes `generated sector Income + Force + Chaos`
  dice and rolls in fixed
  player-slot then persistent roster-slot order, independent of submission order or intervening
  sectors. Accumulate every player's successes in resolver-local player/sector
  storage before paying anybody; the executable has no persistent sector-Chaos
  accumulator. A
  sector already in crackdown, or crossing the strict `Chaos > Tolerance`
  threshold during this phase, pays no group; otherwise controlled groups earn
  all successes and uncontrolled groups earn `floor(group successes / 2)` after
  all same-player gangs in that sector have been aggregated. A newly
  triggered crackdown notifies every active player. The executable physically
  rolls Chaos and creates Crackdowns immediately after the Instant pass, before
  Combat, then resolves Combat and Transactions before paying the stored Chaos
  successes. The recreation preserves the public six-boundary interface while
  performing that preparation after Instant and delaying cash/statistic changes
  until the Chaos boundary.
- Current exclusions: exact original message wording and controlled runtime
  corroboration.
- Confidence: High for the per-gang pool, generated Income field, control
  multiplier, roster RNG order, grouped half payout, and suppression rule;
  Medium for notification presentation.
- Implementation: `CommandResolver.ResolveChaosPhase`,
  `ManualRules.ChaosDiceCount`, `ManualRules.ChaosIncome`, and
  `ManualRules.TriggersCrackdown`.
- Tests: current `ChaosResolutionTests` covers turn-start reset, pooling,
  fixed player/roster RNG order,
  grouped payout,
  statistics, sector-wide
  cross-player aggregation, existing/new crackdown behavior, notifications,
  RNG consumption, and phase hashes; `ManualRulesTests` covers arithmetic and
  the strict threshold.
- Recreation status: Chaos and Control consume generated sector Income, Chaos
  successes remain resolver-local, and the city shows the original owner-only
  **CASH** row computed from tax and influenced sites. No synthetic sector-Chaos
  state is saved or hashed.
- Next experiment: run controlled and
  uncontrolled identical saves immediately below/equal/above Tolerance and
  compare cash, police creation, and RNG state.

### RULE-POLICE-001 — Crackdown detection and combat

- Source: `MANUAL-GOG-1`; Crackdown and Math of the Game descriptions, plus
  `BIN-COMBAT-ORDER-001` and `BIN-POLICE-COMBAT-001`.
- Observed statement: during a crackdown, police attack every gang in the
  sector with Combat 20. Police detection is certain through Stealth 5 and
  drops five percentage points per additional Stealth point, reaching zero at
  Stealth 25; the manual separately lists police Detect as 12. No gang may
  attempt to control the sector until the police leave.
- Observed statement: police remain for three to five turns. Triggering another
  Crackdown while they are present makes them stay longer. Three Crackdowns in
  a five-turn period make the controlling Overlord lose the sector.
- Interpretation: the executable differs from the abbreviated manual table.
  At the Combat phase boundary, snapshot every active gang in a crackdown
  sector in ascending player/roster-slot order. Roll one inclusive percentile
  check per gang against
  `clamp(115 - 5 * effective Stealth - (Hide ? 20 : 0), 0, 100)`.
  Thus a visible gang is certain to be detected through Stealth 3, has a 95%
  chance at Stealth 4, and reaches 0% at Stealth 23; Hide subtracts exactly 20
  percentage points and reaches 0% at Stealth 19. On detection, roll
  `max(0, Police Force 5 + Police Combat 20 - effective Defense)` dice at 5+
  and apply one damage per success. Police attacks do not retaliate or credit a
  player's damage statistic. Gang-command and police damage are accumulated
  against the same phase-start snapshots before casualties and gang retirement
  are applied. Control is rejected at submission while a crackdown is already
  active and fails without changing ownership if police arrive before the
  later Control subphase.
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
  A newly created Crackdown therefore attacks in the same turn's following
  police pass; the common end-of-Combat decrement immediately consumes one of
  its drawn turns, leaving two to four future Combat phases.
- Current exclusions: exact original message wording and notification timing.
- Confidence: High for the executable detection curves, Hide reduction,
  Force 5 + Combat 20 pool, 5+ threshold, effective Defense subtraction,
  player/roster attack order, occurrence
  window/reset, and duration RNG order; Low for the listed exclusions.
- Implementation: `CommandResolver.ResolveCombatPhase`, exposed through
  `MatchState.LastPoliceAttackResolutions` and `PoliceAttackResolved` events;
  `CommandValidator` and `CommandResolver.ResolveControl` enforce the Control
  lockout at both relevant boundaries; `CrackdownResolver` owns duration and
  extension plus the two-turn history represented in the original save layout.
  `HelpContentAugmentation` places the formulas inside the in-game Crackdown
  subject, synthesizing that listed subject only for an incomplete help pack.
- Tests: `PoliceCombatResolutionTests` covers player/roster ordering,
  exact visible and Hide detection boundaries, effective Stealth/Defense,
  exact police attack-pool boundaries, deterministic RNG consumption/hashes,
  notifications, casualties,
  and retained inactive equipment. `ChaosResolutionTests` covers duration,
  extension, five-turn history boundaries, neutralization and cleanup.
  `BoardResolutionTests` covers submission-time and execution-time Control
  lockout without ownership mutation; save/replay tests cover migration and
  multi-turn continuation.
- Next experiment: capture otherwise identical pre-Combat saves spanning
  visible Stealth 3/4/22/23, hidden Stealth 0/18/19, and Defense 24/25, with
  and without a player Attack command, then compare notifications and RNG deltas.

### RULE-MOVE-001 — Adjacent movement and friendly capacity

- Source: `MANUAL-GOG-1`; Move command and command sequence descriptions, plus
  `BIN-MOVEMENT-001`.
- Observed statement: Move relocates a gang to an adjacent sector during the
  Movement phase. The structural limit is six friendly gangs per sector.
- Interpretation: move to any of the eight neighboring sectors, including a
  diagonal neighbor, rejecting a target already at friendly capacity when the
  command is assigned. After all Terminate commands, the executable normalizes
  each player's complete Move set before changing any sector bytes. It counts
  non-movers at their current sectors and movers at their proposed destinations,
  selects the highest-numbered over-capacity sector, and rewrites the first
  qualifying mover in ascending roster order back to its source. It repeats
  until every projected count is at most six. A rewritten command then resolves
  as a successful no-op rather than a failed Move, so when two otherwise legal
  moves compete for one remaining slot the later roster slot moves and the
  earlier one stays. The helper's defensive saturated-cycle branch routes a
  previously rewritten no-op through selector mode 0: a uniform all-sector draw
  followed by the usual horizontal-then-vertical capacity-checked one-step move.
  Only an active gang occupies one of the six places. A gang killed in Combat or
  by police, and one that Terminated, keeps the sector it died in on its inactive
  record — that is the record the hire resolver later reuses in place — so a
  contested sector accumulates more gang records than the bound over a long match
  without ever breaking it.
- Confidence: High static evidence for adjacency, submission capacity, phase
  precedence, projected-count construction, highest-sector selection, roster
  rewrite direction, repeated normalization, mode-0 fallback, and final Move
  application.
- Implementation: movement validation and
  `CommandResolver.ResolveMovementPhase`/`NormalizeMoveDestinations`.
- Tests: `BoardResolutionTests` covers movement events, capacity at submission,
  reversed-submission runtime contention, native earlier-roster cancellation,
  successful no-op results, Terminate precedence, and notifications;
  `OriginalAiSectorSelectionRulesTests` covers mode-0 fallback RNG and routing;
  `NativeSaveSerializerTests` covers a save carrying inactive records beyond the
  bound and still refusing a seventh active gang in one sector.

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
  Control also clears when a Crackdown is active. Repeating Influence clears
  when its target reaches zero resistance or the acting player loses sector
  ownership. These predicates run before Crackdown duration and pending-site
  updates at the following turn start. The
  individual-gang recurring actions are Chaos, Control, Heal, Hide, Influence,
  and Research. Bribe and Snitch are one-off despite having no turn-start
  terminal case in the executable. Attack, Bribe, Equip, Give, Move, Sell,
  Snitch, and Terminate are absent from the recurring-action picker; core
  validation rejects a repeated form submitted through any other path. The
  native sector-wide recurring menu is narrower still and omits Research.
  The shipped winner scan has a clear negative-defense bug: it compares all
  six player slots after initializing nonparticipants to zero strength. When
  combined sector Income, defending strength, and Support is negative, a
  player who issued no Control command can tie or beat the real challenger and
  receive the sector. The recreation deliberately limits candidates to players
  with an actual Control command while preserving the recovered arithmetic and
  tie behavior among those participants.
- Moving or terminating the last friendly gang does not abandon the sector;
  ownership changes only through a separate ownership-changing rule.
- Native phase-order detail: a Crackdown created by this turn's Chaos rolls is
  already active when the later Control scan runs. The original silently omits
  that sector from Control resolution; the recreation records an explicit
  failed result for auditability while preserving the same no-capture outcome.
- Current exclusions: controlled runtime tie corroboration.
- Confidence: High for equation components, generated sector Income,
  influence loss, zero-margin neutral selection, and cross-player winner/order
  behavior.
- Implementation: `ManualRules.ControlStrength`, `ManualRules.ControlMargin`,
  grouped `CommandResolver.ResolveControl`, and site-reset handling.
- Tests: `BoardResolutionTests` covers neutral capture, board/roster ordering,
  pooled strength, generated-Income defense, defended
  failure, recorded deterministic zero-margin chance, positive and zero-margin
  cross-player ties, unique-highest neutral conflicts, retained empty-sector
  ownership after Move/Terminate, a single phase-opening
  defense for several owned-sector challengers,
  execution-time Crackdown rejection, overthrow/statistics, influence reset,
  deterministic hashes, the intentional nonparticipant negative-defense bug
  correction, and the one-off/recurring Hide lifecycle;
  `ManualRulesTests` covers equation arithmetic.

## Upkeep economy

### RULE-UPKEEP-001 — Base income, upkeep, and debt

- Source: `MANUAL-GOG-1`; Upkeep, Finance, Sector Tax, and Gang Upkeep
  descriptions; `EXE-GOG-1.1` cash updater `0x0046e766`, sector recomputation
  helper `0x004782c5`, and write-back call `0x0046f246`.
- Observed statement: each controlled sector grants $1; influenced sites apply
  their listed cash values; active gangs charge their listed upkeep. The
  executable combines the first two components into a sector byte recomputed as
  `1 + completed-site Cash`, then adds that byte once for its owner. Cash may
  become negative. The manual says equipment, Bribe, and Snitch are restricted,
  while the executable's Snitch resolver contains no debt gate.
- Interpretation: for each active player in stable ID order, add flat
  controlled-sector tax and influenced-site cash, then subtract active-gang upkeep, with
  checked integer arithmetic on turns after the initial planning turn. The
  first outer-loop pass skips collection entirely. Runtime affordability rejects equipment and
  Bribe; Snitch remains free and executable in debt. Hiring permits a zero-cost
  gang even while the balance is negative. The native statistics classify each
  component independently: nonnegative gang Upkeep increases Cash Spent,
  negative Upkeep increases Cash Earned by its magnitude, positive combined
  sector Income increases Cash Earned, and zero/negative sector Income takes
  the Cash Spent branch (with zero adding nothing). Opposite-sign components
  therefore do not cancel before the endgame statistics are updated.
  The recomputed byte is the owner-only sector Cash row shown in the city UI.
  It is distinct from the generated 3-7 sector Income byte used by Control and
  Chaos and displayed on the Income row.
- Current exclusions: cash adjustment, special gang/item/site modifiers, and
  integer overflow behavior.
- Confidence: High static evidence for flat sector tax, influenced-site Cash,
  gang upkeep, scan/recomputation order, per-component Cash Earned/Cash Spent
  classification, and negative-cash restrictions; Low for excluded edge cases.
- Implementation: `EconomyResolver.ResolveUpkeep` and
  `MatchState.FinishUpkeep`; `FinanceProjection` previews the same component
  classes and mirrors the recovered last-slot payout overwrite for multi-item
  Sell.
- Tests: `EconomyResolutionTests` covers the initial skip, component accounting,
  positive and mixed-sign statistics accounting, the latent negative-Upkeep
  branch, persistent debt, eliminated players, ordered events/notifications,
  and deterministic phase hashes. Command, transaction, and hire tests cover
  debt restrictions.
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
  uppercase `SMGISLANDS` subsequently gives neutral non-HQ sectors permanent
  Crackdown duration 100.
  Three other exact uppercase names alter the player's opening state:
  `SMGSPANK` adds five more Force-10 Right Hands in the HQ; `SMGKICKASS` adds
  five Force-10 GROUND ZERO gangs equipped with PLASMA GENERATOR, BATTLE SUIT,
  and EMPATHIC ENHANCER; and `SMGHUBBLE` makes every opposing gang detectable
  in every sector. None of these setup modifiers consumes RNG.
- Local setup starts with one human. Add/Remove changes the human count from one
  through six, and Begin fills every remaining slot with a Computer. Human names
  use the Help-specified 10-character name field; portrait 15 is the empty
  marker and cannot be selected as a human face. Consequently there is no
  separate per-player Human/AI switch: remove a local human to leave that color
  slot for AI completion, or add one to reclaim the next local-human slot.
- A face dragged to an empty color moves that local-human identity to the target
  slot. A face dropped on another human exchanges their colors. The resulting
  sparse human slots are preserved as player IDs, then every missing slot is
  filled in ascending order before AI initialization and city generation.
- Current exclusions: the remaining setup call context and an original runtime
  fixture remain open.
- Confidence: High static evidence for ordinary/Armageddon cash, the name
  override, city/HQ generation and Right Hands Force; runtime correlation pending.
- Implementation: `OriginalMatchFactory.Create`, `MatchBootstrap.Create`, and
  `MatchState.CanPlayerDetectGang`.
- Tests: `MatchBootstrapTests` covers Armageddon resources, exact-case
  `SMGFUNDAGE` behavior in ordinary and Armageddon games, and verifies research
  state through the normal query API. `OriginalCityGeneratorTests` locks fixed
  city, Armageddon-rejection, HQ-permutation, empty-slot portrait/name ordering,
  `SMGISLANDS`, all three additional exact-name setup/visibility modifiers,
  online name reservation, and RNG-continuation vectors.

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
  players in player-ID order. Before those scenario checks, exactly one active
  Overlord ends any match immediately as the sole survivor. Emit one
  `MatchEnded` event and one Objective
  notification per player; include the outcome in canonical state hashes and
  prevent the following Upkeep phase from resolving. Timed outcomes include
  score-descending standings for every scenario; equal scores share a
  competition rank, with the next place skipped, and eliminated players follow
  the active ranking unranked in player-slot order. Fixed inactive slots retain
  the executable's -32,000 score sentinel during rank counting. In Greed, an
  active player with cash below -32,000 can therefore have numeric place 6 even
  when they are the only active player; inactive players still display last and
  unranked. Objective
  ranking uses the executable's scenario table: sectors for Big 40/Armageddon,
  owned HQ sectors for Eliminate, accumulated points for Big Man, and the common
  inactive-player count for Kill 'Em All/Siege. Dominance divides its weighted
  numerator by ten before ranking. Big Man points are awarded in player-ID order
  at this boundary before its victory check. Native Eliminate cleanup retires
  every gang by writing sector 100 but leaves inaccessible stale record payloads,
  including equipment; the recreation preserves those item fields for parity and
  final-state inspection while using Force zero and clearing live queue/Hidden
  state to represent native sector-100 inactivity. It also clears pending hires
  and derived site influence. After elimination
  resolution, a one-human match records `PlayerEliminated` immediately when that
  human is no longer active; a hot-seat match continues after an elimination
  only while at least two Overlords remain active.
- Current exclusions: tie-break presentation beyond stable slot order and award
  edge-case parity.
- Confidence: High static evidence for end-boundary timing, thresholds,
  all-scenario scores, competition standings, active/inactive display order,
  durations, weights, Big Man accumulation, Eliminate cleanup/order, and Siege
  setup mapping and exact Siege/Big Man pylon presentation; Low for remaining
  special objective presentation edges.
- Implementation: `MatchOutcomeEvaluator`, scenario-specific elimination and
  Big Man accrual in `MatchState.FinishPlayerElimination`, `MatchState.Outcome`,
  and the canonical state hash.
- Tests: `MatchOutcomeTests` covers authoritative projection, objective event
  and notification emission, immediate single-player defeat versus continuing
  hot-seat play, Eliminate's Right Hands distinction, exact timed
  boundary ties/standings, objective standings, and outcome hashing;
  `EndgameRankingTests` covers recovered all-scenario scores, competition ties,
  and inactive ordering; `ScenarioLifecycleTests` covers Big Man accrual/event
  order and Eliminate cleanup/neutralization with preserved retired equipment.
  `OriginalCityGeneratorTests` covers fresh
  Siege landmark assignment and one-important-sector-per-player starting state;
  `UiNavigationTests` covers the exact pylon crop and both scenarios' marked
  sector sets.
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
- Static binary confirmation: builder `0x0042b9e0` processes Fist, Skull, Big
  Fat Chicken, Dollar Sign, then Safe. Their inclusive baselines are respectively
  5 Overthrows, 50 direct Damage, 10 Hide resolutions, 0 Cash Spent, and a
  999,999 initial least-spent ceiling. It scans all six player slots, including
  inactive ones, and preserves every player tied at the selected value.
- Interpretation: calculate the five superlatives in native priority order from
  authoritative player statistics. Omit Fist, Skull, or Chicken below its
  recovered activity threshold. Retain zero-valued Dollar/Safe ties. Outcome
  data retains every award, while the native row presenter shows only the first
  three awards assigned to a player. Every resolved Hide increments the counter;
  recurring Hide therefore counts again each turn while remaining hidden.
- Current exclusions: none for award calculation or Hide counting.
- Confidence: High for award/statistic mapping, thresholds, scan/order, ties,
  inactive-player eligibility, three-icon presentation cap, and retaliation
  exclusion. Static resolver evidence makes repeated Hide counting High.
- Implementation: `EndgameAwardEvaluator`, `MatchStatistics.TimesHidden`,
  direct-damage accounting in `CommandResolver`, and award snapshots in
  `MatchOutcome`, `MatchEnded` events, and canonical hashes.
- Tests: `EndgameAwardTests` covers every category, exact inclusive thresholds,
  priority, ties, three-icon presentation, outcome/event integration and hashes;
  `CombatResolutionTests` verifies that
  retaliation is not credited; `InstantResolutionTests` verifies initial and
  recurring Hide counts while preserving hidden state across Upkeep.
- Next experiment: capture a native golden Endgame Screen containing tied and
  threshold-boundary awards to confirm final typography and icon placement.
