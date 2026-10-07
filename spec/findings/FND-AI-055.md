---
id: FND-AI-055
title: The equipment selectors start from the equipped item, compare Combat, Defense, Stealth, Detect or Control, and the Research list is Tech capped
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040498A..0x00404C95
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00404CA7..0x00404DBF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00404DD1..0x00404F25
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00404F37..0x004051A5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00405276..0x00405318
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00405404..0x00405525
tool: Ghidra 12.1.3
environment: null
---

## Observation

All are cases of `0x00402D70` taking a player and a roster slot. Item records
are the 166-byte records at `0x004A5F08`; the fields read are +0x7A (the
category), +0x7E (cost), +0x80 (Tech Level), +0x82, +0x84, +0x86, +0x88 and
+0x8C. The per-item research byte is `0x004A2608 + item * 6 + player` and cash
is `0x004A25E8 + player * 4`. "Own Tech" below is selector `0x4A` (the gang
definition's Tech Level, FND-AI-054) and "the cap" is selector `0x62`.

Selector `0x6D` (`0x00405276`, fourth argument a category) returns the first
item from 0 to 63 whose category equals the argument, whose Tech Level is at
most the cap, whose research byte is 0 and whose cost is at most cash, and 0
when there is none.

Selector `0x61` (`0x0040498A`) sums selectors `0x54`, `0x57` and `0x58` into
a bare-hands value `B`. It forms three scores: `s1`, the +0x82 value of
selector `0x6D` with category 1 plus selectors `0x54` and `0x55`; `s0`, that
of category 0 plus selector `0x54`; `s2`, that of category 2 plus selector
`0x56`. The comparisons at `0x00404A99..0x00404B3B` choose category -1 when
`s1`, `s0` and `s2` are all below `B`; otherwise the category with the greatest
score, where 2 wins a tie with either other and 0 wins a tie with 1. For
category -1 it returns -1. Otherwise the running choice starts at the equipped
weapon (selector `0x39`), or item 24 when there is none (`0x00404B69`), and
the loop over items 0 to 63 takes an item of that category with research byte
0, Tech Level at most own Tech, +0x82 strictly greater than the running
choice's (`0x00404C2B`) and cost at most cash (`0x00404C4A`). It returns -1
when the result is 24 or the equipped weapon.

Selector `0x64` (`0x00404DD1`) starts from the equipped armor (selector
`0x3A`), or item 0, and takes items of category 3 with research byte 0, Tech
Level at most own Tech, +0x84 strictly greater than the running choice's and
cost strictly below cash. It returns -1 when the result is 0 or the equipped
armor.

Selector `0x72` (`0x00404CA7`) starts from the equipped armor, or item 1, and
takes items of category 3 with research byte 0, Tech Level at most own Tech
and +0x86 strictly greater, with no cost test. It returns -1 when the result
is 1 or the equipped armor.

Selectors `0x74` (`0x00404F37`) and `0x75` (`0x00405077`) start from the
equipped miscellaneous item (selector `0x3B`), or item 0, and take items of
category 4 with research byte 0, Tech Level at most own Tech and a strictly
greater +0x88 (`0x74`) or +0x8C (`0x75`), with no cost test. Each returns -1
when the result is 0 or the equipped item.

Selector `0x73` (`0x00405404`) walks the list 44, 41, 42, 43, 46, 50, 49, 52
and returns the first item whose Tech Level is at most the cap and whose
research byte is above 0, or -1.

The calls, found by walking each handler's range (FND-EXE-004): selector
`0x61` and `0x64` are called once each by the handlers `0x00428EF0`,
`0x00434080`, `0x0041FEF0`, `0x00435BD0`, `0x00401000`, `0x0043A1D0`,
`0x00431C60`, `0x00436C70`, `0x004605E0`, `0x00420950`, `0x004353A0`,
`0x0040ABC0` and `0x00466910`; selector `0x72` only by `0x0042A6E0`
(`0x0042A70C`); selector `0x74` by `0x00420950` (`0x00420C5A`) and
`0x004353A0` (`0x004358A8`); selector `0x75` by `0x0040ABC0` (`0x0040B63A`)
and `0x00466910` (`0x00467385`); selector `0x73` by `0x00436C70` four times.

## Interpretation

In FMT-DATA-003 the fields are `type`, `cost`, `tech_level`, `combat`,
`defense`, `stealth`, `detect` and `control`, and categories 0 to 4 are melee,
blade, ranged, armor and miscellaneous. The gang selectors `0x54` to `0x58`
are Strength, Blade, Ranged, Fighting and Martial Arts, as the scores pair
them with the categories.

The weapon choice improves on the equipped weapon's Combat, and a class with
no eligible item is scored with item 0's Combat. Family 10 (`0x0042A6E0`)
chooses armor by Stealth. Families 11 and 12 choose a miscellaneous item by
Detect and families 13 and 14 by Control. Family 7's fixed Research list is
capped by the local Tech ceiling.

## Alternatives

The pairing of selectors `0x54` to `0x58` with gang statistics is taken from
the categories they are added to; the selectors' own cases were not read.

## How to reproduce

Read the listed cases of `0x00402D70`. For each, note the offset added to
`0x004A5F08` in the comparison inside the item loop. Walk the handlers listed
in FND-EXE-004 for the `PUSH` before each `CALL 0x00402D70`.
