---
id: FND-TURN-001
title: Instant actions run in player and roster slot order, and each Influence gang changes the site before the next one rolls
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043F692
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004782C5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AB684
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the whole-turn resolver `fn_00472775`, the Instant switch runs inside a scan
of player slots 0 to 5 and, for each player, the 81 gang records in ascending
order. Action values 2, 7, 8, 9, 11 and 13 go to the Bribe, Heal, Hide,
Influence, Research and Snitch cases.

The Influence case compares the target site's current progress with its
definition's base Resistance. When they differ, it rolls only the current
gang's Force plus effective Influence, adds that gang's successes to the site's
progress at once, clamps the progress to the base Resistance, and records a
completion when it gets there, all before the scan moves to the next gang. When
progress already equals the base Resistance, a later Influence gang makes no
roll.

The Influence picker, `fn_0043F692`, is the only code that requests resource
5005, the Influence panel image (`DATA/PX16/PX05005`, with
`DATA/PX08/PX05005`). For each of the sector's three site slots, the
definition and progress bytes at sector record offsets `+7`/`+8`, `+9`/`+10`
and `+11`/`+12`, it lets the slot be chosen only while the definition's base
Resistance differs from the progress. The 36-byte sector record has one owner
byte and these three pairs, and no field naming who influenced a site.

The Control block of the same resolver writes a new sector owner in one place.
Whenever the chosen owner differs from the current one, it writes the owner and
sets the progress of all three site slots to 0, before recording the reports
of control gained and lost. The branch of the Chaos sector pass that makes a
sector neutral also writes owner -1 and sets every progress byte to 0.

The site definition's Tolerance field, reached at `0x004AB684`, has two code
references: the computer players' selector evaluation `fn_00402D70` and the
sector rebuild helper `fn_004782C5`. The Influence case does not read it.
`fn_004782C5` also rebuilds the sector's Support, Cash, statistic modifiers and
special-site flags. Its call from the match loop comes before planning, not
from inside the whole-turn resolver.

## Interpretation

Instant actions resolve in player slot and roster slot order, whatever order
the players gave them in. Influence is cumulative, not pooled: each gang makes
its own roll and changes the site before the next gang acts, and a gang that
comes after the site is complete makes no draw.

A completed site cannot be chosen again. Capturing a sector removes all
progress on its sites, so the new owner starts from 0 on each of them.

A site completed during the Instant phase gives nothing for the rest of that
turn: its Support, Cash, Tolerance and statistic modifiers enter the sector and
gang records only at the next rebuild before planning.

## Alternatives

None known for the order and the Influence arithmetic. The exact instruction
addresses of the Influence case, the Control owner write and the neutralizing
branch have not been recorded, only their place in `fn_00472775`.

## How to reproduce

In `fn_00472775`, find the switch on the gang record's action byte (offset
`+7`) inside the nested loops over 6 players and 81 gangs; its cases 2, 7, 8,
9, 11 and 13 are the Instant actions. Find `fn_0043F692` as the only function
that passes 5005 to the resource loader. List the references to `0x004AB684` to
get `fn_00402D70` and `fn_004782C5`.
