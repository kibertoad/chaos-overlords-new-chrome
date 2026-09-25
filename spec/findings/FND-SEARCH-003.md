---
id: FND-SEARCH-003
title: The city draws a marker for each site the viewer controls and for each other site whose definition the viewer's Search filter selects
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004123CC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00412990
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00412AC4
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The only code that reads the Search filter table outside the Search handler
  is at `0x00412990`, inside the city redraw `fn_004123CC`.
- It walks the three site slots of each sector. A site controlled by the
  active player is always drawn. Any other site is drawn only when the active
  player's filter byte for its definition is set.
- The drawn sites of a sector get ordinals 0, 1 and 2 in slot order, skipping
  the sites that are not drawn, and each is passed to the marker renderer
  `fn_00412AC4`.
- The renderer copies a 20-by-14 rectangle from `PX00150` with its white
  background left out. The source x is `(definition % 11) * 20`, and the source
  y is `(definition / 11) * 14`, plus 28 when the site is not controlled. The
  destination in the 432-by-416 city buffer is x `(sector % 8) * 53 + 9` and
  y `(sector / 8) * 51 + ordinal * 15 + 7`.
- The sheet's pure white cell background is the transparent colour; it is not
  part of the white and grey controlled-site icons.

## Interpretation

Search reveals individual sites on the city map; it does not highlight
sectors. With an empty filter the player sees only the sites they control,
drawn with the white and grey icons. Selecting site types adds the other sites
of those types, drawn with the amber icons.

## Alternatives

- The test the renderer uses for "controlled by the active player" was not
  recorded. The reading that it means a completed site in a sector the active
  player owns agrees with the glossary's `site` entry but is not shown here.

## How to reproduce

Find the reads of `0x004A24E8` (the Search table of FND-SEARCH-001); the one
outside `fn_00448E32` is at `0x00412990` in `fn_004123CC`. Follow its call to
`fn_00412AC4` and read the source and destination arithmetic with 11, 20, 14,
28, 53, 51 and 15.
