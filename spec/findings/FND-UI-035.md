---
id: FND-UI-035
title: The sector panel shows Income from byte 4 and an owner-only Cash from byte 3 of the sector record
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004120EF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004782C5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E766
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The field renderer `fn_004120EF` of the city and detailed-sector screens
  reads the 36-byte sector record at `0x004A08E8 + sector * 0x24`. It shows
  offset 4 (`0x004A08EC` for sector 0) on the Income row, offset 5 on the
  Tolerance row, and offset 6 on the Support row only when the selected sector's
  owner is the active player. Its last row reads offset 3 (`0x004A08EB`) under
  the Cash label baked into the art, again giving 0 for a player who does not
  own the sector.
- The sector refresh before planning, `fn_004782C5`, leaves offset 4 unchanged,
  sets offset 3 to 1, and adds the Cash of each completed site.
- The Upkeep scan in `fn_0046E766` adds the signed byte at offset 3 once to the
  owning player's cash and statistics.
- The Chaos pass of the resolver reads offset 4 for its dice.

## Interpretation

Income and Cash are separate fields. Income is the sector's generated value of 3
to 7, used by Chaos. Cash is the `1 + completed-site Cash` the owner collects,
and only the owner sees it. The panel has no row for sector Chaos.

## Alternatives

- The sentence "only when the owner is the active player" is read as applying to
  Support and Cash; whether Income and Tolerance are also hidden from other
  players has not been checked.
- FND-UPKEEP-001 reads offset 3 as Income and says the Chaos and Control passes
  read offset 3. The Chaos read at offset 4 here, and the Control read, need
  their instruction addresses recorded.

## How to reproduce

In `0x004120EF`, find the reads of `0x004A08EB`, `0x004A08EC`, `0x004A08ED` and
`0x004A08EE` indexed by the sector times `0x24`, and the compare of the owner
with the active player at `0x004ABC84`. In `0x004782C5`, find the store of 1 at
offset 3.
