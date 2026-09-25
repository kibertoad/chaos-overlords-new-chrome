---
id: FND-AI-059
title: The family-6 handler has no equipment gate, its guard target list ends in sector 100, and a gang covers a sector for itself
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00431C60..0x004327BC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00404660..0x004047E9
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 6's handler `0x00431C60` has no jump table. Reading it through adds
these details to FND-AI-029:

- There is no selector `0x6C` call in the handler. The weapon step
  (`0x00431EAA..0x00431FD3`) needs selector `0x65` below 1, selector `0x61`
  not negative, the item different from selector `0x39`'s, and its cost at
  most the player's cash; the armor step does the same with `0x66`, `0x64` and
  `0x3A`. Neither tests the previous action.
- The first draw and every draw of the later loop choose the pool the same
  way: the human pool when the attitude toward the owner query is negative and
  the weight is 10, the full pool otherwise.
- The unreachable Heal branch (`0x0043214C..0x004321F6`) needs Force below 8,
  effective Heal at least -3, a previous action other than 1 (Attack), and a
  cached weight of exactly 0. The branch after it takes Control when the
  cached weight is below 1 and selector `0x2C` returns 1, and otherwise runs
  the same guard-target Move as a gang at weight below 1.

Selector `0x60` (`0x0040476F`) calls selector `0x9A` with ordinal 1 first.
Selector `0x9A` returns the ordinal-th sector of cached weight 10 in ascending
order, or 100 past the end of the list. When the first call gives 100,
selector `0x60` returns -1. Otherwise it walks ordinals from 1 and returns the
first result that selector `0x5F` reports as uncovered, and that walk includes
the 100 past the end.

Selector `0x5F` (`0x00404660`) walks all 81 slots of the player from 0 and
skips a slot whose sector byte is 100 or whose family (selector `0x59`) is not
6. It does not skip the planning gang.

## Interpretation

The three open questions of RULE-AI-025 are settled: the equipment step has no
danger gate, the later draws use the first draw's pool, and `covered_by` counts
the planning gang itself. A gang that covered a weight-10 sector last turn
therefore sees that sector as taken and picks the next one.

When every weight-10 sector is covered, selector `0x5F` finds no active gang
in sector 100, so selector `0x60` returns 100. The handler then passes mode
`0xA4` to the sector selector and stores 100 as the coverage sector until the
step overwrites it. No sector matches the mode, every score is 0, and the
sector selector's tie draw among all 64 sectors (RULE-AI-006) picks a random
destination. Mode 2 is used only when no sector has weight 10.

## Alternatives

A family-6 gang whose stored coverage sector is 100 and whose focus is -1
would make selector `0x5F` report sector 100 as covered. The handler
overwrites the coverage sector with the step in the same branch, so no stored
value of 100 was found.

## How to reproduce

Open `0x00431C60` and follow the selector calls in address order; there is no
`0x6C` among them. Open the selector function `0x00402D70` at cases `0x5F`
(`0x00404660`), `0x60` (`0x0040476F`) and `0x9A` (`0x004069DE`), and read the
compare with 100 at `0x00404795` and the return of the walked value at
`0x004047D6`.
