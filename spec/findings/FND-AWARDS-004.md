---
id: FND-AWARDS-004
title: The awards table holds five codes per player, and the results screen shows the victory splash when exactly one player is still active, whoever controls it
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042B9E0..0x0042C3F4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042CE61..0x0042E03C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494500..0x00494577
tool: Ghidra 12.1.3
environment: null
---

## Observation

Rectangles are written `(x, y, width, height)` on the 640-by-460 drawing
surface.

Award table. The controller `0x0042B9E0` keeps five dwords per player slot at
`0x00494500 + 20 * slot`. Before the categories it sets the first three of
each slot to -1 and a per-slot count to 0; it does not touch the fourth and
fifth. Each category writes its code, 0 to 4 in the order Fist, Skull, Big Fat
Chicken, Dollar Sign, Safe, into entry `count` of each winning slot and adds
one to the count.

Single-survivor test. The controller then counts the slots whose byte at
`0x004ABBE0` is nonzero, remembering the last such slot. The renderer
`0x0042CE61` takes three arguments: a flag that is 1 when that count is
exactly 1 (`0x0042BEDC`), a tab (0 or 1), and the remembered slot. No
controller type (`0x004AB638`) is read.

Renderer. It loads resource 200 into the back buffer at `(106, 25, 428, 410)`
and draws an 8-by-16 tab mark, source `(488, 512, 8, 16)` of surface 6, at
`(468, 33)` for tab 0 or `(520, 33)` for tab 1. When the flag is 1 and the tab
is 0, it draws the splash instead of the table: resource 202 at `(110, 30,
311, 393)`, three fills in the slot's colour at `(110, 30, 40, 12)`,
`(110, 42, 13, 79)` and `(110, 121, 40, 302)`, the name at `(158 - 3 *
length, 46)`, and the portrait, source `(32 * portrait, 480, 32, 32)`
scaled to `(126, 54, 64, 64)`.

Otherwise it draws the table. Rows come first for standing 0 to 5, each
standing's slots in slot order, then the slots whose standing byte
(`0x004ABC08`) is 0xFF, in slot order. For display row `r`:

| Element | Position | Ranked rows | 0xFF rows |
|---|---|---|---|
| Name | `(197, 38 + 66 * r)` | yes | yes |
| Colour fill | `(111, 31 + 66 * r, 20, 62)` | yes | yes |
| Place marker, source `(16 * standing, 48 + 32 * slot, 16, 32)` of resource 201 | `(113, 31 + 66 * r, 16, 32)` | yes | no |
| Portrait, scaled from `(32 * portrait, 480, 32, 32)` | `(132, 30 + 66 * r, 64, 64)` | yes | yes |
| Label string at `0x00487704` | `(227, 62 + 66 * r)` | yes | no |
| Score `0x004A2790`, 5 digits | `(227, 70 + 66 * r)` | yes | no |

Tab 0 then copies `(96, 48, 160, 64)` of resource 201 to `(262, 30 + 66 * r)`
and, for each of the first three award entries that is not -1, the 48-by-48
icon at `(48 * code, 0)` of resource 201 to `(268 + 50 * i, 38 + 66 * r)`,
keyed. Tab 1 copies `(96, 112, 160, 64)` to the same place and draws five
numbers:

| Value | Ranked rows | 0xFF rows |
|---|---|---|
| `0x004A27E0` | 8 digits at `(371, 37 + 66 * r)` | 6 digits at `(383, 37 + 66 * r)` |
| `0x0049CA78` | 8 digits at `(371, 46 + 66 * r)` | 6 digits at `(383, 46 + 66 * r)` |
| `0x004A5ED8` | 7 digits at `(377, 58 + 66 * r)` | 6 digits at `(383, 58 + 66 * r)` |
| `0x004AB620` | 6 digits at `(383, 67 + 66 * r)` | 6 digits at `(383, 67 + 66 * r)` |
| `0x004A27A8` | 6 digits at `(383, 79 + 66 * r)` | 6 digits at `(383, 79 + 66 * r)` |

Controls. After the first draw with tab 0, the controller copies `(106, 25,
428, 410)` to the screen and loops. A press inside `(428, 377, 100, 48)` runs
the push helper `0x0042CB95` with 0 and, when it reports a release inside,
leaves. `(428, 33, 48, 48)` with helper argument 1 redraws with tab 0, and
`(480, 33, 48, 48)` with argument 2 redraws with tab 1. The menu command
`0x81`/9 sets the quit byte `0x00487828` and leaves. On leaving it sets
`0x00487830` to 1.

## Interpretation

`player_awards` is the table at `0x00494500`, and the codes are the builder's
category order. The five statistics are cash earned, cash spent, damage
inflicted, casualties and Overthrows, in that order from the top. The Awards
tab is shown first.

FND-AWARDS-003 reads the splash test as a count of human participants; the
executable counts active players. The splash appears when the match ends with
one player left, human or computer, and then replaces the awards view: with
one survivor, the Awards tab shows the splash again and only the Stats tab
shows the table. When several players are still active, as at the end of a
timed scenario, there is no splash. The splash stays until a control is used;
Done leaves at once without the table ever being shown.

For an eliminated player every statistic is drawn in a six-digit field, so a
cash or damage value of a million or more loses digits there, while the same
value on a ranked row fits.

## Alternatives

The label at `0x00487704` is read as the score caption; its text is not
recorded here. How the number drawer `0x00414187` handles a value wider than
its field is not recorded; losing the leading digits is the likely result.
Surface 6 is taken to be the interface sheet `DATA/PX16/PX00129`.

## How to reproduce

Open `0x0042B9E0` in Ghidra: the clears at `0x0042BA8A` to `0x0042BAAC`, the
five categories, the active count at `0x0042BEA2` and the calls of
`0x0042CE61`. Open `0x0042CE61`: the branch at `0x0042CFED` chooses between
the splash and the table, and the table's two row loops start at
`0x0042D254` and `0x0042D9F1`.
