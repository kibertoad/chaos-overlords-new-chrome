---
id: FND-AI-001
title: The per-gang AI dispatcher stores a family byte and switches on it to fourteen handlers
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00432DA0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048A250..0x0048C0B0
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function `0x00432DA0` is called from `0x00458FA0` at `0x004594DF`. It takes a
player and a roster slot. Both index arrays with a player stride of `0x510` and
a slot stride of `0x10`. The function writes a byte at
`0x0048A250 + player * 0x510 + slot * 0x10` and then switches on that byte:

| Value | Handler |
|---:|---|
| 0 | `0x00428EF0` |
| 1 | `0x00434080` |
| 2 | `0x0041FEF0` |
| 3 | `0x00435BD0` |
| 4 | `0x00401000` |
| 5 | `0x0043A1D0` |
| 6 | `0x00431C60` |
| 7 | `0x00436C70` |
| 9 | `0x004605E0` |
| 10 | `0x0042A6E0` |
| 11 | `0x00420950` |
| 12 | `0x004353A0` |
| 13 | `0x0040ABC0` |
| 14 | `0x00466910` |

The switch has no case for 8. Several handlers write the chosen action and its
two target bytes both into the 16-byte record at `0x0048A250` (player stride
`0x510`, slot stride `0x10`) and into the gang record block near `0x00498DAF`
(player stride `0xA20`, slot stride `0x20`). Handlers split target results by
quotient and remainder of `0x51` (81).

## Interpretation

The byte at offset 0 of each 16-byte planning record is the gang's strategy
family, and each family has its own action handler. The handlers write the
planned action both into the planning record and into the gang record that the
turn resolver reads. Division by 81 decodes a combined
`player * 81 + roster_slot` value into a player and a roster slot.

## Alternatives

The byte could have been a one-turn choice rather than a persistent family. It
is kept across turns: the outer planning pass reads it again, and some handlers
rewrite it to another family (FND-AI-030, FND-AI-033). Value 8 may be unused or
reached through a default path; the default branch has not been recorded.

## How to reproduce

Open `0x00458FA0` (the only normal caller) and follow the call at
`0x004594DF` to `0x00432DA0`. The switch follows the store to
`0x0048A250 + player * 0x510 + slot * 0x10`. Each case address is listed above.
