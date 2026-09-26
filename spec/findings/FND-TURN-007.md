---
id: FND-TURN-007
title: The instant-phase switch has six cases at fixed instruction ranges, and the Influence and Research cases take their target from the gang's target byte
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472AD9..0x00473106
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472CFA..0x00472D0B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472D0C..0x00472ED3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472ED4..0x00473045
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004730AD..0x004730D4
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the whole-turn resolver `fn_00472775` (range in FND-EXE-004), the instant
phase is the scan from `0x00472AD9` to `0x00473106`: player slots 0 to 5
(`0x00472AEE`), and for each, roster slots 0 to 80 (`0x00472B10`, compared
with `0x51`). For each slot it copies the 32-byte gang record into a local copy
(`0x00472B47`), skips the slot when the sector byte is 100 (`0x00472B50`),
and otherwise dispatches on the action byte (record offset `0x07`). The
dispatch at `0x00473084..0x004730A6` subtracts 2, sends values above 11 to the
default, and indexes the byte table at `0x004730C9` and the jump table at
`0x004730AD`. After the case, every non-skipped slot's copy is written back to
the gang record at `0x004730D5..0x004730FF`.

| Action | Case | Instructions | Evidence of the case's work |
|---|---|---|---|
| 2 | Bribe | `0x00472B6B..0x00472BF5` | FND-BRIBE-001, FND-TOLERANCE-001 |
| 7 | Heal | `0x00472BF6..0x00472CF9` | FND-HEAL-001 |
| 8 | Hide | `0x00472CFA..0x00472D0B` | below |
| 9 | Influence | `0x00472D0C..0x00472ED3` | FND-TURN-001, below |
| 11 | Research | `0x00472ED4..0x00473045` | FND-RESEARCH-001, below |
| 13 | Snitch | `0x00473046..0x0047307E` | FND-SNITCH-001, FND-TOLERANCE-001 |

Actions 3 to 6, 10 and 12 map to the default target `0x004730D5`, which is the
write-back.

- Hide adds one to the player's dword at `0x004A25D0 + player * 4`
  (`0x00472D00`) and does nothing else: it reads no gang field and makes no
  test. The array's other writers are city generation `fn_00475FE1`, which
  stores 0 for each player (`0x00476029`), and the network packet handler
  `fn_0046BA84`, which replaces each element with the result of
  `fn_00449E26` on it (`0x0046C25C`). The save reader and writer pass the
  array's address to their block copies.
- Influence reads the gang's byte at record offset `0x08` and uses it as the
  site slot of the gang's sector: the site definition byte at sector offset
  `0x07 + slot * 2` (`0x00472D20`) and the progress byte at `0x08 + slot * 2`
  (`0x00472D52`). It writes the new progress to the same byte (`0x00472EBA`)
  and stores 1 in the sector's entry of `0x00498B78` (`0x00472EC8`). The
  roll is skipped when the Resistance equals the progress (`JZ` at
  `0x00472D6C`). The case reads no sector owner byte.
- Research reads the same record byte `0x08` and uses it as the item number:
  the element `item * 6 + player` of `0x004A2608` (`0x00472EE4`), written back
  at `0x0047303A`.

## Interpretation

Each instant action resolves inside its own case with no shared code except
the record copy and write-back, so the ranges above bound what each command
can change. Every resolved Hide counts toward the Hide award total, including
a recurring Hide repeated turn after turn and a Hide by a gang already hidden,
and the count is made in the Hide case itself. The Influence and Research
orders both keep their choice in the gang's `target` byte: a site slot 0 to 2
for Influence, an item record number for Research.

## Alternatives

What `fn_00449E26` does to a value was not read; it is called on each element
of several arrays as they arrive from the network, which suggests a byte-order
or format conversion.

## How to reproduce

In `fn_00472775`, read the jump table at `0x004730AD` (seven dwords) and the
byte table at `0x004730C9` (twelve bytes) behind the `JMP` at `0x004730A6`.
List the references to `0x004A25D0`.
