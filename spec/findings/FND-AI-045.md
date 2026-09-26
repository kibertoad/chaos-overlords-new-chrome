---
id: FND-AI-045
title: At the start of a match and after a load the computer players' site sums are cached per sector, and a new match also clears the AI flags and seeds the placement anchor
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00409F47..0x0040A1A6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040AA65..0x0040AAE2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040AAE3..0x0040AB1F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046EB4E..0x0046EC9D
tool: Ghidra 12.1.3
environment: null
---

## Observation

The three functions have the ranges given in FND-EXE-004.

`0x0046E766` (the match entry) tests its byte argument at `0x0046EB4E`. When it
is 0 it calls `0x0040AAE3` at `0x0046EC98` and then `0x0046D22F`. Otherwise it
resets per-player values (cash 20, or 500 in scenario 9, among others), calls
the city generator `0x0046DC10` and then, at `0x0046EC45`, `0x0040AA65`.

`0x0040AA65` loops over players 0 to 5. For each it writes 0 to the byte at
`0x00482108 + player` and to the byte at `0x00482158 + player`, calls
`0x00409F47` for the player, writes 81 to the 32-bit value at
`0x00482110 + player * 4`, and writes the result of selector `0x5A` for the
player's roster slot 0 (that gang's sector) plus `0x40` to the 32-bit value at
`0x0048E2F8 + player * 4` (`0x0040AACD`).

`0x0040AAE3` loops over players 0 to 5 and calls `0x00409F47` for each.

`0x00409F47` takes a player. For each sector 0 to 63 it clears the four 16-bit
values at +6, +8, +10 and +12 of the 14-byte per-sector record
`0x0048E310 + player * 0x380 + sector * 14` (FND-AI-044), stores selector 7
(the sector's byte +4, Income) into +6, and then for site slot `k` from 0 to 2
adds selector `0x0D` to +6, selector `0x16` to +8, selector `0x12` to +10 and
selector `0x0C` to +12. Each of these selectors reads a signed 16-bit field of
the site definition whose number is in byte `+7 + k * 2` of the sector record,
in the table at `0x004AB668` with records of `0x3E` bytes: `0x0C` reads +0x18
(Support), `0x0D` reads +0x1E (Cash), `0x12` reads +0x28 (Chaos) and `0x16`
reads +0x30 (Research). No test of the slot's progress byte or of an empty slot
is made. At the end it calls `0x0040A1A7` for the player (`0x0040A195`).

None of the selectors takes the player, so all six players' records hold the
same sums. Of the four sums only +8 has a reader, selector `0x30` (FND-AI-044),
which family 7 calls (FND-AI-035).

## Interpretation

Argument 0 of `0x0046E766` is the load path and a nonzero argument starts a new
match. A new match sets every player's `ai_started` and family-9 flag to 0,
sets the hire gang limit to 81 until the first pass computes it, and seeds the
placement anchor with the Right Hands' sector plus `0x40`: this is where
`seed_anchor` of RULE-AI-013 runs, after the city and the headquarters exist.
The anchor, `ai_started` and the family-9 flag are saved (FND-AI-043,
FND-AI-019), so a load does not redo this.

Both paths cache per sector the sums of the site fields the planner uses. The
Research sum is the `research_score` that family 7 compares; it counts every
site definition in the sector, finished or not, and it is computed once per
match start or load, so a change to a sector's sites later in the match is
not seen until the game is loaded again. The Income plus Cash, Chaos and
Support sums are never read.

The pass through `0x0040A1A7` at the end of `0x00409F47` refreshes the
per-sector and pair records for every player slot, empty ones included, before
anyone plans.

## Alternatives

Whether a sector's site slots can change after city generation, which would
make the cached Research sum stale, is not checked here.

## How to reproduce

List the callers of `0x0040AA65` and `0x0040AAE3` (one each, in `0x0046E766`)
and read the branch on the argument that leads to them. In `0x00409F47`, read
the instructions from `0x00409FED` to `0x0040A187`: the call with selector 7,
the inner loop of three with selectors `0x0D`, `0x16`, `0x12` and `0x0C`, and
the stores to `0x0048E316`, `0x0048E318`, `0x0048E31A` and `0x0048E31C`. The
selector cases are at `0x00402F49`, `0x0040309B`, `0x00403227`, `0x00403177`
and `0x0040306F`.
