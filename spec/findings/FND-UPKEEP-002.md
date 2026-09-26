---
id: FND-UPKEEP-002
title: Case 6 of the selector fn_00402D70 returns the sector's cash_yield byte at offset 0x03, but no call passes 6; the computer players read Income through case 7, offset 0x04
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402F1D..0x00402F67
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00409FF7
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_00402D70` (range in FND-EXE-004) dispatches on its first argument. Its
cases 4 to 7 each load one signed byte of the sector record at
`0x004A08E8 + sector * 0x24`, the sector being the second argument, into the
return value:

| Case | Instruction | Byte read |
|---|---|---|
| 4 | `0x00402F1D` | offset `0x05` (`0x004A08ED`) |
| 5 | `0x00402F33` | offset `0x06` (`0x004A08EE`) |
| 6 | `0x00402F5F` | offset `0x03` (`0x004A08EB`) |
| 7 | `0x00402F49` | offset `0x04` (`0x004A08EC`) |

Of the 402 call sites of `fn_00402D70`, one passes a literal 6 or 7 as the
first argument: the call with 7 at `0x00409FF7` in `fn_00409F47`. That function visits the 64 sectors and, for each, stores the
case 7 value as the starting sector value of a per-player table at
`0x0048E316` before adding the results of cases `0x0D`, `0x16`, `0x12` and
`0x0C`. No call site passes a literal 6.

## Interpretation

Case 6 does return `cash_yield`, the byte Upkeep pays (FND-UPKEEP-001), so the
old wording that the selector's case 6 reads offset `0x03` is right as far as
the case goes. The claim that the computer players read that byte as the
sector's Income is dropped: the one direct use of these cases in the computer
players' sector scoring is case 7, which reads `income` at offset `0x04`. Case
6 can be reached only through a computed first argument, which was not
searched for.

## Alternatives

- A call that computes the case number at run time could still reach case 6.
  The search covered only literal first arguments in the decompiled callers.

## How to reproduce

Open `fn_00402D70` and follow its switch to the four short cases that load a
byte from `0x004A08EB..0x004A08EE` with a stride of `0x24`. List the callers of
`fn_00402D70` and search each for a first argument of 6 or 7; only
`fn_00409F47` passes 7.
