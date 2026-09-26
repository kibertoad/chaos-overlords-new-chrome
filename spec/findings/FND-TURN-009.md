---
id: FND-TURN-009
title: How the order handlers write targets, and roster slot 80 as the sector-wide order's scratch record
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041462F..0x00414D8B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00414D8C..0x004169B2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043F692
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004427FA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004997A8
tool: Ghidra 12.1.3
environment: null
---

## Observation

Gang records are `0x00498DA8 + player × 0xA20 + slot × 0x20`; the sector
selected on screen is at `0x004ABC80` and the player whose turn it is at
`0x004ABC84`.

### The sector-wide handler `fn_0041462F`

Its one argument chooses the one-off menu (0) or the recurring menu (nonzero).
Before the menu result is used, it writes the record of roster slot 80 of the
acting player (`0x004997A8 + player × 0xA20`): player at `+0`, definition -1
at `+1`, the selected sector at `+2` (`0x004147A2` and `0x00414A18`), and 0 at
`+7`, `+8` and `+9`. The pickers it calls get slot `0x50` (80) as their gang
argument: Attack `fn_0043B290` (`0x00414801`), Influence `fn_0043F692`
(`0x00414865` and `0x00414AA7`) and Move `fn_004413EF` (`0x0041488D`). The
menu entries map to actions as follows: one-off menu 1 Attack, 2 Bribe, 3
Chaos, 4 Control, 5 Heal, 6 Hide, 7 Influence, 8 Move, 9 Snitch, 11 None, 13
Terminate; recurring menu 1 Chaos, 2 Control, 3 Heal, 4 Hide, 5 Influence,
7 None. A picker that is cancelled leaves no order.

The loop at `0x00414B25` visits roster slots 0 to 80 and applies the order to
every one of the acting player's records whose sector byte equals the selected
sector. For Heal (7) it skips a record whose Force is 10 or more. For each
record it writes: `+10` (0 from the one-off menu, the action from the
recurring menu), `+11` from slot 80's `+8`, `+7` the action, `+8` from slot 80's
`+8`, and `+9` from slot 80's `+9`. At the end (`0x00414D7B`) it writes 100 to
slot 80's sector byte. No other instruction addresses slot 80's sector byte
directly; loops over all 81 slots (such as `fn_00476F3B`'s) reach it like any
other record.

### The individual handler `fn_00414D8C`

- One-off menu: a picker writes the gang's `+8` (and `+9` where it has a second
  target). When an action is chosen, `0x00415894` and `0x004158B8` write 0 to
  `+10` and `+11`, and `0x0041590B` writes the action to `+7`.
- Recurring menu: Influence and Research first call their picker on the gang
  itself, which writes `+8`; then `+11` is set from `+8` (`0x004151DC`,
  `0x0041522E`). For Chaos, Control, Heal and Hide nothing writes `+8` or
  `+11`. The action is written to `+10` at `0x004152BC` and to `+7` at
  `0x0041590B`. Recurring None writes 0 to `+10` (`0x004152E4`) and 0 to `+7`.
- A second input path, entered at `0x0041611A`, reads a pointer position
  through `fn_00465B64` and tests it against two screen rectangles. A point in
  the 3 × 3 grid at x `0x3C..0xD8`, y `0x40..0xE2` gives a one-off Move to the
  matching neighbour of the selected sector (the centre cell and cells not
  marked in `0x004ABC40` are ignored). When the selected sector's owner is the
  acting player, a point in x `0xE4..0x1A8`, y `0x56..0xCE` picks site slot 0,
  1 or 2 by x (above `0x126` adds one, above `0x168` adds another); if that
  site's progress differs from its `resistance`, the path writes 9 to `+10`,
  the slot to `+11` (`0x0041647A`, `0x004164A0`), 9 to `+7` and the slot to
  `+8` (`0x00416513`, `0x00416539`). The Move result writes 0 to `+10` and
  `+11`.

### The pickers

The Influence picker `fn_0043F692` writes a site slot number, 0 to 2, to the
gang's `+8` (`0x00440884`, `0x00440B0C`). The Research picker `fn_004427FA`
writes an item record number, taken from its list of displayed items at
`0x004948A8`, to `+8` (`0x00442B40`, `0x00442DCF`). The Influence case of the
resolver, the recurring cleanup (FND-TURN-006) and the rebuild all index the
sector's site pairs with `+8` or `+11` × 2, and the Research case and the
cleanup index the remaining-research table `0x004A2608` with it × 6.

## Interpretation

A sector-wide order goes to every gang of the player in the sector, hiding or
not; only Heal leaves out gangs already at Force 10. It writes the same target
into `target` and `repeat_target`, from either menu. A one-off order from the
command bar therefore leaves `repeat_target` holding its target, harmless
because `repeat_action` is 0.

From a gang's own menu, a recurring Influence or Research writes the chosen
site or item into both `target` and `repeat_target`. A recurring Chaos,
Control, Heal or Hide leaves both target bytes as they were.

The recurring Influence shortcut is the site part of that second input path:
pointing at an unfinished site of an owned sector gives the gang a recurring
Influence of that site at once, without the menu.

`repeat_target` holds the site slot (0 to 2) of a recurring Influence and the
item record number of a recurring Research.

Roster slot 80 of each player is scratch space for the command bar: its sector
byte is set to the selected sector only while the order is being chosen and
applied, and is back to 100 before the handler returns. This is why the hire
search stops before slot 80 (FND-TURN-008). The order loop also reaches slot
80 itself, since its sector byte matches while the loop runs; what it writes
there is cleared at the next turn start, which clears the recurring action of
every record whose sector byte is 100.

## Alternatives

That the input path at `0x0041611A` is a drag of the gang onto the map or the
site panel is an inference from the rectangles; how the player starts it has
not been read. What `0x004ABC40` holds (which neighbours are enabled) has not
been read.

## How to reproduce

In `fn_0041462F`, find the writes to `0x004997A8..0x004997B1` before the menu
switch, the pickers called with 80, the loop at `0x00414B25` and the store of
100 at `0x00414D7B`. In `fn_00414D8C`, list the writes to `0x00498DAF`,
`0x00498DB0`, `0x00498DB2` and `0x00498DB3`. In the pickers, list the writes to
`0x00498DB0`.
