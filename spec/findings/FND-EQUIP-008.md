---
id: FND-EQUIP-008
title: The Equip, Give and Sell panels store the item, the item mask and the recipient in target and target_2, and the Equip list and the Give recipients are filtered by the gang definition's Tech Level
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043F136..0x0043F52B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043DE42..0x0043DE80
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043E047
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043E2D6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00445F2E..0x004460FA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004462A7..0x004462CD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00446E68..0x00446E8E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004470AD..0x004470FC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004444B0..0x0044455B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044465E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00444CEA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00444D68..0x00444E8C
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges of the functions named here are in FND-EXE-004. Offsets are those of
the 32-byte gang record (FMT-STATE-001), the 156-byte gang definition and the
166-byte item record.

Equip panel `fn_0043DAD9`:

- When the gang's action byte is already 5 (`0x0043DE42`), the category is
  the queued item's 16-bit type (item offset `0x7A`) minus 1, kept at the type
  when that would be negative (`0x0043DE4B..0x0043DE62`).
- It calls the list builder `fn_0043F136` with the category, the 16-bit field
  at offset `0x82` of the gang's definition (`0x004A2882`, `0x0043DE80` and
  again at `0x0043E52F`), the player and the roster slot.
- On confirmation it stores the chosen list entry, an item record number from
  the 16-entry array at `0x004948A8`, into offset `0x08` of the gang record:
  `0x0043E047` (pointer) and `0x0043E2D6` (keyboard).

List builder `fn_0043F136`:

- It fills the 16 entries of `0x004948A8` with -1 and blanks the 16 text rows
  of 31 bytes at `0x00494900`.
- For items 0 to 63 it computes a category from the 16-bit type at offset
  `0x7A`: `type - 1` when that is 0 or more, else the type itself, so types 0
  and 1 give 0, 2 gives 1, 3 gives 2, 4 gives 3 and 99 gives 98
  (`0x0043F267..0x0043F26F`).
- It lists the item when all of these hold (`0x0043F293..0x0043F31B`): the
  item's 16-bit field at offset `0x80` is at most the Tech Level argument
  (signed); the category equals the requested one; the byte
  `0x004A2608[item * 6 + active_player]` is 0, where `active_player` is the
  value at `0x004ABC84`; and the item number differs from each of the gang's
  weapon, armor and miscellaneous bytes.
- For a listed item it takes the 16-bit Cost (offset `0x7E`) and, when the
  sector record of the gang's current sector has a nonzero byte at offset
  `0x0E` and its owner byte equals the player argument, replaces it with
  `Cost - Cost / 3` (`0x0043F343..0x0043F3B5`), the same test and formula as
  the transaction pass (FND-EQUIP-007). It draws the name and that price on
  row `count` and stores the item number in entry `count` (`0x0043F513`). The
  count has no upper bound in the loop.
- It never reads cash.

Give panel `fn_00445A4F`:

- The candidate recipients (`0x00445F2E..0x00445FB9`): the five entries of
  `0x00494838` are set to -1, then every roster slot of the same player other
  than the giver whose sector byte equals the giver's sector byte is stored in
  the next entry.
- When the gang's action is already 6 (`0x00445FC5`), bit 1 of offset `0x08`
  selects the weapon, bit 2 the armor and bit 4 the miscellaneous item, and
  the recipient entry is the one holding the slot in offset `0x09`.
- A Tech Level requirement is computed as the highest 16-bit field at offset
  `0x80` among the selected items, starting from 0 (`0x00446029..0x004460FA`).
  A candidate is accepted as recipient only when this requirement is at most
  the 16-bit field at offset `0x82` of the candidate's gang definition
  (`0x004470AD..0x004470FC`).
- On confirmation it stores `misc * 4 + armor * 2 + weapon`, each selection
  flag 0 or 1, into offset `0x08`, and the chosen candidate's roster slot into
  offset `0x09` (`0x004462A7..0x004462CD` pointer, `0x00446E68..0x00446E8E`
  keyboard).

Sell panel `fn_00443BBD`:

- When the gang's action is already 12 (`0x004444B0`) the three flags are
  taken from bits 1, 2 and 4 of offset `0x08`; otherwise all three start at 0
  (`0x00444553..0x0044455B`).
- A flag can be switched only when its slot byte (offset `0x04`, `0x05`,
  `0x06`) is not -1 (`0x00444D68`, `0x00444DE3`, `0x00444E61`).
- Confirmation is refused while all three flags are 0 (`0x004445D2`,
  `0x00444C52`); otherwise it stores `misc * 4 + armor * 2 + weapon` into
  offset `0x08` (`0x0044465E` pointer, `0x00444CEA` keyboard).
- The three rows show `Cost / 2` of each carried item (`0x00443E9E`,
  `0x00444127`, `0x004443B6`).

## Interpretation

- The queued item of an Equip, and the item mask of Give and Sell, are
  `target`; the Give recipient is `target_2`, a roster slot of the same
  player. The mask bits are 1 weapon, 2 armor, 4 miscellaneous in all three
  places that read or write them.
- The gang's Tech Level is the definition field at offset `0x82`; the Equip
  list compares the item's Tech Level (offset `0x80`) with it, allowing equal
  values, and so does the Give panel for the highest item given.
- The Equip categories are: 0 for item types 0 and 1 (melee and blade
  weapons), 1 for type 2 (ranged), 2 for type 3 (armor) and 3 for type 4
  (miscellaneous).
- An item is researched for the list when its `research_remaining` byte for
  the active player is 0, and an item the gang already carries in any slot is
  left out.
- A Give recipient must be another of the player's gangs in the giver's
  sector. Every such gang is active, since its sector byte is not 100. The
  resolver checks only that it is still active (FND-EQUIP-007).
- A Sell can select only carried items and at least one of them.

## Alternatives

- The Give panel's other Tech Level references (`0x0044722D..0x00447255`,
  keyboard selection) were not followed; they are assumed to apply the same
  test.
- The five-entry candidate array has no bound in its fill loop; at most five
  other gangs of one player can share a sector under the six-gang limit.

## How to reproduce

In `fn_0043F136`, the loop over 64 items reads `0x004A5F82`, `0x004A5F88`,
`0x004A2608` and the three item bytes of the gang before drawing. In
`fn_0043DAD9`, the calls of `fn_0043F136` pass `0x004A2882`. In `fn_00445A4F`
and `fn_00443BBD`, the stores into `0x00498DB0` (offset `0x08`) and
`0x00498DB1` (offset `0x09`) of the gang record are listed by their data
references.
