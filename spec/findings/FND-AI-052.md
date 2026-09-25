---
id: FND-AI-052
title: The owner query returns -2 under police presence, and the solo Control test compares Force plus Control with Income, Support and the visible foreign gangs
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00403505..0x0040353A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00403AE0..0x00403D0B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00406B7F..0x00406C56
tool: Ghidra 12.1.3
environment: null
---

## Observation

All three are cases of `0x00402D70`. The sector record is the 36-byte record
at `0x004A08E8 + sector * 0x24` and the gang record the 32-byte record at
`0x00498DA8 + player * 0xA20 + slot * 0x20`.

Selector `0x21` (player argument unused, sector in the second argument) loads
the signed byte +0x0F of the sector (`0x0040350D`). When it is nonzero it
returns -2 (`0x0040351D`); otherwise it returns the signed owner byte +0
(`0x0040352F`). It makes no other test.

Selector `0x2C` takes a player, a roster slot and a sector. It returns 0 when
the sector's owner byte is -2 or -3, or its byte +0x0F is nonzero. When the
owner byte is -1 it returns 1 when the sum of the sector's signed bytes +4 and
+6 is less than the sum of the gang's signed bytes +0x17 and +0x03, and 0
otherwise. When the owner byte equals the player it returns 0. Otherwise it
starts from the same sector sum and adds bytes +0x03 and +0x17 of every gang
that selector `0x91` lists for the player and the sector, taking ordinals 1,
2, 3 and so on until the selector returns -1, and returns 1 when that total is
less than the gang's sum (`0x00403CEE`) and 0 otherwise.

Selector `0x91` (player, sector, ordinal) walks the other five players in slot
order and their 81 roster slots in order, counting the gangs whose sector byte
+2 equals the sector and whose visibility byte `+0x0C + player` is nonzero,
and returns `player * 81 + slot` of the gang at the given ordinal, or -1.

## Interpretation

Byte +0x0F is `crackdown_turns` and bytes +4 and +6 are `income` and
`support` (FMT-STATE-002); gang bytes +0x03 and +0x17 are Force and Control.
The owner query hides the owner of a sector under police presence behind -2;
every caller that uses its result as a column of `attitude` reads outside the
player's row for such a sector and for a neutral one (FND-AI-048).

The solo Control test is the Control strength comparison against the sector's
Income and Support plus every visible foreign gang in it, including gangs of
players other than the owner. It rejects a sector under police presence and
the player's own sector. The owner values -2 and -3 of the first test are not
written to a sector's owner byte by any rule recorded in the spec, so that
part of the test has no effect that is known.

## Alternatives

None known.

## How to reproduce

In `0x00402D70`, read the cases at `0x0040350D` (selector `0x21`), `0x00403AE0`
(selector `0x2C`) and `0x00406B7F` (selector `0x91`), following the loads from
`0x004A08E8`, `0x004A08EC`, `0x004A08EE`, `0x004A08F7`, `0x00498DAB`,
`0x00498DB4` and `0x00498DBF`.
