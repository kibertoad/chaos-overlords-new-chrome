---
id: FND-UI-012
title: The completed-Research report copies one 48-by-48 frame of the item's strip into its monitor without centering it
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044FD6C
tool: Ghidra 12.1.3
environment: null
---

## Observation

The completed-Research branch of the report compositor `fn_0044FD6C` loads
`PX06005`, loads the item's `PX04xxx` strip as a 48-by-720 surface, and copies
one 48-by-48 frame into the 48-by-48 monitor aperture. It does not look for the
frame's opaque pixels or centre the item.

## Interpretation

An item whose art is not symmetrical looks off-centre in the Last Turn Events
monitor, as it does in the original.

## Alternatives

- Which frame of the strip the branch copies has not been recorded.

## How to reproduce

In `0x0044FD6C`, find the branch that loads resource 6005 and the copy of a
48-by-48 rectangle from the item strip.
