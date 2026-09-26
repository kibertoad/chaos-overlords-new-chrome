---
id: FND-SAVE-003
title: Save blocks 3, 4, 27 and 39 hold the selected sector, the portrait, the city map picture number and the local-human flags, and the preference bytes are the Mentality, time limit and objective
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABBF0..0x004ABC07
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A5F00..0x004A5F05
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494830..0x00494831
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABC58..0x004ABC5D
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges of the functions named here are in FND-EXE-004. Block numbers are those
of FND-SAVE-001; blocks 16, 17, 18, 21, 36 and 37 are in FND-STATE-003. Every
reference to each global was listed; the save
`fn_00463CC5` and the load `fn_0046381A` are left out below, and so are the
network copies in `fn_0046981D` and `fn_0046A115` where they only send or
receive the value.

- Block 3, `0x004ABBF0`, six dwords. The new-game routine `fn_0046DC10`
  stores in each player's dword the sector of the player's roster slot 0
  (`0x0046E0E7`). Planning `fn_0046FD80` copies the current player's dword into
  the map selection `0x004ABC80` when the player's planning starts
  (`0x0046FE57`) and copies the selection back when it ends (`0x004708F2`);
  `fn_00471F06` also stores into it (`0x00472677`).
- Block 4, `0x004A5F00`, six bytes. Written by the setup screens
  (`fn_0040B9C0`, `fn_0040E0A0`, `fn_0040F72E`) and read, one byte per player,
  by the screens that draw the players, for example `fn_0044FD6C`, which takes
  the value times 32 as the left edge of a 32-pixel cell (`0x00450693`).
- Block 27, `0x00494830`, one word. `fn_00439563` loads the image `PX10000`
  plus this value into surface 2 as the 432-by-416 city map
  (`0x00439628..0x00439637`). `fn_004384C0` sets it to 0 (`0x004384E1`); the
  network receive `fn_0046A115` is the only other store (`0x0046A6BC`).
- Block 39, `0x004ABC58`, six bytes. The local setup `fn_0040E0A0` sets a
  slot's byte to 1 when its controller is 0 (a human at this computer) and to
  0 otherwise (`0x0040EAD8`, `0x0040EAE7`); the network setups `fn_004677F0`
  and `fn_00456F80` and the event function `fn_00462579` also store into it.
  `fn_0041B4EA`, `fn_0045519D`, `fn_0045EAB1` and `fn_00471F06` read it, the
  first and last together with the bytes at `0x004ABC88`.
- Blocks 40 to 42 come from `0x00487850`, `0x00487854` and `0x00487858`, the
  bytes the preference loader `fn_0046439A` fills from the registry values
  FND-OPTIONS-001 lists as `prefsDiff`, `prefsTimeLimit` and
  `prefsObjective`, and which the preference writer `fn_00464783` stores back.

## Interpretation

- Block 3 is each player's selected sector, restored when that player plans
  again.
- Block 4 is each player's portrait number.
- Block 27 is the number of the city map picture, 0 in every game this
  executable starts; the source's name for it, the current player, is wrong.
- Block 39 marks the slots played by a human at this computer.
- The three preference bytes are the Mentality (`0x00487850`, FND-AI-004),
  the time limit and the objective (`0x00487858`, the scenario FND-SETUP-009
  describes), in that order.

## Alternatives

Block 27 could be set to another value by a network peer through
`fn_0046A115`; no build of this executable sends anything but 0, since no
other store exists.

## How to reproduce

List the references to each global above, leaving out the save and load
functions.
