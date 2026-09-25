---
id: FND-AI-049
title: The family-4 handler, read from its jump table, groups previous Chaos with Equip and Hide with Attack and Move, and chooses the human pool by the sector owner's attitude
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00401000..0x00401EEF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00401EBA..0x00401EE5
tool: Ghidra 12.1.3
environment: null
---

## Observation

`0x00401000` (range in FND-EXE-004) reads the gang's sector and previous action
and jumps through the eleven-entry table at `0x00401EBA` for actions 0 to 10;
larger values skip the switch (`0x00401EA6`). The targets are:

| Previous action | Target |
|---|---|
| 0 None, 4 Control, 7 Heal | `0x00401047` |
| 1 Attack, 8 Hide, 10 Move | `0x00401955` |
| 3 Chaos, 5 Equip | `0x00401232` |
| 2, 6, 9, and 11 to 13 | nothing |

Every Move this handler writes takes its destination from sector selector
`0x00408642` mode 2. The Heal gate, the pool choice, the draws and the
strength test are the same as in family 0 (FND-AI-048): the pool is the
human-only list (selectors `0x28` and `0x29`) when the player's attitude at
`0x004AB590 + player * 0x18 + owner * 4` toward the owner that selector `0x21`
returns is negative and the weight is 10, and the full list (selectors `0xAA`
and `0x91`) otherwise (`0x00401993`, `0x0040128A`).

Previous None, Control or Heal (`0x00401047`): the Heal gate writes Heal (7).
Otherwise selector `0x5B` below 1 writes Chaos (3) (`0x0040110C`) and a
positive count writes Move (`0x00401194`). The branch then writes -1 to aux
+10 (`0x00401216`).

Previous Attack, Hide or Move (`0x00401955`): when the sector's weight
(selector `0xAF`) is not 10 and the player owns the sector, selector `0x5B`
below 1 writes Chaos (`0x00401D9E`) and a positive count writes Move
(`0x00401E26`). When the weight is not 10 and the player does not own it,
Control (4) is written when the previous action and the older action (selector
`0x3F`) are both Move and selector `0x2C` is nonzero (`0x00401C16..0x00401C69`),
and Move otherwise. At weight 10 one draw is made: a passed test writes Attack
with the sector at aux +10; a failed test writes action 0 and -1 in aux +10
and +12 (`0x00401B71..0x00401BCB`).

Previous Chaos or Equip (`0x00401232`): at weight 10, up to five draws
stopping at the first passed test, and Attack on the last target drawn with
the sector at aux +10. Then, when selector `0x6C` is positive and the planned
action is not Attack, the weapon Equip and then the armor Equip of family 0,
with the cooldown set to the cost times 3. Then, when the planned action is
neither Equip nor Attack (`0x00401745`): in a sector the player owns with a
selector-`0x5B` count below 2 (`0x00401785..0x004017A5`), a second owner test
that is again true writes Chaos (`0x00401852`), and the Control store behind it
(`0x004017F6`) is not reached; any other case writes Move (`0x004018DA`).

## Interpretation

This corrects FND-AI-031: its branch for previous Attack, Snitch or Move is
taken after previous Attack, Hide or Move, its branch for previous Hide or
Equip is taken after previous Chaos or Equip, and where it says Hide the
branches write Chaos. Family 4 raises Chaos in its own sectors unless another
of its gangs did so last turn, attacks visible hostile human gangs it can
beat, and otherwise moves through mode 2; it takes a foreign sector by Control
only after two moves in a row. The target pool depends on the owner's
attitude, as for families 0 and 3, with the same out-of-row reads for a
neutral sector or one under police presence.

## Alternatives

None known.

## How to reproduce

In `0x00401000`, read the bounds test at `0x00401EA6` and the table at
`0x00401EBA`. Follow each target to its byte stores to `0x0048A258`.
