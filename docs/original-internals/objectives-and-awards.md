# Objectives, ranking, and awards

Status: active clean-room research log

How the original ranks players while a match runs and scores them when it ends:
the player-rail portrait positions, and the endgame award thresholds, their
priority, tie-breaking, and the visible slots they are shown in. The
objective-evaluation order itself is
[BIN-ENDTURN-001](randomness-and-turn-structure.md#bin-endturn-001---elimination-cleanup-reports-and-objective-order).

Part of the [original executable internals research](../ORIGINAL-INTERNALS.md):
the [finding format](../ORIGINAL-INTERNALS.md#finding-format) defines the fields
every entry carries, the [finding index](../ORIGINAL-INTERNALS.md#finding-index)
lists every `BIN-*` ID across the set, and addresses are valid only for the
reference executable fingerprinted there.

<!-- doc-index:begin toc depth=3 -->
- [Player ranking](#player-ranking)
  - [BIN-RANKING-001 - player-rail portrait positions](#bin-ranking-001---player-rail-portrait-positions)
- [Endgame awards](#endgame-awards)
  - [BIN-AWARDS-001 - thresholds, priority, ties, and visible slots](#bin-awards-001---thresholds-priority-ties-and-visible-slots)
<!-- doc-index:end -->

## Player ranking

### BIN-RANKING-001 - player-rail portrait positions

**Observation:** `PX05011` handler `0x004518d9` iterates the six player slots
and copies a portrait only when that slot's standing byte is not `-1`. Its
portrait destinations use local rail x values 98, 138, 178, 218, 258, and 298,
with a 32-by-32 aperture and vertical position determined by the standing.

**Interpretation:** Eliminated slots do not receive a ranking-panel portrait;
the post-ranked inactive ordering used by endgame award processing is a
separate presentation concern. The recreation uses the recovered rails while
retaining competition-standing vertical placement for active players.

**Confidence:** High static evidence for slot predicate, rail coordinates,
aperture, and standing-driven placement from `0x004518d9`.

## Endgame awards

### BIN-AWARDS-001 - thresholds, priority, ties, and visible slots

Award builder `0x0042b9e0` scans all six fixed player slots for each category,
without consulting active-state bytes. It processes the award table in visible
priority Fist, Skull, Big Fat Chicken, Dollar Sign, Safe. The first three use
initial maxima 5 Overthrows (`0x004a27a8`), 50 direct Damage
(`0x004a5ed8`), and 10 Hide resolutions (`0x004a25d0`); values below those
baselines receive no award. Dollar uses Cash Spent (`0x0049ca78`) from a zero
maximum, while Safe uses the same array from an initial minimum of 999,999.
Second-pass equality writes preserve every tied player in ascending slot order.

The per-player award table can retain all five assignments, but renderer
`0x0042ce61` reads only its first three entries for each displayed player row.
Thus result state may retain every superlative while presentation must cap icons
at three in the builder's priority order. These observations replace the former
manual-derived zero-activity and five-visible-icon assumptions.

The Hide counter has one resolver write: action dispatcher `0x00472775` reaches
the action-8 case at `0x00472d00`, which increments the current player's
`0x004a25d0` counter without testing any hidden-state predicate. Consequently
every resolved Hide counts; in the recreation's ordinary lifecycle, recurring
Hide counts again after the next Upkeep has revealed the gang.

Mode 6 is now bounded. If at least one human participates, a sector owned by a
player whom the active AI views negatively receives `+2` only when that owner
is human. It then adds one independent leader-routing point: with a unique
leader other than the active player, only that leader's sectors receive `+1`;
with no unique leader, every sector owned by a player tied at standing zero
receives `+1`; when the active player is the unique leader, every other
player-owned sector containing fewer than four active-player gangs receives
`+1`. These additions feed the same nearest-square, maximum-tie RNG, and
x-then-y step logic as the other nonzero modes.

The site-data offsets used by modes 7 through 9 align exactly with the decoded
62-byte `SITES` record: selectors `0x0c`, `0x0d`, and `0x10` return Support,
Cash, and Stealth for a sector's selected site slot. Selector `0x1c` tests
whether that site's definition Resistance minus its accumulated influence is
below one. Modes 7 and 8 score the Support and Cash of not-yet-influenced sites
in owned sectors. Mode 9 scores Stealth for already-influenced sites in owned
sectors. Mode 7 additionally requires selector `0x6f` to be zero; `0x6f`
counts a player's gangs in the sector whose immediately previous action at
planning-record offset `+5` is 9 (**Influence**). Before dispatch, the outer
planner also rewrites one prior-action byte when more than one gang retained
Influence in the same sector, preventing duplicate continuity assignments.

Mode 10 changes its owner test according to the human-player count. With no
human players it scores every non-neutral sector not owned by the active
player `+1`; with at least one human player it scores every human-owned sector
`+1`. The mode-specific admission test does not consult the directional
attitude table, but the common block at `0x004098d4..0x004099ad` subsequently
multiplies an admitted hostile human-owned sector by five. Mode 11
gives `+1` to the sector returned by selector `0x5a` for the active player.
Modes 12 and 14 restrict selection to Big Man's central sectors 27, 28, 35,
and 36; modes 13 and 15 restrict it to Eliminate's six headquarters candidates
9, 12, 30, 33, 51, and 54. These four modes require the active player's gang
count in that sector to be below 6. Modes 12 and 13 also exclude sectors already
owned by the active player. Instruction-level inspection at
`0x004093d3..0x004099ad` corrects the earlier shorthand about their weight: a
hostile human-owned objective receives `+5` inside the mode case and is then
multiplied by five again in the common post-switch block, for an effective
weight of 25. Every other admitted objective receives `+1`.
Mode 16 gives `+1` to the sector returned by selector `0x77`. Its case at
`0x004045bb..0x0040465b` scans all 81 planning slots in ascending order,
without testing whether the gang is active, counts only records whose family
byte is 11, makes ordinals 0, 6, 12, and so on block leaders, and returns the
first auxiliary short of the most recent leader when it reaches the acting
slot. Selector `0x76` at `0x00404539..0x004045b6` independently returns one
only when the acting slot itself is such a leader. A mode above `0x3f`
directly adds `+1` to sector `mode - 0x40`.

The post-score loop at `0x004099f8..0x00409b6c` first clears every sector whose
unavailable byte at sector-record offset `+15` is nonzero. Its second filter
reads the acting planning record's family byte at offset `+0`: only literal
families 0 and 1 call selector `0x2c`, and a non-owned destination is cleared
when that gang cannot strictly Control it. Family 11 therefore applies the
unavailable-sector filter to modes 10 and 16 but deliberately bypasses the
strict-Control filter. This closes the former uncertainty around their late
guards.

All 48 direct references to `0x00408642` have also been enumerated. The static
mode arguments map to the recovered family handlers as follows:

| Selector mode | Handler/family call sites |
|---:|---|
| 2 | family 4 (`0x00401000`), four calls |
| 3 | family 9 (`0x004605e0`), two calls |
| 5 | family 0 (`0x00428ef0`), eight calls; family 1 (`0x00434080`), seven calls; family 7 (`0x00436c70`), one call |
| 6 | family 2 (`0x0041fef0`), two calls |
| 7 | family 5 (`0x0043a1d0`), four calls |
| 8 | family 3 (`0x00435bd0`), four calls |
| 9 | family 10 (`0x0042a6e0`), two calls; dispatcher `0x00432da0`, one call |
| 10 and 16 | family 11 (`0x00420950`), two mode-10 calls and one mode-16 call |
| 12 and 13 | family 13 (`0x0040abc0`), one call each |
| 14 and 15 | family 14 (`0x00466910`), one call each |

The one mode-0 call belongs to runtime function `0x00476a94`, outside the
family table. Family 6 (`0x00431c60`) dynamically uses mode 2 when selector
`0x60` finds no stored target and otherwise encodes that target as
`sector + 0x40`. Family 7 and family 12 also contain encoded-sector calls.
No direct call carries literal mode 1, 4, or 11; any live use must therefore
come through a computed argument. `tools/ghidra/ReportCallArguments.java`
provides a repeatable bounded inventory of the three pushed arguments at each
direct call.

The complete family-6 handler is live. With cached current-sector weight below
one it Moves: selector `0x60` enumerates weight-10 sectors in ascending order
and chooses the first not covered according to selector `0x5f`, or falls back
to mode 2. It clears the first auxiliary short and stores the routed one-step
destination in the second. With positive weight it makes one preliminary
target draw using the human-only pool for a hostile-owned weight-10 sector and
the complete visible pool otherwise; selector `0x2b` applies the established
quarter-strength comparison through the same ordinal in the complete visible
list. A passing draw Attacks immediately. A failure tries weapon then armor,
with nonpositive cooldowns and replacement cooldown `cost * 3`. It then retains
literal Heal and local Control/Move branches which are unreachable while the
cached positive weight remains unchanged, followed by up to five more target
draws and an Attack of the final target after all comparison failures. Attack
stores the current sector in the first auxiliary short; equipment and Move
clear it. Greed with fewer than four turns remaining overwrites the result with
Terminate.

Focused inspection of family 0 at `0x00428ef0` establishes a switch on the
immediately previous action (`0x3e`) and a final comparison against the older
action (`0x3f`). Previous None applies the Force-8/effective-Heal-`-3` gate,
then selector `0x5b` counts previous Hide assignments in the gang's current
sector: zero chooses Hide and a positive count chooses mode-5 Move. Previous
Attack moves immediately unless cached opponent weight is 10; at weight 10 it
makes one human-pool/full-pool asymmetric draw and uses selector `0x2b` for the
quarter-strength comparison, then Attack, strict-solo Control, or Move.

Previous Hide or Equip makes up to five such draws at weight 10 and attacks the
last selected target even if every comparison fails. When no Attack was
prepared, positive selector `0x6c` enables weapon then armor equipment with
nonpositive cooldown and `cost * 3` replacement, followed by owned-sector
Heal/Hide or non-owned mode-5 Move. Previous Control Hides in owned territory
and Moves elsewhere. Previous Heal, Snitch, or Move first applies the same Heal
gate. At weight 10 it makes exactly one target draw: success Attacks, while
failure writes None and clears both auxiliary shorts to `-1`. Without weight
10 it uses strict solo Control where possible, otherwise selector `0x5b`
chooses Hide or Move. Previous Research Moves; unlisted action values retain
None. Finally, a newly planned Move with an older Move changes the family byte
to 11 in Siege and 2 otherwise. This literal older-action test is broader than
an inferred three-consecutive-Move rule. The complete handler, target draws,
mode-5 calls, cooldowns, auxiliary behavior, no-action path, and transition are
live and replay-wired.

Family 4 at `0x00401000` is the final dispatcher handler. It switches on the
same previous-action byte but uses mode 2 for all routing. Previous None,
Control, or Heal applies the Force-8/effective-Heal-`-3` gate, then selector
`0x5b` chooses Hide at count zero or Move otherwise. Previous Attack, Snitch,
or Move makes one asymmetric target draw at weight 10; selector `0x2b` success
Attacks, while failure writes None and clears both auxiliary shorts. Without
weight 10, owned territory chooses Hide at count zero and Move otherwise;
non-owned territory chooses Control only when previous and older actions are
both Move and strict solo Control succeeds, otherwise Move.

Previous Hide or Equip makes up to five weight-10 draws and attacks the last
selection even after all failures. Without Attack, selector `0x6c` enables the
same weapon-before-armor equipment opportunity and `cost * 3` cooldowns. The
remaining owned-sector branch Hides when selector `0x5b` is below two and
otherwise Moves; non-owned territory Moves. No handler-local family transition
or Greed override exists. Although no mapped scenario/hire-role table cell
assigns family 4, an unmapped cell preserves a pre-existing family value. The
complete handler and a live Greed-role-5 preservation/replay path are now
implemented.

Both mode-6 calls belong to family 2. That handler writes public action byte 10
(**Move**) with the selected mode-6 destination when its current/selected
sector branch cannot proceed locally. Its visible-gang path writes action byte
1 (**Attack**). At the later local-sector decision, the pair flag from
`0x0040a1a7` supplies one of two exact action-byte-4 (**Control**) gates: the
sector owner must already be viewed negatively, no defending owner gang may be
visible, and the observer-to-owner combat-advantage flag must be set. The other
Control gate is restricted to a hostile human-owned sector with zero visible
human gangs and rejects a previous-turn Control action. Thus the pair flag does
not directly select an attack; it permits Control after the territorial
Combat + Defense test has established overwhelming local advantage.

Focused inspection of the complete family-2 handler at `0x0041fef0` establishes
its exact action order. Selector `0x64`'s armor opportunity precedes selector
`0x61`'s weapon opportunity. Each requires a nonpositive corresponding
cooldown, a different affordable item, and an immediately previous action other
than Attack; either Equip writes raw item cost times three to its cooldown and
clears the first auxiliary short. Failed equipment Heals only below Force 8,
at effective Heal at least `-3`, and with cached current-sector opponent weight
strictly below 5. An owned current sector then Moves through mode 6.

In a non-owned sector, positive cached opponent weight and at least one visible
hostile gang enter a five-attempt Attack loop. Weight 10 draws the actual target
from all visible human-controlled gangs; other weights draw from visible gangs
whose owner is viewed negatively. Selector `0x2b` still resolves the same
ordinal through the complete visible-opponent list for the quarter-strength
comparison. A passing comparison stops early, but five failures still Attack
the final actual target. The handler stores the current sector in the first
auxiliary short for Attack and clears it for its other ordinary actions.

Without that Attack path, previous Control, Armageddon, or failed strict solo
Control writes mode-6 Move; otherwise the handler writes Control. The two late
hostility gates described above can overwrite any earlier ordinary action with
Control and clear the auxiliary short. Finally, Greed with fewer than four
turns remaining overwrites the action with Terminate. The recreation now wires
this complete branch order, mode-6 target, action target, cooldown, auxiliary
write, and RNG consumption into replay-recorded planning.

The surrounding action writes give the site modes public command semantics.
Family 5 uses mode 7 after writing Move; when the selected Support-priority
site is already local, the same branches write Influence instead. Family 3
has the identical shape around mode 8 but ranks sites by Cash. These are thus
Support-focused and Cash-focused Influence strategies, not distinct movement
rules. Family 10 calls mode 9, compares selector 8 for the chosen and current
sectors, and moves only when the chosen sector has the larger value. Selector
8 sums Stealth across completed/influenced sites, so this path seeks a stronger
local Stealth modifier before its Hide-or-Chaos decision. The dispatcher also
contains a scenario-specific mode-9 Move for gang slot zero.

Focused inspection of family 3 at `0x00435bd0` now bounds its cash-site
continuations. Immediately previous actions None, Control, Equip, and Heal
share one branch. It first writes Heal only below Force 8 with effective Heal
at least `-3`. Otherwise, in a sector owned by the acting player, it scans the
three sites in slot order and retains the first strict maximum positive Cash
whose remaining Resistance is positive, writing Influence with that local site
slot. With no qualifying site it writes Control when selector `0x2c` accepts
the current sector, and otherwise Move through mode 8. Previous Snitch writes
that mode-8 Move directly.

Previous Influence first runs the same selector-`0x6c` equipment opportunity
used by family 1, including weapon-before-armor selection and the cost-times-
three cooldown. Without Equip it applies the same Force-8/Heal-`-3` gate. It
then retains the previous Influence site while that site remains unfinished
and the sector remains owned; otherwise it rescans for the first strict
maximum positive-Cash unfinished site, or moves through mode 8 when none
exists. The mode-8 selector block independently confirms that each owned
candidate sector is scored by the sum of every positive Cash value among its
unfinished sites, before the common nearest-square, maximum-tie RNG, and
x-then-y routing logic.

Previous Attack, Hide, and Move share an opponent-continuation branch. When the
cached visible-opponent weight is not 10, it returns to the owned cash-site,
strict Control, or mode-8 Move sequence without the earlier Heal gate. At
weight 10 it makes one bounded draw. Hostile-owned sectors draw an actual
target from visible gangs belonging to human-controller players; other sectors
draw from every visible opponent. Both pools scan player-major, then gang-slot
order.

The comparison selector preserves a notable original asymmetry: it receives
the selected ordinal but resolves that ordinal through the every-visible-
opponent pool even when the actual target came from the human-only pool. It
permits Attack when
`(target Force + target Combat) / 4 - attacker Defense` is no greater than
`attacker Force + attacker Combat - target Defense`; failure leaves None and
clears the auxiliary target fields. A successful comparison attacks the actual
selected player/slot tuple, not necessarily the gang used for the comparison.

After the switch, three consecutive Move actions change the stored family to
11 in Siege and 2 in every other scenario. Finally, with fewer than four turns
remaining in Greed, the handler unconditionally overwrites the planned action
with Terminate. Switch cases without a recovered action body intentionally
leave None rather than invoking a generic fallback.

**Recreation status:** the complete family-3 handler is live and replay-
recorded, including local site targets, equipment cooldowns, mode-8 Move
targets, opponent-pool selection and comparison, terminal family transitions,
and the late Greed Terminate override.

**Confidence:** High for the switch cases, comparisons, action writes, pool and
site scan order, tie behavior, mode-8 score sum, and call order from the
fingerprinted version-1.1 executable; runtime corroboration remains pending.

Focused inspection of family 5 at `0x0043a1d0` establishes that it has the
same complete action-switch and terminal shape as family 3, with Support-site
selection and mode 7 substituted for Cash-site selection and mode 8. Previous
None, Control, Equip, and Heal use the same Force-below-8 and effective-Heal-
at-least-`-3` gate, then select the first strict maximum positive-Support
unfinished site in an owned sector, attempt strict solo Control, or Move.
Previous Influence uses the same weapon-before-armor equipment opportunity and
cost-times-three cooldowns, Heal gate, previous-site retention, and local site
rescan. Previous Snitch moves directly through mode 7.

The previous Attack/Hide/Move branch is also instruction-for-instruction
equivalent in public behavior: visible-opponent weight 10 performs one bounded
draw, optionally chooses the actual target from visible human-controller gangs,
resolves the comparison ordinal through the full visible-opponent pool, and
uses the same quarter-strength combat predicate before writing Attack or None.
Its terminal block applies the same three-consecutive-Move family change (11 in
Siege, 2 otherwise) and the same final-three-turn Greed Terminate overwrite.

Mode 7 adds one distinction beyond its Support score. Selector `0x6f` scans all
81 planning records for the acting player and counts records whose mirrored
gang sector matches the candidate and whose immediately previous action at
offset `+5` is Influence. A candidate owned sector is scored only when that
count is zero; its score is then the sum of positive Support values for all
unfinished sites. Selector `0x41` reads planning offset `+6`, confirming that
the previous-Influence continuation retains the previous target's first byte,
which is the local site slot.

**Recreation status:** the complete family-5 handler and mode-7 selector are
live and replay-recorded, including duplicate previous-Influence destination
exclusion.

**Confidence:** High static evidence for the listed branches, selector fields,
Support scan/sum, target-pool asymmetry, terminal order, and RNG call order in
the fingerprinted version-1.1 executable; runtime corroboration remains
pending.

Family 7 at `0x00436c70` is now completely bounded at the action level. It
starts from the current sector's cached visible-opponent weight. Weight 10
makes one bounded draw: a hostile-owned sector selects the actual target from
visible human-controller gangs, while other sectors use every visible
opponent. The comparison ordinal still resolves through the full visible list
and uses the shared quarter-strength combat predicate. Attack is written only
when that comparison succeeds and the actual selected owner is hostile; the
first auxiliary short then stores the current sector. A failed comparison
continues into the normal research sequence rather than ending at None.

Without weight 10, family 7 applies selector `0x6c`'s equipment-need gate and
the family-1 weapon-before-armor choice. Both slots require a nonpositive
cooldown, a different affordable item, and write a raw-cost-times-three
cooldown plus focus `-1`. A prepared Equip, Move, Attack, or Influence skips
the remaining decision body. Otherwise an immediately previous Equip, Move,
Attack, or Influence clears the first byte of its previous target, and Force
below 8 with effective Heal at least `-3` writes terminal Heal.

Selector `0x30` begins with the current sector and replaces it only with an
owned sector having a strictly greater cached Research score. That cache is
the signed sum of all three site definitions' Research modifiers; ties retain
the earlier candidate and the current sector is not required to be owned. A
changed best sector is encoded as `sector + 0x40` for one-step Move and clears
the focus. At the selected local sector, the first slot-order site with
positive Research and positive remaining Resistance receives Influence. With
no such site, the focus becomes the current sector and item Research begins.

Pending previous Research repeats the same item. A completed previous ranged,
blade, or armor item next requests blade, armor, or the fixed miscellaneous
priority `[44, 41, 42, 43, 46, 50, 49, 52]`; every other type next requests
ranged. Type scans choose the first positive item ID whose type matches, whose
Tech does not exceed selector `0x62`'s gang/local-site cap, and whose per-player
research value remains positive. A failed continuation retries ranged, blade,
melee, armor, then the fixed miscellaneous list. If every category is
exhausted, the handler changes to family 0, moves through mode 5, and clears
the focus. Greed's final three turns overwrite any result with Terminate.

**Recreation status:** the complete family-7 equipment, Heal, Attack,
Research-site selection, Influence, encoded Move, item continuation/fallback,
family-0 exhaustion transition, focus state, and Greed override are live and
replay-recorded.

**Confidence:** High static evidence for branch order, score and site fields,
item scan order, target-pool asymmetry, target/focus writes, and RNG order in
the fingerprinted version-1.1 executable; runtime corroboration remains
pending.

Family 9 at `0x004605e0` is now completely bounded at the action level. It
first tries selector `0x61`'s weapon and selector `0x64`'s armor. Each candidate
must differ from the equipped item and be affordable. Unlike the equipment
branches that query selectors `0x65` and `0x66`, family 9 does not inspect the
existing weapon or armor cooldown before replacing the item; a successful
Equip overwrites the matching cooldown with three times raw item cost.

With neither upgrade available, a gang in its own sector always writes Move
through shared sector mode 3, which seeks another player's owned territory.
In a non-owned sector, cached visible-opponent weight 10 enters a five-draw
target loop. Its hostile-human-owner pool choice, full-visible-list comparison
ordinal, quarter-strength predicate, early success exit, and final Attack after
five failed comparisons match family 12. When the weight is not 10, an
immediately previous Control writes mode-3 Move; every other previous action
writes Control. This handler has no Heal, miscellaneous equipment, or Greed
terminal override.

**Recreation status:** the complete family-9 weapon/armor, mode-3 Move,
five-draw Attack, and Control sequence is live and replay-recorded, including
the absence of an equipment-cooldown gate.

**Confidence:** High static evidence for branch order, comparisons, action
writes, selector arguments, target-pool order, and RNG call order in the
fingerprinted version-1.1 executable; runtime corroboration remains pending.

Family 10 at `0x0042a6e0` is now completely bounded at the action level. It
first calls selector `0x72`, which scans researched type-3 armor within the
gang's raw Tech and retains the first strict maximum Defense improvement. An
unequipped gang uses item 1 as its zero-Defense sentinel. The handler accepts
the returned armor only when its offset-`+14` cooldown is at most zero and raw
item cost is at most cash, then writes Equip and the literal cooldown 2. This
differs from the cost-times-three cooldown used by families 1, 3, 5, and 11.

If that opportunity fails, an empty miscellaneous slot plus a clear research
flag for item 44 writes Equip for **Smoke Bombs**. This special branch performs
no separate raw-Tech or cash comparison in the handler. Otherwise it writes
Heal only below Force 10, with effective Heal at least `-3`, and with cached
visible-opponent weight exactly zero in the current sector.

The remaining path calls sector mode 9 and compares selector 8 for the returned
sector against selector 8 for the current sector. Mode 9 scores only owned
sectors and sums each strictly positive Stealth value whose site is already
finished. If the returned sector's sum is strictly larger, the handler writes
Move and calls mode 9 a second time for the actual destination. It does not
reuse the probed destination, so a maximum tie can consume two bounded draws
and choose a different tied sector on the second call. If the probe is not
strictly better, selector `0x5b` counts same-sector records with previous
Chaos; zero writes Chaos and any positive count writes Hide.

**Recreation status:** the complete family-10 decision sequence, selector
`0x72`, fixed Smoke Bombs branch, literal armor cooldown, mode-9 score, double
selection, and Chaos/Hide fallback are live and replay-recorded. As with other
recovered handlers, behavior after a prepared command is unavailable under the
recreation's validator remains provisional.

**Confidence:** High static evidence for comparisons, scan/tie order, action
writes, and RNG order in the fingerprinted version-1.1 executable; runtime
corroboration remains pending.

Family 12 at `0x004353a0` is now completely bounded at the action level. It
starts from cached selector `0xaf` for the current sector. When no opposing
gang is visible, it tries selector `0x61`'s weapon, selector `0x64`'s armor,
and selector `0x74`'s maximum-Chaos miscellaneous upgrade in that order. The
weapon and armor must differ from the current item, their matching cooldown
must be nonpositive, and raw cost must be at most current cash. Unlike families
1, 3, 5, and 11, a successful weapon or armor Equip writes a cooldown equal to
the raw item cost rather than three times that cost. Miscellaneous equipment
uses the same inclusive cash gate and writes no cooldown.

After failed equipment opportunities, Force below 10 and effective Heal at
least `-3` writes Heal. Otherwise the handler passes `current sector + 0x40`
to the shared sector selector and writes Move. The encoded mode adds one point
to the current sector, but the common selector then clears the source-sector
score. Its maximum is consequently zero: it draws among all 64 tied sectors
and applies the normal x-then-y one-step capacity routing toward that draw.
This apparently indirect random movement is the literal shared-selector path,
not a direct encoded destination like the separate hire-placement selector.

With a visible opponent, family 12 makes up to five bounded target draws. A
hostile human-owned current sector with weight 10 draws the actual target from
visible human-controller gangs; otherwise it uses every visible opponent. As
in families 3 and 5, selector `0x2b` resolves the same ordinal through the full
visible list for the quarter-strength combat comparison. A passing comparison
stops the loop early. Five failed comparisons do not cancel the command: the
final selected actual target is still written as Attack. Finally, Greed with
fewer than four turns remaining overwrites any prepared action with Terminate.
There is no three-consecutive-Move family transition in this handler.

The recreation additionally guards the possible state in which the current
sector has a hostile human owner but its only visible opposing gangs are
computer-controlled. The filtered actual-target pool is then empty. Planning
still consumes one bounded draw and preserves None instead of calling the
recreation RNG with zero and aborting the turn. The reference executable's
observable outcome for this sparse three-player edge remains uncorroborated.

**Recreation status:** the complete family-12 equipment, Heal, encoded Move,
five-draw Attack, target-ordinal asymmetry, and Greed override sequence is live
and replay-recorded. Prepared commands still pass through the recreation's
normal validator.

**Confidence:** High static evidence for comparisons, action writes, selector
arguments, target-pool order, and RNG call order in the fingerprinted
version-1.1 executable; runtime corroboration remains pending.

Families 13 and 14 both use the fixed objective sets as Move destinations:
scenario value 8 selects modes 12/14 and is Big Man, while scenario value 6
selects modes 13/15 and is Eliminate. Family 13 uses the variants that exclude
already-owned objectives; family 14 uses the variants that retain them. This
establishes an original Eliminate movement bias toward every possible
headquarters location, not merely an attack-score bonus against a currently
visible Right Hands gang.

Instruction-level inspection of the terminal blocks at
`0x0040b87d..0x0040b9a6` and `0x004675c8..0x004676f1` establishes their exact
outer guard. Selector `0x1f` returns one only when a Big Man gang is in sector
27, 28, 35, or 36, or an Eliminate gang is in headquarters candidate 9, 12,
30, 33, 51, or 54. When that selector is not one and the handler has not already
written Equip, family 13 unconditionally replaces the planned command with Move
through mode 12/13; family 14 does the same through mode 14/15. Both handlers'
equipment blocks are themselves inside selector-`0x1f` branches, so an
off-objective gang cannot have written Equip before reaching this override.
The recreation therefore safely applies the complete off-objective terminal
path during replay-recorded planning and submits its exact one-step destination
through normal Move resolution.

Family 14 continues at `0x00467700..0x004677df` when selector `0x1f` is one
or the newly planned action is Equip. It changes the command to Heal and the
stored family to 13 exactly when the immediately previous action is Control,
Force is below 10, and effective Heal is at least `-3`. The repeated previous-
action query then rejects Attack, but that comparison is redundant after the
exact-Control guard. The recreation applies this terminal override during
replay-recorded preparation, including the family transition and normal Heal
resolution.

The shared owned-objective branch at family-13 lines `100..110` and family-14
lines `103..114` is also bounded. Strategic refresh `0x0040a1a7` caches
selector `0x90` per player and sector, and selector `0xaf` reads that cache.
Selector `0x90` scans other players and their 81 gang slots in ascending order;
for the first observer-visible gang in the requested sector it returns 10 for
a hostile human owner and 1 otherwise, or zero when no visible opponent is
present. When an objective sector belongs to the acting player and this cache
is zero, both handlers choose Heal before their equipment/site branches at
Force below 10 and effective Heal at least `-3`. This shared Heal path is now
live and replay-verified for both objective scenarios. Family 14 still applies
its later family-13 transition when the previous action was Control.

The preceding contested-objective branch is also bounded in both handlers.
Selector 2 returns scenario turns remaining. Only even absolute parity with a
nonzero cached selector-`0x90` weight enters target selection; the other path
writes Control immediately. Target selection makes as many as three inclusive
bounded draws. A hostile human-owned sector with cached weight 10 draws from
visible human gangs in the sector; otherwise it draws from visible gangs owned
by the sector owner. Selector `0x2b` evaluates each drawn ordinal against the
same ordinal in the full ascending visible-opponent list using
`(target Force + target Combat) / 4 - attacker Defense <= attacker Force +
attacker Combat - target Defense`. Its result controls only whether the retry
loop stops early: after three attempts the final selected gang is still used.
At Force 5 or higher that gang becomes the exact Attack target. With no selected
gang or lower Force, the handler chooses Heal when Force is below 10 and
effective Heal is at least `-3`, otherwise Control. Replay-recorded planning now
preserves the selected owner/roster-slot tuple, resolves it back to the stable
gang ID, and submits the normal validated Attack command. This branch may
attack a visible non-hostile sector owner, so recovered commands require
visibility but do not inherit the provisional fallback planner's hostility
filter.

When the acting player already owns the objective and visible opponents are
present, both handlers use the full ascending visible-opponent pool without the
remaining-turn parity gate. The shared loop starts from zero rather than two,
so this branch makes at most five bounded draws before applying the same Force,
Heal, and Control result selection. It is live for both families with exact
target replay.

When the owned objective has no visible opponent and the Heal gate fails, both
handlers try selector `0x61`'s weapon and selector 100 (`0x64`)'s armor in that
order. Each requires a nonpositive matching cooldown, a different item, enough
cash, and a previous action other than Attack; unlike family 1/11, a successful
objective Equip writes the literal cooldown 2. Selector `0x75` then chooses a
researched type-4 miscellaneous item within raw gang Tech whose Chaos bonus
strictly improves on the current item (item zero is the empty-slot baseline).
The caller applies the inclusive cash gate and no cooldown. If no affordable
miscellaneous upgrade exists, the handler scans the three local sites in slot
order, retaining the first strict maximum positive Support whose remaining
Resistance is positive, and writes Influence with that exact slot; otherwise
it writes None. These Equip/Influence targets now pass through normal validated
commands and authoritative replay. No Research action appears anywhere in
either complete handler.

`0x00408553` sorts the 64 sector scores descending while retaining their sector
indices. The caller chooses uniformly among every sector tied for the maximum;
a unique maximum consumes no RNG, while a tie consumes one bounded call (three
raw `rand()` calls). If record zero's strategic target is outside the immediate
3-by-3 neighborhood, the routine moves first along x and then independently
along y, retaining each component only when the resulting sector contains at
most five of the active player's gangs. It can therefore return a diagonal
neighbor without exceeding the six-friendly-gang capacity. If record zero is
adjacent and positive, the randomly selected maximum-tied sector is returned
directly. Candidate sectors are also removed late when marked unavailable or
when the active gang cannot strictly Control a non-owned destination under the
relevant gang-state branch. If those late filters leave a maximum below 1, the
routine still counts the maximum-tied entries, which are then all 64 zero-score
sectors, consumes one bounded draw, and runs the same x-then-y capacity routing
toward the drawn sector. It does not resume the radius search or return a
special sentinel.

`0x0040a1a7` proves the capacity field's identity: it clears all 64 integers at
`0x00489950 + player*0x100`, scans the player's 81 gang records, and increments
the integer indexed by each active gang's sector. The selector's four routing
comparisons against 5 therefore test destination occupancy, not terrain or a
pathfinding cost.

**Interpretation:** mode 5 is the general movement fallback recovered in the
family-1 continuity paths. Its exact neutral/owned/enemy ratio is 5:2:1, and
the routine separates strategic target scoring from the capacity-checked
single-tile Move ultimately queued. Replay-recorded AI preparation now uses
this kernel for all three live family-1 continuations. Randomness is used only
for mode-0 neighbor selection and equal-best final scores in the bounded paths
inspected here.

**Confidence:** High for the address, ring expansion, score-map sorting,
mode-0 directions, modes 1 through 5 weights, site-field offsets, human-player
count, per-player sector gang counts, unique-leader selector, mode-6 weights and owner
branches, fixed sector sets, direct call inventory, maximum-score tie
randomization, x-then-y step return, family-11 leader/anchor grouping, and the
family-gated late candidate filters. Medium remains only for the player-order
predicates and dynamic call arguments.

**Next validation:** validate the complete recovered family inventory with
controlled runtime traces before claiming runtime parity; the remaining
mode-10/mode-16 uncertainty is no longer a static-analysis item.
