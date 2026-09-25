---
id: FND-SAVE-003
title: What the code does with save blocks 3, 4, 16, 17, 18, 21, 27, 36, 37 and 39, and which preference each preference byte holds
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
    address: 0x00482110..0x0048215D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048DB48..0x0048E2DF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494830..0x00494831
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AB650..0x004AB667
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A2600..0x004A2605
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABC58..0x004ABC5D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00459439..0x0045946D
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges of the functions named here are in FND-EXE-004. Block numbers are those
of FND-SAVE-001. Every reference to each global was listed; the save
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
- Block 16, `0x00482110`, six dwords. `fn_0040AA65`, which resets the
  computer players, sets each to 81 (`0x0040AAAA`). The planner `fn_00458FA0`
  sets the player's dword again on every call (`0x00459597..0x00459656`) from
  the value its query function `fn_00402D70` returns for query `0x23`, the
  player's dword at `0x0048E2E0` and the scenario, caps it at 80
  (`0x00459665`), and compares it with the dword at `0x0048E2E0` in its
  branches (`0x004596C1` and eight more).
- Block 17, `0x0048DB48`, one dword per player and roster slot (stride
  `0x144` per player). The only store is 0, by the planner, for a roster slot
  whose gang is gone (`0x00459323`). Query `0x5D` of `fn_00402D70` reads it
  (`0x00405C7B`, `0x00405CB7`): when the gang's Force byte is below it, it
  returns 1 minus 10 times Force divided by it, otherwise 0.
- Block 18, `0x00482158`, six bytes. `fn_0040AA65` clears each; `fn_0040AB20`,
  called from the event function `fn_00462579`, sets the player's byte, sets
  the first byte of each of the player's active gangs' planning records to 9
  and runs the planner. The planner, while the byte is set, writes 9 into the
  same bytes (`0x004594C6`). The planner also tries to set the byte itself,
  when query 2 is below 14 and query `0x31` for the player is nonzero, but
  indexes it with the counter of the loop just before it, which has ended at
  64 (`0x00459464`, `[EBP-0xC]`); the byte written is `0x00482198`, the low
  byte of the fourth entry of a table of doubles at `0x00482180`, and the
  player's own byte is not changed.
- Block 21, `0x00482140`, six dwords. The planner stores 0 into the player's
  dword at 56 places and nothing else stores into it; no instruction reads it.
- Block 27, `0x00494830`, one word. `fn_00439563` loads the image `PX10000`
  plus this value into surface 2 as the 432-by-416 city map
  (`0x00439628..0x00439637`). `fn_004384C0` sets it to 0 (`0x004384E1`); the
  network receive `fn_0046A115` is the only other store (`0x0046A6BC`).
- Block 36, `0x004AB650`, six dwords. `fn_0046DC10` sets each to 0 when the
  preference byte `0x00487850` is 3 (`0x0046DD3B`) and otherwise to 2 plus a
  draw from 1 to 4 (`0x0046DC91`, FND-RNG-006). The resolver `fn_00472775`
  reads it twice. At `0x00473F03` it lowers player A's entry of the
  attitude matrix towards player B by the larger of A's value and an amount
  the resolver has just computed, down to at least -10. At `0x00475762`, where
  it adds one to B's `overthrow_count`, it lowers A's attitude towards B by
  twice A's value, down to at least -10.
- Block 37, `0x004A2600`, six bytes. `fn_0046DC10` sets all six to 1 when
  `0x00487850` is 3 (`0x0046DCBE`) and to 0 otherwise (`0x0046DC45`). No
  instruction reads them.
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
- Blocks 16, 17, 18 and 21 are state of the computer players' planner. Block
  16 is recomputed before it is read, so its saved value does not matter. Block
  17 is always 0 in a running game, so query `0x5D` always returns 0. Block 18
  marks a player whose gangs the planner handles in the mode that writes 9,
  and is set only through `fn_0040AB20`; the planner's own attempt to set it
  is a bug that writes a stray byte instead. Block 21 has no effect.
- Block 27 is the number of the city map picture, 0 in every game this
  executable starts; the source's name for it, the current player, is wrong.
- Block 36 is how strongly each player's attitude towards another drops in
  those two cases.
- Block 37 is written and never used.
- Block 39 marks the slots played by a human at this computer.
- The three preference bytes are the Mentality (`0x00487850`, FND-AI-004),
  the time limit and the objective (`0x00487858`, the scenario FND-SETUP-009
  describes), in that order.

## Alternatives

What queries `0x23`, `0x31` and `0x5D` compute belongs to the computer players'
findings and was not followed here. Whether the stray byte at `0x00482198`
changes anything depends on the code that reads the table at `0x00482180`;
its one reader found, `fn_0045CF05`, has no caller.

## How to reproduce

List the references to each global above. For the stray store, read the
instructions `0x00459439..0x0045946D`: the index loaded at `0x00459464` is the
counter of the loop at `0x00459377`, which runs to 64.
