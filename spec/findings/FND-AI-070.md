---
id: FND-AI-070
title: The family-12 handler equips and heals when unopposed, otherwise moves toward the sector of the player's first gang, and attacks when opposed
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004353A0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00435A34..0x00435A8B
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 12's handler `0x004353A0` starts from the cached opponent weight of the
current sector (selector `0xAF`).

With no visible opponent it tries selector `0x61`'s weapon, selector `0x64`'s
armor and selector `0x74`'s greatest-Detect miscellaneous item (FND-AI-055), in
that order. The weapon and armor must differ from the equipped item, have a
cooldown at most 0, and cost at most the player's cash; a successful weapon or
armor Equip writes a cooldown equal to the item's cost (not three times it).
The miscellaneous item uses the same cash test and writes no cooldown. Then
Heal when Force is below 10 and effective Heal is at least -3.

Otherwise it writes Move (10) to the acting gang's planning record
(`0x00435A46`) and gang record (`0x00435A60`), then calls the sector selector
`0x00408642` at `0x00435A86` with three arguments: the player (`[EBP+0x8]`),
a mode, and the acting slot (`[EBP+0xC]`). The mode is the result of
selector `0x5A` called at `0x00435A76` with the player, 0 and 0, plus `0x40`
(`0x00435A7E`). Selector `0x5A` gives the sector byte of a gang record, and
here it is asked for roster slot 0, not for the acting slot. The selector
itself reads the acting slot's sector through selector `0x5A` with its third
argument (`0x00408A6C..0x00408A78`) and searches from there. The result is
stored as the planned target at `0x00435AA0`.

With a visible opponent it makes up to five target draws: in a sector owned by
a hostile human with weight 10, from the visible gangs of human players;
otherwise from every visible opponent. Selector `0x2B` compares the gang with
the same ordinal in the full list. A passing comparison stops early; after
five failures the last drawn target is still attacked. In scenario 0 with
fewer than four turns remaining, Terminate replaces the action. There is no
three-Move family change.

## Interpretation

An unopposed family-12 gang heads for the sector of its player's roster-slot-0
gang. The encoded mode adds to that sector's score for every sector the ring
visits, so the search stops at radius 1 with that sector as the only scored
one, and the gang routes one step toward it (RULE-AI-006). A gang that stands
in that sector itself has its score removed with the source sector's, so every
pair ties at 0 and the selector draws among them. When roster slot 0 is empty,
its sector byte is 100 and the mode is the guard end marker `0x40 + 100`.

In Eliminate the first gang is the Right Hands, so the family gathers around
it. EXP-TURN-013's original dump at call 655 of `roll` shows player 1's slot 1
on sector 0 calling the selector with mode 73 (sector 9, where slot 0
stands), and sector 9 as the only scored pair.

## Alternatives

When the current sector has a hostile human owner but every visible opponent
there belongs to a computer player, the human-only list is empty. What the
original does then is not recorded: the bounded wrapper raises a bound of 0 to
1 and returns 1, so the draw would pick an ordinal past the end of an empty
list.

## How to reproduce

Disassemble `0x00435A34..0x00435A8B`: the stores of 10 at `0x0048A258` and
`0x00498DAF`, the pushes `0x5A`, the player, 0 and 0 before `0x00402D70`, the
`ADD EAX,0x40`, and the three pushes before `0x00408642`, the last of them the
acting slot. Compare with the selector's own `0x5A` call at `0x00408A6C`.
FND-AI-038 recorded the same handler with the acting gang's sector in the
mode.
