---
id: FND-DATA-007
title: Each match start reads DATA/Gangs, DATA/Items and DATA/Sites whole into fixed tables, and the code reads their fields at the offsets the format entries give
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E8C1..0x0046EA62
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487BE8..0x00487C0B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A2800..0x004A5ED7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A5F08..0x004A8887
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AB668..0x004ABBBB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044C476..0x0044D1BA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042E040..0x0042EE45
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges of the functions named here are in FND-EXE-004.

Loading. `fn_0046E766`, which WinMain (`fn_00460CCF`) calls from six places when a
match begins or is resumed, reads the three definition files one after the other at
`0x0046E8C1..0x0046EA62`. For each it builds a length-prefixed name from the
install directory (FND-PLATFORM-009) and the string `data\Gangs`
(`0x00487BE8`), `data\Items` (`0x00487BF4`) or `data\Sites` (`0x00487C00`),
names file slot 0 with it through `fn_0042B60F`, and, only when that call
reports the file can be opened, opens the slot, reads one block and closes it:

| File | Bytes read | Table | Record size |
|---|---|---|---|
| `DATA/Gangs` | `0x36D8` (14,040) at `0x0046E937` | `0x004A2800` | `0x9C` |
| `DATA/Items` | `0x2980` (10,624) at `0x0046E9C5` | `0x004A5F08` | `0xA6` |
| `DATA/Sites` | `0x554` (1,364) at `0x0046EA53` | `0x004AB668` | `0x3E` |

Each read is the whole file. When a file cannot be opened the read is skipped
with no message, and the table keeps what it held.

References into each table, grouped by offset within the record (an access
through a copy of a whole record, or through a pointer to a record, is not in
these counts):

Sites (`0x004AB668 + site * 0x3E`):

- `0x16` is read by eleven functions, among them the rebuild `fn_004782C5`,
  the resolver `fn_00472775`, the Influence picker `fn_0043F692`, the
  computer players' query function `fn_00402D70` and the finance screen
  `fn_0044D1BB`.
- `0x18`, `0x1C` and `0x1E` are read by `fn_004782C5` and `fn_00402D70`;
  `0x18` also by `fn_0047712A`, and `0x1E` also by `fn_0044D1BB`.
- `0x20` to `0x3A` are read by `fn_004782C5` and `fn_00402D70`, and `0x20`
  to `0x28` also by `fn_00476516`.
- `0x3C` is read only by `fn_004782C5` (`0x004785A5`), which turns values 1
  and 2 into sector byte `0x0D` and value 3 into sector byte `0x0E`
  (FND-STATE-001).
- No instruction addresses `0x14` or `0x1A`. `fn_0044C476`, the site
  information panel, copies a whole 62-byte record to its stack
  (`0x0044C4A2..0x0044C4A9`) and reads from the copy the name, `0x14` (as the
  row of a site picture, 64 pixels per row), `0x16` (less the site's progress in one branch),
  `0x18`, `0x1C`, `0x1E` and `0x3C` (added to 29 to pick a text resource). It does not read `0x1A`. `fn_004499A9` and `fn_0044FD6C` pass a
  record's address, the start of its name, to a text routine.

Gangs (`0x004A2800 + gang * 0x9C`):

- `0x1E` is read by 20 functions, and `0x20` by the two screens that show a
  gang's description (`fn_00449E80`, `fn_00455B6B`).
- `0x7A` is read by `fn_00402D70`, the finance screen, the computer players'
  planner `fn_00458FA0`, `fn_004716EB` and the resolver (`0x004759CF`,
  `0x00475C9A`).
- `0x7C` is read by `fn_00402D70`, `fn_004078D9`, `fn_00449E80`, the finance
  screen, `fn_0044E6ED`, `fn_00455B6B` and `fn_0046E766` (`0x0046F07A..0x0046F125`).
- `0x82` is read by nine functions, among them the Equip picker
  `fn_0043DAD9` (`0x0043DE8E`) and the Research picker `fn_004427FA`, and not
  by the statistics rebuild `fn_0047781F`.
- `0x7E`, `0x80` and `0x84` to `0x9A` are read by `fn_0047781F` in the order
  FND-GANG-007 gives, and by the screens and `fn_00402D70`. `0x9A` is also read
  by the Detailed Combat routine `fn_0042E040`.

Items (`0x004A5F08 + item * 0xA6`):

- `0x1E` is read by eleven functions, several of which load image
  `PX04000` plus the value (for example `0x004504FA` in `fn_0044FD6C`).
- `0x7A` is read by the Equip picker (`0x0043DE5A`), `fn_0043F136`,
  `fn_004427FA`, `fn_004437E7`, `fn_00436C70`, `fn_00402D70`, the resolver
  (`0x00474A4E`) and `fn_0047781F` (`0x004781D6`).
- `0x7C` is read once, by the new-game routine `fn_0046DC10` at `0x0046DEDB`
  (FND-RESEARCH-002).
- `0x7E` is read by 19 functions, `0x80` by `fn_00402D70`, `fn_0043F136`,
  `fn_004437E7` and `fn_00445A4F`.
- `0x82` to `0x9C` are read by `fn_0047781F` (FND-GANG-007), `0x82` to `0x88`
  and `0x8C` also by `fn_00402D70`.
- `0x9E`, `0xA0` and `0xA2` are read only by `fn_0042E040`, at
  `0x0042E749`, `0x0042E769` and `0x0042E729` and again at `0x0042EA7B`,
  `0x0042EA9B` and `0x0042EA5B`, for the attacker's and the defender's
  weapon. The first selects image `PX07000` plus the value, the second image
  `PX07100` plus the value, and the third
  the sound `500` plus the value (`fn_0045867C`). Without a weapon the routine
  uses strips 7000 and 7102 and sound 500, or strips 7001 and 7118 and sound
  501 when the gang definition's `0x9A` is above 0.
- `0xA4` is read by `fn_0042EE46` and `fn_0042F98B` only.

## Interpretation

The tables in memory are the files as they are on disk, so a file offset is a
table offset. The reads confirm the layout of the three format entries: the
statistic fields by the pairing FND-GANG-007 records, the site's Resistance,
Support, Tolerance, Cash and special value by the rebuild FND-STATE-001
records, the item's type, animation and sound fields by the places they are
used. The site's `frequency` field (`0x1A`) is never read. A missing or
unreadable definition file is not reported: the match goes on with an empty
table.

## Alternatives

A field read only through a pointer held in a register, or through a copy of a
whole record, would be missing from the lists above. The whole-record copies
found are the site panel's (covered above) and the copies of gang and item
records in `fn_004546C5`, `fn_00414D8C`, `fn_004169B3` and `fn_00445A4F`,
which were not followed field by field.

## How to reproduce

List the references to each table range and reduce each referenced address
modulo the record size. The three reads follow the three calls of
`fn_0042B60F` in `fn_0046E766`; the pushed byte counts are `0x36D8`,
`0x2980` and `0x554`.
