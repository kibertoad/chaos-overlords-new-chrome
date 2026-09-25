---
id: FND-UPKEEP-001
title: Upkeep charges each active gang its definition's Upkeep and pays each owned sector's rebuilt Cash byte, from the second turn on
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E766
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004782C5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046F246
tool: Ghidra 12.1.3
environment: null
---

## Observation

In `fn_0046E766`, the function that runs the turn loop:

- The cash update scans players 0 to 5. For each player it first walks the
  player's gang records and, for every active gang, subtracts the Upkeep of the
  gang's definition from the player's cash word in the array at `0x004A25E8`.
  A negative Upkeep adds its magnitude to the player's cash earned at
  `0x004A27E0`; an Upkeep of 0 or more is added to the player's cash spent at
  `0x0049CA78`.
- It then walks the 64 sector records and, for each whose owner byte (offset
  `0x00`) equals the player, adds the signed byte at offset `0x03` of the
  record to the player's cash, once. A value below 1 is subtracted, signed,
  from cash spent; a positive value is added to cash earned. Both
  classifications are made per gang and per sector, before any totals could
  cancel.
- The enclosing loop sets a local flag to 1 before its first pass and skips the
  whole cash update on that first pass. On every pass, before planning, it
  calls `fn_004782C5` for each sector. That helper sets the byte at offset
  `0x03` to 1 and adds the Cash field (offset 8 of the loaded site table entry)
  of each completed site. At the call site `0x0046F246` the whole 36-byte
  result is copied back into the sector table.
- On later passes the cash update runs before the sector records are rebuilt
  again.

Cross-references show that the arrays at `0x004A27E0` and `0x0049CA78` are
the cash earned and cash spent that the game saves and draws, and that their
other writers in the whole-turn resolver are the successful Chaos and Sell
paths (cash earned) and the Bribe, Equip and Hire paths (cash spent).

## Interpretation

What a player collects each Upkeep is the flat tax of 1 for each owned sector
plus the Cash of the completed sites in it, kept together in one byte of the
sector record as `1 + completed-site Cash`. The sector's generated Income is
not what is collected. The first turn of a match has no Upkeep: players plan
their first turn with their starting cash, and Upkeep begins with the second
pass of the loop. The byte collected at an Upkeep is the one rebuilt before the
previous planning phase.

## Alternatives

- An earlier reading of this finding took the byte at offset `0x03` to be the
  sector's Income as well, read by the sector panel's Income row, the Chaos and
  Control passes and case 6 of the AI sector selector `fn_00402D70`. That
  reading was an interpretation, not an observation of those reads. FND-UI-035
  observes the same panel renderer drawing offset `0x04` on the Income row and
  offset `0x03` on the owner-only row under the Cash label, and FND-CHAOS-001
  observes the Chaos pass reading offset `0x04`. The claim about `fn_00402D70`
  case 6 has not been checked against its instructions, and no finding gives
  the byte the Control pass reads.
- "Active" is taken to mean a sector byte other than 100.

## How to reproduce

In `fn_0046E766`, find the player loop that references `0x004A25E8`,
`0x004A27E0` and `0x0049CA78`: its gang loop reads the definition's Upkeep and
its sector loop compares offset `0x00` of each 36-byte record at `0x004A08E8`
with the player before reading offset `0x03`. The flag tested before that loop
starts at 1. At the call site `0x0046F246`, the 36-byte record the helper
built is copied back into the table.
