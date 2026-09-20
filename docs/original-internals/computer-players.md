# Computer players

Status: active clean-room research log
Last updated: 2026-09-20

The original computer player, from the outer planning pass down to a single
gang's action: the per-gang command dispatcher and its handlers, command
history, scenario-sensitive family selection, hire-offer ranking, role schedule
and placement anchors, the global Mentality byte and its consumers, the shared
weighted sector selector, the directional attitude matrix, and the per-player
difficulty band. [AI-SPEC.md](../AI-SPEC.md) states the intended behavior these
findings support.

Part of the [original executable internals research](../ORIGINAL-INTERNALS.md):
the [finding format](../ORIGINAL-INTERNALS.md#finding-format) defines the fields
every entry carries, the [finding index](../ORIGINAL-INTERNALS.md#finding-index)
lists every `BIN-*` ID across the set, and addresses are valid only for the
reference executable fingerprinted there.

<!-- doc-index:begin toc depth=3 -->
- [Planning and dispatch](#planning-and-dispatch)
  - [BIN-AI-001 - per-gang command dispatcher and action handlers](#bin-ai-001---per-gang-command-dispatcher-and-action-handlers)
  - [BIN-AI-002 - scenario-sensitive family selection](#bin-ai-002---scenario-sensitive-family-selection)
  - [BIN-AI-003 - outer AI planning pass and command history](#bin-ai-003---outer-ai-planning-pass-and-command-history)
- [Hire decisions](#hire-decisions)
  - [BIN-AI-003A - strategic hire-offer ranking](#bin-ai-003a---strategic-hire-offer-ranking)
  - [BIN-AI-003B - base hire-role schedule](#bin-ai-003b---base-hire-role-schedule)
  - [BIN-AI-003C - AI hire destination and persistent placement anchor](#bin-ai-003c---ai-hire-destination-and-persistent-placement-anchor)
- [Mentality and sector selection](#mentality-and-sector-selection)
  - [BIN-AI-004 - global AI Mentality byte and first consumers](#bin-ai-004---global-ai-mentality-byte-and-first-consumers)
  - [BIN-AI-005 - shared weighted sector selector](#bin-ai-005---shared-weighted-sector-selector)
- [Attitude and difficulty](#attitude-and-difficulty)
  - [BIN-AI-006 - directional attitude and hostility matrix](#bin-ai-006---directional-attitude-and-hostility-matrix)
  - [BIN-AI-007 - per-player difficulty resolution band](#bin-ai-007---per-player-difficulty-resolution-band)
<!-- doc-index:end -->

## Planning and dispatch

### BIN-AI-001 - per-gang command dispatcher and action handlers

**Observation:** Focused Ghidra 12.1.3 analysis identifies `0x00432da0` as a
dispatcher reached from `0x00458fa0` at call site `0x004594df`. Its two
arguments index arrays with player stride `0x510` and gang stride `0x10`. The
dispatcher writes a selected family byte at `0x0048a250 + player * 0x510 +
gang * 0x10`, then dispatches that value through the following handler table:

| Value | Handler |
|---:|---:|
| 0 | `0x00428ef0` |
| 1 | `0x00434080` |
| 2 | `0x0041fef0` |
| 3 | `0x00435bd0` |
| 4 | `0x00401000` |
| 5 | `0x0043a1d0` |
| 6 | `0x00431c60` |
| 7 | `0x00436c70` |
| 9 | `0x004605e0` |
| 10 | `0x0042a6e0` |
| 11 | `0x00420950` |
| 12 | `0x004353a0` |
| 13 | `0x0040abc0` |
| 14 | `0x00466910` |

No case for value 8 appears in this handler switch. Several handlers write the
chosen command and parameters both to a `0x10`-stride array rooted near
`0x0048a250` and to a second projection with player stride `0xa20` and gang
stride `0x20` rooted near `0x00498daf`. Target-sector results are repeatedly
split using quotient and remainder by `0x51` (81).

**Interpretation:** The first byte is an AI strategy/action-family selector,
the handler switch maps it to command planners, and the two projections are
the original per-gang planning record and live gang-state projection. Division
by 81 encodes a player/sector or owner/sector pair. The later bounded handler
analyses identify every dispatched family, public command write, and target
encoding.

**Confidence:** High for addresses, call site, strides, switch values, handler
mapping, mirrored writes, division constant, record semantics, public actions,
and target encodings. The supporting evidence is completed in `BIN-AI-003`
through `BIN-AI-005` and the family-specific sections below.

**Static follow-through:** `0x00458fa0`, the relevant `0x00432da0` selectors,
all fourteen dispatched handlers, and all public command/target writes are now
bounded. Controlled original-runtime traces remain corroboration, not a gap in
the static mapping.

### BIN-AI-002 - scenario-sensitive family selection

**Observation:** Query selector 0 returns `DAT_004abbe8`; setup/save analysis
identifies this byte as the scenario ID in the same 0-through-9 order used by
`ScenarioId`. Query selector `0x7c` returns the per-player word at
`0x00482128 + player * 4`. When query `0x48` reports that the planning record's
byte at +1 is nonzero, `0x00432da0` maps scenario and query `0x7c`'s hire role to
the family below. A dash means the switch performs no assignment and preserves
the record's current family.

| Scenario | Mode 0 | Mode 1 | Mode 2 | Mode 3 | Mode 4 | Mode 5 | Mode 6 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Greed | 0 | 0 | 3 | 2 | 6 | - | 7 |
| Power | 0 | 1 | 3 | 2 | 6 | 5 | 7 |
| Acceptance | 0 | 0 | 5 | 2 | 6 | - | 7 |
| Dominance | 0 | 0 | 5 | 2 | 6 | 3 | 7 |
| Kill 'Em All | 0 | 1 | 3 | 2 | 6 | 5 | 7 |
| Big 40 | 0 | 1 | 3 | 2 | 6 | 5 | 7 |
| Eliminate | 0 | 13 | 14 | - | 6 | 5 | 7 |
| Siege | 10 | 0 | 3 | 11 | 12 | - | 7 |
| Big Man | 0 | 13 | 14 | 3 | - | - | - |
| Armageddon | 0 | 1 | 3 | 2 | 6 | 3 | - |

Every assigning mode-4 branch also copies the signed query-`0x5a` gang
projection into the planning record's +12 word. Out-of-range modes likewise
preserve the current family. `OriginalAiFamilyRules` implements this table and
side-effect flag in isolation. The same function separately compares the
scenario global with values 6, 7, and 8 after family dispatch.

**Confidence:** High for selector storage, scenario identity/order, table
values, preserve behavior, mode-4 copy, and global comparisons. Hire roles 0
through 6 are internal schedule indices rather than named public concepts; their
complete scenario schedules, adjustment rules, ranking modes, and family
effects define their semantics in `BIN-AI-003B`.

**Static follow-through:** The outer planner's complete family-count,
affordability, standing, prior-role, and late-turn adjustments are recovered
below. `AiPlanningState` persists the six role pairs and all six-by-81 family
records in hashes, saves, and replays.

### BIN-AI-003 - outer AI planning pass and command history

**Observation:** `0x00458fa0`, the sole normal caller of the per-gang dispatcher,
is itself reached from `0x0040ab20` at `0x0040abac` and from the initialization
routine `0x0046e766` at `0x0046f4ec`. On first use for a player it calls
`0x00409de1` for all 81 indices and initializes per-player state. On subsequent
passes it iterates all 81 records with player stride `0x510` and record stride
`0x10`, copies bytes at offsets +5..+7 into +2..+4, clears +8..+10, and
decrements the two 16-bit fields at +12 and +14 only when their associated
queries are nonnegative. It skips records whose mirrored `0x20`-stride byte at
`0x00498daa` is decimal 100. It later iterates the same 81 records, seeds family
9 under a separate condition, and calls `0x00432da0` for every non-100 record.

After the per-record pass, `0x00458fa0` performs a ten-way switch on the same
state-query value 0 through 9 seen by `0x00432da0`. Each branch starts from a
deterministic current-turn schedule, adjusts that slot against existing family
counts, calls `0x004078d9` with an offer-ranking mode, and writes the selected
hire role to `0x00482128`. The function also iterates
64 entries in a separate sector-sized pass before dispatching gangs.

**Interpretation:** `0x00458fa0` is the outer AI planning pass. The +2..+4 and
+5..+7 triples are older/previous command projections, +8..+10 is the newly
planned triple, decimal 100 marks an unused gang slot, and +12/+14 are the
weapon/armor replacement cooldowns. The post-dispatch ten-way switch is the
scenario-specific hiring strategy, while the 64-entry pass prepares sector
weights. Save/load references and complete handler dataflow establish these
field meanings without requiring runtime deltas.

**Confidence:** High for callers, loop bounds, addresses, strides, copies,
clears, decrements, sentinel, dispatcher call coverage, command-history fields,
equipment cooldowns, and the complete hire-role schedule.

**Static follow-through:** The three action/target generations, both cooldowns,
and their save/load paths are mapped in `BIN-AI-004` and the family sections.
The independent four-valued setup selection is AI Mentality, whose staging,
persistence, and consumers are also bounded in `BIN-AI-004`.

## Hire decisions

### BIN-AI-003A - strategic hire-offer ranking

**Observation:** `0x00458fa0` calls `0x004078d9` after choosing a role for the
next hire. The helper scans exactly three offer bytes at `0x004abbc0 +
player * 3`, returns an offer-slot index, and only then compares that winner's
raw Force (`gang +0x00`, selector `0x8d`) with player cash. An unaffordable
winner returns `-1`; it does not fall back to another offer. If requested mode
0 has cash strictly above 200 and the scenario is not Greed, the helper first
substitutes mode 3.

| Requested mode | Ranking and eligibility |
|---:|---|
| 0 | Lowest Upkeep at or below 3, with nonnegative Control; later ties win |
| 1 | Highest Heal from a zero baseline; Greed also requires Upkeep <= 3; later ties win |
| 2 | Highest Research from a zero baseline; Greed also requires Upkeep <= 3; later ties win |
| 3 | Highest Combat plus only positive Blade, Range, Fighting, and Martial Arts; zero baseline; later ties win |
| 4 | Highest Stealth + Strength. Greed requires Upkeep <= 4, Strength >= 0, and Stealth > 3 and gives later ties priority; other scenarios require Strength >= 0 and replace only on a strict improvement, so the first maximum wins |
| 5 | Highest Detect at or above the initial baseline 10; later ties win |

The field identities follow the decoded 156-byte gang record: Upkeep at +2,
Combat through Martial Arts at +4 through +30, and raw Force at +0. Direct
instruction inspection was required for modes 1, 2, and 4 because Ghidra's
decompiler reused the player parameter as a local accumulator and emitted
misleading pseudocode. `OriginalAiHireRules` implements the instruction-level
behavior, and the live AI planner uses the selected post-command role's ranking
mode.

When no offer survives ranking/affordability, selector `0x8e` chooses the slot
passed to `0x004078b8`, which writes `0xfe` into that offer's per-player state.
Greed always chooses slot zero (confirmed from the emitted instructions; the
decompiler renders its zero-iteration loop misleadingly). Every other scenario
chooses the first strict minimum of this integer score, initialized to 5000:

`Stealth * 20 * positive-stat-sum / (Force + Upkeep + 1)`

The sum includes positive Combat, Defense, Control, Heal, Influence, Research,
Strength, Blade, Range, Fighting, Martial Arts, and Tech; it excludes Stealth,
Detect, and Chaos. `OriginalAiHireRules.SelectRejectedOfferIndex` implements
this exact failure-path choice. The live post-command AI preparation returns
that choice when ranking or affordability fails, and the replay recorder applies
the snub as a separate authoritative mutation.

**Confidence:** Verified for all comparisons, eligibility boundaries, tie
directions, the cash-200 override, post-selection affordability, and no-fallback
behavior, plus the failed-hire rejection formula and tie direction.

**Static follow-through:** `BIN-AI-003B` now supplies every scenario-specific
role schedule and adjustment in `0x00458fa0`; the live post-command hiring pass
uses the resulting ranking mode, preserves failed-offer rejection, and records
the authoritative mutation for replay.

### BIN-AI-003B - base hire-role schedule

**Observation:** Before its family-count and affordability adjustments,
`0x00458fa0` seeds a schedule slot from the current turn (query `0x2f`). Nine
scenarios use `turn % 10`; Dominance uniquely uses `turn % 11`. Each table cell
below is `ranking mode / hire role`, directly matching the call argument to
`0x004078d9` and the dword written to `0x00482128` by the final switch.

| Scenario | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Greed | 0/1 | 4/6 | 0/1 | 2/2 | 0/1 | 3/4 | 2/2 | 0/1 | 2/2 | 3/3 | - |
| Power | 0/0 | 1/1 | 0/0 | 0/0 | 4/6 | 0/0 | 3/4 | 0/0 | 3/3 | 2/5 | - |
| Acceptance | 0/1 | 4/6 | 3/4 | 0/1 | 2/2 | 3/3 | 2/2 | 0/1 | 2/2 | 0/1 | - |
| Dominance | 0/1 | 0/1 | 2/5 | 2/2 | 4/6 | 3/3 | 2/2 | 0/1 | 2/5 | 0/1 | 3/4 |
| Kill 'Em All | 0/0 | 1/1 | 0/0 | 0/0 | 4/6 | 0/0 | 3/4 | 0/0 | 3/3 | 2/5 | - |
| Big 40 | 0/0 | 1/1 | 0/0 | 0/0 | 4/6 | 0/0 | 3/4 | 0/0 | 3/3 | 2/5 | - |
| Eliminate | 0/0 | 1/2 | 0/0 | 1/2 | 4/6 | 1/1 | 1/2 | 0/0 | 1/1 | 2/5 | - |
| Siege | 0/1 | 4/6 | 0/1 | 2/2 | 0/1 | 3/4 | 2/2 | 3/4 | 2/2 | 5/3 | - |
| Big Man | 0/0 | 1/2 | 0/0 | 1/1 | 1/1 | 2/3 | 1/2 | 0/0 | 1/1 | 1/2 | - |
| Armageddon | 0/0 | 1/1 | 0/0 | 3/3 | 0/0 | 3/4 | 0/0 | 3/3 | 0/0 | 2/5 | - |

The Power, Kill 'Em All, and Big 40 switch bodies are identical. Static
constants at `0x00481018` onward decode as floats 52, 4, 2, 100, 3, and 6.
The planner computes `total duration turns / 52` and retains that factor in an
x87/local-stack value used by quota comparisons. Ghidra misleadingly renders
several later uses as multiplication by `0.0`; instruction windows confirm they
still consume the saved factor. `OriginalAiHireRoleRules` implements the exact
pre-adjustment schedule. It also implements the identical Power/Kill 'Em All/
Big 40 adjustment block: late turns remap slots 4 and 8; selector `0x9a`,
selector `0x5f`, the prior role, and family-5/family-7 presence redirect slot 6;
family counts cap slots 8, 6, 9, and 4 at respectively `factor * 4`,
`factor * 4`, `factor * 3`, and `factor`; and fewer than four family-0-or-4
gangs forces slot zero.
The same class now contains instruction-verified adjustments for every
scenario. These preserve strict versus inclusive quota
boundaries and original statement order: Siege's cash floor is strict and its
missing-family-6-or-12 fallback runs last; Big Man redirects four slots only
when the family-0-or-4 count is strictly above five; Armageddon's missing-family-2
override is last even after another rule has reset the slot. Greed, Acceptance,
and Dominance retain their distinct late-turn windows, selector-driven fallback
chains, quota multipliers, and Dominance's unique slot 10.

The enclosing attempt gates are also scenario-specific. Greed requires the
active gang count to be at or below its computed limit and requires remaining
turns to be strictly greater than integer `total duration / 8`. Power,
Acceptance, and Dominance require the inclusive gang limit plus more than two
remaining turns. Kill 'Em All, Big 40, Eliminate, Siege, and Armageddon use only
the inclusive gang limit. Big Man enters its schedule without that limit check.
`OriginalAiHireRoleRules.ShouldAttemptHire` implements these boundaries.

The limit feeding those gates is now instruction-verified too. Selector
`0x22` returns one when any sector is neutral and not under Crackdown; selector
`0x23` counts sectors owned by the requested player. When no such neutral
sector remains, cash above 300 yields the fixed limit 80, while cash at or
below 300 yields `active gangs + owned sectors`. While a neutral sector does
remain, Greed uses the integer truncation of `owned sectors * 1.5`, Power,
Acceptance, and Dominance use `owned sectors * 2`, and scenarios 4 through 9
use `owned sectors * 4`. Every result is capped at 80. The `1.5` is the decoded
double at `0x00481010`; the emitted x87 conversion truncates the nonnegative
count. `OriginalAiHireRoleRules.CalculateHireGangLimit` implements the block.

Query `0x2f` is the zero-based elapsed-turn counter at `0x0049ca68`; query 2
returns total scenario duration minus that counter. The counter initializes to
zero and increments once after the outer planner loop, so recreation turn 1
feeds schedule slot zero and the full duration as turns remaining.

Selector `0x90` scans every other player's 81 gang slots for a gang whose
mirrored sector byte equals the requested sector and whose per-observer
visibility byte is set. It returns 10 when that gang's owner has state 0 or 3
and the observer-to-owner attitude is negative, 1 for another visible opposing
gang, and 0 when none exists. Selector `0x9a` returns the requested ordinal
sector whose cached `0x90` value is 10, or sentinel 100. With ordinal 1 in the
hire planner, it therefore identifies the first sector containing a visible
hostile opposing gang. Selector `0x5f` scans active family-6 gangs and returns
one whose current sector or cached destination matches that sector, otherwise
`-1`. The adjustment input now names these as visible-hostile-sector presence
and family-6 coverage rather than retaining raw selector numbers.

Selector `0x8f` is now closed as well. Its case in `0x00402d70` returns the
per-player dword at `0x00482160`; `0x00458fa0` copies the current hire role from
`0x00482128` into that array before computing the next role. It therefore means
**previous hire role**, not previous schedule slot. The five family-6 scheduling
blocks nevertheless compare it with their scenario-specific special slot number:
Power/Kill 'Em All/Big 40 use 6, Greed and Armageddon use 5, Acceptance uses 2,
and Dominance uses 10. Every one of those slots ultimately writes hire role 4,
which the family table maps to family 6. Dominance's comparison can never be true
for a role bounded to 0..6, and the other constants suppress unrelated prior
roles by numerical coincidence. This is a shipped slot-versus-role indexing bug,
not an unknown field interpretation. The recreation deliberately compares the
previous role with 4 in all five blocks, preserving the apparent no-consecutive-
family-6 intent without reproducing the defect. See `DECISIONS.md`.

The remaining Greed-only byte read is also semantic. The scenario scorer at
`0x0047712a` stores, for each active player, the count of players with a
strictly greater scenario score at `0x004abc08 + player`; tied leaders therefore
both hold zero. In schedule slot 9, only a player with a nonzero value (behind
at least one higher-scoring player) is reset to slot 0 when fewer than ten
turns remain or cash is below 100. `OriginalAiHireAdjustmentInputs` exposes
that exact condition as `HasHigherScoringPlayer`.

**Confidence:** Verified for scenario order, periods, every ranking-mode call,
every hire-role write, shared scenario bodies, constants, retained factor, x87
comparison direction, equality boundaries, adjustment ordering, and the
Greed-only scenario-standing predicate, selector `0x8f`'s previous-role storage,
plus the complete hire-limit inputs, multipliers, cash boundary, and cap. The
role-4 comparison is a documented recreation correction rather than a claim
about the shipped instruction stream.

The recreation's `AiPlanningState` now preserves the verified six current-role
words, six previous-role words, and six-by-81 family slots in canonical hashes,
native saves, and replays. Save/replay version 7 migrates earlier snapshots to
role zero and family sentinel 99 without advancing the RNG. `PrepareAiPlanning`
now performs the verified role rollover and applies the scenario/role family
table to each active gang before the existing hostility pass. The separate
post-command `PrepareAiHiring` pass derives the verified gate and adjustment
inputs, writes the next current role, and returns the prepared offer choice;
replay version 8 records that mutation.

The first short of the related 14-byte auxiliary record at `0x0048c0ba` is now
represented as an authoritative polymorphic focus value. Family 11 uses it as
a formation sector. Family 7 writes a sector while attacking or establishing a
research position, an item while researching, and `-1` after equipment or
routing. The same six-by-81 storage is hashed, saved, and replayed; its existing
`formationSectors` serialized name is retained as a pre-1.0 implementation
detail. The record's second short is now represented independently as the
family-6 coverage sector. Every assigning hire-role-4 dispatch initializes it
to the gang's current sector. Family 6 writes a selected strategic destination
before routing, then overwrites it with the actual one-step Move destination.
Selector `0x5f` uses this persisted value only when the first short equals
`-1`; otherwise it tests the gang's live sector. Both shorts are authoritative,
hashed, saved, and replayed.

### BIN-AI-003C - AI hire destination and persistent placement anchor

**Observation:** The hire-destination helper `0x00408214` takes player, mode,
and offer slot. Values at least `0x40` directly write and return sector
`mode - 0x40`, without validation or RNG. Mode 1 builds a weighted multiset in
exact order: every owned sector from 0 through 63 once, followed by every
active gang slot from 0 through 80 once at that gang's current sector.
Duplicate sectors remain duplicated, and one bounded draw selects a one-based
ordinal. An empty multiset still calls the original wrapper with its bound
clamped to one and resolves destination `-1`.

Mode 0 first scans occupied gang records to find the minimum and maximum sector
and each extreme's multiplicity. When an offer slot is present and either
extreme contains fewer than six gangs, it consumes one bounded draw over two
choices, tentatively picks that extreme, and falls back to the other when the
chosen extreme is full. Execution then deliberately falls through mode 1 and
overwrites the tentative result. Thus the extreme choice changes RNG state but
not the final destination. If both extremes are full or the offer slot is
negative, that preliminary draw is skipped. Modes 2 through 63 return sentinel
99 without writing a destination or consuming RNG.

The outer planner `0x00458fa0` maintains one per-player encoded placement anchor
at `DAT_0048e2f8`. New-match initialization seeds it to the Right Hands sector
plus `0x40`. It retains a proposed anchor only while that owned sector has at
least one neutral, available immediate neighbor, occupancy at most five, and
the scenario is not Big Man; otherwise selector `0x25` chooses a replacement,
which is stored plus `0x40`. The anchor is part of the original save/load state.

Selector `0x24(player, center)` returns zero unless `center` is player-owned,
then counts neutral cells whose Crackdown-duration byte is zero in its 3-by-3
neighborhood in dy-major, dx-minor order. Its row-wrap check uses the literal linear bound
`0 <= candidate < 65`. Index 64 is not a fixed sentinel: its owner and
Crackdown-offset reads alias bytes at `0x004a11e8` and `0x004a11f7` in the
following 486-by-10-byte runtime block. Bounded decompilation of the resolver at
`0x00472775` shows that byte zero of record zero is the mirrored owner for
player-zero gang slot zero. The index-64 availability read aliases byte five of
record one, a retaliation-damage accumulator for player-zero gang slot one.
Fresh recreation matches initialize the owner alias to player zero, so the
neutral-neighbor predicate never consults the availability alias at index 64.
Arbitrary save-loaded alias values are irrelevant because original saves are
unsupported.
For ordinary scenarios selector `0x25` makes three deterministic ascending-
sector passes over owned sectors with occupancy below six:

1. choose the first strict maximum positive selector-`0x24` count (baseline
   zero, so zero never qualifies and equal values retain the earlier sector);
2. if none, choose the first sector whose selector-`0x5b` previous-Chaos count
   is zero; and
3. if none, choose the first strict minimum nonzero selector-`0x26` count from
   baseline nine. Selector `0x26` counts non-player-owned cells in the same
   literal 3-by-3 bounds and does not test availability.

If all three passes fail, selector `0x25` returns `-1`. Big Man (scenario 8)
instead has an empty radius-zero list, then tests `[18, 26, 19, 27]`, then
`[9, 17, 25, 33, 10, 18, 26, 34, 11, 19, 27, 35, 12, 20, 28, 36]`, taking the
first player-owned sector with occupancy below six. If neither list succeeds,
it preserves the incoming anchor even when that anchor is full. Selector
`0x25` consumes no RNG.

The normal failure value `-1` is stored as encoded anchor 63. A later refresh
subtracts 64 passes raw `-1` to selector `0x24`, whose center-owner read occurs
before its neighborhood bounds checks and therefore aliases adjacent memory as
well. Big Man is the only selector-`0x25` path that reads the incoming anchor;
when its scans fail it returns `anchor - 64`, so the outer re-encoding preserves
63, ordinary sector encodings, or the inactive encoding 164 unchanged.

Normal planner calls to `0x00408214` pass this encoded anchor, so they always
take the direct `>= 0x40` path and consume no placement RNG. The role-4 paths
for Kill 'Em All, Power, Greed, Big 40, Acceptance, Dominance, and Armageddon
(scenario IDs 0 through 5 and 9) can instead override the anchor with the first
visible-hostile sector returned by selector `0x9a`, again encoded with
`0x40`. Siege hire-role slots 5 and 7 override it with the Right Hands sector
plus `0x40`. Resolution at `0x00472775` later consumes the selected destination
during the internal Hire phase.

The ten normal `0x00408214` call sites are `0x00459bc8`, `0x0045a08c`,
`0x0045a55c`, `0x0045aafe`, `0x0045af7e`, `0x0045b3fe`, `0x0045b671`,
`0x0045ba56`, `0x0045bc23`, and `0x0045bfe9`. The seven visible-hostile
overrides occur at `0x00459bb1`, `0x0045a075`, `0x0045a545`, `0x0045aae7`,
`0x0045af67`, `0x0045b3e7`, and `0x0045bfd2`; Siege's Right Hands override is
at `0x0045ba3f`. All 18 direct xrefs are inside the outer planner.

**Interpretation:** ordinary AI hiring does not use mode 0 or mode 1's random
placement result: the planner has already reduced the choice to a persisted,
encoded sector. The otherwise surprising mode-0 draw is relevant only if an
unencoded caller reaches that helper.

**Confidence:** High static evidence for `0x00408214` mode control flow, exact
mode-1 multiset order and RNG use, encoded direct mode, anchor initialization
and persistence, selectors `0x24` through `0x26`, all direct call sites,
scenario overrides, and the planner/resolver call path. The behavior is
recovered and wired into the recreation's live AI planner.

**Implementation:** the six encoded anchors are authoritative `AiPlanningState`
members covered by canonical hashes, native saves, and replays. Fresh local
matches create all six original player slots and initialize each anchor from
gang slot zero. `OriginalAiHirePlacementModeRules` isolates the exact transient
role/scenario override, including the raw-100 visible-hostile sentinel. Live AI
preparation now preserves or refreshes the anchor, counts previous Chaos actions
at the candidate sector, selects the first visible hostile regardless of its
controller type, applies Big Man and Siege overrides, and feeds the encoded
zero-RNG result to deferred Hire resolution.

**Implementation:** `AiPlanningState` now persists and hashes the older,
immediately previous, and newly planned action bytes for all six-by-81 slots.
Active slots roll at AI planning entry; accepted computer commands update the
planned byte, and cancellation clears it. Hire resolution now reuses the first
inactive roster slot and clears that slot's family and three action generations.
The six first-plan flags are now authoritative, hashed, and persisted: first
preparation resets all 81 records and skips rollover, while subsequent passes
roll active records. The recovered duplicate cleanup is live: for each sector
with more than one previous Chaos, selector `0x70(..., 1)` rewrites only the
first ascending matching slot to None; it then does the same for previous
Influence through selector `0x71`, rewriting the first match to Snitch. The two
command-dependent target bytes now roll, reset, hash, save, and replay with
their action generation. Accepted computer commands encode them using the
resolver-confirmed meanings documented in `BIN-AI-004`.

**Next validation:** capture fixed original placement decisions across ordinary,
Big Man, visible-hostile, and Siege paths while preserving pass order and the
zero-RNG encoded path. Variation caused only by arbitrary original-save alias
bytes is outside the supported scope.

## Mentality and sector selection

### BIN-AI-004 - global AI Mentality byte and first consumers

**Observation:** The Win32 string table in EXE-GOG-1.1 maps resource IDs 46,
47, 48, and 49 to `GOON`, `CRIMINAL`, `CRIME LORD`, and `HOMICIDAL MANIAC`.
The setup presenter `0x0045519d` loads the displayed choice at call site
`0x00455502` using resource ID `46 + (signed byte)[0x00487850]`. In the central
state-query function `0x00402d70`, case `0x36` returns that same signed byte.
This distinguishes it from the scenario-like global `0x004abbe8` and from
query selectors `0x2f` and `0x31` used elsewhere in the planner.

The six focused writes now have bounded data-flow classifications:

- `0x00464618` is preference initialization. `0x0046439a` obtains the registry
  value named `prefsDiff` with `RegQueryValueExA` and copies its low byte into
  `0x00487850`.
- `0x00439542` is the setup-panel apply path. `0x00438da5` snapshots the global
  into a local selection, changes that local from the setup hit regions, and
  writes it back alongside the selected scenario and duration fields.
- `0x00461bf6` and `0x00461fea` are two commit paths in the same UI/event
  handler. The first path initially copies `0x00487850` to the one-byte staging
  field `0x0049833c`; both paths later copy that staging byte back.
- `0x00463bc7` and `0x00463bf1` restore the same one-byte staging field after
  `0x0046381a` serializes/deserializes it with adjacent setup fields. They are
  selected by two distinct four-byte format markers. Whether those formats are
  file, local IPC, or legacy-network envelopes is intentionally left unlabeled.

There are eight genuine selector-`0x36` calls, all in two functions: calls at
`0x0040a734` and `0x0040a7b8` in `0x0040a1a7`, plus calls at `0x0043466a`,
`0x004346d3`, `0x00434da6`, `0x00434dd9`, `0x00435090`, and `0x004350c3` in
family-1 handler `0x00434080`. A nearby call at `0x0040950f` is not a consumer:
its actual selector argument is `0x21`; `0x36` only appears in a preceding
comparison.

`0x0040a1a7` rebuilds a 24-byte record for each ordered player pair. For the
active observer, offset `+0` counts every sector owned by the other player.
Offset `+2` counts those sectors where the observer's gangs present there have
a strictly greater combined effective Combat + Defense total than the visible
defending owner's gangs. Selectors `0xb0`/`0xb1` enumerate only defenders whose
per-observer visibility byte is 1; selectors `0x5e`/`0x47` enumerate the
observer's own local gangs. The runtime gang bytes at `0x00498dba` and
`0x00498dbb` are effective Combat and Defense: the attack resolver independently
adds the former to Force at `0x00473dd3` and consumes the latter as Defense at
`0x00473b17`.

When both counts are positive, the exact test at `0x0040a816` is signed integer
`(advantaged sectors * 100) / owned sectors > 75`; exactly 75 percent does not
qualify. Target controller types 0 or 3 (local or legacy-remote human) enter the
test at Mentality 1 or higher. Other controller types enter it only below
Mentality 2. On success, `0x0040a859` sets the ordered pair's byte at `+20`, and
`0x0040a86d` writes `-10` to `attitude[observer, other]`. The row stride `0x90`,
pair stride `0x18`, and the same observer-major indexing in the flag's only
external consumer at `0x0042085d` establish the direction. The flag is an
ephemeral planner predicate; its consumer can admit a later branch when the
owner is already hostile and no visible defending gang is available.

The six family-handler calls form three paired gates. Four formerly anonymous
state selectors are now bounded:

- selector 3 returns the active player's cash. The turn resolver at
  `0x00472775` compares this field with action and equipment costs, subtracts
  those costs, and adds received income back into the same per-player word;
- selector 4 returns the selected sector's signed Tolerance byte at sector
  stride `0x24`. The right-panel presenter `0x004120ef` independently renders
  this byte on the `TOLERANCE` row;
- selector `0x21` returns the sector owner, or `-2` for a disabled sector; and
- selector `0x35` tests whether that owner has player type 0 or 3. Setup and
  initialization paths identify those types as local-human and legacy-remote
  human respectively, so the selector is a human-owner predicate. This is a
  planner classification only: original networking remains an explicit
  non-goal and no transport or protocol behavior is being recreated.

The raw planned-action bytes are also identified. The command atlas
`PX00129.bmp` lists the fourteen commands in numeric order, while the turn
resolver groups byte 10 by destination sector and accounts byte 3 using sector
income. Together these independently map byte 3 to **Chaos**, byte 10 to
**Move**, and byte 13 to **Snitch**.

The bounded family-1 blocks now establish several observable decisions. One
path keeps Snitch only when cash is greater than 50 and otherwise writes Move.
Another path requires at least 50 cash before entering its crime choice, then
writes Chaos when the target sector has Tolerance below 4 and Snitch otherwise;
its fallback writes Move. For a human-owned target, Mentality 1 or higher can
enter that crime choice. The paired non-human-owner path admits Mentality 0,
while another pair has an exact-Mentality-2 override. Mentality 3 shares the
`>= 1` branches but not that exact-value override.

The remaining selectors in those gates are now structurally identified:

- `0x3c` reads the gang's Force byte;
- `0x3d` reads its queued-action byte;
- `0x51` reads its effective Heal statistic; and
- `0x2a` tests the current sector's Crackdown record and returns nonzero while
  police are active; and
- `0x2c` is a strict single-gang Control feasibility predicate. It rejects
  disabled, unavailable, or already-owned sectors. For a neutral sector it
  tests whether gang Force + Control exceeds sector Income + Support. For an
  enemy sector it adds every defending gang's Force + Control to that defense
  and performs the same strict comparison. Its nested selector `0x91`
  enumerates other players' gangs in that sector only when the querying
  player's per-gang visibility byte is nonzero, so unseen defenders are not
  included in the AI estimate. The field at record offset `+23` is
  independently used by the Control resolver at `0x00472775`; offset `+24`
  returned by selector `0x51` is the following Heal statistic.

These are verified branch facts, not yet a complete policy table: the target
enumeration and earlier guards still determine which gang/sector pair reaches
each gate. The AI planning records begin at `0x0048a250`, use a 16-byte stride,
and retain three three-byte action/target generations: older at offsets
`+2..+4`, immediately previous at `+5..+7`, and newly planned at `+8..+10`.
Selectors `0x3f`, `0x3e`, and `0x3d` read their action bytes at `+2`, `+5`,
and `+8` respectively. The raw action values 0 through 14 are the public
command IDs; this is planner history, not a separate internal action enum.

For each active slot, `0x00458fa0` performs the exact rollover before planning:
`+2..+4 <- +5..+7` at `0x004590a9`/`0x004590c2`, then
`+5..+7 <- +8..+10` at `0x0045913f`/`0x00459158`, followed by clears of the
new tuple at `0x004591d5`, `0x004591ef`, and `0x00459209`. Strategic refresh
then runs at `0x0045936f`. The duplicate-Chaos cleanup queries selector `0x5b`
at `0x0045939c` and may rewrite the immediately previous action at
`0x004593d6`/`0x00459424`; only afterward does the dispatcher visit all active
gangs at `0x004594df` and write their new actions at `+8`. The hire-anchor
selector-`0x24` check at `0x00459502` and selector-`0x25` fallback at
`0x00459553` therefore observe the `+5` immediately previous action. There is
no second promotion at the end of planning.

Selector `0x5b`'s dispatcher case at `0x004048b8` counts same-sector roster
members whose `+5` action is 3 (**Chaos**); selector `0x6f` at `0x00404949`
does the analogous count for previous action 9 (**Influence**). Inactive
records use sector 100. Their tuples are not shifted or cleared by rollover,
but the dispatcher resets a slot when it is reused, and selector `0x5b` omits
inactive slots naturally through its same-sector test.

Reset/initialization helper `0x00409de1` clears offsets `+2..+10`, family 99,
and the other per-record planning fields. The complete six-by-81-by-16-byte
record block (length `0x1e60`) and the six first-plan flags at `0x00482108`
are both serialized by save/load paths `0x0046381a` and `0x00463cc5`. Thus the
history survives save/load independently of the command resolver and has an
explicit first-planning lifecycle.

Hire resolution in `0x00472775` scans gang records upward from slot zero while
the mirrored sector byte is not 100, with a strict usable bound of 80. When it
finds the first inactive slot it copies the complete new 32-byte gang record
into that slot; a full first 80 slots takes the failure path. This establishes
ascending inactive-slot reuse, not append-only roster growth. The planner's
reused-record reset prevents the new gang from inheriting the prior occupant's
family or action history.

The branches above are therefore command-continuity decisions, including the
case entered after a prior Snitch command.

All four action-7 (**Heal**) assignments in this handler are now bounded.
Every path first requires effective Heal at least `-3`; three require Force
below 9, while the path following no prior action or prior Chaos requires Force
below 8. No recovered family-1 path heals at Force 9. For that previous-None or
previous-Chaos path, the complete terminal branch is recovered: below the
strict Force/Heal boundary it writes Heal when selector `0x2a` reports no
Crackdown and Move through mode 5 when police are active; outside that boundary
an older Snitch writes Chaos and every other older action writes mode-5 Move.
The recreation executes this action-level branch through
`OriginalAiFamilyOneRules`. Replay-recorded preparation now supplies its exact
mode-5 destination and tie RNG, including the all-zero post-filter path. Only
the fallback when the selected action/step has no legal recreation candidate
remains provisional.

The other family-1 paths use the common Force-below-9 Heal gate. The isolated
rules also guard two distinct cash comparisons: the strict continuation changes
from Move at cash 50 to Snitch at 51, while the post-equipment crime branch
admits cash 50 and changes from Chaos to Snitch as Tolerance moves from 3 to 4.

The post-equipment continuation at `0x004345d0..0x00434712`, reached after
prior Control, Equip, or Snitch when no preceding equipment opportunity writes
Equip, is now complete. Selector `0x35` splits on whether the current sector
owner is human. A human owner takes the crime branch at cash at least 50 and
Mentality at least Criminal. A non-human owner takes it only when the raw owner
differs from the active player, is strictly greater than zero, cash is at least
50, and Mentality is exactly Goon. The literal positive-owner comparison means
player zero is deliberately excluded on this side. The crime branch writes
Chaos at Tolerance at most 3 and Snitch from 4; every failed gate writes Move
through mode 5. `OriginalAiFamilyOneRules.SelectPostEquipmentContinuation`
preserves these comparisons in the live planner.

The preceding equipment decision is also recovered and live. Selectors `0x39`
and `0x3a` read the equipped weapon and armor. Selector `0x61` chooses the
weapon candidate first. Selector `0x64` then chooses a researched type-3 armor
candidate whose Tech requirement is within the gang's raw Tech, whose Defense
at item-record offset `+0xc` strictly improves on the current armor, and whose
cost is strictly less than cash. Selectors `0x65` and `0x66` read signed
planning-record cooldown shorts at offsets `+12` and `+14`. A successful Equip
writes raw item cost times three to the matching cooldown. At the start of each
later planning pass, an equipped slot decrements its cooldown while an empty or
inactive slot resets it to zero.

Selector `0x6c` scans the acting gang's 3-by-3 neighborhood, clipping horizontal
wrap but permitting linear index 64. In Greed, a nearby cell qualifies when its
cached weight is 10 and its owner is the acting player. In other scenarios, a
cell qualifies when it has any different nonnegative owner or its cached weight
is 10. Such a cell opens the gate only while the gang lacks a weapon or armor.
Independently, a current sector owned by the acting player with weight 10 opens
the gate even when both equipment slots are filled. Weight 10 denotes a visible
hostile human gang. Index 64 preserves the original array aliases: its owner
reads player zero gang-slot-zero ownership storage, and its per-player weight
reads the next player's sector-zero weight (zero for the final player). The
recreation carries these comparisons, exact item target, cooldowns, and the
fallback continuation through authoritative planning, command resolution,
saves, canonical hashes, and replays.

The previous-Heal case is also complete at the action level. It repeats Heal
under the common Force-below-9/effective-Heal-at-least-`-3` gate. Otherwise it
queries selector `0x2c` for the acting gang's current sector, writes Control
when that strict solo-control predicate succeeds, and writes Move with a mode-5
destination when it fails. `OriginalAiFamilyOneRules.SelectHealContinuation`
and the live planner preserve this branch and its complete mode-5 target
selection.

**Interpretation:** `0x00487850` is the original match-global, zero-based AI
Mentality setting, seeded from a persisted preference and then carried through
setup staging/serialization. Family handler 1 uses it to redirect cash-qualified
crime behavior between human and non-human owners and between Chaos, Snitch,
and Move. A player-pair scoring pass also changes paths by mentality.

**Confidence:** High for resource IDs, address, display expression, query
selector, all six write classifications, all eight genuine consumer call sites,
cash/Tolerance/owner/human-owner selector meanings, command-byte mappings,
comparison constants, pair counters, integer ratio, observer-to-target write
direction, resulting raw record writes, the global's identity and persistence,
effective-stat labels, the three-generation action-history lifecycle, and the
complete target enumerators. A prepared recovered tuple rejected by modern
validation now remains unsubstituted, matching the native handler's absence of
any rejected-command policy.

The resolver at `0x00472775` establishes the complete public-command decoding
of those two bytes. Attack uses target player and that player's roster slot.
Equip and Research use an item ID in byte one. Influence uses the local site
slot in byte one. Move uses the destination sector. Give uses an equipment-slot
mask (`1` weapon, `2` armor, `4` miscellaneous) followed by the friendly target
roster slot. Sell uses the same mask in byte one. Commands without an explicit
target leave both bytes zero. Direct family-handler writes independently
confirm the Move, Equip, Attack, Influence, and Research cases; resolver lines
151-207, 346-352, 557-616, and 713-715 provide bounded decode evidence.

**Static follow-through:** The shared sector selector, site selectors, visible-
gang pools, objective targeting, family-11 formation routing, and every handler's
earlier guards are now bounded below and represented by executable regression
vectors. Native handlers write their tuples directly and have no corresponding
rejected-command path. Original policy therefore preserves an exact prepared
tuple but submits nothing when modern validation cannot represent it, rather
than invoking recreation-only scoring. Controlled original-turn traces remain
useful corroboration for the recovered decisions.

### BIN-AI-005 - shared weighted sector selector

**Observation:** `0x00408642` is the shared sector-target routine used by the
recovered family handlers. Its parameters are the active player, a selection
mode, and the active gang. Mode 0 chooses one of the eight immediate neighbors
(`-9`, `-8`, `-7`, `-1`, `+1`, `+7`, `+8`, `+9`) with uniform calls to the
original bounded RNG, rejecting row-wrap and off-board results.

For nonzero modes the routine clears an 8-by-8 integer score map, obtains the
gang's current sector through selector `0x5a`, and examines successively larger
clipped squares around it, from radius 1 through 7. Each radius rescans the full
square rather than only its perimeter, and the search stops after the first
square which contributes any candidate. The current sector is removed before
final selection. Modes 1 through 5 have bounded scoring rules:

- mode 1 scores a neutral sector `+1` only when selector `0x2c` says the gang
  can take it by strict solo Control;
- mode 2 scores an owned sector `+1`;
- mode 3 scores a sector owned by another player `+1`;
- mode 4 scores `+1` when player-pair predicate `0x2d` accepts its owner; and
- mode 5 scores a solo-controllable neutral sector `+5`, an owned sector with
  no previous-Chaos gang assignment (selector `0x5b` equals zero) `+2`, and an
  enemy-owned sector `+1`.

The remaining modes are structurally bounded but not all subordinate fields
are named yet. Selector `0x32` counts players whose controller type is 0 or 3,
so mode 6 and mode 10 branch on the number of human players. Selector `0x2d`
compares two players' positions in the six-byte player-order table, rejecting
neutral and self comparisons. Selector `0x2e` returns the sole player whose
scenario standing byte at `0x004abc08` is zero, or `-1` when zero or multiple
players share that value. `0x0047712a` builds the scenario score at
`0x004a2790`, then sets each active player's standing byte to the number of
players with a strictly greater score and inactive slots to `0xff`; selector
`0x2e` therefore returns the unique current leader. Selector `0x5e` counts one
player's nonempty gang records in a sector.

The scorer uses cash for Greed; controlled-sector count for Power, Big 40, and
Armageddon; accumulated current Support for Acceptance; and the duration-scaled
Dominance numerator followed by signed integer division by ten. Kill 'Em All
and Siege give every active player the same count of inactive player slots.
Eliminate counts ownership of the six generated Headquarters sectors, while
Big Man is the deliberate exception to the scorer's normal reset: it retains
each player's prior score and adds one for each currently owned sector among
27, 28, 35, and 36. Its score is therefore the accumulated objective total,
not a fresh projection of current center control. These scores and the exact
zero-based competition standings are now isolated in
`OriginalAiScenarioStandingRules` and feed live mode-6 movement.

The same table is not AI-private. End-turn evaluator `0x00476857` calls
`0x0047712a` before testing the active-player count and every scenario-specific
completion condition. It sets the end flag immediately when exactly one of the
six active-state bytes is nonzero, before entering the scenario switch. The
`PX05011` Player Ranking path at `0x004518d9` reads
the score and standing arrays directly. Endgame awards/statistics path
`0x0042ce61` iterates standing values 0 through 5 in order, visiting player
slots 0 through 5 within each tied standing, then appends inactive (`0xff`)
players in slot order. Thus objective games use the same scenario score table,
ties use competition standings, and eliminated players are displayed after the
ranked active players. The six fixed inactive score sentinels remain `-32000`
while standings are counted, so an extreme active score below that value can
retain an unusually low numeric place even though inactive rows render last.

## Attitude and difficulty

### BIN-AI-006 - directional attitude and hostility matrix

**Observation:** `0x004ab590` is a six-by-six signed integer matrix indexed as
`observer * 6 + other player`. Initialization at `0x0046dc10` depends on the
global AI Mentality. At Homicidal Maniac, every cell whose target player has
human controller type 0 or 3 is initialized to `-10`, while cells targeting a
computer player are initialized to `+10`. At all lower mentalities every cell
starts at zero. This changes preferences only; it grants no resources,
statistics, rolls, or visibility.

At the start of turn resolution at Mentalities 0 through 2, every entry below
`+10` increases by one. Homicidal Maniac skips the entire recovery loop.
Later resolution paths subtract from one directed cell and clamp the result at
`-10`. At non-Homicidal mentalities, initialization gives every player a fixed
reaction value from one bounded RNG draw, `Next(4) + 2`, producing 3 through 6.
Homicidal Maniac assigns reaction zero and consumes no such draw. No later
writer modifies these values. The combat path lowers the defender owner's
attitude toward the attacker by `max(reaction, damage dealt)`. A sector-control
transfer lowers the previous owner's attitude toward the new owner by exactly
twice that previous owner's reaction value.
The player-pair ratio pass at `0x0040a1a7` can also force a directed entry to
`-10` under its mentality-dependent guards.

Negative entries are the hostility boundary used by central target queries.
Selector `0x92` enumerates visible gangs in a requested sector only from
players whose observer-relative entry is negative; selector `0xab` performs a
corresponding count for visibility state 1. Selector `0x90` returns weight 10
for a visible human gang belonging to a negatively viewed player and weight 1
for other visible gangs. Numerous AI handlers read the same `< 0` predicate
directly, including shared sector-selector mode 6.

Family 11 (`0x00420950`) supplies the clearest consumer. Instruction-level
inspection is required here because the decompiler drops the assignments after
the entry calls: selector `0x5a` is saved at stack local `-0x4` and is the
active gang's current sector; selector `0x61` is independently saved at `-0x8`.
Selector `0x61` chooses a weapon upgrade. Its subordinate selector `0x6d`
admits only a requested weapon class whose tech requirement does not exceed
selector `0x62`'s local research ceiling, whose player research flag is clear
(complete), and whose cost does not exceed current cash. It compares the first
eligible item Combat
bonus for the melee, blade, and ranged classes after adding the matching
effective skills: Strength for melee, Strength + Blade for blade, Range for
ranged, and Strength + Fighting + Martial Arts for bare hands. It then scans
all 64 items in the winning class and retains only a strictly larger Combat
bonus subject to the same research and cash gates, but this second pass compares
item Tech against the gang's raw Tech rather than selector `0x62`. Class-score
ties resolve ranged, then melee, then blade, then bare hands. It returns `-1` when
bare hands win, no upgrade beats the baseline, or the result is already the
equipped weapon.

Selector `0x65` reads the first of two planning-record shorts at offsets
`+12/+14`. Planning initialization sets the first to zero for an unarmed gang
and otherwise decrements it by one; a weapon Equip writes `item cost * 3` back
to it. Family 11 admits the selector-`0x61` weapon only when this replacement
cooldown is at most zero, the item is affordable, and the previous action is
not **Attack**. It then writes **Equip**, the item ID, and the new three-times-
cost cooldown. The following analogous opportunities use selectors `0x64` and
`0x74` for the other equipment slots before the Heal gate.

After those Equip and Heal opportunities, the handler reads the owner of its
current sector. In an active-player-owned sector it always writes **Move** and
calls mode 10 at `0x00420e7b`. The subsequent write at `0x00420eea` leaves the
first auxiliary short equal to the current sector rather than replacing it
with the chosen destination.

In any other sector, selector `0xac(active player, current sector, 0)` scans
other players in ascending slot order and their gangs in ascending slot order.
It returns the first encoded `player * 81 + gang` whose sector equals the
current sector, whose observer-specific visibility/status byte is nonzero, and
whose raw gang-state byte at record offset `-1` from the sector field is zero.
The handler writes **Attack** against that decoded player/gang whenever the
result is nonnegative. If no such target exists, selector `0x76` determines
formation leadership: considering only family-11 gangs in ascending gang-slot
order—including inactive slots whose planning family remains 11—ordinals 0,
6, 12, and so on return 1. Those anchors write **Move** with mode 10 at
`0x00421085` and replace their stored formation-sector short with the chosen
destination. Other family-11 gangs write **Move** with mode 16 at `0x00421157`
and retain their current-sector short. Selector `0x77` finds the corresponding
block anchor and returns its stored formation-sector short, so mode 16 awards
that sector `+1` and feeds it through the common ring/path selection. Neither
path is subjected to the selector's family-0/1 strict-Control late filter.

Thus mode 10 is not a generic hostile-target rule: with humans present it seeks
human-owned territory regardless of attitude, and with no humans it seeks any
other non-neutral owner's territory. Mode 16 is the follower path for five of
each six family-11 gangs, while the first gang in each block establishes the
formation destination.

**Recreation status:** the equipment priority/cooldowns, Heal gate, and exact
first-visible local Attack target are live and replay-wired. Modes 10 and 16 are
also live; the separate six-by-81 formation-sector shorts preserve inactive
family records and are included in authoritative hashes, saves, and replays.

**Interpretation:** this is an attitude/hostility system, not a scalar combat
bonus. Homicidal Maniac begins maximally hostile toward human players and
maximally friendly toward computer players; ordinary interactions can create
directed hostility at other mentalities, and hostility decays toward
friendliness by one point per turn. The earlier scalar attack score only
approximated part of that outcome; the current recreation persists and resolves
the matrix, applies the recovered recovery and combat/Control mutations, and
uses negative hostility in attack targeting.

**Confidence:** High for matrix dimensions and direction, `[-10,+10]` bounds,
initial values, mentality-gated per-turn recovery, reaction range/immutability, combat and
Control decrements, negative-hostility target gating, controller classification,
mode-10 target ownership, mode-16 group semantics, and both formation modes'
late filters. Low only for the original public/internal name of the reaction
value.

The exact new-match order is now bounded. For non-Homicidal games the six
reaction draws are the only RNG calls between entry to `0x0046dc10` and city
generation at its decompiled line 85. Homicidal games skip those draws. The
offer filler remains later in the outer setup caller. The recreation now
initializes at this point, recovers attitudes at the Command-to-Execution
whole-turn resolver boundary, applies the recovered combat and Control changes,
uses hostility for AI attack candidates, and includes the state in canonical
hashes, native saves, and replays.

**Next validation:** capture fixed original traces proving the combat-advantage
threshold, reaction, recovery ordering, and family-11 weapon replacement
cooldown through the first complete turns.

### BIN-AI-007 - per-player difficulty resolution band

**Observation:** new-match initialization also fills a six-entry integer table
at `0x004a2570`. Every slot starts at 1. At Goon, computer-controlled slots are
changed to 0; Criminal leaves all slots at 1; Crime Lord and Homicidal Maniac
change computer-controlled slots to 2. Human-controlled slots remain 1 at all
four mentalities.

The whole-turn resolver `0x00472775` reads this table repeatedly while resolving
gang actions. Its helper `0x00475f70(pool, threshold)` rolls `pool` inclusive
d6 values with `0x0045d227(6)` and counts results greater than or equal to the
threshold. Every one of the nine band-table reads is now bounded:

- Heal uses `Heal + 4` dice. Bands 0/1 succeed on 5+, while band 2 succeeds on
  4+; successes add Force, capped at 10.
- Influence uses `Force + Influence`. Band 0 removes `trunc(pool/5)` dice and
  succeeds on 5+; band 1 uses the full pool at 5+; band 2 uses the full pool at
  4+.
- Research uses `Force + Research`. Band 0 removes `trunc(pool/5)` dice and
  succeeds on 6; band 1 uses the full pool at 6; band 2 uses the full pool at
  5+.
- Each Chaos gang separately includes sector Income in
  `Income + Force + Chaos`. Band 0 removes one fifth of the pool at 5+, band 1
  uses the full pool at 5+, and band 2 uses the full pool at 4+. When the band-2
  player owns that sector, only `successes - trunc(successes/4)` contributes to
  the Crackdown comparison.
- A hidden target evades when an inclusive d20 roll is below
  `Stealth + 14 - Detect` for attacker bands 0/1 or
  `Stealth + 10 - Detect` for band 2.
- Main Attack reduces a band-0 defender's Defense by one quarter. It then rolls
  `Force + CombatRating - adjusted Defense` at 6+/5+/4+ for attacker bands
  0/1/2. Positive pools impose minimum damage `trunc(pool/4)`.
- Retaliation first requires the target's action byte not to be 8 (**Hide**).
  It is then allowed when the attacker has effective Martial Arts zero, when
  the attacker has a weapon equipped, or when the defender has both positive
  effective Martial Arts and no weapon equipped. Equivalently, a bare-handed
  positive-Martial-Arts attacker suppresses retaliation unless the defender is
  also a bare-handed positive-Martial-Arts gang. Eligible retaliation rolls
  its corresponding pool at 5+ for defender bands 0/1 or 4+ for band 2, then
  halves successes using integer truncation.

The recreation implements these bands and formulas in
`OriginalResolutionRules` and routes the corresponding resolution paths
through them. This is a mechanical resolution calibration, not merely a
planning preference.

The compound operands are direct gang-record fields. Offset `+7` is the public
action byte; selector `0x39` exposes offset `+4`, the equipped-weapon item or
`-1`; and selector `0x58` exposes offset `+31`, the last of the fourteen
effective statistics and therefore Martial Arts. The attack block uses those
same offsets from both its copied attacker record and the targeted live record.

**Confidence:** High for initialization, controller/mentality mapping, helper
semantics, all nine reads, formulas, thresholds, integer truncation, and the
complete retaliation-eligibility predicate.

**Next validation:** capture fixed original traces at bands 0, 1, and 2 for
every affected action, including Hide and both Martial Arts branches.
