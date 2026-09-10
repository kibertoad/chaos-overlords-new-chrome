# AI specification

Status: provisional recreation baseline  
Last updated: 2026-09-10

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
- Before each computer player's plan, a replay-recorded preparation step applies
  the recovered directional hostility rule. Eligible opponents become maximally
  hostile when the computer has a strict effective Combat + Defense advantage
  in more than 75 percent of that opponent's controlled sectors. The same step
  prepares recovered family-1 commands and consumes any mode-5 maximum-tie RNG
  exactly once; subsequent `Plan` queries do not consume it again.
- Control ranking uses the recovered strict solo-strength boundary: the acting
  gang's Force + Control must exceed sector Income plus detectable defending
  Force + Control and owner-influenced Support. Equal or weaker solo attempts
  are demoted below useful commands.
- Heal ranking follows the recovered continuation gates: effective Heal must
  be at least -3 and Force must be below 9. A gang at Force 9 can legally Heal,
  but the original planner does not select it in any recovered family-1 path.
- Family 1 has one narrower live override when the immediately previous action
  is None or Chaos. With Force below 8 and effective Heal at least -3 it chooses
  Heal unless the current sector has an active Crackdown, in which case it
  chooses Move. Otherwise an older Snitch chooses Chaos and every other older
  action chooses Move. The action branch and complete mode-5 Move selection are
  recovered and live. Only the unavailable-command fallback remains
  provisional when the selected step is not legal in the recreation.
- When family 1's immediately previous action is Heal, it repeats Heal while
  Force is below 9 and effective Heal is at least -3. Outside that gate it
  chooses Control when the strict selector-`0x2c` solo-control predicate
  succeeds, and mode-5 Move otherwise. This action branch and complete mode-5
  destination are also live; the same unavailable-action fallback qualification
  applies.
- Family 1's post-equipment continuation after prior Control, Equip, or Snitch
  is recovered as an isolated rule kernel. A human-owned sector chooses crime
  at cash 50 or more and Mentality Criminal or higher. A non-human-owned sector
  chooses crime only for a different raw owner strictly above player zero, cash
  50 or more, and exactly Goon Mentality. Crime is Chaos through Tolerance 3
  and Snitch from 4; every failed gate chooses mode-5 Move. This branch is not
  live yet because the preceding equipment cooldown and nearby-danger gates
  are not fully represented.
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

Every mentality uses the same authoritative state, command validation, economy,
and RNG stream as a human player. The AI receives no extra cash, statistics,
visibility, or hidden resources. Its recovered per-player resolution band does
intentionally calibrate the documented command dice pools and success thresholds
for computer players; those rule differences are explicit simulation state, not
secret planner information or an added resource bonus.

Hiring now applies the original scenario schedule, family-quota adjustments,
attempt gate, and exact three-offer role ranking after command planning. An
unaffordable ranked winner does not fall back to another offer. It chooses at
most one offer during its planning turn. Static analysis has now recovered the
separate persistent placement anchor, its neutral-neighbor acceptance rule,
deterministic fallback passes, Big Man central-sector ordering, visible-hostile
and Siege overrides, and the encoded no-RNG destination path. These placement
rules are isolated in `OriginalAiHireAnchorRules`,
`OriginalAiHirePlacementRules`, and `OriginalAiHirePlacementModeRules` and are
now wired into live planning. The planner refreshes or preserves the encoded
anchor using owned-sector occupancy, neutral-neighbor availability, and the
previous Chaos-action count; then applies Big Man, first-visible-hostile, and
Siege overrides. Encoded placement consumes no RNG. Actual placement remains
deferred to the internal Hire phase.

Movement scoring currently includes the recovered objective geography for
both Big Man (sectors 27, 28, 35, and 36) and Eliminate (the six possible
headquarters sectors 9, 12, 30, 33, 51, and 54). A pure kernel implements the
exact original modes 1-5 clipped-square search, late filters, stable maximum
ties, RNG consumption, and x-then-y six-gang-capacity gate. Mode 5 is wired for
the two recovered family-1 continuations during replay-recorded preparation;
when late filtering leaves a zero maximum it draws uniformly among all 64 tied
sectors and capacity-routes toward that draw. The other mode consumers remain
unwired pending their complete outer guards.

