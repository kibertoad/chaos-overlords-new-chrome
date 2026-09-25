---
id: FND-EQUIP-007
title: The transaction pass skips inactive gangs, reads the Equip item, the Give mask and the Sell mask from target and the Give recipient from target_2, and tests only the Give recipient's sector
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047489A..0x00474E27
tool: Ghidra 12.1.3
environment: null
---

## Observation

The transaction pass of the whole-turn resolver `fn_00472775` (FND-EXE-004
gives its range) occupies `0x0047489A..0x00474E27`. It loops over player slots
0 to 5; for each it sets the three 81-entry pending arrays to -1
(`0x004748E4..0x00474906`), then scans roster slots 0 to 80, copying each
32-byte gang record into a local copy.

- Active test: the copied sector byte (offset `0x02`) is compared with 100 at
  `0x0047496B`; a record equal to 100 is skipped whole, with no action handled
  and no copy back. The dispatch on the action byte (offset `0x07`) follows at
  `0x00474D0E` (5), `0x00474D1B` (6) and `0x00474D28` (12). The copy is
  written back at `0x00474D3A..0x00474D64` for every active record.
- Equip (action 5), `0x00474986..0x00474ADF`. The item number is the byte at
  offset `0x08` (`target`). The price is the item's 16-bit Cost at
  `0x004A5F86 + item * 0xA6` (item record offset `0x7E`). When the sector
  record of the gang's own current sector has a nonzero byte at offset `0x0E`
  (`0x004749A6`) and its owner byte equals the loop's player
  (`0x004749C1`), the price becomes `price - price / 3` (`0x004749ED`). The
  signed comparison at `0x00474A05` takes the failure branch when the player's
  cash at `0x004A25E8` is below the price: it records a report through
  `fn_00477748` with type 6 and arguments 2, the gang's sector and the gang's
  definition byte (offset `0x01`) (`0x00474B01`), and changes nothing else.
  Otherwise it subtracts the price from cash (`0x00474A22`), adds it to cash
  spent at `0x0049CA78` (`0x00474A35`), and switches on the item's 16-bit type
  at `0x004A5F82 + item * 0xA6` (record offset `0x7A`) through the jump table
  at `0x00474AB3`: types 0, 1 and 2 store the item in the weapon byte
  (`0x00474A67`), type 3 in the armor byte (`0x00474A78`), type 4 in the
  miscellaneous byte (`0x00474A89`); a type above 4 (`0x00474AA0`) stores it
  nowhere, after the cash has been taken. Neither the gang's Tech Level nor the
  player's research progress is read.
- Give (action 6), `0x00474C19..0x00474CEE`. The recipient's roster slot is
  the byte at offset `0x09` (`target_2`), in the same player's roster. The
  block first reads the recipient's sector byte from the global roster and
  skips the whole Give when it is 100 (`0x00474C19`). Otherwise it tests the
  byte at offset `0x08` (`target`) against 1, 2 and 4 in that order
  (`0x00474C4A`, `0x00474C75`, `0x00474CA8`); each set bit copies the giver's
  weapon, armor or miscellaneous byte into the weapon, armor or miscellaneous
  pending array at the recipient's slot and sets the giver's byte to -1. No
  bit test checks that the slot holds an item, and the recipient's Tech Level
  and sector are not compared with the giver's.
- Sell (action 12), `0x00474B15..0x00474C0C`. The byte at offset `0x08`
  (`target`) is tested against 1 (`0x00474B15`), 2 (`0x00474B51`) and 4
  (`0x00474B95`). Each set bit stores `Cost / 2` of the item in the weapon,
  armor or miscellaneous byte into the resolver's general scratch local
  `[EBP-0x10B8]` and sets that byte to -1; no branch tests that the byte holds
  an item. After the three tests the scratch local is added to cash earned at
  `0x004A27E0` (`0x00474BDF`) and to cash at `0x004A25E8` (`0x00474BF3`), both
  indexed by the copied record's player byte (offset `0x00`) rather than the
  loop counter. The case never assigns the scratch local before the tests.
- Delivery, `0x00474D6B..0x00474E27`, after the roster scan of each player:
  each pending entry other than -1 is stored into the matching item byte of
  the recipient's global record. The loop does not read the recipient's sector
  byte.

## Interpretation

- A gang that died in this turn's combat, or any inactive record, carries out
  no transaction, and a Give to a gang that died this turn is not carried out:
  the giver keeps its items.
- The Factory discount applies when the buying gang stands in a sector its
  player owns that has a Factory, at the time of the transaction.
- The resolver trusts the order: an Equip of an item whose Tech Level the gang
  lacks or that the player has not researched is carried out if the cash is
  there, and the item's type alone decides the slot.
- The Sell payout starts from whatever the scratch local held. In every Sell
  order the panel can build at least one bit is set, so the value is always
  overwritten. With no bit set it would pay the stale value: the price the last
  Equip handled in this pass computed, whether or not it was paid, or 485 (the last index the preceding
  combat-record loop stored there) when there was none.
- A selected Sell slot holding -1 would read the Cost of item -1, the 16-bit
  word at `0x004A5EE0`, which lies inside the per-player array at
  `0x004A5ED8`.

## Alternatives

- Whether an order can reach the resolver with an empty Sell mask, an empty
  selected slot or an unaffordable item type depends on the panels and on the
  computer players' order writers, which are not covered here.

## How to reproduce

In `fn_00472775`, go to `0x0047489A`, the player loop whose first inner loop
stores -1 into three local arrays. Follow the comparison with 100 at
`0x0047496B` and the three action tests. The Equip case reads offset `0x7E`
and `0x7A` of the 166-byte item record through `0x004A5F86` and `0x004A5F82`;
the Give case compares the recipient's sector byte with 100 before its three
bit tests; the Sell case ends in the two additions at `0x00474BDF` and
`0x00474BF3`. The delivery loop starts at `0x00474D6B`.
