---
id: FND-AI-019
title: Each AI planning record keeps three generations of action and target, rolled before dispatch
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458FA0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00409DE1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048A250..0x0048C0B0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00482108..0x0048210E
tool: Ghidra 12.1.3
environment: null
---

## Observation

The planning records start at `0x0048A250`, 16 bytes each, 81 per player
(player stride `0x510`, block length `0x1E60`). Offsets +2..+4, +5..+7 and
+8..+10 each hold an action byte followed by two target bytes. Selectors
`0x3F`, `0x3E` and `0x3D` of `0x00402D70` read the action bytes at +2, +5 and
+8. Selector `0x41` reads +6. The action values 0 to 14 are the command
numbers the gang record uses.

For each record whose gang is active, `0x00458FA0` rolls the history before
planning: it copies +5..+7 to +2..+4 at `0x004590A9` and `0x004590C2`, copies
+8..+10 to +5..+7 at `0x0045913F` and `0x00459158`, and clears +8, +9 and +10
at `0x004591D5`, `0x004591EF` and `0x00459209`. The two signed 16-bit values
at +12 and +14 are read by selectors `0x65` and `0x66`. At the start of each
later pass, an equipped weapon or armor slot lowers its value by 1, while an
empty slot or an inactive record sets it to 0.

The strategic refresh runs at `0x0045936F`. The duplicate cleanup follows: it
calls selector `0x5B` at `0x0045939C` and may rewrite the previous action at
`0x004593D6` or `0x00459424`. For each sector where more than one of the
player's gangs has previous action Chaos, it rewrites the first such record in
ascending slot order to 0 (through selector `0x70` with argument 1). It then
does the same for previous action Influence (through selector `0x71`),
rewriting the first match to 13 (Snitch). Only after this does the dispatcher
visit the active gangs, at `0x004594DF`, and write their new actions at +8.
There is no second roll at the end of planning.

Selector `0x5B`, at `0x004048B8` inside `0x00402D70`, counts the player's gangs
in a sector whose +5 action is 3 (Chaos). Selector `0x6F`, at `0x00404949`,
counts those whose +5 action is 9 (Influence). Inactive records have sector
100, so they never match a sector; their history is not rolled or cleared,
but the record is reset when the slot is reused.

`0x00409DE1` clears +2..+10, sets the family byte to 99 and clears the other
planning fields. The whole record block and six first-plan flags at
`0x00482108` are written and read by the save and load functions `0x00463CC5`
and `0x0046381A`.

## Interpretation

The history lets a family handler continue what the gang did last turn (the
previous action at +5) and the turn before (the older action at +2). The
cleanup stops two gangs in one sector from both continuing Chaos, or both
continuing Influence. The +12 and +14 values are the weapon and armor
replacement cooldowns (FND-AI-021).

## Alternatives

The first-plan flags are read as one byte per player from the six-byte size of
the block. Offset +1 (the flag selector `0x48` tests, FND-AI-002), +11 and +15
are not described.

## How to reproduce

The copy and clear addresses above are inside `0x00458FA0`. The selector cases
for `0x5B` and `0x6F` are at `0x004048B8` and `0x00404949`. The save and load
functions move a block of `0x1E60` bytes from `0x0048A250`.
