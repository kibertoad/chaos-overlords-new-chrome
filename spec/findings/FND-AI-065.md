---
id: FND-AI-065
title: When no offer is hired, the AI snubs offer slot 0 in Greed and elsewhere the offer with the smallest Tech Level times positive statistics per cost
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00406753..0x004069D9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004078B8
tool: Python 3.14.7 script disassembling the file bytes with Capstone 5.0.7
environment: null
---

## Observation

When no offer survives the ranking and cash test of `0x004078D9`
(FND-AI-064), selector `0x8E` of `0x00402D70` chooses an offer slot, and the
slot is passed to `0x004078B8`, which writes `0xFE` into that offer's
per-player state byte. The finding does not give the address of the byte
written.

The case starts at `0x00406753` by storing 0 as its result and 0 as a running
best, then compares selector 0 (the scenario at `0x004ABBE8`) with 0.

In scenario 0 the loop at `0x0040676E` copies its counter, which starts at 0,
increments the counter and leaves when the copy is 0, so it leaves before its
first pass and the case returns slot 0.

In every other scenario the loop from `0x004067FE` starts from a best of 5000
and runs over the three offer slots. For each offer's gang definition it adds
Combat (`0x004A287E`) and Defense (`0x004A2880`) when above 0, then each of the
ten fields from `0x004A2888` onward that is above 0: Chaos, Control, Heal,
Influence, Research, Strength, Blade, Range, Fighting and Martial Arts in the
names of FMT-DATA-002. It multiplies Tech Level (`0x004A2882`) by that sum
(`IMUL` at `0x00406963`) and by 20, then divides by the offer's `hire_cost`
plus `upkeep` plus 1 with a signed `IDIV` at `0x004069B7`. A value strictly
below the best (`JLE` at `0x004069C2` skips the others) becomes the best and
its slot the result, so the first of equal values is kept. Stealth and Detect
are not read.

## Interpretation

A computer player that does not hire snubs one offer instead, so its offer row
changes every turn. `0xFE` is -2 as a signed byte, the snub order in
`hire_orders` (FND-HIRE-001), so the byte written is most likely that offer's
element of `hire_orders`. Greed always snubs the first offer. The other
scenarios snub the offer with the least useful statistics per cost, weighted
by Tech Level; when every value is 5000 or more they snub slot 0.

This corrects the field names of FND-AI-011, which named every field after
Defense one slot off (see FND-AI-064): its Stealth multiplier is Tech Level,
and its summed fields are the same twelve, its Control being Chaos and its
Tech being Martial Arts. Tech Level is the multiplier and is not summed.

## Alternatives

None. The multiplication comes before the division, so the value truncates
once, toward zero.

## How to reproduce

Disassemble from `0x00406753`. The scenario test is at `0x00406761`, the Greed
loop at `0x0040676E` to `0x004067F9`, and the scoring loop from `0x004067FE`
to `0x004069D4`, whose inner loop of ten starts at `0x004068D6`. Follow the
caller that passes the result to `0x004078B8`, which stores `0xFE`.
