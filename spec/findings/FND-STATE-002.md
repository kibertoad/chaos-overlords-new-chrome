---
id: FND-STATE-002
title: The gang record holds the player at byte 0, the definition at byte 1 and Force at byte 3, its statistics follow the definition's order, and each picker's target bytes are read back by the resolver
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E01A..0x0046E065
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475A21..0x00475C65
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047781F..0x004782C4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472B29..0x0047523A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043BB9B..0x0043BE74
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00441D89..0x00442011
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043E047..0x0043E2D6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00440884..0x00440B0C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004462BD..0x00446E8E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00444674..0x00444D00
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00442B40..0x00442DCF
tool: Ghidra 12.1.3
environment: null
---

## Observation

Offsets are in the 32-byte gang record at `0x00498DA8 + player * 0xA20 +
slot * 0x20`. The gang definition table sits at `0x004A2800` (156-byte
records; the new-match setup reads `0x36D8` bytes into it at `0x0046E937`),
the item table at `0x004A5F08` (166-byte records, `0x2980` bytes at
`0x0046E9C5`) and the site table at `0x004AB668`. FND-EXE-004 gives the ranges
of the functions named here.

Player, definition and Force:

- The new-match setup `fn_0046DC10` creates each player's Right Hands in slot
  0: the player slot at byte 0 (`0x0046E01A`), 0 at byte 1 (`0x0046E02C`), 10
  at byte 3 (`0x0046E03F`), and -1 at bytes 4 and 5 (`0x0046E052`,
  `0x0046E065`).
- The hire block of the whole-turn resolver `fn_00472775` builds a new record
  in a local copy (at `EBP-0x20BC`) and copies all eight DWORDs into the free
  slot at `0x00475C51..0x00475C5D`. The copy holds: byte 0, the hiring player
  (`0x00475A21`); byte 1, the offered definition from `0x004ABBC0`
  (`0x00475A8C`); byte 2, the offered sector from `0x004A27C8`
  (`0x00475A70`); byte 3, 10 when the player's byte at `0x004A5EF0` is set,
  otherwise 4 plus the result of `fn_0045D227(5)` (`0x00475AB8`,
  `0x00475AC6..0x00475ACE`); bytes 4 to 6, -1; bytes 7 to `0x0B`, 0; bytes
  `0x0C..0x11`, 0 except 1 for the hiring player; byte `0x12` from definition
  field `0x7E`, `0x13` from `0x80`, `0x14` from `0x84`, `0x15` from `0x86`,
  and `0x16..0x1F` from the ten fields `0x88..0x9A`
  (`0x00475AEC..0x00475BA9`). Definition field `0x82` is not copied.
- Byte 1 indexes the definition table wherever it is used: the Upkeep scan
  reads field `0x7C` through it at `0x0046F068..0x0046F07A`, and the combat
  bookkeeping copies it into byte 0 of the combat record at `0x004743DE`.
- The damage application stores the new Force at byte 3 (`0x00474833`) and
  marks the gang dead when that byte is below 1 (`0x0047485A`).

Hire slot search (`0x00475BDB..0x00475C2D`): the loop starts at slot 0, stops
at the first slot whose sector byte is 100, and gives up when the index
reaches `0x50`. A hire lands only in slots 0 to 79; slot 80 is never filled by
a hire.

Statistics: `fn_0047781F` rebuilds bytes `0x12..0x1F`. For each byte `0x12 +
k` it takes the definition field, adds the matching field of each item byte
(4, 5, 6) that is not -1, and adds sector byte `0x16 + k` when the gang's
player owns the gang's sector. The fields line up as follows:

| Gang byte | Definition field | Item field | Sector byte |
|---|---|---|---|
| `0x12` | `0x7E` | `0x82` | `0x16` |
| `0x13` | `0x80` | `0x84` | `0x17` |
| `0x14` | `0x84` | `0x86` | `0x18` |
| `0x15` | `0x86` | `0x88` | `0x19` |
| `0x16 + j`, j = 0..9 | `0x88 + 2j` | `0x8A + 2j` | `0x1A + j` |

