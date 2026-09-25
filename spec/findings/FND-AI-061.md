---
id: FND-AI-061
title: The family-11 handler's miscellaneous Equip and Heal also need a previous action other than Attack, and most branches keep the current sector as focus
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00420950..0x004211D7
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 11's handler `0x00420950` has no jump table. It saves the current sector
(selector `0x5A`) at stack local -4 and runs these tests in order; each
writing branch returns:

1. Weapon: selector `0x65` below 1, selector `0x61` not negative, the item
   different from selector `0x39`'s, its cost at most the player's cash, and a
   previous action (selector `0x3E`) other than 1. Writes Equip and a weapon
   cooldown of three times the cost.
2. Armor (`0x00420AEB..0x00420C3E`): the same tests through selectors `0x66`,
   `0x64` and `0x3A`, and an armor cooldown of three times the cost.
3. Miscellaneous (`0x00420C5A..0x00420D5E`): selector `0x74` not negative, the
   item different from selector `0x3B`'s, its cost at most the player's cash,
   and a previous action other than 1. Writes Equip with no cooldown.
4. Heal (`0x00420D7A..0x00420E12`): Force (selector `0x3C`) below 8, effective
   Heal (selector `0x51`) above -4, and a previous action other than 1.
5. Owned sector: the owner query (selector `0x21`) compared with the active
   player at `0x00420E34`; the Move through mode 10 of FND-AI-024.
6. Otherwise selector `0xAC`'s Attack, then the leader and follower Moves of
   FND-AI-024.

The three Equip branches, Heal, the owned-sector Move, the Attack and the
follower Move all store local -4, the current sector, in the first auxiliary
value (for example at `0x00420AB6` and `0x00420DF9`). Only the leader Move
stores its destination.

## Interpretation

The open questions of RULE-AI-029 about the armor, miscellaneous and Heal tests
are settled: armor has the weapon's tests, the miscellaneous item needs a
different item, cash and a previous action other than Attack, and the Heal
gate of families 0 to 3 also needs a previous action other than Attack. The
miscellaneous Equip writes no cooldown. Under police presence the owner query
is -2, so a gang in its own sector goes on to the Attack and block Moves.

## Alternatives

None known.

## How to reproduce

Open `0x00420950` in the instruction view; the decompiler drops the locals
written after the entry calls (FND-AI-024). Follow the selector `0x3E` compares
with 1 in each Equip branch and in the Heal branch, and the stores of local -4
into `0x0048C0BA`.
