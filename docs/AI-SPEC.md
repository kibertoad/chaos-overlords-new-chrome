# AI specification

How the rebuild's computer players are built: the Original and Advanced policies,
what the planner reads and guarantees, and how far recorded runs of the original
prove it. The original's planner itself is specified in the `AI` area of the
[spec](../spec/index/by-area.md#ai); this document does not restate it.

<!-- doc-index:begin toc depth=2 -->
- [Original and Advanced policy architecture](#original-and-advanced-policy-architecture)
- [Inputs and invariants](#inputs-and-invariants)
- [Current policy](#current-policy)
- [How far the planner is proved](#how-far-the-planner-is-proved)
<!-- doc-index:end -->

## Original and Advanced policy architecture

`AiPolicyPlanner` is the only runtime policy entry point. Original mode delegates
directly to `AiTurnPlanner.Plan`. Advanced mode calls that same recovered planner
and composes small, named transformations over its command list; it does not fork
or copy strategy-family handlers. Both policies share legal-command generation,
visibility, costs, objective scoring, resolution, and authoritative state. A new
Advanced behavior must therefore be implemented as an isolated delta, documented
in the imported in-game Help augmentation, and covered by paired Original and
Advanced fixtures.

Original is the default. Advanced currently makes exactly these changes:

- It gives an active gang left idle by Original at most one locally compelled
  legal fallback: Control its current uncontrolled sector, Chaos in a sector it
  controls, Attack a detectable rival in its sector, or Heal while injured, in
  that strict order. If none is legal, it remains idle. Attack ties prefer lower
  current target Force and then target ID.
- On Crime Lord and Homicidal Maniac only, a gang at Force 8 or higher in a sector
  it controls moves to a neighboring non-controlled sector when Original leaves
  it idle or repeats Hide, Snitch, Bribe or Chaos from its preceding turn. The
  move uses
  the existing objective/income destination score, then lower sector ID. A
  detectable local rival suppresses an Advanced-added move, which may not reduce
  the friendly gangs remaining after already planned outbound moves below one;
  incoming moves are not counted. Original Move commands are not cancelled. Goon
  and Criminal intentionally keep their more passive cadence.

Advanced policy changes are evaluated with paired same-seed Original/Advanced
fixtures. Each policy pass requires an isolated A/B case recording idle
gang-turns, controlled and retained sectors, survival, and scenario progress;
large combined tournaments supplement these cases but cannot substitute for
them because one improvement could otherwise conceal another regression.
The aggregate gates run 12 identical seeds for 15 turns per isolated feature:
Crime Lord expansion on Power and Criminal idle recovery on Kill 'Em All. Each
gate requires Advanced to control at least as many sector-turns, to end with at
least as many sectors, and to hold at least as many defended sector-turns as
Original. The expansion gate also requires more outward moves, the idle gate
fewer idle gang-turns. In the current samples expansion raises outward moves
from 632 to 840, controlled-sector turns from 3,707 to 4,255, final controlled
sectors from 542 to 615 and defended controlled-sector turns from 3,676 to
4,134. Idle recovery removes all 114 idle gang-turns and raises controlled-sector
turns from 3,972 to 4,003, final controlled sectors from 540 to 553 and defended
controlled-sector turns from 3,917 to 3,940. Seed pairs execute independently
with a maximum of two workers.

These transformations consume no RNG and add no cash, statistics, discounts,
damage, success chance, or hidden information. AI Mentality remains the separate
global resolution setting. The policy is immutable match setup and is included in
native saves, replays, canonical hashes, and online game settings.

## Inputs and invariants

- `AiTurnPlanner.Plan` only accepts the active computer player during Command.
- It reads authoritative `MatchState` and returns at most one command per active
  gang without mutating state or consuming simulation RNG.
- Each gang submits the one command its planning record describes, and only
  when the authoritative command validator accepts it and it fits the shared
  budget. A record whose action needs a target it does not hold plans nothing.
- An Attack is additionally restricted by the same cooperative sector detection
  query exposed to players, so the planner does not target gangs it cannot
  observe.
- Before each computer player's plan, a replay-recorded preparation step applies
  the recovered directional hostility rule. Eligible opponents become maximally
  hostile when the computer has a strict effective Combat + Defense advantage
  in more than 75 percent of that opponent's controlled sectors. The same step
  runs every gang's family handler and consumes any mode-5 maximum-tie RNG
  exactly once; subsequent `Plan` queries do not consume it again. `Plan`
  refuses a player whose handlers have not written its records this turn.
- Control choices use the recovered strict solo-strength boundary: the acting
  gang's Force + Control must exceed sector Income plus detectable defending
  Force + Control and owner-influenced Support. Equal or weaker solo attempts
  are not chosen.
- Heal choices follow the recovered continuation gates: effective Heal must
  be at least -3 and Force must be below 9. A gang at Force 9 can legally Heal,
  but the original planner does not select it in any recovered family-1 path.
- Family 1 has one narrower live override when the immediately previous action
  is None or Chaos. With Force below 8 and effective Heal at least -3 it chooses
  Heal unless the current sector has an active Crackdown, in which case it
  chooses Move. Otherwise an older Snitch chooses Chaos and every other older
  action chooses Move. The action branch and complete mode-5 Move selection are
  recovered and live. If modern command validation cannot project the exact
  prepared tuple (notably a same-sector Move after capacity routing), Original
  policy leaves the gang without a submitted command; it never substitutes a
  recreation-scored action.
- When family 1's immediately previous action is Heal, it repeats Heal while
  Force is below 9 and effective Heal is at least -3. Outside that gate it
  chooses Control when the strict selector-`0x2c` solo-control predicate
  succeeds, and mode-5 Move otherwise. This action branch and complete mode-5
  destination are also live under the same no-substitution boundary.
- Family 1's branch after prior Control, Equip, or Snitch first checks its
  recovered nearby-danger/equipment gate. It prefers a legal weapon upgrade,
  then a legal armor upgrade, when the corresponding planning cooldown has
  expired. Successful Equip planning starts a cooldown of three times the raw
  item cost. If no equipment is selected, a human-owned sector chooses crime
  at cash 50 or more and Mentality Criminal or higher. A non-human-owned sector
  chooses crime only for a different raw owner strictly above player zero, cash
  50 or more, and exactly Goon Mentality. Crime is Chaos through Tolerance 3
  and Snitch from 4; every failed gate chooses mode-5 Move. The complete branch,
  including exact item and Move targets, is live and replay-recorded.
- A shared nonnegative spending budget prevents the planner from intentionally
  queuing more Bribe/Equip cost than the player currently holds while still
  allowing validator-approved free actions from a negative balance.
- `ChooseHire` considers only authoritative `HireRules.Validate` successes in
  controlled sectors and does not mutate state.
- The client submits every selected command/hire and phase transition through
  `MatchReplayRecorder`.

## Current policy

Every gang plans through the family its scenario and hire role select
(RULE-AI-002), and nothing ranks commands outside those handlers. What a
scenario emphasizes comes from that table and from the handlers themselves,
such as the objective families 13 and 14 and the Terminate the final Greed
turns write.

Difficulty is one global match setting, matching the original setup panel's
**AI Mentality** choice rather than a property selected per opponent. It sets
the computer players' resolution band (RULE-AI-018), the starting attitudes and
whether they recover each turn (RULE-AI-014, RULE-AI-015), and the
combat-advantage hostility gate (RULE-AI-003). In planning, the handlers read it
directly only in family continuations such as family 1's choice between crime
and taking a sector (RULE-AI-020), and Advanced policy's expansion runs only at
Crime Lord and above (DEV-AI-003).

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
sectors and capacity-routes toward that draw. Mode 6 is live for family 2 and
routes toward the current scenario leaders, including tied-leader and active-
player-leads branches plus hostile-human weighting. Modes 7-9 are live in
families 5, 3, and 10, objective modes 12-15 are live in families 13/14, and
encoded `sector + 0x40` is live for family 12. Family 12 encodes the sector of
its player's roster-slot-0 gang (FND-AI-070), so it steps toward that gang; a
gang already standing there falls back to the all-zero tie draw.

## How far the planner is proved

The Original policy has no planning code of the rebuild's own: every decision
comes from a spec rule of the `AI` area, and no `PLACEHOLDER` comment remains in
the planner. What the rebuild still departs from on purpose is listed in
[DEVIATIONS.md](../DEVIATIONS.md): DEV-AI-002 (a planned action no human could
order gives no command), DEV-AI-004 to DEV-AI-006 (reads and writes outside the
original's tables), DEV-AI-007 and DEV-AI-008 (Moves to distant sectors and
hires outside the player's sectors, on by default and switched off for the
replays), and DEV-AI-003 (Advanced AI, off by default).

### What the recorded runs compare

`OriginalNewGameExperimentTests` replays every `EXP-SETUP-` and `EXP-TURN-`
run of the original. For each one the rebuild has to make every draw the
original made, with the same bound and result in the same order, and reach the
same state: players, sectors, gangs, attitudes, scores and reports. From
EXP-TURN-048 on, the runs also hold the computer players' planning records
(FMT-STATE-007), which the replay compares byte for byte: family, the three
generations of action and target, cooldowns, focus and coverage sector. A
planning pass that picks another family, action, target or sector, or draws
once more or once less, fails the replay at that draw. The runs cover all ten
scenarios and all four Mentalities. Some of them write a family, a raider flag,
cash or a retirement into the original's memory before a Done press to reach
branches no ordinary match reaches; the replay makes the same writes.

The planning pass (RULE-AI-001), the family dispatch (RULE-AI-002), the hire
choice (RULE-AI-008 to RULE-AI-013) and every family handler (RULE-AI-019 to
RULE-AI-031) run in each replayed turn, so every row of the `AI` area in
[PARITY.md](../PARITY.md) is `validated`. A rule's spec status says how much of
it the runs prove:

- `established`: a static reading and the runs agree, and the runs reach every
  branch the rule describes. RULE-AI-003, RULE-AI-005, RULE-AI-006, RULE-AI-008,
  RULE-AI-011, RULE-AI-012, RULE-AI-014 to RULE-AI-019 and RULE-AI-027.
- `supported`: the runs agree wherever they reach, and some branches rest on
  the static reading alone. The rule's Open questions and its PARITY.md notes
  name those branches. RULE-AI-001, RULE-AI-002, RULE-AI-004, RULE-AI-007,
  RULE-AI-009, RULE-AI-010, RULE-AI-013, RULE-AI-020 to RULE-AI-026 and
  RULE-AI-028 to RULE-AI-031.

### How matches end

RULE-OBJECTIVE-004 gives each scenario's end. Recorded runs reach the end of
six-month Greed, Acceptance and Dominance matches (EXP-TURN-037 to
EXP-TURN-039, EXP-TURN-041) and of a Big Man match (EXP-TURN-058), and EXP-UI-023
ends a match with a lone survivor after the probe retires the five computer
players. No recorded run ends Power, Big 40, Siege or Armageddon on its
objective, or Kill 'Em All or Eliminate on the last player left. The recorded
runs stop when the only human is eliminated, since the original then ends the
match for that human (RULE-OBJECTIVE-005), so a computer player's win can be
recorded only while the human survives.

### Tournaments

`AiTournamentTests` (category `LongRunning`, run each night by
`.github/workflows/nightly-ai-campaigns.yml`) plays six computer players through
every scenario at several seeds. Each match must replay from its journal to the
same state hash, save and load to the same hash, stay within its phase-boundary
limit and make progress toward its scenario's objective; Big Man must end on
its objective. These matches are not compared with the original: they guard the
rebuild against crashes, nondeterminism and stalled matches.
