---
id: FND-AI-050
title: Each scenario's hire block adjusts the schedule slot by late-turn remaps, a forced hunter slot with a previous-role guard, family quotas and a minimum of family 0 or 4
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004596BB..0x0045C06C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045C06D..0x0045C176
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00481018..0x00481033
tool: Ghidra 12.1.3
environment: null
---

## Observation

The hire switch of `0x00458FA0` (FND-AI-003, FND-AI-009) has one block per
scenario value. In each block the schedule slot `k` starts as selector `0x2F`
(elapsed turns) modulo 10, or modulo 11 in scenario 3. The terms below are:

- `f`: selector 1 (the match length) divided by the float 52.0 at `0x00481018`,
  kept as a float. The other float constants are 4.0 (`0x0048101C`), 2.0
  (`0x00481020`), 100.0 (`0x00481024`), 3.0 (`0x00481028`), 6.0 (`0x0048102C`)
  and 10.0 (`0x00481030`). A quota test resets `k` to 0 when `f * constant` is
  less than or equal to the count converted to float.
- `R`: selector 2 (turns remaining). `cash`: selector 3.
- `H`: selector `0x9A` with argument 1, the first sector in ascending order whose
  cached weight (`sector_weight`) is 10, or 100 when there is none.
- `cov`: selector `0x5F` for `H`, -1 when no family-6 gang covers it.
- `prev`: selector `0x8F`, the previous hire role (FND-AI-014).
- Counts over the player's active gangs by family byte: `c2` family 2 (selector
  `0x68`), `c3` family 3 (`0x6A`), `c5` family 5 (`0x6B`), `c7` family 7
  (`0x69`), `c6` families 6 and 12 (`0x67`), `c04` families 0 and 4 (`0x6E`).

A "hunter test" with slot `F`, guard `G` and required counts means: when `H`
is 100, or `cov` is not -1, or a required count is below 1, or `prev` equals
`G`, then if `k` equals `F` it is replaced by the redirect; otherwise `k`
becomes `F`.

| Scenario | Gate | Remaps | Hunter test | Quotas (reset to 0 when count reaches) | Minimum `c04` | Last override |
|---|---|---|---|---|---|---|
| 0 | active gangs at most the limit, and `R` greater than the match length / 8 | `R` < 10 and `k` in {1, 3, 6, 8}: 0. `k` = 9, `scenario_standing` nonzero and (`R` < 10 or cash < 100): 0 | `F` 5, `G` 5, needs `c3` and `c7`; redirect: `c7` = 0 gives 1, else `c3` = 0 gives 3, else 9 | {3, 6, 8}: `c3` at 4f. 5: `c6` at 2f. 9: `c2` at 2f, or cash below 100f. 1: `c7` at f | 5 | none |
| 1 | at most the limit, and `R` > 2 | `R` < 10 and `k` = 4: 1. `k` = 8 and (`R` < 10 or cash < 100): 1 | `F` 6, `G` 6, needs `c5` and `c7`; redirect: `c7` = 0 gives 4, else `c5` = 0 gives 9, else 8 | 8: `c2` at 4f. 6: `c6` at 4f. 9: `c5` at 3f. 4: `c7` at f | 4 | none |
| 2 | at most the limit, and `R` > 2 | `R` < 5 and `k` in {1, 4, 6, 8}: 0. `k` = 5 and (`R` < 10 or cash < 100): 0 | `F` 2, `G` 2, needs `c5` and `c7`; redirect: `c7` = 0 gives 1, else `c5` = 0 gives 4, else 5 | {4, 6, 8}: `c5` at 6f. 5: `c2` at 2f. 2: `c6` at 3f. 1: `c7` at f | 4 | none |
| 3 | at most the limit, and `R` > 2 | `R` < 8 and `k` in {2, 3, 4, 6, 8}: 0. `k` = 5 and (`R` < 10 or cash < 100): 0 | `F` 10, `G` 10, needs `c3` (tested twice) and `c7`; redirect: `c7` = 0 gives 4, else `c3` = 0 gives 2, else `c5` = 0 gives 3, else 5 | {3, 6}: `c5` at 3f. {2, 8}: `c3` at 3f. 5: `c2` at 2f. 10: `c6` at 3f. 4: `c7` at f | 4 | none |
| 4, 5 | at most the limit | `k` = 8 and cash < 100: 1 | as scenario 1 | as scenario 1 | 4 | none |
| 6 | at most the limit | none | none | 9: `c5` at 3f. 4: `c7` at f | 4 | none |
| 7 | at most the limit | none | none | {3, 6, 8}: `c3` at 4f. {5, 7}: `c6` at 10f. 9: `c2` at 2f, or cash below 100f. 1: `c7` at f | 5 | `c6` below 1: `k` = 5 |
| 8 | none | `k` in {0, 2, 5, 7} and `c04` > 5: 4 | none | none | none | none |
| 9 | at most the limit | none | `F` 5, `G` 5, needs `c3`; redirect: `c3` = 0 gives 9, else 3 | 3: `c2` at 4f. 5: `c6` at 4f. 9: `c3` at 3f | 4 | `c2` below 1: `k` = 3 |

