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
sectors and capacity-routes toward that draw. Mode 6 is live for family 2 and
routes toward the current scenario leaders, including tied-leader and active-
player-leads branches plus hostile-human weighting. Modes 7-9 are live in
families 5, 3, and 10, objective modes 12-15 are live in families 13/14, and
encoded `sector + 0x40` is live for family 12. Because family 12 encodes its current
sector and the common selector later clears the source score, that path uses
the original all-zero tie draw and one-step routing behavior.

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
   influenced-site Stealth for a Hide/Chaos path. Objective modes 12-15 are
   implemented as exact shared-selector kernels for Big Man's four central
   sectors and Eliminate's six headquarters candidates, including ownership,
   six-gang capacity, nearest-ring, tie-RNG, step-routing, and compounded
   hostile-human weighting. Their family-13/14 off-objective terminal handlers
   are live: unless an Equip was already selected, they replace the action with
   an exact objective Move. The upstream equipment blocks cannot run while the
   gang is off-objective. Family 14 also has a live on-objective terminal
   continuation: an immediately previous Control changes to Heal at Force below
   10 and effective Heal at least `-3`, then changes the stored family to 13.
   Both families also use their shared owned-objective branch to Heal at those
   same stat boundaries when selector `0x90` finds no visible opposing gang in
   the sector. Their contested-objective branch is live as well: on even
   remaining-turn parity with a visible opponent, it makes three bounded target
   draws, applies the recovered combat retry predicate, and then submits the
   exact selected visible gang as an Attack at Force 5 or higher. A missing
   target or lower Force falls back to Heal at the same stat boundary, then
   Control; odd parity or no visible opponent selects Control directly. The
   owned-objective opponent path uses the full visible pool and up to five
   draws without the turn-parity gate. With no visible opponent and no Heal,
   both families try weapon, armor, and maximum-Chaos miscellaneous upgrades
   in order, then Influence the unfinished local site with the highest positive
   Support. Exact item/site targets and the objective handlers' fixed two-turn
   weapon/armor cooldown are live and replay-safe. These observations complete
   the family-13/14 command handlers; neither contains a Research assignment.
   The six-by-six directional
   attitude matrix, Homicidal human/computer initialization, non-Homicidal per-turn recovery,
   negative-hostility enumerators, `3..6` non-Homicidal reaction values, exact
   combat/Control decrements, and the mentality-gated sector Combat + Defense
   advantage hostility pass are also identified and implemented with their
   exact pre-city RNG order. The separate per-player resolution band is
   initialized to 0/1/2 for computer players at Goon/Criminal/higher settings;
   all nine consumers now drive the recovered Heal, Influence, Research, Chaos,
   Crackdown, hidden-detection, Attack, and retaliation formulas. Family 11's
   exact weapon/armor/miscellaneous priority, replacement cooldowns, Heal gate,
   and first-visible-local-opponent Attack are wired into live planning and
   replay. Its weapon selector includes the recovered class scoring/ties,
   research, local-cap, raw-Tech, affordability, strict-improvement,
   previous-Attack, and cooldown gates. Its blocks-of-six mode-10 formation
   anchors and mode-16 followers are also live; their separate per-gang
   formation-sector shorts are authoritative, hashed, saved, and replayed.
   Family 3's complete recovered handler is live. Its cash-site core handles
   previous None, Control, Equip, Heal, Influence, and Snitch. It heals below Force 8 at effective Heal
   `-3` or better, otherwise retains or selects the first strict maximum
   positive-Cash unfinished local site for Influence, attempts strict solo
   Control where applicable, or follows exact mode-8 movement toward the
   nearest owned sector with the greatest summed unfinished positive Cash.
   Its previous-Influence equipment opportunity uses the recovered
   weapon-before-armor selector and cost-scaled cooldowns. After Attack, Hide,
   or Move, it either repeats the territorial cash-site logic or selects a
   visible opponent and applies the recovered asymmetric combat comparison.
   Three consecutive Moves switch the family to 11 in Siege and 2 otherwise;
   Greed's final three turns overwrite the result with Terminate. Unsupported
   previous-action cases intentionally preserve None, matching the handler.
   Family 2's complete aggressive territorial handler is live. It tries armor
   before weapon, requires an expired slot cooldown and a previous action other
   than Attack, and writes a raw-cost-times-three replacement cooldown. It Heals
   below Force 8 at effective Heal `-3` or better only while the current visible-
   opponent weight is below 5. Owned sectors Move through mode 6. Elsewhere it
   draws up to five hostile targets, preferring the human-only pool at weight
   10, and attacks the final selection even when every quarter-strength combat
   comparison fails. With no attack it Controls only when strict solo Control
   is possible, the previous action was not Control, and the scenario is not
   Armageddon; otherwise it Moves through mode 6. Two terminal hostility gates
   can replace any prepared command with Control when visible defenders are
   absent, and Greed's final three turns then force Terminate. Exact targets,
   cooldowns, focus writes, standing-derived movement, and RNG consumption are
   replay-recorded.
   Family 5's complete recovered handler mirrors that sequence around Support
   rather than Cash. Its local scan and mode-7 movement sum positive Support
   from unfinished owned sites, and mode 7 excludes any candidate sector where
   another planning record already has previous Influence. Its equipment,
   opponent targeting/comparison, three-Move transition, Greed override, and
   intentional None cases are live at the same replay-recorded boundary.
   Family 7's complete research-specialist handler is live as well. It makes
   one conditional hostile Attack draw, otherwise tries the family-1
   weapon/armor opportunity, then applies the Force-8 Heal gate. Its persisted
   polymorphic focus value tracks a Research sector or item. The handler moves
   toward the strictly greatest owned sum of all site Research modifiers,
   Influences the first unfinished positive-Research local site, repeats or
   cycles exact Research item categories, and falls back through ranged,
   blade, melee, armor, and a fixed eight-item miscellaneous priority. Exhausted
   research changes the gang to family 0 and mode-5 Move; Greed's final three
   turns still force Terminate. Exact targets, focus writes, and RNG
   consumption are replay-recorded.
   Family 9's complete handler tries weapon and armor upgrades without checking
   their existing cooldowns, then writes a cost-times-three replacement
   cooldown. Without equipment it moves from owned territory through mode 3,
   uses the shared five-draw visible-opponent Attack loop in non-owned
   territory at weight 10, moves after a previous Control, and otherwise
   Controls. Exact actions, targets, cooldowns, and RNG consumption are live
   and replay-recorded.
   Family 10's complete recovered handler prioritizes a strict-Defense armor
   upgrade with a literal two-turn cooldown, then a special researched Smoke
   Bombs Equip, then Heal below Force 10 only with no visible local opponent.
   Otherwise it probes mode 9 for a strictly stronger sum of completed positive
   site Stealth and calls mode 9 again for the Move destination; without an
   improvement it chooses Chaos unless another gang in the sector has previous
   Chaos, in which case it Hides. The intentional second selector call and its
   independent tie RNG are replay-recorded.
   Family 12's complete handler branches first on current-sector visibility.
   With no visible opponent it prefers weapon, armor, and maximum-Chaos
   miscellaneous upgrades, using raw-cost weapon/armor cooldowns, then Heals
   below Force 10 at effective Heal `-3` or better, and otherwise uses its
   encoded-current-sector zero-maximum random Move. With visible opponents it
   makes up to five bounded target draws, preserves the human-pool/full-pool
   ordinal asymmetry, and attacks the final target even when every combat
   comparison fails. Greed's final three turns overwrite the result with
   Terminate. An empty human-only actual-target pool consumes one safe bounded
   draw and preserves None, preventing a zero-range RNG failure; the reference
   outcome for that sparse multiplayer edge is not yet runtime-corroborated.
   Exact actions, targets, cooldowns, and RNG consumption are live and
   replay-recorded.
   The exact ten-scenario by
   seven-hire-role family table is implemented by `OriginalAiFamilyRules`,
   including unmapped cells which preserve the current family. AI planning
   preparation now rolls the current role into the previous role and updates
   every active gang's authoritative family slot. The mode-4 second auxiliary-
   sector copy awaits representation; the first auxiliary short is the live
   family-2/7 focus or family-11 formation value. The original objective-specific base
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
   underlying second auxiliary short remains deliberately unmodeled. The hire
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
   bounded in `ORIGINAL-INTERNALS.md`. Mode 6 is now live as a family-2
   Move route toward the unique scenario leader (or all tied leaders), with an
   additional two-point preference for hostile human owners when humans
   participate. The exact scenario scorer and competition-standing bytes are
   rebuilt for all ten scenarios, including Dominance's final integer division.
   Its pair flag permits Control—not Attack—when no defending
   owner gang is visible. The hostility pass counts only visible
   defenders, requires a strict integer ratio above 75 percent, and writes
   `-10` in the observer-to-owner direction. Static executable kernels now
   guard family-1's cash 50/51, Force 8/9, effective-Heal -3/-4, Tolerance
   3/4, human-owner Mentality-at-least-Criminal, and non-human-owner exact-Goon
   boundaries, including the original raw-owner-greater-than-zero asymmetry.
   The complete previous-None/Chaos, previous-Heal, and post-equipment action
   branches are live, including selector `0x2a` as the current-sector
   active-Crackdown predicate, selector `0x2c` as strict solo Control, and
   replay-recorded mode-5 destinations. The post-equipment path also carries
   selector `0x6c`'s 3-by-3 danger test, selectors `0x61`/`0x64`'s exact weapon/
   armor choices, and selectors `0x65`/`0x66`'s planning cooldowns into live
   command submission and resolution. Capture controlled original turns that
   reach the remaining family choices through their complete selector context
   before replacing more recreation policy.
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
