---
id: FND-AI-011
title: When no offer is hired, the AI snubs offer slot 0 in Greed and the least efficient offer elsewhere
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004078B8
tool: Ghidra 12.1.3
environment: null
---

## Observation

When no offer survives the ranking and cash test of `0x004078D9`, selector
`0x8E` of `0x00402D70` chooses an offer slot, and the slot is passed to
`0x004078B8`, which writes `0xFE` into that offer's per-player state byte. The finding does
not give the address of the byte written.

In scenario 0, selector `0x8E` always returns slot 0. The decompiler shows a
loop with no passes here; the emitted instructions return 0 directly. In every
other scenario it returns the first slot with the strictly smallest value of

`Stealth * 20 * positive_sum / (Force + Upkeep + 1)`

computed in integers, starting from a best of 5000. `positive_sum` adds each of
Combat, Defense, Control, Heal, Influence, Research, Strength, Blade, Ranged,
Fighting, Martial Arts and Tech that is above 0; Stealth, Detect and Chaos are
left out. Force here is the gang definition field at +0 of the statistics
block, as in FND-AI-008.

## Interpretation

A computer player that does not hire snubs one offer instead, so its offer row
changes every turn. `0xFE` is -2 as a signed byte, which is the snub order
in `hire_orders` (FND-HIRE-001), so the byte written is most likely that
offer's element of `hire_orders`. Greed always snubs the first offer; the other scenarios snub the
offer with the worst ratio of useful statistics to cost.

## Alternatives

The order of the multiplication and division affects truncation. The finding
gives the expression as written above; the exact instruction order has not
been recorded. A value of 5000 or more never qualifies, so all three offers can
be skipped; what selector `0x8E` returns then (and so which slot is snubbed) is
not recorded.

## How to reproduce

Find the cases of `0x00402D70` for selector `0x8E` and follow the caller that
passes its result to `0x004078B8`, which stores `0xFE`. The Greed test compares
selector 0 with 0.