The order within a block is the order of the columns: remaps, hunter test,
quotas, the minimum (`c04` below the value sets `k` to 0), the last override.
The "at most the limit" gate compares `0x0048E2E0 + player * 4` (FND-AI-044)
with `0x00482110 + player * 4`; when a gate fails the block does nothing more.

Each block then switches on `k` to call the offer ranking `0x004078D9` with a
mode and store the role in `0x00482128 + player * 4`, the pairs of FND-AI-009.
Scenarios 0 and 7 then set the chosen offer to -1 when cash is below the
16-bit value at +0x7A of the offer's gang definition (`0x00459B7A`,
`0x0045B9EF`). A negative offer leads to selector `0x8E` and `0x004078B8`;
otherwise `0x00408214` receives the placement: `H + 0x40` when `k` is the
block's hunter slot `F`, the Right Hands' sector (selector `0x5A` for slot 0)
plus `0x40` in scenario 7 when `k` is 5 or 7 (`0x0045BA0D..0x0045BA3F`), and
the placement anchor at `0x0048E2F8 + player * 4` otherwise.

After the switch, at `0x0045C06D`, for every scenario: `n` is selector `0x23`
(the number of sectors the player owns). When `n / 4` (signed, truncated) is
less than `c6` and `n` is greater than 6, the loop from `0x0045C0B6` visits the
player's roster slots in ascending order and takes the first active one whose
family byte (selector `0x59`) is 6 or 12. It calls selector `0x3D` (planned
action) with `n` as the slot argument (`0x0045C12E`), not the visited slot;
unless that returns 1, it writes family 0 to the visited slot's record
(`0x0045C15B`) and returns. When it returns 1 the loop moves on and tests the
same slot `n` again for each later match.

## Interpretation

Each computer player hires a hunter (family 6, or family 12 in scenario 7) as
soon as a hostile human gang is visible in a sector no hunter covers, provided
the families the test needs are present; the redirect makes the hunter's own
slot hire the first missing family instead. The quotas cap each family at a
multiple of the match length in 52-turn units, and the minimum keeps four or
five gangs of family 0 or 4 before anything else. Late in a match, slots move
to the cheap default hire.

FND-AI-009 describes scenarios 1, 4 and 5 as identical. They share the
schedule, the hunter test and the quotas, but scenario 1 alone has the
turns-remaining gate and the late-turn remaps of slots 4 and 8; scenarios 4
and 5 remap slot 8 on cash alone.

A guard fires when the previous hire role equals `G`. Scenario 0 writes the
roles 1, 2, 3, 4 and 6 and scenario 3 the roles 1 to 6, so their guards (5 and
10) never fire. In scenarios 1, 4 and 5 the guard fires after a role-6 hire, in
scenario 2 after a role-2 hire and in scenario 9 after a role-5 hire. When it
fires, the hunter slot is not forced that turn, and a turn whose own slot is
the hunter slot hires by the redirect, so no hunter is hired that turn.

When the player owns more than six sectors and its hunters outnumber a quarter
of them, one hunter a turn goes back to family 0. The test of the planned
action looks meant to spare a hunter that is attacking, but it reads the
roster slot numbered by the sector count, so which hunter is spared depends on
an unrelated gang.

The cash test in scenarios 0 and 7 repeats the one the ranking makes before it
returns (RULE-AI-008) and changes nothing.

## Alternatives

The hunter placement `H + 0x40` is used only when `k` equals `F` after all
adjustments. The adjustments after the hunter test only set `k` to 0 or, in
scenario 9, to 3, so a final `k` equal to `F` always comes from the forcing
branch, where `H` is a sector.

## How to reproduce

In `0x00458FA0`, the switch on selector 0 at `0x0045C031` jumps through the
table at `0x0045C045`. Each block starts with the calls to selectors `0x2F`
and 2 and ends with the calls to `0x004078D9`, `0x00408214` or `0x004078B8`.
The float constants are loaded with `FMUL` from `0x00481018` onward. The tail
starts at `0x0045C06D` with the call to selector `0x23`.
