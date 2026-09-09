# AI specification

Status: provisional recreation baseline  
Last updated: 2026-09-09

The original executable's difficulty branches and evaluation weights have not
yet been recovered. The current planner exists to make Human-versus-Computer
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
- Selection is stable by score, action, target kind, target ID, and secondary ID.
- A shared projected cash budget prevents the planner from intentionally queuing
  more Bribe/Equip cost than the player currently holds.
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

Hiring ranks valid affordable offers by Force, Tech, Upkeep, and initial cost.
It currently chooses at most one offer per Hire phase.

The test suite drives Greed, Power, Acceptance, and Dominance through complete
two-computer six-month matches. Each scenario is run twice at a fixed seed and
must produce the same final state hash; its complete mutation stream must also
round-trip through the replay serializer to that hash. Kill 'Em All, Big 40,
Eliminate, Siege, Big Man, and Armageddon each run the same deterministic,
replay-verified two-computer harness through 20 turns or objective completion.

## Required parity work

1. Recover original difficulty levels, bonuses, observable-information rules, and
   command-selection entry points around the dispatcher noted in
   `ORIGINAL-INTERNALS.md`.
2. Capture fixed-state decisions for every scenario and difficulty.
3. Replace provisional weights and tie-breaking only when supported by those
   fixtures.
4. Extend the current ten-scenario two-player coverage to larger player counts,
   difficulty variants, objective completion stress cases, and statistical
   reference traces.
