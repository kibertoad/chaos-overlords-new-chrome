---
id: FND-AI-036
title: The family-9 handler equips without a cooldown check, then leaves owned land or fights and takes enemy land
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004605E0
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 9's handler `0x004605E0` first tries selector `0x61`'s weapon and then
selector `0x64`'s armor. Each must differ from the equipped item and be
affordable. It does not read the existing cooldown (selectors `0x65` and
`0x66` are not called); a successful Equip overwrites the matching cooldown
with three times the item's cost.

With neither upgrade, a gang in a sector its player owns always writes Move
through mode 3. In a sector not owned, a cached opponent weight of 10 enters a
loop of up to five target draws with the same list choice, full-list
comparison, early exit on success and Attack on the last target after five
failures as family 12 (FND-AI-038). When the weight is not 10, a previous
Control writes Move through mode 3 and every other previous action writes
Control. The handler has no Heal, no miscellaneous Equip and no scenario 0
override.

## Interpretation

Family 9 is a raider: it leaves its own land for other players' land and takes
it by Control, fighting visible hostile humans on the way.

## Alternatives

None known.

## How to reproduce

Open `0x004605E0`; its two mode 3 calls are listed in FND-AI-028.

This evidence was recorded in the older notes under the endgame awards
heading; it concerns only the AI.
