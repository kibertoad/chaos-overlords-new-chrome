---
id: FND-AI-024
title: The family-11 handler equips, heals, attacks the first visible local gang, or moves in blocks of six
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00420950
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 11's handler is `0x00420950`. The decompiler drops the assignments after
its entry calls, so this was read from the instructions: selector `0x5A` (the
gang's current sector) is saved at stack local -4, and selector `0x61` at -8.

Selector `0x61` picks a weapon upgrade. Its helper selector `0x6D` admits only
an item of the requested weapon class whose Tech is at most the local research
ceiling of selector `0x62`, which the player has finished researching, and
whose cost is at most the player's cash. For the melee, blade and ranged
classes it takes the first eligible item's Combat bonus and adds the matching
effective skills: Strength for melee, Strength + Blade for blade, Ranged for
ranged; bare hands score Strength + Fighting + Martial Arts. It then scans all
64 items of the winning class and keeps only a strictly greater Combat bonus,
under the same research and cash tests, but this second pass compares the
item's Tech with the gang's own Tech (from its definition) instead of selector
`0x62`. Ties between class scores go to ranged, then melee, then blade, then
bare hands. It returns -1 when bare hands win, when no item beats the
baseline, or when the result is the weapon already equipped.

Selector `0x65` reads the planning record's +12 value. The handler takes the
selector-`0x61` weapon only when that value is at most 0, the item is
affordable and the previous action is not 1 (Attack). It then writes Equip,
the item number, and a cooldown of three times the item's cost. The same kind
of opportunity follows for armor and for the miscellaneous slot, through
selectors `0x64` and `0x74`, before a Heal gate.

After those, the handler reads the owner of the current sector. In a sector
the active player owns it always writes Move and calls sector selector mode 10
at `0x00420E7B`; the write at `0x00420EEA` leaves the first auxiliary value
equal to the current sector.

In any other sector, selector `0xAC(player, sector, 0)` scans the other
players in ascending slot order and their gangs in ascending slot order, and
returns the first `player * 81 + slot` whose sector is the current sector,
whose visibility byte for the observer is nonzero, and whose gang record byte
just before the sector byte is 0. When the result is not negative the handler
writes Attack against that player and slot. Otherwise selector `0x76` decides
leadership: counting only family-11 records in ascending slot order, including
inactive slots whose family byte is still 11, the records with ordinals 0, 6,
12 and so on are leaders. A leader writes Move with mode 10 at `0x00421085`
and stores the chosen destination in its first auxiliary value. Any other
family-11 gang writes Move with mode 16 at `0x00421157` and keeps its first
auxiliary value at the current sector. Selector `0x77` finds the leader of the
gang's block and returns its stored sector, which mode 16 scores +1.

## Interpretation

Family 11 is the Siege formation: it arms up, heals, fights anything visible in
the sector it stands in, and otherwise moves in groups of six behind a leader
toward human-owned land (or any other player's land when no human plays). The
Heal gate's thresholds are not recorded here.

## Alternatives

The gang record byte just before the sector byte (+1) is the gang's definition
in FMT-STATE-001; selector `0xAC` requires it to be 0, which reads as a test
for the Right Hands (definition 0), but the old notes call it a "state" byte
and its meaning is not settled. The Heal gate and the armor and miscellaneous
selectors' tests are not written out for this handler.

## How to reproduce

Open `0x00420950` in the disassembly and follow the stores to the stack locals
-4 and -8 after its first two selector calls. The mode 10 calls are at
`0x00420E7B` and `0x00421085`, the mode 16 call at `0x00421157`. The selector
cases `0x61`, `0x6D`, `0x62`, `0xAC`, `0x76` and `0x77` are in `0x00402D70`.
