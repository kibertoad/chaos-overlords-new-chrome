---
id: FND-HIRE-002
title: The six-gang limit on a hire counts only the hiring player's gangs in the target sector
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047592B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498DAA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A27C8..0x004A27DA
tool: Ghidra 12.1.3
environment: null
---

## Observation

The hire block of the whole-turn resolver `0x00472775` loops player slot 0 to
5 and offer slot 0 to 2. For an order holding a sector, the count that starts
at `0x0047592B` (and ends at `0x004759A8`) resets a counter and scans 81 gang
records at `0x00498DAA + player * 0xA20 + record * 0x20`, the sector byte of
each record, comparing each with the order byte at
`0x004A27C8 + player * 3 + slot`. The player index in that address is the
hiring player's; there is no second loop over players. The counter reaches the
branch that tests it against 6 only through this scan. When the count is 6 or
more, the block records the sector-capacity report and sets the order to -1.
Otherwise it goes on to the cash test, then the Force draw, then the free-slot
search in the same player's roster. The hire block contains no other test of
how many gangs a sector holds.

## Interpretation

Only the hiring player's own gangs use up the six places in the target
sector. Other players' gangs in that sector do not stop a hire.

## Alternatives

An earlier reading of the same block counted all players' gangs. The address
expression, indexed by the current player alone, rules it out.

## How to reproduce

In `0x00472775`, find the loop that reads the order bytes at `0x004A27C8`
with an inner bound of 3. The instructions from `0x0047592B` to `0x004759A8`
read the sector byte at `0x00498DAA` with stride `0x20` and player stride
`0xA20`, then compare the counter with 6.
