---
id: FND-AI-054
title: Selector 0x62 caps the gang definition's Tech Level at 5, 8 or 10 by the research level of the gang's sector, whoever owns it
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004051C3..0x00405265
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00405835
tool: Ghidra 12.1.3
environment: null
---

## Observation

The case for selector `0x62` of `0x00402D70` starts at `0x004051C3`. It calls
selector `0x4A` for the player and roster slot, which returns the signed
16-bit value at `0x004A2882 + definition * 0x9C`, `definition` being the gang
byte +1 (case at `0x00405835`). It calls selector `0x5A` for the gang's sector
and reads the signed byte +0x0D of that sector's 36-byte record at
`0x004A08F5 + sector * 0x24`. When that byte is 0 the value is lowered to 5
if it is above 5 (`0x004051F5`); when 1, to 8 if above 8 (`0x0040520B`);
when 2, to 10 if above 10 (`0x00405221`); any other byte leaves it. The case
returns the value (`0x00405265`). It reads no owner byte and no other sector.

## Interpretation

`0x004A2800` is the gang definition table and +0x82 its `tech_level`
(FMT-DATA-002); sector byte +0x0D is `research_level` (FMT-STATE-002). The
Tech ceiling of a computer gang is its definition's Tech Level, capped at 5 in
a sector with no research site, 8 with a level-1 site and 10 with a level-2
site. The sector's owner does not matter to this selector.

## Alternatives

None known.

## How to reproduce

In `0x00402D70`, read the case at `0x004051C3`: the two selector calls, the
load from `0x004A08F5` and the three comparisons with 5, 8 and 10.
