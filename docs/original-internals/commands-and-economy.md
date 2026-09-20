# Commands, gangs, and economy

Status: active clean-room research log
Last updated: 2026-09-20

The commands a gang can be given and the money that pays for them: hire offers
and their comparison panel, the Instant commands resolved before the board, the
move and sector-control passes, effective gang statistics, the equipment
economy including Factory pricing, Give, Sell, and Terminate, and the
end-of-turn upkeep arithmetic.

Part of the [original executable internals research](../ORIGINAL-INTERNALS.md):
the [finding format](../ORIGINAL-INTERNALS.md#finding-format) defines the fields
every entry carries, the [finding index](../ORIGINAL-INTERNALS.md#finding-index)
lists every `BIN-*` ID across the set, and addresses are valid only for the
reference executable fingerprinted there.

<!-- doc-index:begin toc depth=3 -->
- [Hiring](#hiring)
  - [BIN-HIRE-001 - initial and replacement offers](#bin-hire-001---initial-and-replacement-offers)
  - [BIN-HIRE-COMPARISON-001 - fixed-width signed values in the three-offer panel](#bin-hire-comparison-001---fixed-width-signed-values-in-the-three-offer-panel)
- [Instant commands](#instant-commands)
  - [BIN-BRIBE-001 - shipped three-dollar cost and direct tolerance delta](#bin-bribe-001---shipped-three-dollar-cost-and-direct-tolerance-delta)
  - [BIN-INFLUENCE-001 - Influence picker targets and detail entry](#bin-influence-001---influence-picker-targets-and-detail-entry)
  - [BIN-INSTANT-001 - roster-order actions and cumulative Influence](#bin-instant-001---roster-order-actions-and-cumulative-influence)
  - [BIN-RESEARCH-000 - initial progress and Armageddon completion](#bin-research-000---initial-progress-and-armageddon-completion)
  - [BIN-RESEARCH-001 - same-phase completion suppresses later rolls](#bin-research-001---same-phase-completion-suppresses-later-rolls)
  - [BIN-SNITCH-001 - debt-independent delta and post-Instant floor](#bin-snitch-001---debt-independent-delta-and-post-instant-floor)
- [Movement and sector control](#movement-and-sector-control)
  - [BIN-CONTROL-001 - cross-player winner and zero-margin neutral candidate](#bin-control-001---cross-player-winner-and-zero-margin-neutral-candidate)
  - [BIN-MOVEMENT-001 - Terminate pass before roster-ordered Move](#bin-movement-001---terminate-pass-before-roster-ordered-move)
  - [BIN-MOVEMENT-002 - Move panel neighborhood target mapping](#bin-movement-002---move-panel-neighborhood-target-mapping)
- [Gang statistics and equipment](#gang-statistics-and-equipment)
  - [BIN-EFFECTIVE-STATS-001 - gang, equipment, and controlled-site aggregation](#bin-effective-stats-001---gang-equipment-and-controlled-site-aggregation)
  - [BIN-EQUIP-001 - Factory price division and rounding](#bin-equip-001---factory-price-division-and-rounding)
  - [BIN-EQUIP-002 - fixed transaction scan, deferred gifts, and Sell overwrite](#bin-equip-002---fixed-transaction-scan-deferred-gifts-and-sell-overwrite)
  - [BIN-EQUIP-003 - Give item-selection hit targets](#bin-equip-003---give-item-selection-hit-targets)
  - [BIN-EQUIP-004 - Sell item-toggle hit targets](#bin-equip-004---sell-item-toggle-hit-targets)
  - [BIN-EQUIP-005 - Equip and Research category/list targets](#bin-equip-005---equip-and-research-categorylist-targets)
  - [BIN-GANG-DEFINITION-001 - PX05022 alternate definition panel](#bin-gang-definition-001---px05022-alternate-definition-panel)
  - [BIN-GANG-RETIRE-001 - death and Terminate preserve inactive record payload](#bin-gang-retire-001---death-and-terminate-preserve-inactive-record-payload)
  - [BIN-GANG-VALUES-001 - fixed two-cell gang values replace template padding](#bin-gang-values-001---fixed-two-cell-gang-values-replace-template-padding)
- [Economy and finance](#economy-and-finance)
  - [BIN-FINANCE-001 - alternate Financial panel destination and close face](#bin-finance-001---alternate-financial-panel-destination-and-close-face)
  - [BIN-UPKEEP-001 - flat sector tax and influenced-site Cash share one byte](#bin-upkeep-001---flat-sector-tax-and-influenced-site-cash-share-one-byte)
<!-- doc-index:end -->

## Hiring

### BIN-HIRE-001 - initial and replacement offers

**Observation:** `0x0046e766` initializes each player's three fixed offer bytes
at `0x004abbc0` to signed -100 and the matching action bytes at `0x004a27c8` to
-1. The fresh-game loop writes literal `0x9c` and `0xff` for every one of the
six-by-three slots without drawing an offer. `0x004078b8` writes snub action -2
to the selected slot. The resolver at
`0x00472775` scans players 0..5 and slots 0..2, negates that same slot's offer
for a snub or successful hire, clears its action, and creates a hired gang with
a bounded `1..5` result plus four. Failed hires clear the action without
negating the offer. The planning-entry helper `0x004716eb` later scans slots
0..2 and fills each negative slot in place using repeated bounded `1..89` draws,
rejecting all currently positive slot values and that slot's negated old ID.
Its only callers are the computer and human planning-entry paths at `0x0046f4e3`
and `0x0046fe8b`; the resolver does not call it. The human handler at
`0x00416c75` assigns the selected slot and clears both other action bytes, so
hire and snub are mutually exclusive selections rather than two actions in one
turn. Its Reject branch toggles the selected slot from -1 to -2 or from -2 to
-1; clicking Reject on a slot holding a sector destination changes it to -1
rather than directly snubbing it. Dragging an offer writes the new destination
to that slot and unconditionally clears the other two slots, so a later drag
replaces the earlier selection and dragging the same offer can retarget it.
The handler does not read or update cash or cumulative cash spent. The resolver
first counts all gang records in the target sector at `0x0047592b`-
`0x004759a8`; six causes a failure with no random draw. It then checks
then-current cash at `0x004759bd`-`0x00475a15`. Only after those checks does it
roll Force at `0x00475ac4`; it subsequently searches the player's 80 gang slots
at `0x00475bdb`-`0x00475c2d`, so a full roster failure consumes the Force draw.
After successful gang creation, it updates cumulative cash spent at
`0x00475c88`-`0x00475ca8` and subtracts cash at
`0x00475cba`-`0x00475ce4`. Rejected, cancelled, and failed hires do not change
either value.

**Interpretation:** Initial offers are populated on planning entry. Successful
hire or snub leaves a signed tombstone in its permanent slot until that player's
next planning entry; refill never compacts or shifts slots. Multiple malformed
vacancies would refill in ascending slot order. Replacement selection is
rejection sampling over gang IDs 1 through 89; a just-hired or snubbed gang
cannot immediately replace itself. Hired Force is uniformly 5 through 9 through
the recovered bounded wrapper. Selecting a hire reserves no cash: affordability
is evaluated during resolution, and an unaffordable action is cleared without
creating a vacancy.

Hire resolution is reached from the outer turn loop through the direct call
chain `0x0046f706` -> `0x004726c0`, then `0x00472750` -> `0x00472775`. The
successful path subtracts only the gang definition's initial hire cost at
`0x00475cba`-`0x00475ce4`; it does not read the definition's Upkeep field.
Because the newly copied gang record has a real destination sector instead of
inactive sentinel 100, it participates in the separate Upkeep scan at the top
of the next `0x0046e766` outer-loop iteration. This statically distinguishes
"hire cost at the end of turn N" from "first Upkeep at the start of turn N+1,"
even though the normal UI exposes no player-controlled pause between them.

The six-byte array at `0x004a5ef0` is a persistent per-player modifier. Fresh
local-game setup clears each byte, compares that player's Pascal name with the
exact uppercase string `SMGMILK` at `0x0046e23f`-`0x0046e272`, and sets the
matching byte. The save reader/writer transfers all six bytes at `0x00463b6c`
and `0x00464071`; no per-turn or post-hire reset exists. At `0x00475aaa` the
resolver gives a flagged player's recruit Force 10 and bypasses the normal
bounded Force draw. The same name scan identifies adjacent original cheat
flags, corroborating that this is a name-triggered modifier rather than a
scenario or controller rule.

**Confidence:** High static evidence for arrays, sentinels, selected-slot writes,
mutual exclusion, resolver/refill order, call sites, RNG bounds, and the exact
`SMGMILK` trigger/effect; a controlled runtime sequence remains useful
corroboration.

### BIN-HIRE-COMPARISON-001 - fixed-width signed values in the three-offer panel

**Observation:** The Hire comparison renderer `0x004546c5` uses
`0x00414187` for Tech Level, Upkeep, Combat, Defence, Stealth, and Detect,
then `0x004142e7` for its ten modifier rows. Every inspected call passes
literal width two and a signed word value; for example, the consecutive
modifier calls at `0x00454a85`, `0x00454ace`, and `0x00454b17` read adjacent
signed word fields before passing that width. The renderer proceeds directly
to the next value's destination calculation after each call. It does not
compare the three offers or select a separate best-value palette.

**Interpretation:** Hire comparison values use the native two-cell rule: a
one-digit positive value occupies the right cell, while a negative value uses
the red numeric glyph row and renders its absolute digits without an ASCII
minus sign. Baseline zeroes are bright green and modifier zeroes dim green.
The previously added green/red best-offer comparison tint and zero-padded Tech
Level were not native behavior.

**Static follow-through:** The recreation shares
`NativeTwoCellNumberPresentation` with the Gang, Item, Site, and Sector panels
for all sixteen Hire rows. It keeps the recovered 12-by-7 field clears, but no
longer derives a color from the other offers.

**Confidence:** High for the fixed-width, signed rendering and absence of a
comparison branch from bounded call-site contexts in complete renderer
`0x004546c5`; native capture remains useful only for final palette comparison.

## Instant commands

### BIN-BRIBE-001 - shipped three-dollar cost and direct tolerance delta

**Observation:** Case 2 in the Instant switch at `0x00472775`, lines 114-127,
compares the player's cash with 3. Below 3 it calls the failure-report helper
without mutation. Otherwise it subtracts 3 from cash, directly adds 3 to the
sector tolerance byte, and adds 3 to the player's spending statistic. The
block contains no 40-point clamp. This contradicts the manual's printed $5
cost and capped description.

**Interpretation:** Compatibility play uses the shipped $3 threshold and cost.
Successful Bribe adds 3 directly to effective sector tolerance, including
values above 40; insufficient cash produces the existing ordered failure and
no mutation. The printed $5/cap helper remains isolated as manual evidence and
is not used by authoritative resolution, finance projection, or AI budgeting.

**Confidence:** High static evidence for threshold, cash/statistic deltas,
tolerance delta, and failure branch. Exact original message wording remains
unverified.

### BIN-INFLUENCE-001 - Influence picker targets and detail entry

**Observation:** The `PX05005` handler `0x0043f692` uses three guarded,
half-open panel-local selection rectangles: site 0 `(106,17)-(226,81)`, site 1
`(208,73)-(328,137)`, and site 2 `(106,127)-(226,191)`. These are not all
identical to the staggered site-card artwork apertures. A normal click selects
only an unfinished site; Cancel abandons the picker, while confirmation rejects
an unset selection. The handler's double-click branch uses the same guarded
targets and opens details for the clicked eligible site.

**Interpretation:** Site-card rendering and selection geometry are deliberately
separate. The exact target map must be used for both single-click selection and
double-click detail entry, preserving completed-site inertness.

**Confidence:** High static evidence for all three target bounds, completed-site
guards, control branches, and double-click detail path from `0x0043f692`.
Runtime capture remains useful for selection-border presentation.

### BIN-INSTANT-001 - roster-order actions and cumulative Influence

**Observation:** The Instant-action switch in `0x00472775` executes while the
resolver scans player slots 0 through 5 and each player's 81 gang slots in
ascending order. Cases 2, 7, 8, 9, 11, and 13 dispatch Bribe, Heal, Hide,
Influence, Research, and Snitch respectively. In the Influence block at lines
151-185, the resolver compares the site's current progress with its base
resistance, then rolls only the current gang's `Force + effective Influence`.
It immediately adds that gang's successes, clamps progress to the base value,
and records completion before the roster scan continues. Once progress equals
the base resistance, a later Influence command skips its roll.

**Interpretation:** Instant actions resolve in fixed player/roster-slot order,
not submission order. Friendly Influence is cumulative rather than pooled:
each gang consumes its own roll stream and mutates the site before the next
gang acts. A gang encountered after completion consumes no Influence RNG.

The `PX05005` Influence picker is handled by `0x0043f692`, the sole code site
that requests resource ID 5005. For each of the sector's three definition/
progress pairs at `+7/+8`, `+9/+10`, and `+11/+12`, it enables selection only
when the definition's base Resistance differs from current progress. Completed
sites therefore have no selectable command path. The native 36-byte sector
record contains one owner byte and these definition/progress pairs; it has no
per-site influencer identity.

The Control pass in the same resolver changes owner only at lines 801-821.
Whenever the selected owner differs, it writes the new sector owner and zeros
all three site-progress bytes before recording attained/lost-control reports.
The separate neutralization branch at lines 313-319 likewise writes owner
`-1` and zeros every progress byte. Consequently the original has no direct
takeover of an already cooperative site: sector capture first removes all site
cooperation, and the new owner must build progress again from zero. The
recreation represents native completed progress as zero remaining Resistance
and keeps an explicit derived `InfluencedBy` identity, but enforces the same
completed-site rejection and ownership-change reset.

The site Tolerance definition field at `0x004ab684` has only two code
references: AI selector evaluation in `0x00402d70` and sector recomputation
helper `0x004782c5`. It is not consumed by the Influence action block. The same
helper rebuilds Support, Cash, local stat modifiers, and special-site flags, and
its normal match-loop call occurs before planning rather than inside the
whole-turn resolver. A site completed during Instant therefore remains pending
through that turn's later Combat, Transaction, Chaos, and Control passes; its
benefits become active at the following pre-planning rebuild.

**Confidence:** High static evidence for action dispatch, scan order,
per-gang Influence pools, immediate progress mutation, clamping, completion
guard, picker eligibility, absence of a site-owner field, ownership-change
reset, definition-field reference inventory, and delayed benefit boundary.
Runtime seed correlation remains pending.

### BIN-RESEARCH-000 - initial progress and Armageddon completion

**Observation:** Fresh-game initializer `0x0046dc10` owns the only setup writes
to the 384-byte item-major/player-minor Research array at `0x004a2608`. For
ordinary scenarios it scans all 64 item records and all six player slots and
copies byte `0x004a5f84 + item * 0xa6` into each player's entry. The loaded
`ITEMS` record is 166 (`0xa6`) bytes and its research-difficulty word begins at
offset `0x7c`; the byte copied here is its low byte. When active scenario dword
`0x004abbe8` is 9 (Armageddon), the alternate branch writes zero to all 64 by 6
entries instead. Neither branch calls the RNG.

The resolver's Research case and the item-selection consumers read the same
`item * 6 + player` bytes. A zero value is therefore already complete: ordinary
matches begin with precisely the real zero-difficulty technologies available,
while Armageddon begins with every item-table entry complete. The executable
also zeroes type-99 padding entries because it blindly covers all 64 records.
The recreation deliberately excludes padding IDs from its researched-item set;
they are not technologies and cannot be selected, so this avoids invalid
authoritative state without changing observable mechanics.

**Confidence:** High static evidence for array address and layout, loop bounds,
source field/stride, scenario branch, zero semantics, and absence of RNG.

### BIN-RESEARCH-001 - same-phase completion suppresses later rolls

**Observation:** Case 11 in the Instant switch at `0x00472775`, lines 186-208,
reads the player's remaining value for the selected item and enters the dice
calculation only while that value is nonzero. A successful gang subtracts its
successes immediately and clamps the value at zero before the fixed roster scan
continues.

**Interpretation:** Multiple gangs may queue Research for the same item, but an
earlier player/roster-slot completion suppresses every later roll for that item
in the same Instant phase. Submission order cannot change which gang consumes
the final research roll or the following RNG state.

**Confidence:** High static evidence for the remaining-value guard, immediate
mutation, zero clamp, and its placement within the player/roster scan.

### BIN-SNITCH-001 - debt-independent delta and post-Instant floor

**Observation:** Case 13 in `0x00472775`, lines 210-212, subtracts 3 directly
from the sector tolerance byte and marks the sector changed. It contains no
cash read or failure branch. After the complete Instant player/roster scan,
the sector loop at lines 224-226 raises every tolerance below 1 to exactly 1.

**Interpretation:** Snitch executes even while its player is in debt and first
applies a direct -3 delta. The single global post-Instant floor, rather than a
per-command base-zero clamp, then prevents any sector from entering later
phases below tolerance 1. This also prevents commandless negative-tolerance
Chaos triggers in the shipped turn path.

**Confidence:** High static evidence for the direct delta, absence of a cash
gate, global clamp value, and clamp placement after all Instant actions.

## Movement and sector control

### BIN-CONTROL-001 - cross-player winner and zero-margin neutral candidate

**Observation:** The Control block in the whole-turn resolver `0x00472775`
first accumulates action-4 strength in the player/81-slot scan at lines 733-744,
then resolves sectors in ascending board order in the loop beginning at line
751. Within each sector, it initializes its best margin to zero, winner to -1,
and the first candidate to -1. It then scans player slots 0 through 5,
subtracting the phase sector Income, defending-gang strength, and
influenced-site Support from each player's pooled Control strength. A strictly
larger margin replaces the candidate list; an
equal margin appends that player. When more than one candidate exists, the call
at `0x004756d9` passes the candidate count to the recovered one-based bounded
RNG wrapper. The selected value indexes through a four-byte slot immediately
before the player-candidate array, so value 1 selects candidate zero. Capture
proceeds only when the selected candidate is not -1.

**Interpretation:** Control tie-break RNG is consumed in ascending sector order,
independent of command submission order. Participants aggregate by ascending
player and persistent roster slot. Negative margins cannot capture. A unique
positive leader captures without a random draw. Equal positive leaders are chosen uniformly in
ascending player-slot order. At best margin zero, the original neutral -1 entry
remains ahead of every tied player: the one-based result 1 means no capture and
results 2 onward select the tied players in ascending slot order. Thus one
zero-margin challenger has the manual's 50-percent chance, while `n` tied
zero-margin challengers each have probability `1 / (n + 1)` and the remaining
outcome leaves ownership unchanged. Every random selection consumes the usual
three raw RNG values.

**Intentional exception:** The candidate scan at lines 778-793 does not test
whether a player issued Control. All six strength cells start at zero, and only
actual commands add to them. Ordinarily positive defense leaves nonparticipants
below the leading candidates, but if sector Income plus defending strength plus
Support is negative, every zero-strength nonparticipant has a positive margin
and can tie or beat the real challenger. A selected nonparticipant is then
written directly to the owner byte at lines 801-813. This is a clear shipped
bug: the recreation considers only players represented by actual Control
commands. A distinct-site fixture using Research Lab, Science Center, and
Headquarters produces total defense -1 and proves an idle player is never
awarded ownership in the corrected model.

**Confidence:** High static evidence for board/player/roster scan order,
initialization, comparison behavior, candidate order, one-based RNG call,
neutral sentinel, nonparticipant bug, and capture predicate. The manual independently corroborates
the single-player zero-margin probability; multi-player runtime capture remains useful.

The same focused owner-field audit establishes retained control of an empty
sector. Within the complete whole-turn resolver, the owner byte at
`0x004a08e8 + sector * 0x24` is written only at decompiled lines 313-318, where
the third qualifying Crackdown sets it to -1 and clears influence totals, and at
lines 801-815, where a non--1 Control winner replaces the prior owner and clears
those totals. The Movement and Terminate passes contain no owner write.

**Interpretation:** Moving or terminating the last friendly gang in a sector
does not itself abandon control. Ownership persists until another explicit
ownership-changing rule runs. A later Control attempt still includes sector
Income and influenced Support but naturally has no defending-gang contribution.

**Confidence:** High static evidence within the whole-turn resolver; moving and
terminating last-gang recreation fixtures guard the negative behavior.

### BIN-MOVEMENT-001 - Terminate pass before roster-ordered Move

**Observation:** The whole-turn resolver `0x00472775` has two distinct movement
passes. Lines 678-701 scan player slots 0 through 5 and each player's 81 roster
slots, resolving action 14 (**Terminate**). Only after that pass finishes do
lines 702-725 repeat the same player/roster scan and resolve action 10
(**Move**) from its stored destination sector. At the start of each player's
Move pass, line 703 calls `0x00476a94`.

That helper repeatedly rebuilds two 64-entry count arrays. Every active
non-mover increments its current-sector count; every active action-10 record
increments its proposed-destination count and is appended to an ascending
roster-slot list. Its sector scan retains the last total above six, making the
highest-numbered overcrowded sector the next one repaired. It first finds the
earliest mover into that sector whose source's projected total is below six and
rewrites the destination to that source. If no such mover exists, it rewrites
the earliest mover into the sector anyway. When that record is already a
rewritten source no-op, the helper instead calls sector selector `0x00408642`
with literal mode 0. Mode 0 assigns no positive weights: it consumes one
bounded draw over all 64 tied sectors and applies the common x-then-y one-step
capacity routing. The count/rewrite loop restarts until no projected sector
exceeds six. The outer resolver then copies every remaining action-10
destination without another capacity test.

**Interpretation:** Every Terminate resolves before any Move, regardless of
command submission order. Within each pass, results follow ascending player
slot and persistent roster slot. Competing Moves do not use sequential
first-mover priority. When otherwise legal moves overfill a destination, the
earliest qualifying roster slot is rewritten first and becomes a resolved
no-op, leaving later roster slots to move. Multiple overcrowded sectors are
repaired from highest sector ID down, with a complete recount after every
rewrite. Terminate changes only the copied gang record's sector byte to inactive
sentinel 100; it does not erase equipment or other stale payload before copying
the record back.

**Confidence:** High static evidence for pass precedence, action identities,
loop bounds, player/roster ordering, simultaneous projected counts,
repair-sector and mover selection, mode-0 fallback/RNG behavior, final Move
target decoding, and Terminate's exact record mutation. Runtime corroboration
remains useful.

### BIN-MOVEMENT-002 - Move panel neighborhood target mapping

**Observation:** The `PX05006` Move handler at `0x004413ef` accepts a pointer
inside the shared panel and then tests the panel-local neighborhood rectangle
`(132,26)-(294,182)`. It subtracts that origin and maps the resulting point
into three 54-pixel columns and three 52-pixel rows. The native index is
`column + 3 * row`; index 4 (the actor's center sector) is explicitly excluded,
and out-of-city cells are disabled before any destination write. Cancel and
confirmation use the common shared-panel controls; confirmation rejects when
no valid destination was selected.

**Interpretation:** `PX05006` is an exact 3-by-3 162-by-156 neighborhood
target, not nine independently sized art apertures. Its eight legal cells map
directly to the surrounding sector offsets in row-major order; the center is
display-only and must remain inert.

**Confidence:** High static evidence for the panel, neighborhood bounds, cell
pitch, row-major mapping, center exclusion, invalid-edge guard, and control
branches from `0x004413ef`. Runtime capture remains useful for selection-border
presentation.

## Gang statistics and equipment

### BIN-EFFECTIVE-STATS-001 - gang, equipment, and controlled-site aggregation

**Observation:** Turn-start function `0x0046e766` first rebuilds every 36-byte
sector record through `0x004782c5`. That helper scans the sector's three site
definition/progress pairs. For every site whose progress has reached its base
Resistance, it accumulates the site's Support, Cash, Tolerance, all fourteen
stat modifiers, and special-site flag into the sector record. The normal path
then scans all six players and all 81 gang slots and calls `0x0047781f` for
every active gang before planning begins.

Helper `0x0047781f` reconstructs each of the gang's fourteen effective-stat
bytes independently. Each byte starts with the corresponding field from the
156-byte gang-definition record, then adds the same field from each of the
three equipped item IDs that is not `-1`, using the 166-byte item-record
stride. It adds the matching sector aggregate only when the gang's player is
the current owner of the gang's sector. The rebuilt 32-byte gang record is
written back immediately. This same sequence also runs after a completed turn
when the match continues.

The whole-turn resolver `0x00472775` copies that rebuilt gang record before
dispatch. Its Heal case reads effective Heal and passes `Heal + 4` directly to
the common dice helper. Its Research case reads effective Research and passes
`Force + Research`, subject only to the per-player difficulty adjustment
documented in `BIN-AI-007`. Thus item and completed-site modifiers are part of
the shipped action pools rather than display-only values. A site completed
during the current Instant pass is absent from the already-built sector and
gang records, so it cannot affect another action until the following planning
boundary.

The executable stores site progress and derives completed benefits for the
sector owner; it has no independent site-owner field. The recreation's
explicit `InfluencedBy` field is therefore valid only with zero remaining
Resistance and a matching sector owner. Authoritative construction now rejects
positive-Resistance influenced sites, while zero-Resistance sites with no
influencer remain valid as pending completions (and for the Headquarters
special case).

**Confidence:** High static evidence for all fourteen fields, gang/item/site
source strides, three equipment slots, completion and sector-owner gates,
turn-start ordering, Heal and Research consumers, and the recreation
representation invariant.

**Recreation status:** `EffectiveStatisticsCalculator` performs the same base,
three-item, and controlled completed-site aggregation. Focused tests cover Heal
equipment/site pools, Research equipment aggregation and site-stat activation,
ownership scope, delayed same-Instant activation, and invalid influenced-site
state.

### BIN-EQUIP-001 - Factory price division and rounding

**Observation:** In EXE-GOG-1.1, the Equip resolver inside `0x00472775` loads
the selected item's signed raw Cost at `0x00474998`. When sector flag
`+0x0e` is set and the sector owner at `+0x00` matches the purchasing player,
`0x004749e1..0x004749f3` performs signed integer division by three and subtracts
that quotient from the raw Cost. The same sector flag has only four direct read
sites: `0x0043f36d`, `0x0044d498`, `0x0044dc36`, and this resolver read at
`0x004749b2`; the equipment-selection path at `0x0044d1bb` independently shows
the same `cost - cost / 3` operation. The manual identifies Factory as the
controlled/influenced site that lowers item purchase prices.

**Interpretation:** Factory pricing is `Cost - trunc(Cost / 3)`. It is not a
30-percent discount and is not `floor(Cost * 70 / 100)`. All item costs are
nonnegative, so C# integer division reproduces the original truncation. Thus a
$11 Katana costs $8, while a $12 item costs $8.

**Confidence:** High static evidence for the branch, divisor, operation order,
owner check, and rounding. A runtime capture remains useful corroboration but is
not required to choose between the former provisional formulas.

### BIN-EQUIP-002 - fixed transaction scan, deferred gifts, and Sell overwrite

**Observation:** The transaction pass in `0x00472775` loops player slots 0
through 5 and, inside each player, all 81 gang-record slots in ascending order.
It initializes three 81-entry pending arrays to -1 before the gang scan. For
action 5 (**Equip**) it immediately checks and subtracts cash, then replaces the
copied gang record's weapon, armor, or miscellaneous byte. For action 6
(**Give**) it writes selected item bytes into those pending arrays at the
recipient slot and clears the giver's copied item bytes. Only after every gang
for that player has been processed does the resolver copy non--1 pending values
into recipients at decompiled lines 633-643.

Action 12 (**Sell**) tests the fixed selection-mask bits in weapon, armor, then
miscellaneous order. Each selected branch clears that copied item byte and
assigns `Cost / 2` to the same local value at decompiled lines 605-617; the cash
and earned-cash updates at lines 618-621 occur once after all three branches.
The branches do not accumulate their values.

**Interpretation:** Transactions resolve by player slot and persistent roster
slot, independent of command submission order. Incoming gifts are applied only
after the recipient's own transaction, so they overwrite a same-turn purchase
or surviving same-slot item; later roster-slot givers overwrite earlier pending
gifts to the same target slot. A multi-slot Sell destroys every selected item
but pays only half the raw Cost of the highest selected fixed slot
(miscellaneous, else armor, else weapon). This last behavior is retained as an
original compatibility quirk rather than corrected to the manual's apparent
combined-value intent.

**Confidence:** High static evidence for loop bounds/order, action dispatch,
pending-array lifecycle/application, selection-mask order, item clearing, and
single Sell credit. Runtime corroboration remains useful.

### BIN-EQUIP-003 - Give item-selection hit targets

**Observation:** The `PX05015` Give handler at `0x00445a4f` first translates
pointer input into its shared panel-local coordinates. Its three item-toggle
checks use the half-open rectangles `(103,15)-(155,67)`,
`(103,79)-(155,131)`, and `(103,143)-(155,195)`. These surround the smaller
portrait apertures by one pixel. The same handler presents up to five eligible
friendly recipients in roster order at `(209,16 + 36*n)-(241,48 + 36*n)` and
accepts Enter/Execute only when at least one eligible item and a recipient have
been selected; Escape and the Cancel control abandon the dialog.

**Interpretation:** The selection targets are fixed 52-by-52 cells with a
64-pixel vertical pitch. Artwork sizing must not be substituted for input
geometry: the visual item aperture is slightly smaller than its selectable
cell. Recipient selection belongs to that same panel rather than a second
target-picker screen. The recreation follows that interaction; Up/Down
recipient cycling is retained as an explicit keyboard-navigation quality-of-life
addition.

**Confidence:** High static evidence for the three bounds, pitch, control
branches, and confirmation predicate from `0x00445a4f`; native interactive
capture remains useful for pressed-state presentation.

### BIN-EQUIP-004 - Sell item-toggle hit targets

**Observation:** The adjacent `PX05013` Sell handler at `0x00443bbd` follows
the shared panel pointer translation and tests three half-open item rectangles:
`(111,15)-(301,67)`, `(111,79)-(301,131)`, and
`(111,143)-(301,195)`. Each toggles the corresponding equipped fixed slot only
when it is populated. Its Cancel and confirmation controls use the common
panel-local positions; confirmation rejects an empty item mask.

**Interpretation:** Sell's three interaction rows are fixed 190-by-52 cells
with a 64-pixel pitch. They do not cover the full-width rendered label and
price row, so wide artwork must not widen the click target.

**Confidence:** High static evidence for all item bounds, slot guards, toggle
behavior, and confirmation branch from `0x00443bbd`; native interactive
capture remains useful for pressed-state presentation.

### BIN-EQUIP-005 - Equip and Research category/list targets

**Observation:** The `PX05004` Equip handler at `0x0043dad9` translates
pointer input into panel-local coordinates. Its four category checks are the
half-open 32-by-32 cells `(104,16)-(136,48)`,
`(104,52)-(136,84)`, `(104,88)-(136,120)`, and `(104,124)-(136,156)`.
Selecting a category clears the current choice and rebuilds the list through
`0x0043f136`. That helper clears sixteen fixed list entries before examining
the 64 catalog records; the original extracted catalog has at most fifteen
records in any resulting category. The item-list pointer region is
`(148,26)-(328,169)`, and the handler derives its row directly as
`floor((y - 26) / 9)`. Its list-render helper `0x0043efe5` uses the same
nine-pixel cadence.

The separate `PX05007` Research handler at `0x004427fa` uses the same shared
entry table and calculates the clicked row from that same 26-pixel baseline,
but its accepted pointer rectangle is `(148,19)-(328,162)`. Thus its first
row includes the seven pixels above the rendered list baseline while its final
row ends at the same relative list position.

**Interpretation:** This is a fixed sixteen-row list, not a scrolling
twelve-row projection. The category artwork can remain slightly larger than
the hit cells, but input must use the inset 32-by-32 rectangles. Equip and
Research have intentionally distinct list hit boxes even though they render at
the same nine-pixel cadence. The recreation retains its existing keyboard
navigation.

**Confidence:** High static evidence for category/list bounds, reset/rebuild,
sixteen-entry backing table, and row calculation from `0x0043dad9`,
`0x0043f136`, `0x0043efe5`, and `0x004427fa`; native interactive capture
remains useful for pressed-state presentation.

### BIN-GANG-DEFINITION-001 - `PX05022` alternate definition panel

**Observation:** The definition-information handler `0x00455b6b` loads
`PX05022` into the alternate backing region and closes by copying source
`(344,144)-(664,353)` to `(128,124)-(448,333)`. It copies the 64-by-64 gang
portrait to backing `(370,161)-(434,225)`, yielding screen `(154,141)-(218,205)`. Name text is written at backing `(444,171)`, followed by three 30-character description rows at y=189, 198, and 207. The force/current-value column begins at backing x=516; upkeep, Tech Level, and the right statistics column begin at x=612. These map to screen x=300 and x=396. Statistics use screen rows 243, 252, 270, 279, 288, 297, and 306. Both keyboard confirmation and the sole pointer exit face use local `(33,169)-(82,191)`.

**Interpretation:** Hire-offer definition inspection is not the normal
`PX05000` live-gang panel. It has no instance equipment, uses 30-character
description rows, and must preserve the alternate 320-pixel crop and its
24-pixel rightward field shift.

**Confidence:** High static evidence for resource identity, crop/destination, portrait, all recovered text/value origins, description length, statistic rows, and exit control from complete handler `0x00455b6b`; native capture remains useful for palette and label clipping.

### BIN-GANG-RETIRE-001 - death and Terminate preserve inactive record payload

**Observation:** After player attacks and police damage have accumulated against
phase-start snapshots, whole-turn resolver `0x00472775` applies damage in its
player/81-slot loop at decompiler lines 530-540. When resulting Force is below
one, line 537 writes only sector byte `+2` (`0x00498daa`) to inactive sentinel
100 and line 538 increments the owner's casualty statistic. The preceding
combat-report copy records Force and all three equipment bytes, but this death
branch contains no writes to weapon `+4`, armor `+5`, or miscellaneous `+6`.

The separate Terminate pass at lines 678-701 copies the complete 32-byte record,
tests action 14, changes only the copied sector byte to 100, and copies all eight
dwords back. It therefore preserves every other raw field, including equipment.
This matches Eliminate-scenario bulk retirement helper `0x00476f3b`, which also
writes only sector 100. Normal hire insertion later copies a complete new gang
record into an inactive slot, replacing rather than recovering that payload.

**Interpretation:** Equipment on a dead or terminated native gang is stale,
inaccessible record state—not returned inventory and not a usable stash. The
manual's statement that Terminate removes its items is true at the gameplay
level but not a literal erasure of the record. The recreation keeps the three
item IDs for parity and final-state inspection while using Force zero and
clearing live queue/Hidden state to represent native inactivity safely.

**Confidence:** High from the bounded post-damage and Terminate loops, exact
field writes, full-record copy boundaries, Eliminate helper, and hire overwrite
path.

**Recreation status:** combat death, police death, Terminate, and Eliminate bulk
retirement all retain inaccessible equipment fields. Regression tests cover all
four paths; active-gang predicates prevent the stale items from contributing to
simulation state.

### BIN-GANG-VALUES-001 - fixed two-cell gang values replace template padding

**Observation:** The `PX05000` live-gang handler at `0x00449e80` draws its
numeric values with the two-character integer helpers at backing x=172 and
x=268 (screen x=276 and x=372), including all statistic rows. The helper
paints both six-by-seven cells and right-aligns a one-character value into the
second cell. The same handler negates the stored upkeep before rendering it.
Its no-instance force branch draws the two-character unknown marker in that
same two-cell field.

A focused recheck of `0x004142e7` and its `PX05000`/`PX05022` call sites
shows that every such numeric call supplies a literal width of two and disables
leading zeroes. For a one-digit value, the first iteration copies an opaque
blank glyph over the first destination cell, then the second copies the digit.
Negative values select the red digit row and render the absolute-value digits;
they do not select an ASCII minus glyph or allocate a third cell. The verified
decoded `PX05022` art contains exactly two green `0` glyphs at every numeric
field before this overwriting occurs; it does not contain a value multiplier or
a literal `00` suffix. See `BIN-NUMBER-HELPERS-001` for the native bright
baseline versus dim modifier zero distinction.

**Interpretation:** Gang numeric fields are not generic right-aligned strings.
They occupy precisely the two cells beginning at the recovered field origin.
Rendering a live digit before that field leaves the zero placeholders visible,
producing the erroneous `300`/`??00` padding and can erase adjacent panel
pixels. The raw table value `3` must therefore replace the placeholder with a
blank-plus-`3` field, not be scaled to 300. A visible `00` after a live value
is evidence that the cleared/drawn field began at the wrong pixel origin.

**Confidence:** High static evidence from the complete `PX05000` and
`PX05022` renderers (`0x00449e80`, `0x00455b6b`), their bounded helper-call
contexts, and fixed-width number helper `0x004142e7`; verified decoded panel
art confirms each placeholder field is two glyph cells wide.

## Economy and finance

### BIN-FINANCE-001 - alternate Financial panel destination and close face

**Observation:** The City/Sector Financial handler at `0x0044d1bb` loads
`PX05008` through resource id `0x1390` and follows the alternate 320-pixel
panel route. Its off-screen render coordinates use source left 344, which is
copied to final screen x=128 through x=448 at y=124. The player portrait is
drawn at source `(370,161)-(434,225)`, yielding final `(154,141)-(218,205)`.
The value column at source x=610 yields a final **left** edge x=394, with four
fixed glyph cells on final rows y 151, 160, 178, 196, 214, 223, 241, and 268.
Each uses baseline helper `0x00414187` at width four. The contract count uses
one bright cell at x=316 when below ten or two cells otherwise, followed by a
dynamic closing parenthesis at x=322 or x=328; the opening parenthesis remains
baked into the template. The handler translates
pointer coordinates from the same 128-pixel left edge and tests its close face
as `(161,293)-(210,315)`.

**Interpretation:** Financial panels are an explicit exception to the normal
344-by-209 shared panel destination: they draw the native 320-by-209 source
area at `(128,124)`. The City/Sector choice is made before opening the panel;
the modal's recovered pointer branch closes only through its own face, so the
recreation does not expose the city-console split controls inside Finance. Its
values are fields with a left origin, not generic right-aligned strings; using
x=394 as a right edge shifts every number 24 pixels left and can leave the
template zeroes exposed.

**Confidence:** High static evidence for the resource, alternate-path source
and destination relationship, portrait/value/count geometry, pointer
translation, and close rectangle from `0x0044d1bb`; a native capture remains
useful for color and pressed-state presentation.

### BIN-UPKEEP-001 - flat sector tax and influenced-site Cash share one byte

**Observation:** Cash updater `0x0046e766` scans players 0 through 5. Its active
gang-record loop at decompiled lines 194–215 subtracts each gang definition's
Upkeep. The following sector loop at lines 216–232 adds signed sector byte `+3`
once when owner byte `+0` matches the player. This is not the generator's
density-derived 3–7 value during playable turns. The enclosing loop initializes
`local_8` to one, skips cash collection on its first iteration, and calls
`0x004782c5` for every sector before the first planning phase. That helper sets
byte `+3` to one, adds site-table Cash offset `+8` for each completed site, and
the `0x0046f246` call site copies the full 36-byte result back. Later iterations
collect cash before recomputing the sector records again.

**Interpretation:** The shipped cash result is the Help-described flat $1 per
controlled sector plus Cash from its influenced sites. The executable stores
those components combined as `1 + completed-site Cash`; the recreation exposes
them separately in events and Finance projections. Generated city density must
not be substituted for sector tax. This combined byte is also operational
sector Income: `0x004120ef` renders sector `+3` on the owner-gated Income row,
AI selector case 6 in `0x00402d70` returns that same byte, and the whole-turn
resolver consumes sector Income in both Chaos and Control. Consequently the
generated 3-7 value is not the playable Control/Chaos value after the first
pre-planning recomputation; it establishes the sector's base Tolerance before
the operational byte is replaced. Because the first pass skips the entire cash
collection branch, players enter their initial planning turn with setup cash
unchanged; recurring Upkeep begins on the next outer-loop iteration.

The same loop also supplies the complete Upkeep statistics boundary. For every
active gang, lines 197-212 subtract signed Upkeep from current cash
`0x004a25e8`; a negative value adds its magnitude to Cash Earned
`0x004a27e0`, while zero or positive Upkeep adds directly to Cash Spent
`0x0049ca78`. For every owned sector, lines 217-229 add its signed combined
Income byte to current cash; values below one subtract that signed value from
Cash Spent, while positive values add to Cash Earned. These branches execute
per gang and per sector, so positive and negative components are classified
before totals can cancel. Cross-reference inventories confirm the arrays are
the saved/rendered Cash Earned and Cash Spent fields and locate their other
resolver writers at successful Chaos/Sell and Bribe/Equip/Hire paths.

**Confidence:** High static evidence for arrays, offsets, initial skip,
recomputation/write-back, UI and AI consumers, player/gang/sector scan order,
per-component statistics branches, and arithmetic;
controlled runtime corroboration remains pending.
