# Combat, chaos, and police

Status: active clean-room research log
Last updated: 2026-09-24

How the board resolves violence: the fixed player and roster order the attack
and police rolls run in, the exact detection and damage formulas, how opening
damage is credited, cooperative sector visibility, and the panels that report
the outcome. Chaos payouts and Crackdown lifetimes sit here because they share
that resolution order.

Part of the [original executable internals research](../ORIGINAL-INTERNALS.md):
the [finding format](../ORIGINAL-INTERNALS.md#finding-format) defines the fields
every entry carries, the [finding index](../ORIGINAL-INTERNALS.md#finding-index)
lists every `BIN-*` ID across the set, and addresses are valid only for the
reference executable fingerprinted there.

<!-- doc-index:begin toc depth=3 -->
- [Combat](#combat)
  - [BIN-ATTACK-001 - Attack picker selector and target hit map](#bin-attack-001---attack-picker-selector-and-target-hit-map)
  - [BIN-COMBAT-ORDER-001 - player/roster attack and police rolls](#bin-combat-order-001---playerroster-attack-and-police-rolls)
  - [BIN-COMBAT-RESULTS-001 - results pager, selection map, and bottom control](#bin-combat-results-001---results-pager-selection-map-and-bottom-control)
  - [BIN-COMBAT-STATS-001 - full opening damage is credited](#bin-combat-stats-001---full-opening-damage-is-credited)
  - [BIN-DETECT-001 - cooperative sector visibility aggregation](#bin-detect-001---cooperative-sector-visibility-aggregation)
- [Chaos and police](#chaos-and-police)
  - [BIN-CHAOS-001 - roster-order rolls and grouped uncontrolled payout](#bin-chaos-001---roster-order-rolls-and-grouped-uncontrolled-payout)
  - [BIN-POLICE-001 - occurrence window, neutralization, and duration order](#bin-police-001---occurrence-window-neutralization-and-duration-order)
  - [BIN-POLICE-002 - Crackdown report recipients and ordering](#bin-police-002---crackdown-report-recipients-and-ordering)
  - [BIN-POLICE-COMBAT-001 - exact detection and damage formulas](#bin-police-combat-001---exact-detection-and-damage-formulas)
<!-- doc-index:end -->

## Combat

### BIN-ATTACK-001 - Attack picker selector and target hit map

**Observation:** Interactive Attack selection is handled by `0x0043b290`; the
other `PX05003` resource user, `0x0043d132`, has no pointer-processing path.
The handler tests five enabled opponent portraits at `(98,16 + 36*n)` with
32-by-32 bounds. It separately partitions the target area
`(135,16)-(337,193)` by X boundaries 202 and 270 and Y boundary 105, yielding
six half-open regions: widths 67, 68, 67 in the first row and heights 89 then
88. Disabled opponent/target entries remain inert. Changing opponent resets
the selected target; confirmation requires both selections. The common Cancel
control exits the picker.

**Interpretation:** The attack input map is not the gang-card artwork map:
portrait buttons use a 36-pixel vertical pitch and targets use a complete,
slightly uneven 3-by-2 partition that includes surrounding card whitespace.

**Confidence:** High static evidence for both resource users, interactive
ownership, all selector/target bounds, enabled guards, selection reset, and
confirmation predicate from `0x0043b290`. Runtime capture remains useful for
pressed-state and reticle presentation.

### BIN-COMBAT-ORDER-001 - player/roster attack and police rolls

**Observation:** The action-1 (**Attack**) block in `0x00472775` is nested in
the player 0-through-5 and roster 0-through-80 scan beginning at lines 337-338;
the action test occurs at line 346 and the attack/retaliation calculation stays
inside that iteration. After the gang-combat pass, lines 448-470 run another
player/roster scan for active gangs in Crackdown sectors and perform each
police detection and damage roll there.

**Interpretation:** Attack RNG and result order are fixed player slot then
persistent roster slot, independent of command submission order. Reciprocal
orders still form one encounter, with the first gang reached by that scan as
the opening attacker. Police likewise visit gangs in player/roster order, not
sector or gang-ID order.

**Confidence:** High static evidence for both scan bounds, action dispatch,
and placement of their RNG consumers. Runtime seed correlation remains pending.

### BIN-COMBAT-RESULTS-001 - results pager, selection map, and bottom control

**Observation:** The `PX05012` Combat Results handler at `0x00451f80`
translates pointer input through shared panel origin `(104,124)`. Its previous
and next arrows are the half-open local rectangles `(31,33)-(57,56)` and
`(59,33)-(85,56)`. The five other-player portrait targets are
`(202,16 + 36*n)-(234,48 + 36*n)`, for `n=0..4`, and accept only players with
a populated result row. The viewer-force selector is not six separate
40-by-40 portrait hits: one local `(101,27)-(189,183)` region maps x greater
than 144 to the right column and y greater than 78 and 130 to the second and
third rows. Its bottom confirmation face is `(33,169)-(82,191)`.

**Interpretation:** The native page arrows use 26-by-23 pointer targets even
though their presentation is smaller. The broad force selector assigns one of
six packed result slots, including its gutters, while other-player selection
remains exact 32-by-32 portrait cells. `PX05012` has no in-panel Detail
control; the bottom face exits the panel, while the separate console Detailed
Combat route opens `PX05014`.

**Confidence:** High static evidence for all input bounds, row/column
thresholds, slot-population guards, bounded pager branches, and bottom-control
exit from complete handler `0x00451f80`; native capture remains useful for
pressed-state presentation.

### BIN-COMBAT-STATS-001 - full opening damage is credited

The player-indexed Damage Inflicted array at `0x004a5ed8` has one resolver
write. In attack dispatcher `0x00472775`, the opening damage value is finalized
at decompiler lines 381-393, accumulated into the target's phase damage at
lines 395-396, and immediately added to the attacker's statistic at instruction
`0x00473ccc` / lines 397-398. No target-Force or remaining-damage comparison
intervenes. The later retaliation calculation has no write to this array.

Therefore each resolved opening attack credits its complete computed damage,
including excess beyond the target's Force and multiple same-phase attacks that
collectively overkill it. Only Force application is floored at zero;
retaliation damage is deliberately excluded from the statistic.

**Confidence:** High from the sole-write inventory and local dataflow.

### BIN-DETECT-001 - cooperative sector visibility aggregation

**Observation:** Visibility rebuild `0x0046fa11` runs once from each of the
computer- and human-planning entry paths in outer turn function `0x0046e766`.
For each observer it initializes 64 sector strengths to -32000 (or 1000 for the
`SMGHUBBLE` modifier), then scans that observer's 81 gang slots. Active records
are those whose sector byte `+2` is not 100. A strict-greater comparison selects
the first highest effective Detect byte `+21` in each occupied sector as the
base and remembers that gang slot.

A second 81-slot scan adds every other active gang as a helper. Each helper
always contributes 1. When its signed Detect is greater than 9, it additionally
contributes `(Detect - 8) / 2` using signed integer truncation. The exact helper
sequence is therefore: all values through 9 add 1, 10–11 add 2, 12–13 add 3,
14–15 add 4, 16–17 add 5, 18–19 add 6, and higher pairs continue without a cap.
Negative helpers still add 1. Finally, the routine scans every other player's
active gang records and marks a target visible exactly when effective Stealth
byte `+20` is less than or equal to that observer-sector strength. Friendly
active records are marked visible unconditionally.

Attack picker `0x0043b290` calls roster builder `0x0043d132` for a selected
enemy owner and the acting gang's sector. That builder scans the owner's 81
records and includes one only when its sector matches and its observer-indexed
visibility byte at `+12 + observer` is nonzero. Only those included roster slots
can be written back as the Attack target.

**Interpretation:** The shipped helper arithmetic differs from the manual's
printed 0–10/11–12/.../19+ capped bands at boundaries, negative values, and
above 19. Authoritative compatibility uses the executable formula. Ordering
only chooses which equal-best gang is excluded from helper treatment; because
equal values contribute identically, roster order cannot change the aggregate.
Hide is not read by this routine and does not affect cooperative visibility.

**Confidence:** High from the complete compact function, signed byte loads,
loop bounds, exact branch/division arithmetic, field offsets, threshold, and
both direct call sites.

**Recreation status:** `ManualRules.SectorDetectionStrength` implements the
native base/helper calculation, and `MatchState.CanPlayerDetectGang` supplies
active same-sector effective statistics plus the explicit omniscience rule.
Boundary tests cover negative helpers, every transition around Detect 10–12,
the printed cap boundary, and an above-cap value. Both the Attack picker and
authoritative command validation exclude undetected targets; the latter also
protects multiplayer and replay submission paths that bypass presentation.

## Chaos and police

### BIN-CHAOS-001 - roster-order rolls and grouped uncontrolled payout

**Observation:** The action-3 (**Chaos**) pass in `0x00472775` scans player
slots 0 through 5 and each player's 81 gang slots in ascending order. Lines
245-265 calculate each participating gang's pool from the persistent sector
Income byte at `0x004a08ec + sector * 0x24`, current Force, and effective Chaos.
They roll each gang separately, store its success count by gang slot, and add
that count to a player-by-sector aggregate.
After all rolls, the sector pass at lines 268-327 evaluates Crackdown totals and
zeroes participating gang results when the sector triggers.

The later payout pass at lines 645-672 rebuilds player-by-sector totals from
the stored per-gang successes. When the player does not own that sector, lines
668-670 divide the completed aggregate by two once; line 672 then adds it to
cash. The division is not performed per gang.

The passes between those two halves are not Chaos work: action-1 Combat begins
at lines 329-346, the police scan follows, and actions 5, 6, and 12 are handled
by the Transaction pass beginning around lines 557, 587, and 605. After the
Chaos payout, the resolver runs Terminate and Move at lines 678-725, followed by
Control from line 733 onward. The physical native order is therefore Instant;
Chaos rolls/Crackdown creation; Combat; Transactions; Chaos payout; Terminate;
Move; Control.

**Interpretation:** Chaos RNG order is fixed player slot, then persistent roster
slot, regardless of submission order or intervening sectors. Same-player gangs
in one sector still share the final success result and payout. Uncontrolled
income is `trunc(total successes / 2)`, preserving an odd success contributed
across multiple gangs rather than rounding each gang independently. A new
Crackdown exists before the same turn's police scan, while its income remains
suppressed and all surviving Chaos income waits until after Transactions. The
success arrays are resolver-local state: the pass does not write a persistent
sector-Chaos value. In particular, the pool's sector component is the generated
3-7 Income byte, not the separate owner Cash byte recomputed from controlled
sites.

**Confidence:** High static evidence for scan order, per-gang rolls/storage,
player-sector aggregation, Crackdown suppression, ownership comparison, and
single post-aggregation division. Runtime seed correlation remains pending.

### BIN-POLICE-001 - occurrence window, neutralization, and duration order

**Observation:** In `0x00472775`, each sector's two signed Crackdown occurrence
shorts live at `0x004abcc0` and `0x004abcc2`. Before evaluating a new trigger,
lines 268-276 replace a non--100 slot with -100 only when it is strictly less
than `current turn - 5`. Lines 303-312 fill the first empty slot and then the
second. If neither is empty, lines 313-321 report control loss to the owner, set the sector
owner to -1, clear its three influence-derived totals, and write the current
turn into both occurrence slots.

Only after that history/neutralization block, the bounded call at `0x0047419b`
requests 1 through 3, adds 2, and adds the resulting 3 through 5 to the sector's
existing police-duration byte. The earlier lines 291-301 have already zeroed
the triggering sector's per-gang Chaos results and emitted notifications for
participating players.

The new duration is visible to the later police read at `0x0047415e`. Near the
end of the whole-turn resolver, `0x00475e74` decrements every positive duration
below the permanent sentinel 100, after the same turn's police attacks.

**Interpretation:** The occurrence window is inclusive: a trigger exactly five
turns before the current one still counts. A third retained trigger resets both
fixed history slots to the current turn, so another trigger while those slots
remain recent can neutralize reacquired control again. Duration extends rather
than replaces existing police presence, and its one bounded draw occurs after
history mutation and any ownership cleanup. A newly triggered Crackdown attacks
in that turn, then the shared final decrement leaves two through four future
police Combat phases from the initially drawn three through five.

**Confidence:** High static evidence for sentinels, strict expiration comparison,
slot-fill/reset order, cleanup fields, duration range/addition, and RNG call
order. Runtime corroboration of notification presentation remains pending.

### BIN-POLICE-002 - Crackdown report recipients and ordering

**Observation:** The whole-turn resolver `0x00472775` clears a local
64-sector-by-six-player presence table at lines 65-74. It scans all 81 roster
slots per player and sets a sector/player byte when a slot's sector matches;
inactive native slots carry the out-of-board sector sentinel 100. This table is
built before the Instant and Chaos action passes. After the Chaos rolls, the
sector pass at lines 268-327 traverses sectors in ascending ID. For a sector
whose total exceeds Tolerance, lines 291-301 clear participating gangs' Chaos
results and call report recorder `0x00477748` with type 1 only for player bytes
set in that opening presence table. A player need not have ordered Chaos to
receive the report, and an active player without a gang in the sector receives
none. If the two occurrence slots are already occupied, lines 313-321 then call
the same recorder with type 3 for the sector's previous owner before clearing
ownership. This control-loss report follows the type-1 reports in the sector
pass, and the branch does not require the previous owner to have a local gang.

**Interpretation:** The visible `POLICE CRACKDOWN.` report is scoped to players
with a gang in the affected sector at the beginning of resolution, while
`SECTOR CONTROL LOST.` separately reaches a displaced owner on the third
retained occurrence. The report table retains the first 32 entries per player,
as documented in `BIN-EVENT-001`.

**Confidence:** High static evidence for presence-table construction, type-1
recipient gate, type-3 owner call, and per-sector ordering. Exact native display
timing remains unverified.

**Recreation status:** `PrepareChaosPhase` selects Crackdown notification
recipients from the sector's active roster occupants before its Chaos rolls,
including non-participants. `CrackdownResolver` separately emits Control Lost
for the displaced owner. This change alters stored notification history, so
online session version 8 retires earlier matches.

**Next validation:** Compare multi-player Crackdown report pages with a native
reference at the first and third occurrence boundaries.

### BIN-POLICE-COMBAT-001 - exact detection and damage formulas

In whole-turn resolver `0x00472775`, the police pass calculates a threshold
immediately before its inclusive 1-through-100 RNG call at `0x0047419b`. The
instructions at `0x00474173`-`0x00474199` recognize current action byte 8
(**Hide**), multiply that boolean by 20, multiply effective Stealth by 5, and
compare the roll with `115 - 5 * Stealth - (Hide ? 20 : 0)`. Because the roll
is bounded to 1 through 100, probabilities outside that range are effectively
clamped without skipping the RNG call.

When detected, the call at `0x004741d6` passes `25 - effective Defense` and
success threshold 5 to the shared dice routine `0x00475f70`. The 25 combines
the manual-listed Police Force 5 and Combat 20. The resolver therefore does
not use the manual's visible “certain through Stealth 5” table or a separate
Police Detect 12 hidden-hit calculation.

**Confidence:** High from bounded decompiler dataflow and instruction-context
reports against executable SHA-256
`A1430159BBE20869E277A5000311344F4EC141AB77C96B385336617149E97D89` in
Ghidra 12.1.3. Runtime boundary captures remain useful for notification timing,
not for selecting the implemented arithmetic.
