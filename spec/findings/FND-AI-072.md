---
id: FND-AI-072
title: Five attack draws hand the strength test the gang's sector where it expects a roster slot
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042A1E0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00434B32
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00436650
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00401A4F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043AC6A
tool: Ghidra 12.1.3
environment: null
---

## Observation

Selector `0x2B` of the query function `0x00402D70` takes a player, a roster
slot and an ordinal. It reads the sector byte of the player's gang record in
that slot through selector `0x5A`, and asks selector `0x91` for the gang at the
ordinal among the gangs of other players, in player order and then slot order,
whose sector byte equals that sector and whose `visible_to` byte for the player
is nonzero; `0x91` returns -1 when there is none. It then returns whether
`(y.force + y.combat) / 4 - x.defense <= x.force + x.combat - y.defense`, with
`x` the record in the given slot and `y` the one `0x91` returned. For -1 it
indexes the gang table at -1 and reads the 32 bytes before the first record.

Twenty calls push selector `0x2B`. Fifteen pass the handler's slot argument
`[EBP+0xC]` as the slot, as the family-0 call `0x004295EF` does. Five pass a
stack local that holds the gang's sector, read from the record just before the
target draw:

| Call | Handler | Pushed slot | Branch |
|---|---|---|---|
| `0x0042A1E0` | family 0, `0x00428EF0` | `[EBP-0x10]` | previous Heal, Hide or Move, draw `0x0042A1AB` |
| `0x00434B32` | family 1, `0x00434080` | `[EBP-0xC]` | previous Attack, Hide or Move |
| `0x00436650` | family 3, `0x00435BD0` | `[EBP-0xC]` | previous Attack, Hide or Move |
| `0x00401A4F` | family 4, `0x00401000` | `[EBP-0x10]` | previous Attack, Hide or Move, draw `0x00401A1A` |
| `0x0043AC6A` | family 5, `0x0043A1D0` | `[EBP-0xC]` | previous Attack, Hide or Move |

## Interpretation

At these five calls the strength test compares the player's record in the
roster slot numbered like the gang's sector with the gang at the drawn ordinal
among the visible gangs of that record's sector (BUG-AI-007). A sector number
is at most 63 and a player has 81 records, so the read stays inside the
player's table: it finds another of the player's gangs, a gone gang's record
or an unused one, whose sector byte is 100.

## Alternatives

None. EXP-TURN-022 records the call at `0x00436650` with the arguments
`[0x2B, 2, 30, 1]` for a gang in sector 30.

## How to reproduce

Run `ReportCallArguments.java 0x00402D70` and keep the calls that push `0x2B`.
For each, run `ReportInstructionWindow.java` on the ten instructions before
the call and read the load before the second push.
