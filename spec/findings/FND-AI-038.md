---
id: FND-AI-038
title: The family-12 handler equips and heals when unopposed and wanders at random, and attacks when opposed
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
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 12's handler `0x004353A0` starts from the cached opponent weight of the
current sector (selector `0xAF`).

With no visible opponent it tries selector `0x61`'s weapon, selector `0x64`'s
armor and selector `0x74`'s greatest-Chaos miscellaneous item, in that order.
The weapon and armor must differ from the equipped item, have a cooldown at
most 0, and cost at most the player's cash; a successful weapon or armor Equip
writes a cooldown equal to the item's cost (not three times it). The
miscellaneous item uses the same cash test and writes no cooldown. Then Heal
when Force is below 10 and effective Heal is at least -3. Otherwise it passes
the current sector plus `0x40` to the sector selector and writes Move. That
mode scores the current sector, but the selector then removes the current
sector's score, so the best score is 0: it draws among all 64 tied sectors and
routes one step toward the drawn sector.

With a visible opponent it makes up to five target draws: in a sector owned by
a hostile human with weight 10, from the visible gangs of human players;
otherwise from every visible opponent. Selector `0x2B` compares the gang with
the same ordinal in the full list. A passing comparison stops early; after
five failures the last drawn target is still attacked. In scenario 0 with
fewer than four turns remaining, Terminate replaces the action. There is no
three-Move family change.

## Interpretation

Family 12 is a Siege skirmisher that wanders at random when nothing is in
sight.

## Alternatives

When the current sector has a hostile human owner but every visible opponent
there belongs to a computer player, the human-only list is empty. What the
original does then is not recorded: the bounded wrapper raises a bound of 0 to
1 and returns 1, so the draw would pick an ordinal past the end of an empty
list.

## How to reproduce

Open `0x004353A0`; the store of the raw cost as cooldown, the encoded
`sector + 0x40` passed to `0x00408642`, and the draw loop counted to 5.

This evidence was recorded in the older notes under the endgame awards
heading; it concerns only the AI.
