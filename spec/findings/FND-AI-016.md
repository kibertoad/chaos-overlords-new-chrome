---
id: FND-AI-016
title: The AI hire destination helper returns an encoded sector directly and has two unused random modes
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408214
tool: Ghidra 12.1.3
environment: null
---

## Observation

`0x00408214` takes a player, a mode and an offer slot.

- A mode of `0x40` or more writes and returns sector `mode - 0x40`, with no
  check and no random draw.
- Mode 1 builds a list in this order: every sector from 0 to 63 that the player
  owns, once each, then, for every active roster slot from 0 to 80, that gang's
  sector. Repeated sectors stay repeated. One call of the bounded wrapper
  `0x0045D227` picks a one-based position in the list. An empty list still calls
  the wrapper, whose bound is raised to 1, and the destination becomes -1.
- Mode 0 first scans the occupied gang records for the smallest and the largest
  sector and counts the gangs in each. When the offer slot is not negative and
  either extreme holds fewer than six gangs, it makes one wrapper call with
  bound 2, picks that extreme, and takes the other one when the pick is full.
  Execution then falls through into mode 1, which overwrites the result. When
  both extremes are full or the offer slot is negative, the preliminary draw is
  skipped.
- Modes 2 to 63 return 99 without writing a destination or drawing.

## Interpretation

The helper writes the sector a hired gang will be placed in. The mode 0 choice
between the extremes moves the random generator on but never changes the
destination, because mode 1 always runs after it. The normal planner calls use
the encoded mode only (FND-AI-017), so modes 0 and 1 are not reached in
ordinary play.

## Alternatives

The mode 0 fall-through could be a deliberate two-step choice; the observation
is that the extreme choice is overwritten in every path, so it has no effect
beyond the draw. Where the written destination is stored (which array) is not
recorded.

## How to reproduce

Find the references to `0x00408214`: all 18 direct references are inside
`0x00458FA0` (FND-AI-017). In the function, the comparison of the mode with
`0x40` comes first; the wrapper `0x0045D227` is called in the mode 0 block and
in the mode 1 block.
