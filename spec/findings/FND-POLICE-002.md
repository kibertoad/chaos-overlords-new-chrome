---
id: FND-POLICE-002
title: A Crackdown report goes to every player who had a gang in the sector when resolution began, and a control-loss report to the displaced owner
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00477748
tool: Ghidra 12.1.3
environment: null
---

## Observation

- At its start, before the instant actions and the Chaos rolls, the whole-turn
  resolver `0x00472775` clears a local table of 64 sectors by six players. It
  scans all 81 roster slots of each player and sets the byte for a sector and
  player when a slot's sector equals that sector. An inactive slot holds the
  sector value 100, which matches no sector.
- After the Chaos rolls, the sector pass visits sectors in ascending order.
  For a sector whose total exceeds its Tolerance, it clears the participating
  gangs' Chaos results and calls the report recorder `0x00477748` with report
  type 1, once for each player whose byte is set in that table.
- When both Crackdown slots are already in use (FND-POLICE-001), the same pass
  then calls the recorder with report type 3 for the sector's previous owner,
  before clearing the ownership. That call does not require the owner to have a
  gang in the sector.

## Interpretation

The Crackdown report reaches every player who had a gang in the sector when
resolution began, whether or not that player ordered Chaos there, and no player
without one. The control-loss report reaches the displaced owner on the third
Crackdown in the window, after that sector's Crackdown reports. The report
table keeps the first 32 reports per player (FND-EVENT-001).

## Alternatives

None known. The order in which the type-1 calls visit the players, and whether
the type-3 call is made when the sector has no owner, were not recorded.

## How to reproduce

In `0x00472775`, find the nested loops at the start that clear and fill a
local 64 by 6 byte table, then the calls to `0x00477748` with first arguments
1 and 3 in the sector pass.
