---
id: FND-HEAL-001
title: The Heal case rolls Heal plus 4 dice without testing Force first, caps Force at 10 and records no report
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472BF6..0x00472CF9
tool: Ghidra 12.1.3
environment: null
---

## Observation

The Heal case of the instant-phase switch in `fn_00472775` is
`0x00472BF6..0x00472CF9` (FND-TURN-007). It works on the local copy of the
gang record.

- It reads the player's dword at `0x004A2570 + player * 4` (`0x00472BFC`) and
  compares it with 0, 1 and 2 (`0x00472C73..0x00472C94`). For 0 and 1 it calls
  the dice helper `fn_00475F70` with the pool `heal + 4` and threshold 5
  (`0x00472C1B`, `0x00472C3B`); for 2 it passes threshold 4 (`0x00472C5B`).
  `heal` is the signed byte at record offset `0x18`. For any other value it
  calls nothing.
- It adds the returned count to the signed Force byte at record offset `0x03`
  in a 32-bit local (`0x00472C9F..0x00472CB2`), replaces the sum with 10 when
  it is greater than 10 (`0x00472CB8..0x00472CC5`), and stores the low byte
  in the Force byte (`0x00472CD5`).
- It stores 1 in the gang's entry of the byte array at `0x00498990 + player *
  0x51 + slot` (`0x00472CED`), which the network session host `fn_0046A7CB`
  reads (`0x0046AFC4`) to decide which gang records to send.
- No instruction of the case reads Force before the dice call, and the case
  makes no call to the report helper `fn_00477748`.

## Interpretation

A healing gang always rolls, even at Force 10, and so always uses its random
draws; the result is then capped. Bands 0 and 1 roll the same pool, and band 2
succeeds on a 4 as well. No Heal line appears in the Last Turn report.

## Alternatives

For a band value other than 0, 1 or 2 the count would be whatever an earlier
case left in the shared local; the band is always 0, 1 or 2 in a match, so
this path is not reached.

## How to reproduce

In `fn_00472775`, follow the jump table entry `0x00472BF6` (case 7). The three
calls to `0x00475F70` push 5, 5 and 4, and the comparison with 10 is at
`0x00472CB8`.
