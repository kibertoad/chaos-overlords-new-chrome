---
id: FND-STATE-005
title: Each resolution sets byte 9 of all 486 combat records to -1 and rewrites bytes 0 to 8 only for gangs that took part in a fight
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004740B7..0x00474243
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004742EC..0x00474557
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042E1A5
tool: Ghidra 12.1.3
environment: null
---

## Observation

Records are at `0x004A11E8 + 10 * (player * 81 + slot)`. All writes to
bytes 0 to 9 found by reference search are listed here; FND-EXE-004 gives the
ranges of the functions.

In the whole-turn resolver `fn_00472775`:

- The police loop (`0x004740B7..0x00474243`) visits every player slot and
  every roster slot, living or not, and first stores -1 in byte 9
  (`0x0047413C`). When the gang is in a sector with police and the police hit
  it, it stores the police damage in byte 9 (`0x00474224`) and flags the gang
  as having fought.
- The bookkeeping loop (`0x004742EC..0x00474557`) visits every record again
  and writes bytes 0 to 8 only for a gang flagged as having fought (as
  attacker, as attack target, or hit by police; the flag array is set at
  `0x0047391C`, `0x00473930` and `0x004741B1`):

| Byte | Instruction | Value |
|---|---|---|
| 0 | `0x004743EE` | gang byte 1, the definition |
| 1 | `0x0047441D` | gang byte 3, Force before the damage is applied |
| 2 | `0x0047446C` | gang byte 3 minus the gang's total damage this phase, capped at 10 |
| 4 | `0x00474528` | the damage of the gang's own attack, from an array that the attack loop sets to -1 for each attacker before resolving it (`0x0047396D`) |
| 5 | `0x00474557` | the retaliation damage the gang took, from an array the attack loop sets to 0 for each attacker (`0x00473990`) |
| 6 | `0x0047449B` | gang byte 4, weapon |
| 7 | `0x004744CA` | gang byte 5, armor |
| 8 | `0x004744F9` | gang byte 6, misc |

- Byte 3 is written only by Detailed Combat `fn_0042E040` (`0x0042E1A5`),
  which copies byte 1 into it. The same function also moves whole records
  (DWORD, DWORD, WORD copies at `0x0042E968..0x0042EDF0`).
- The stored values are single bytes taken from the low byte of the source.

## Interpretation

Byte 9 is cleared for every record each resolution, so a gang the police did
not hit always shows -1 there. Bytes 0 to 8 are written only for gangs that
fought in the resolution; any other record keeps the bytes 0 to 8 it had
from the last resolution in which that roster slot fought, including a slot
whose gang has since died or been replaced by a hire. The records are saved
with the match (FND-SAVE-001, block 26).

## Alternatives

- Byte 2 is computed before the damage application stores the new Force; the
  cap at 10 is the clamp of the per-gang damage total at `0x004742B1..
  0x004742D7`. Whether any reader shows bytes 0 to 8 of a record whose gang did
  not fight depends on the Detailed Combat selection, which is not read here.

## How to reproduce

List the references to `0x004A11E8..0x004A11F1`. In `0x00472775`, read the
loop at `0x004740B7` and the loop at `0x004742EC`, and the conditions on the
flag array before each store.
