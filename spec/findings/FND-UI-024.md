---
id: FND-UI-024
title: The sector marker reads the presence bytes, the information panels close on Enter and refuse clicks outside, and Game Information opens by itself after a load or a Join
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00412BF7..0x00413011
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00412AAD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004906A4..0x004906A7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044E6ED..0x0044F2FB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00410770..0x00411118
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004120EF..0x004123CB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044B699..0x0044C475
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044C476..0x0044D1BA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045519D..0x00455B6A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00448718..0x00448E31
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047025F..0x0047026C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487B98
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004. Sector records are the `0x24`-byte
records at `0x004A08E8` (FMT-STATE-002), gang records the `0x20`-byte records
at `0x00498DA0`, 81 per player (FMT-STATE-001). Panel-local coordinates are
those of the backing buffer; the shared panel maps them to the screen by adding
`(104,-20)` (FND-UI-014).

- Marker. `fn_00412BF7(player, sector)` is called for each sector 0 to 63 by
  the map drawer `fn_004123CC` at `0x00412AAD` and for one sector by four other
  callers. It first sets an incoming flag when any of the three bytes at
  `0x004A27C8 + player * 3` equals the sector. When the sector's byte at offset
  `0x10 + player` is set it sets an enemy flag for any other slot 0 to 5 whose
  byte at `0x10 + slot` is set, and an idle flag when any of the player's 81
  gang records has the sector at offset `0xA` and 0 at offset `0xF`. It draws
  frame `enemy + 2*idle + 4*incoming` of the marker strip (source y
  `67 + 20*frame`, x 492 to 512 of buffer 6) keyed onto the sector's cell in
  buffer 2. When the byte is not set, it first copies a saved 20-by-20 cell from
  buffer 5 back to the cell of the sector in the dword at `0x004906A4`, when that
  is not -1. Then, only with the incoming flag, it saves the current sector's
  cell to buffer 5, draws the frame at source y 227 keyed onto the cell and
  stores the sector at `0x004906A4`. `fn_0046E766` and `fn_0046FD80` set
  `0x004906A4` to -1.
- Gangs in Sector. `fn_0044E6ED(player, sector)` is called with the active
  player at `0x004ABC84` and the selected sector at `0x004ABC80`. It opens only
  when the sector's byte at `0x10 + player` is set, and otherwise plays slot 4.
  It scans that player's 81 gang records for the sector alone and draws one
  column per match with no bound; a seventh column would start at buffer x 336.
  Per column it reads the gang's definition from byte 1 and draws: Tech Level,
  the definition's 16-bit value at offset `0x82` of the `0x9C`-byte definition
  record at `0x004A2800`; Upkeep, the negated 16-bit value at offset `0x7C`;
  Combat, Defense, Stealth and Detect from gang offsets `0x12` to `0x15`, all
  through `fn_00414187` with width 2; and the ten skills from offsets `0x16` to
  `0x1F` through `fn_004142E7`. It never reads the Base Statistics byte. Its
  keys are `VK_RETURN` and `0x2B` only. A press outside panel-local
  `(104..448, 124..333)` plays slot 4. The close face is panel-local
  `(33..82, 169..191)`. A press inside the panel and off the face does nothing,
  and a double click is handled as a press.
- Cards. The card list builder `fn_00410770` writes each listed slot to
  `0x004ABC68 + 4*n` with no bound, so a seventh entry would land on
  `0x004ABC80`, the selected sector.
- Sector values. `fn_004120EF(sector, flush)` draws into buffer 1 at x 568: the
  sector code (a column letter and a row digit) at y 60; at y 69 the string
  resource `0x11 + byte 4` cut to two characters by `fn_00466673`; the signed
  byte 5 at y 78; byte 6 at y 87 and byte 3 at y 96 when the sector's owner
  (byte 0) is the active player, else 0. The three numbers go through
  `fn_00414187` with width 2. With `flush` set it copies `(568,60)-(580,103)`
  to the screen.
- Item Information. `fn_0044B699` rotates its picture on slot-0 ticks through
  frames 0 to 14 and back to 0, drawing to screen `(162,141)`. Its keys are
  `VK_RETURN` and `0x2B`. Its close face is screen `(161..210, 293..315)`; a
  press outside screen `(128..448, 124..333)` plays slot 4. The Cost and Tech
  Level row is backing row 236, screen row 216. It is opened by a double click
  (event 5) on a list row in Equip `fn_0043DAD9` (panel-local rectangle x 148 to
  328, y 26 to 169, row `(y - 26) / 9`) and in Research `fn_004427FA` (y 19 to
  162, the same row formula), and by `fn_00414D8C`, `fn_004169B3`,
  `fn_00449E80`, Give `fn_00445A4F`, Sell `fn_00443BBD` and `fn_0043B290`.
