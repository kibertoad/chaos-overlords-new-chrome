# AI specification

Status: provisional recreation baseline  
Last updated: 2026-09-09

The original executable's complete difficulty branches and evaluation weights
have not yet been recovered. Static analysis has recovered the outer per-gang
dispatcher, its 15-value handler map, the distinct zero-based four-valued AI
Mentality global, all six writes to it, and all eight genuine query consumers.
The current planner exists to make Human-versus-Computer
matches operable while preserving deterministic simulation and replay behavior.
It must not be cited as behavioral parity with the original AI.

## Inputs and invariants

- `AiTurnPlanner.Plan` only accepts the active computer player during Command.
- It reads authoritative `MatchState` and returns at most one command per active
  gang without mutating state or consuming simulation RNG.
- Candidate commands come exclusively from `CommandOptionCatalog.LegalCommands`.
- Attack candidates are additionally restricted by the same cooperative sector
  detection query exposed to players, so the baseline does not target gangs it
  cannot observe.
- Control ranking uses the recovered strict solo-strength boundary: the acting
  gang's Force + Control must exceed sector Income plus detectable defending
  Force + Control and owner-influenced Support. Equal or weaker solo attempts
  are demoted below useful commands.
- Heal ranking follows the recovered continuation gates: effective Heal must
  be at least -3 and Force must be below 9. A gang at Force 9 can legally Heal,
  but the original planner does not select it in any recovered family-1 path.
- Selection is stable by score, action, target kind, target ID, and secondary ID.
- A shared nonnegative spending budget prevents the planner from intentionally
  queuing more Bribe/Equip cost than the player currently holds while still
  allowing validator-approved free actions from a negative balance.
- `ChooseHire` considers only authoritative `HireRules.Validate` successes in
  controlled sectors and does not mutate state.
- The client submits every selected command/hire and phase transition through
  `MatchReplayRecorder`.

## Current policy

The baseline favors healing damaged gangs, taking uncontrolled sectors,
influencing valuable sites, useful affordable equipment, and incomplete
research. Scenario modifiers emphasize attacks for Kill 'Em All/Eliminate,
sector acquisition for Power/Big 40/Armageddon, important sectors for Siege,
central sectors for Big Man, Support for Acceptance, and income for
Greed/Dominance. Terminate is deliberately last-resort.

Difficulty is one global match setting, matching the original setup panel's
**AI Mentality** choice rather than a property selected per opponent. Goon,
Criminal, and Crime Lord progressively increase attack preference. Homicidal
Maniac adds the largest attack bias and particularly favors human-controlled
targets. These weights are provisional recreation policy: the manual and
contemporary FAQ support the behavioral direction, but not the numeric values.

Every mentality uses the same authoritative information, validation, economy,
combat formulas, and RNG as a human player. The AI receives no extra cash,
statistics, rolls, visibility, or other difficulty bonus. Setup hover text makes
that fair-play invariant explicit.

Hiring ranks valid affordable offers by Force, Tech, Upkeep, and initial cost.
It currently chooses at most one offer during its planning turn; placement is
deferred to the internal Hire phase.

The test suite drives Greed, Power, Acceptance, and Dominance through complete
two-computer six-month matches. Each scenario is run twice at a fixed seed and
must produce the same final state hash; its complete mutation stream must also
round-trip through the replay serializer to that hash. Kill 'Em All, Big 40,
Eliminate, Siege, Big Man, and Armageddon each run the same deterministic,
replay-verified two-computer harness through 20 turns or objective completion.

## Required parity work

1. Finish naming the subordinate predicates feeding shared weighted sector
   selector `0x00408642`. Its ring search, modes 1-5, mode-5 movement weights
   (neutral/owned/enemy `5:2:1`), maximum-score random ties, and orthogonal
   next-step routing are now bounded, while modes 6-16 remain only
   structurally mapped. Selector `0x3e` is identified as the
   previous-turn action byte. Selectors 3 (player cash), 4 (sector
   Tolerance), `0x21` (sector owner), `0x2c` (strict Control feasibility),
   `0x35` (human owner), `0x3c` (Force), `0x3d` (queued action), and `0x51`
   (Heal), plus action bytes 3 (Chaos), 10 (Move), and 13 (Snitch), are now
   bounded in `ORIGINAL-INTERNALS.md`. Capture the resulting cash 50/51, Force
   8/9, and Tolerance 3/4 boundaries as fixed-state reference fixtures before
   replacing recreation policy or its provisional destination weights.
2. Capture fixed-state decisions for every scenario and difficulty.
3. Replace provisional weights and tie-breaking only when supported by those
   fixtures.
4. Extend the current ten-scenario two-player coverage to larger player counts,
   difficulty variants, objective completion stress cases, and statistical
   reference traces.

The original manual and contemporary developer FAQ corroborate four global
mentalities, increasing aggression, a player-denial emphasis at Homicidal
Maniac, and fair play without AI bonuses. They do not corroborate the current
score constants; exact parity remains blocked on completing the consumer trace
and reference-decision work above.
