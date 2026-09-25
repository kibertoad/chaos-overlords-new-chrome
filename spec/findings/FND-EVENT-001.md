---
id: FND-EVENT-001
title: The Last Turn reports are a keep-first table of 32 ten-byte records per player, cleared before each resolution and filled only by the resolver
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AAE08..0x004AB588
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABCA8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00477748
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004726C0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044F2FC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044FD6C
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The report table starts at `0x004AAE08`. It holds six players at a stride of
  `0x140` bytes, each with exactly 32 records of 10 bytes. A record holds a
  one-byte occupied flag, a two-byte report type and three two-byte arguments.
  The offsets of these fields inside the record were not recorded.
- The per-player report count starts at `0x004ABCA8`.
- The recorder `fn_00477748` writes the new record at the index given by the
  player's count. When the count is already 32 it returns at once, without
  moving or overwriting any record.
- The wrapper `fn_004726C0` clears the occupied byte of every record of every
  player, then enters the whole-turn resolver `fn_00472775`. The resolver sets
  all six counts to 0 before it records anything.
- All 12 direct calls to `fn_00477748` are inside `fn_00472775`.
- The Last Turn Events handler `fn_0044F2FC` scans the 32 occupied bytes of the
  requested player in ascending record order. It reads no other copy of older
  reports.
- The compositor `fn_0044FD6C` has a switch with cases 0 to 9. Its labels are
  the executable's string resources 33 to 44. From the recorder's callers and
  the compositor, the types are:

| Type | Recorded when | Recipients |
|---|---|---|
| 0 | Never; the compositor's empty case | None |
| 1 | A Crackdown is created in a sector | Each player who had a gang in the sector when resolution began |
| 2 | A player takes control of a sector | The new owner |
| 3 | A player loses control of a sector | The previous owner |
| 4 | An Influence completes a site | The influencing player |
| 5 | Research completes an item | The researching player |
| 6 | Bribe, Equip or Hire fails for lack of cash, with an argument of 1, 2 or 4 telling which | The player whose order failed |
| 7 | A Hire fails because the sector is full | The hiring player |
| 8 | A Hire fails because the player has the most gangs allowed | The hiring player |
| 9 | A player is eliminated | All six player slots |

- No other report type exists. Nothing records a report for an evaded target,
  an unavailable item, a failed move or any other refused order.

## Interpretation

The Last Turn Events panel shows only what the resolution that has just ended
recorded for the player. The table keeps the first 32 reports of a resolution
and drops every later one; it is not a ring buffer.

## Alternatives

- The argument that tells the three cash failures apart might be one of the
  three general arguments or part of the type field. The observation places it
  as an argument, but which one is not recorded.
- Which argument values each of the 12 call sites passes was not recorded.

## How to reproduce

Open `fn_00477748` and follow the comparison of the count at `0x004ABCA8` with
32. List its callers: all 12 lie in `fn_00472775`. Open `fn_004726C0`, the
function that calls `fn_00472775`, for the loop over the occupied bytes. Open
the compositor `fn_0044FD6C`, reached from `fn_0044F2FC`, and read its switch
and the string IDs 33 to 44 it loads.