The test suite drives Greed, Power, Acceptance, and Dominance through complete
two-computer six-month matches. Each scenario is run twice at a fixed seed and
must produce the same final state hash; its complete mutation stream must also
round-trip through the replay serializer to that hash. Kill 'Em All, Big 40,
Eliminate, Siege, Big Man, and Armageddon each run the same deterministic,
replay-verified two-computer harness through 20 turns or objective completion.

## Required parity work

1. Finish naming the last subordinate fields feeding shared weighted sector
   selector `0x00408642`. Its ring search, all direct family call sites,
   modes 1-5, site Support/Cash/Stealth modes 7-9, human-player count,
   mode-5 movement weights (neutral/owned/enemy `5:2:1`), maximum-score random
   ties, and x-then-y one-step routing are now bounded. The routing field at
   `0x00489950` is the active player's per-sector gang count, so the `<=5`
   checks are the original six-friendly-gang destination limit. Modes 7 and 8 are
   confirmed as Support- and Cash-focused Influence routing, while mode 9 seeks
   influenced-site Stealth for a Hide/Chaos path. The six-by-six directional
   attitude matrix, Homicidal human/computer initialization, non-Homicidal per-turn recovery,
   negative-hostility enumerators, `3..6` non-Homicidal reaction values, exact
   combat/Control decrements, and the mentality-gated sector Combat + Defense
   advantage hostility pass are also identified and implemented with their
   exact pre-city RNG order. The separate per-player resolution band is
   initialized to 0/1/2 for computer players at Goon/Criminal/higher settings;
   all nine consumers now drive the recovered Heal, Influence, Research, Chaos,
   Crackdown, hidden-detection, Attack, and retaliation formulas. The family-11
   local Attack gate and its blocks-of-six mode-10 formation anchors/mode-16
   followers are also bounded. Its exact selector-`0x61` weapon upgrade rule,
   including class scoring/ties, research, local-cap, raw-Tech, affordability,
   strict-improvement, previous-Attack, and cooldown gates, is isolated in
   `OriginalAiEquipmentRules`; it is intentionally not applied to unrelated
   families by the provisional scalar planner. The exact ten-scenario by
   seven-hire-role family table is implemented by `OriginalAiFamilyRules`,
   including unmapped cells which preserve the current family. AI planning
   preparation now rolls the current role into the previous role and updates
   every active gang's authoritative family slot. The mode-4 auxiliary-sector
   copy awaits representation of that separately stored field. The original objective-specific base
   turn schedules are isolated in `OriginalAiHireRoleRules`; Dominance alone
   uses an eleven-turn period, while the other nine objectives use ten. The
   objective-specific adjustments and hire-attempt gates are also
   instruction-verified and isolated, including late-game remaps,
   duration-scaled family quotas, and final mandatory-family overrides. Greed
   stops at the final duration eighth, Power/Acceptance/Dominance stop with two
   turns left, most other scenarios use only an inclusive gang limit, and Big
   Man uniquely bypasses that normal limit gate. The limit itself is exact:
   it branches on neutral non-Crackdown territory, the strict cash-above-300
   boundary, owned-sector counts, active gangs, and scenario multipliers of
   1.5, 2, or 4 before capping at 80. The three-offer helper's six exact
   role rankings, scenario-specific filters and tie directions, rich-player
   mode override, post-ranking affordability check, and no-fallback behavior
   are isolated in `OriginalAiHireRules`. Its failed-hire rejection selector is
   also exact: Greed rejects slot zero while other scenarios minimize a
   Stealth-weighted positive-stat efficiency ratio with first-tie priority.
   Greed's remaining schedule flag is now identified as whether at least one
   player has a strictly greater scenario score; tied leaders do not set it.
   Authoritative state now preserves the fixed six current/previous hire roles
   and six-by-81 family slots. The live planner applies the hire attempt gate,
   computes the post-command role from the exact schedule and adjustments, and
   uses its ranking mode for exact three-offer selection. For selector `0x5f`,
   queued Move targets project the recovered family-6 coverage behavior; the
   underlying two auxiliary shorts remain deliberately unmodeled. The hire
   model now preserves three fixed slots, same-slot tombstones, mutually
   exclusive actions, and next-planning-entry refill. Exact hire destination
   selection and its persisted anchor are statically recovered in
   `BIN-AI-003C`, covered by pure kernels, and wired into live planning. The
   encoded anchors are authoritative and persisted.
   Static analysis now recovers the exact three-generation per-gang action
   tuples, active-slot rollover, duplicate cleanup, dispatch/anchor ordering,
   reset/reuse behavior and first-plan flags.
   All three complete action tuples are authoritative and persisted. Their two
   target bytes retain the original command-dependent encodings: player/roster
   slot for Attack, item for Equip/Research, local site slot for Influence,
   sector for Move, equipment mask plus friendly roster slot for Give, and
   equipment mask for Sell. The six first-planning flags are now
   authoritative, hashed, and persisted: a player's first preparation resets
   all 81 records and skips action rollover; later preparations roll active
   records normally. Resolved hires reuse the first inactive slot and reset its
   family and action history. After rollover, each sector with duplicate prior
   Chaos rewrites only its first ascending matching slot to None; duplicate
   Influence similarly rewrites only its first match to Snitch.
   A failed ranking now uses the recovered scenario-specific rejection selector
   and records the resulting snub. Selectors `0x3f`, `0x3e`, and `0x3d` read
   the older, immediately previous, and newly planned action bytes. Selectors 0 (scenario), `0x48`
   (planning-record initialized flag), `0x5a` (mirrored gang projection),
   `0x7c` (per-player hire role), 3 (player cash), 4 (sector
   Tolerance), `0x21` (sector owner), `0x2c` (strict Control feasibility),
   `0x35` (human owner), `0x3c` (Force), `0x3d` (queued action), and `0x51`
   (Heal), plus action bytes 3 (Chaos), 10 (Move), and 13 (Snitch), are now
   bounded in `ORIGINAL-INTERNALS.md`. Mode 6 is now recovered as a family-2
   Move route toward the unique scenario leader (or all tied leaders), with an
   additional two-point preference for hostile human owners when humans
   participate. Its pair flag permits Control—not Attack—when no defending
   owner gang is visible. The hostility pass counts only visible
   defenders, requires a strict integer ratio above 75 percent, and writes
   `-10` in the observer-to-owner direction. Static executable kernels now
   guard family-1's cash 50/51, Force 8/9, effective-Heal -3/-4, Tolerance
   3/4, human-owner Mentality-at-least-Criminal, and non-human-owner exact-Goon
   boundaries, including the original raw-owner-greater-than-zero asymmetry.
   The complete previous-None/Chaos and previous-Heal action
   branches are live, including selector `0x2a` as the current-sector
   active-Crackdown predicate, selector `0x2c` as strict solo Control, and
   replay-recorded mode-5 destinations. The recovered post-equipment terminal
   branch remains isolated until selectors `0x65`/`0x66` and the surrounding
   selector-`0x6c` gate have authoritative state. Capture controlled original
   turns that reach the remaining choices through their complete selector
   context before replacing more recreation policy.
2. Capture fixed-state decisions for every scenario and difficulty.
3. Replace provisional weights and tie-breaking only when supported by those
   fixtures.
4. Extend the current ten-scenario two-player coverage to larger player counts,
   difficulty variants, objective completion stress cases, and statistical
   reference traces.

The persisted simulation now carries the recovered directional attitude matrix
and reaction values through hashing, saves, and replays, and command resolution
uses the recovered 0/1/2 calibration. The current scalar Mentality ranking
bonuses remain explicitly provisional and must not be described as exact
original planning policy.

The original manual and contemporary developer FAQ corroborate four global
mentalities, increasing aggression, a player-denial emphasis at Homicidal
Maniac, and fair play without hidden resources. They do not corroborate the current
score constants; exact parity remains blocked on completing the consumer trace
and reference-decision work above.
