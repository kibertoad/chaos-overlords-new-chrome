---
id: FND-AI-032
title: The family-2 handler equips, heals, attacks visible hostiles and takes weak or hostile sectors
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041FEF0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042085D
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 2's handler `0x0041FEF0` runs in this order:

1. Armor (selector `0x64`) before weapon (selector `0x61`). Each needs its
   cooldown at most 0, an item different from the one equipped that the player
   can afford, and a previous action other than Attack. Either Equip writes a
   cooldown of the item's cost times 3 and clears the first auxiliary value.
2. Heal when Force is below 8, effective Heal is at least -3, and the cached
   opponent weight of the current sector is strictly below 5.
3. In an owned sector, Move through mode 6.
4. In a sector not owned, with a positive cached weight and at least one
   visible hostile gang: a loop of up to five target draws. At weight 10 the
   target is drawn from all visible gangs of human players; at other weights
   from the visible gangs whose owner is viewed negatively. Selector `0x2B`
   compares the gang with the same ordinal in the list of all visible
   opponents (FND-AI-033). A passing comparison stops the loop; after five
   failures the last drawn target is still attacked. Attack stores the current
   sector in the first auxiliary value.
5. Without that Attack: Move through mode 6 when the previous action was
   Control, when the scenario is 9, or when the gang cannot take the sector
   alone; otherwise Control. These ordinary actions clear the first auxiliary
   value.
6. Two late gates can replace any earlier action with Control and clear the
   auxiliary value. One requires that the sector's owner is already viewed
   negatively, that no gang of the owner is visible there, and that the
   player-pair flag of FND-AI-018 is set (read at `0x0042085D`). The other
   requires a sector owned by a hostile human with no visible human gangs and a
   previous action other than Control.
7. In scenario 0 with fewer than four turns remaining, Terminate replaces the
   action.

## Interpretation

Family 2 is the aggressive territorial family. The player-pair flag does not
start an attack; it lets the gang take the sector by Control once the
territorial Combat + Defense test has shown overwhelming strength.

## Alternatives

Whether step 4 requires "at least one visible hostile gang" through selector
`0xAB` or through the pool size is not recorded.

## How to reproduce

Open `0x0041FEF0`; the two mode 6 calls to `0x00408642`, the loop counter
compared with 5, the read of the pair record's +20 byte at `0x0042085D`, and
the final scenario and turns-remaining comparison.

This evidence was recorded in the older notes under the endgame awards
heading; it concerns only the AI.
