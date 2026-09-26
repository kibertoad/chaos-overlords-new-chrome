---
id: FND-OBJECTIVE-001
title: The Player Rankings panel draws a portrait on a fixed rail per player, at a height set by the standing, and none for an eliminated player
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004518D9
tool: Ghidra 12.1.3
environment: null
---

## Observation

The handler of the Player Rankings panel `DATA/PX16/PX05011`, at `0x004518D9`,
visits the six player slots and copies a portrait only for a slot whose
standing byte is not -1 (`0xFF`). The portraits' destinations use the
panel-local x values 98, 138, 178, 218, 258 and 298 for slots 0 to 5, a
32-by-32 rectangle, and a vertical position set by the standing. The handler
reads the scenario score and standing arrays directly (FND-AI-005).

## Interpretation

Each player has a fixed vertical rail in the panel, and the higher the
player's standing, the higher the portrait sits. Tied players share a height.
Eliminated players have no portrait.

## Alternatives

The formula for the vertical position (its origin and the step per standing)
is not recorded, nor whether the x values are the portraits' left edges or
their centres. The panel's own position on screen is not stated here.

## How to reproduce

Find the load of resource 5011 through `0x00464108` and its handler
`0x004518D9`. Find the loop over six slots that tests the standing byte at
`0x004ABC08 + player` against `0xFF`, and the table or arithmetic giving 98
plus 40 per slot.