- Site Information. `fn_0044C476(site, second, own)` takes the site
  definition in the low byte of its first argument and the progress in the next
  byte. It shows the definition's Resistance, or that value minus the progress
  when `own` is nonzero. The console at `0x0047162A` and Influence at
  `0x00440F77` pass `own` as whether the sector's owner is the active player;
  Search at `0x004496D1` passes 0. Keys, faces and the outside test are those of
  Item Information.
- Game Information. `fn_0045519D` draws the scenario name from string
  resource `scenario + 1` for scenarios 0 to 3 and appends, from the string at
  `0x00487768`, string `0x36` to `0x39` for a `turn_limit` of 26, 52, 104 or
  208; any other value appends the scenario name again. Mentality is string
  `0x2E + mentality` and the limit `0x32 + choice`. A slot whose
  `0x004ABBE0` byte is 0 is labelled with string `0x3C`; otherwise a slot whose
  byte at `0x004ABC58` is 0 gets `0x3B` and any other `0x3A`. The New Game
  setup `fn_0040E0A0` sets `0x004ABC58` to 1 for controller 0 and 0 for the
  others; the Host setup `fn_004677F0` and the resumed-network handler
  `fn_00456F80` set it to 1 or 0 per seat; the pump clears it at `0x00462B8C`
  when a player is handed to the computer. Keys are `VK_RETURN` and `0x2B`, the
  OK face is at screen `(161,293)`, and a press outside panel-local
  `(104..448, 124..333)` plays slot 4.
- Automatic opening. `fn_0046FD80` calls `fn_0045519D` at `0x0047026C` when the
  byte at `0x00487B98` is set. It is set at `0x00461921`, `0x00461937` and
  `0x0046194D` (the title's load dispatcher, launch modes 1 to 3), at
  `0x00461BA5` (Join), at `0x00462030` (a resumed network game) and at
  `0x004621F8`, and cleared at `0x0046F67E`. The New Game path at `0x0046179A`
  and the Host path at `0x004619E5` do not set it. The mode 3 path calls
  `fn_0040DAB9`, which returns 0, and then goes to the quit path, so the store
  at `0x004621F8` is never reached.
- Idle warning. `fn_00448718` plays slot 3 for Cancel and OK through
  `fn_00418CCC` and `fn_00418821`. It reacts to event 3 only; a double click
  (event 5) does nothing. A press outside panel-local `(104..448, 124..333)`
  plays slot 4. Escape cancels. On slot-0 ticks it blinks one warning line: the
  strip `(165..262, 189..198)` of buffer 7 is copied to screen
  `(269..366, 169..178)` for six ticks and the area is filled black for two.

## Interpretation

The marker circle comes from the sector's presence bytes, so an enemy counts
when the player can see it. The idle mark reads the player's own gangs, and the
incoming mark reads `hire_orders`. A sector where the player has no visible gang
but a hire is on its way shows the incoming mark alone, and only one sector at a
time keeps it: drawing any sector without the player's presence puts back the
cell under the last incoming mark. In a full map draw, which visits the sectors
in order, the mark therefore survives only on an incoming sector with no later
sector that lacks the player's presence.

Gangs in Sector lists the active player's own gangs and shows the definition's
Tech Level and Upkeep, Upkeep drawn as a negative number in red. The Income row
of the sector panel is a two-character string chosen by the byte, where the
other rows are numbers. Income and Tolerance are shown to every player.

The information panels close on Enter and on the key code `0x2B`; none closes on
Escape. Site Information shows the remaining Resistance for a site in one of
the active player's sectors and the base value elsewhere, including in Search
results.

Game Information labels as human every seat whose human byte is set, network
humans included. It opens by itself at the first planning after a saved game is
loaded and on a joining computer. A new local game and the hosting computer do
not show it.

## Alternatives

- The incoming-mark reading follows from the copy order; a capture of a map
  with two sectors holding only incoming hires would confirm it.
- The string resources `0x11` onward and `0x36` onward are not reproduced here.

## How to reproduce

In `0x00412BF7`, find the loop over `0x004A27C8`, the reads of `0x004A08F8`,
the constant `0x1EC` and the references to `0x004906A4`. In `0x0044E6ED`, find
the reads at `0x004A2882` and `0x004A287C`. List the references to `0x00487B98`
and `0x004ABC58`, and the callers of `0x0045519D` and `0x0044C476`.
