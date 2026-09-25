---
id: FND-AI-046
title: Selector 0x5B always counts gangs whose previous action is Chaos, and the branches that test it write Chaos at a low count
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00405ADD..0x00405B4A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040485C..0x00404979
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00428FD9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042A419
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004010E9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00401D7B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040179A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042AAB0
tool: Ghidra 12.1.3
environment: null
---

## Observation

The case for selector `0x5B` of `0x00402D70` starts at `0x00405ADD`. It loops
over the player's 81 roster slots and counts those whose gang sector (gang byte
+2) equals the third argument and whose planning byte +5 (the previous action)
equals the literal 3 (`MOVSX` at `0x00405B32`, `CMP EAX,0x3` at `0x00405B3A`).
The fourth argument is not read. Selector `0x6F` (`0x00405B50`) is the same
count with the literal 9.

The instructions at `0x004048B8` and `0x00404949` belong to the cases of
selectors `0x70` (`0x0040485C`) and `0x71` (`0x004048ED`). Each walks the
player's slots in ascending order and returns the slot of the n-th gang in the
given sector whose previous action is 3 (`0x70`) or 9 (`0x71`), n being the
fourth argument, or -1 when there are fewer.

Every call to selector `0x5B` passes the player, a sector and 0:

| Call | Function | Count tested | Action written at the low count | Otherwise |
|---|---|---|---|---|
| `0x0045939C` | planning pass | above 1 | the duplicate cleanup of FND-AI-019 | nothing |
| `0x00428FD9` | family 0, previous None | 0 | Chaos (3), `0x00428FFB` | Move through mode 5 (`0x00429083`) |
| `0x0042A419` | family 0, previous Heal, Snitch or Move | below 1 | Chaos (3), `0x0042A4F4` | Move through mode 5 (`0x0042A467`) |
| `0x004010E9` | family 4, previous None, Control or Heal | below 1 | Chaos (3), `0x0040110C` | Move through mode 2 (`0x00401194`) |
| `0x00401D7B` | family 4, previous Attack, Snitch or Move, owned sector | below 1 | Chaos (3), `0x00401D9E` | Move through mode 2 (`0x00401E26`) |
| `0x0040179A` | family 4, previous Hide or Equip, owned sector | below 2 | Chaos (3), `0x00401852` | Move through mode 2 (`0x004018DA`) |
| `0x0042AAB0` | family 10 | 0 | Chaos (3), `0x0042AAD2` | Hide (8), `0x0042AB2F` |

At `0x0040179A` the Chaos store is reached through a second owner test
(`0x004017BD`) that repeats the first one; the Control store behind it
(`0x004017F6`) is reached only when the owner differs, which the first test has
already excluded.

## Interpretation

Selector `0x5B` has no action argument: it always counts previous Chaos, as
FND-AI-019 reads it. The addresses FND-AI-019 gives for it are those of
selectors `0x70` and `0x71`. The descriptions of families 0 and 4 in
FND-AI-030 and FND-AI-031 name Hide where the branches write Chaos: those
branches raise Chaos in the sector when no gang of the player (at `0x0040179A`:
at most one) carried out Chaos there the turn before, and move on otherwise. The
count includes the planning gang itself, so a gang that raised Chaos last turn
moves on.

## Alternatives

None known.

## How to reproduce

Find `case 0x5b` in `0x00402D70` and the comparison with the literal 3 that
follows the load from `0x0048A255`. List the calls to `0x00402D70` whose first
pushed argument is `0x5B` and follow each to the byte stored at `0x0048A258`.
