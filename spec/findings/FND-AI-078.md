---
id: FND-AI-078
title: Family 7 researches at once when its focus equals the best research sector, and reads the previous target without testing the previous action
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00436C70..0x004384BF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
tool: Ghidra 12.1.3
environment: null
---

## Observation

In family 7's handler `0x00436C70` (FND-AI-001), after the Heal test, the
call at `0x004372B9` asks selector `0x30` of `0x00402D70` for the best
research sector, passing the player and the gang's sector, which the handler
keeps in the local `[EBP-0x4]` from its entry. The result goes to
`[EBP-0xC]`. Selector `0x30` starts from the sector it is passed and replaces
it with each sector the player owns whose cached Research sum is strictly
greater (FND-AI-035); it has no path that returns -1.

The handler then loads the 16-bit focus value of the gang's auxiliary record
(`0x0048C0BA + player * 0x46E + slot * 14`, FND-AI-081) with sign extension
at `0x004372DD` and compares it with the best sector at `0x004372E5`:

- Equal (`JZ 0x004372F8`): the handler goes straight to item Research.
- Not equal, and the best sector is -1 (`0x004372EE`, falling through to
  `0x004372F8`): item Research as well.
- Otherwise (`JNZ 0x004377D0`): at `0x004377D3` the gang's sector is compared
  with the best sector. When they differ the handler stores Move (10) at
  `0x004377EE`, passes the best sector plus `0x40` to the sector selector
  `0x00408642`, stores its result as the first target and -1 in the focus at
  `0x0043788B`. When they are equal (`JZ 0x0043789A`) it looks for a Research
  site to influence in slots 0 to 2 (Influence stored at `0x00437904`); with
  none, it stores the gang's sector in the focus at `0x004379A7` and goes on
  to item Research.

The item Research on the equal-focus path (from `0x004372F8`) and on the
current-sector path (from `0x004379AF`) open the same way: selector `0x41`
gives the first previous target byte (planning record `+0x06`, FND-AI-019),
and the research byte `0x004A2608 + item * 6 + player` of that item is loaded
with sign extension and tested against 0 (`0x0043731A`, `0x004379D1`). A
nonzero byte repeats that item; a zero byte reads the item's type word at
`0x004A5F82 + item * 0xA6` and asks for the next category as FND-AI-035
describes. Neither path reads the previous action byte (planning record
`+0x05`, selector `0x3E`) before the research byte; the handler reads it only
earlier, to clear the previous target after an Equip, Move, Attack or
Influence. Every Research store writes the item into the first target byte
and the same value, sign extended, into the focus.

All five branches of this part of the handler, Heal (`0x004372A8`), the
equal-focus Research (`0x004377CB`), the Move (`0x00437895`), the Influence
(`0x00437985`) and the current-sector Research (`0x00437DAE` and the fall
through after `0x00437E7A`), join at `0x00437E82`. There selector `0x40`
gives the first planned target byte (planning record `+0x09`, sign
extended), and only a value of -1 (`0x00437E96`) enters the fallback scans
for ranged, blade, melee and armor items and the fixed list, ending in
family 0 and a Move through selector mode 5 (FND-AI-035).

The handler has 19 stores to the focus: the Attack's gang sector
(`0x00436EB8`), five item stores on the equal-focus path (`0x00437417`,
`0x00437519`, `0x0043761B`, `0x004376EF`, `0x004377C3`), the Move's -1
(`0x0043788B`), the gang's sector before the current-sector Research
(`0x004379A7`), five item stores on that path (`0x00437ACE`, `0x00437BD0`,
`0x00437CD2`, `0x00437DA6`, `0x00437E7A`), five in the fallback scans
(`0x00437F87`, `0x0043807C`, `0x00438171`, `0x00438266`, `0x0043835B`) and the
-1 of the family-0 Move (`0x0043842C`). The Heal and Influence branches store
no focus.

## Interpretation

Family 7 compares the focus it stored at the end of its last pass with the
sector it would research in now. A match skips both the Move and the
Influence step: the gang researches where it stands, even when that is not
the best sector, and even when the best sector still has a site to
influence. Since a Research stores the item number in the focus, a match can
come from an item number equal to a sector number as well as from a sector
stored by an earlier pass. The -1 test of the best sector never passes for
an active gang, whose sector is 0 to 63.

The research continuation follows the previous target byte alone. After a
previous Equip, Move, Attack or Influence that byte has just been cleared to
0, and after an action that writes no target byte it is 0 unless an earlier
store of the same pass left one (FND-AI-074, FND-AI-019), so in those cases
the gang reads item 0. A previous action whose target byte survives without
being Research makes the gang read that byte as an item number: an Influence
the duplicate cleanup rewrote to Snitch keeps its site slot, and family 2's
Control override keeps the Move's sector.

The fallback test at `0x00437E82` reads the planned target, not the planned
action. The Heal leaves that byte 0, the Influence a site slot, and the Move
the sector selector's result, which is a sector (RULE-AI-006), so only a
Research whose item scan found nothing reaches the fallback.

## Alternatives

The branch targets come from the instructions at the addresses given; the
decompiled control flow of `0x00436C70` agrees with them. That selector
`0x30` cannot return -1 rests on its case in `0x00402D70`, which returns
either its sector argument or a loop index from 0 to 63.

## How to reproduce

Disassemble `0x004372AD..0x004372F8`, `0x004377D0..0x004377F6`,
`0x0043789A..0x004378CB` and `0x00437E82..0x00437E9F`, list the references to
`0x00437E82`, and read case `0x30` of `0x00402D70`.
