# Game rules and evidence

Status: partial, active research
Last updated: 2026-09-08

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
- Interpretation: the Instant resolver changes the acting gang's persistent
  `Hidden` flag to true and records the before/after transition without an RNG
  roll.
- Current exclusions: reveal timing, detection contests, action restrictions,
  site/detect modifiers, and repeated Hide behavior in the original executable.
- Confidence: High that Hide enters hidden state; Low for timing and all exit or
  detection interactions.
- Implementation: `CommandResolver.ResolveHide` and `MatchGangState.Hidden`.
- Tests: `InstantResolutionTests.HideMarksGangHiddenAndRecordsTransition`.
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
  `ManualRules.ApplyInfluenceProgress`.
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