Byte `0x12` is built last (`0x0047816F..0x00478293`). After the definition and item fields it adds, from the
bytes just built: `0x1B + 0x1E + 0x1F` when the weapon byte is -1; otherwise,
by the weapon's item field `0x7A`, `0x1B` for 0, `0x1B + 0x1C` for 1, `0x1D`
for 2, and nothing for any other value. Then it adds the armor's and misc
item's field `0x82` and sector byte `0x16`.

In the resolver's action cases, which read the local copy: Heal passes byte
`0x18` plus 4 to the dice helper `fn_00475F70` (`0x00472C10`); Influence uses
byte `0x19` plus Force (`0x00472D8A`); Research uses byte `0x1A` plus Force
(`0x00472F17`); Chaos uses sector byte 4 plus byte `0x16` plus Force
(`0x0047329D`); Control uses Force plus byte `0x17` (`0x0047538F`).

Target bytes. Each picker writes bytes 8 and 9 of the stored record, and the
resolver reads them from its local copy:

| Action | Picker writes | Resolver reads |
|---|---|---|
| Attack (1), `fn_0043B290` | byte 8, a player slot, and byte 9, a roster slot, from its two candidate lists (`0x0043BB9B`/`0x0043BBBB`, `0x0043BE54`/`0x0043BE74`) | byte 8 as the target player and byte 9 as the target's roster slot (`0x0047399B` and following) |
| Move (10), `fn_004413EF` | byte 8, the chosen sector (`0x00441D89`, `0x00442011`) | byte 8 copied into byte 2 (`0x0047522E`) |
| Equip (5), `fn_0043DAD9` | byte 8, an item number from the list at `0x004948A8` (`0x0043E047`, `0x0043E2D6`) | byte 8 as the item: price field `0x7E` (`0x00474986`), type field `0x7A` (`0x00474A3C`) |
| Influence (9), `fn_0043F692` | byte 8, the chosen site slot (`0x00440884`, `0x00440B0C`) | byte 8 as the site slot 0 to 2 of the gang's sector (`0x00472D0C` and following) |
| Give (6), `fn_00445A4F` | byte 8, a mask of weapon 1, armor 2, misc 4, and byte 9, a roster slot from the list at `0x00494838` (`0x004462BD`/`0x004462CD`, `0x00446E7E`/`0x00446E8E`) | byte 8 as the mask and byte 9 as the recipient's roster slot of the same player (`0x00474C19` and following) |
| Sell (12), `fn_00443BBD` | byte 8, the same mask (`0x00444674`, `0x00444D00`) | byte 8 as the mask (`0x00474B15` and following) |
| Research (11), `fn_004427FA` | byte 8, an item number from the list at `0x004948A8` (`0x00442B40`, `0x00442DCF`) | byte 8 as the item: element `item * 6 + player` of `0x004A2608` (`0x00472EE4`, `0x0047303A`) |

The Equip picker writes no other byte for the queued item.

## Interpretation

Byte 0 is the owning player, byte 1 the definition, byte 3 the current Force.
The statistics at `0x12..0x1F` follow the definition's field order with its
Tech Level field left out, and item and site fields follow the same order, so
the names the format entries give the definition fields carry over. The
resolver's own uses fix five of them from behaviour: `0x16` feeds Chaos,
`0x17` Control, `0x18` Heal, `0x19` Influence and `0x1A` Research. Bytes
`0x1B..0x1F` are the combat skills: effective Combat gains the skill or skills
that fit the weapon class, and a gang with no weapon gains three of them.

The roster has 81 slots, but hiring uses only the first 80. Slot 0 is reused
by a hire once the Right Hands are dead.

## Alternatives

- Which statistic each of `0x1B..0x1F` names rests on the definition table's
  field names (FMT-DATA-002), which come from an outside source; this reading
  shows only that the three tables agree in order and which bytes Combat takes
  for each weapon class.
- The range of `fn_0045D227(5)` (0 to 4 or 1 to 5) belongs to the RNG entries.

## How to reproduce

In `0x0046DC10`, read the stores at `0x0046E01A..0x0046E065`. In
`0x00472775`, read the local record build at `0x00475A21..0x00475BA9`, the
slot search at `0x00475BDB..0x00475C2D`, and the action cases that use the
local copy. In `0x0047781F`, follow the fourteen accumulations and the weapon
class switch. In each picker, list the stores to `0x00498DB0` and
`0x00498DB1`.
