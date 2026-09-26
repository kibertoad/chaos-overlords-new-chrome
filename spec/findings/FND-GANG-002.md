---
id: FND-GANG-002
title: The gang definition panel is the 320-pixel alternate panel PX05022 with its own field origins
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00455B6B
tool: Ghidra 12.1.3
environment: null
---

## Observation

The definition information handler `fn_00455B6B` loads `DATA/PX08/PX05022`
into the alternate part of the panel buffer. Its closing copy moves the source
rectangle `(344,144)-(664,353)` to the screen rectangle `(128,124)-(448,333)`.
In buffer coordinates:

- the 64-by-64 gang portrait goes to `(370,161)-(434,225)`, which is screen
  `(154,141)-(218,205)`;
- the name is written at `(444,171)`, followed by three description rows of 30
  characters at y = 189, 198 and 207;
- the Force and current-value column starts at x = 516, and Upkeep, Tech Level
  and the right statistics column at x = 612.

On screen these columns are at x = 300 and x = 396, and the statistic rows are
at y = 243, 252, 270, 279, 288, 297 and 306. Keyboard confirmation and the only
pointer exit, the local rectangle `(33,169)-(82,191)`, both close the panel.

## Interpretation

A hire offer's definition is shown in its own panel, not in the live-gang panel
`PX05000`. It has no equipment cells, uses description rows of 30 characters,
and is drawn as the 320-pixel alternate crop, so its fields sit 24 pixels to
the right of the matching fields of the 344-pixel shared panel. Buffer
coordinates map to screen coordinates by subtracting 216 from x and 20 from y.

## Alternatives

Which key is the keyboard confirmation is not named. Which statistic sits on
which row is not listed.

## How to reproduce

Start at `fn_00455B6B`, one of the six callers that open a panel in the
alternate 320-pixel form (FND-OPTIONS-001). Its drawing calls use the buffer
positions above, and its pointer test compares with 33, 169, 82 and 191 after
subtracting the screen origin `(128,124)`.
