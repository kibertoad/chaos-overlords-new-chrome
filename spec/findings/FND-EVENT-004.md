---
id: FND-EVENT-004
title: A Last Turn report record holds an occupied byte at +0, a padding byte at +1 and four 16-bit fields from +2, and the resolver's twelve recorder calls pass fixed arguments
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00477748..0x0047781E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004726C0..0x00472774
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AAE08..0x004AB587
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABCA8..0x004ABCBF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
tool: Ghidra 12.1.3
environment: null
---

## Observation

The recorder `fn_00477748` (its range is in FND-EXE-004) takes five stack
arguments: a 32-bit player slot and four values it stores as 16-bit words,
here called the type and the arguments `a1`, `a2` and `a3`.

- It returns without writing anything when the player is -1 (the compare at
  `0x0047774E`) or when the player's count is 32 or more (the signed compare
  at `0x0047775B`).
- The count is a 32-bit integer at `0x004ABCA8 + player * 4`. The record it
  selects is at `0x004AAE08 + player * 0x140 + count * 10`.
- It stores the byte 1 at record `+0` (`0x0047777F`), the type at `+2`
  (`0x004777A0`), `a1` at `+4` (`0x004777C1`), `a2` at `+6` (`0x004777E2`) and
  `a3` at `+8` (`0x00477803`), each as a 16-bit word, then adds 1 to the count
  (`0x0047780E`). No instruction of the recorder writes record `+1`.
- The wrapper `fn_004726C0` stores 0 at `+0` of every record, players 0 to 5
  outside and records 0 to 31 inside (`0x0047270D`). It does so before it
  chooses, by the flags at `0x00482178` and `0x00487B58`, between the
  resolver `fn_00472775` and the two other functions `fn_0046A7CB` and
  `fn_0040CED0`. It writes no other byte of the table.
- The resolver's first loop over players 0 to 5 stores 0 in each count
  (`0x00472844`). The same loop fills a local byte table, element
  `sector * 6 + player`, with 1 when any of the player's 81 roster records has
  that sector in its sector byte (`0x0047287A`, `0x004728E1`).
- The save writer moves the 1,920 bytes at `0x004AAE08` as block 24
  (FND-SAVE-001). The counts at `0x004ABCA8` are not one of its blocks.

The twelve calls to the recorder, all in `fn_00472775`, pass these values.
"Gang" is the resolver's working copy of the roster record being resolved; an
offer is one of a player's three hire offers, whose definition is the byte at
`0x004ABBC0 + player * 3 + offer` and whose chosen sector is the byte at
`0x004A27C8 + player * 3 + offer`.

| Call | Block | Recipient | Type | `a1` | `a2` | `a3` |
|---|---|---|---|---|---|---|
| `0x00472BE9` | Bribe (action 2), when the player's cash is below 3 | The gang's player | 6 | 1 | The gang's sector byte | 0 |
| `0x00472E98` | Influence (action 9), when the new progress reaches the site's Resistance | The gang's player | 4 | The gang's sector byte | The gang's target byte (the site slot) | 0 |
| `0x0047301C` | Research (action 11), when the item's remaining research falls below 1 | The gang's player | 5 | The gang's target byte (the item) | 0 | 0 |
| `0x0047366F` | Sector pass, when a sector's Chaos total exceeds its byte at `+5` | Each player 0 to 5, in ascending order, whose presence byte for the sector is set | 1 | The sector | 0 | 0 |
| `0x00473724` | The same branch, when both stored Crackdown turns of the sector are in use | The sector's owner byte, read as a signed value | 3 | The sector | 0 | 0 |
| `0x00474B01` | Equip (action 5), when the price exceeds the player's cash | The gang's player | 6 | 2 | The gang's sector byte | The gang's byte at `+1`, sign-extended |
| `0x00475827` | Control pass, when a sector changes owner | The new owner | 2 | The sector | The previous owner, -1 when there was none | 0 |
| `0x00475848` | Right after the call above | The previous owner | 3 | The sector | The new owner | 0 |
| `0x00475D5B` | Hire, when the search of roster slots 0 to 79 for a sector byte of 100 finds none | The hiring player | 8 | The offer's definition | 0 | 0 |
| `0x00475DA4` | Hire, when the definition's price is nonzero and exceeds the player's cash | The hiring player | 6 | 4 | The offer's definition | 0 |
| `0x00475DED` | Hire, when the player already has 6 or more gangs whose sector byte equals the chosen sector | The hiring player | 7 | The offer's chosen sector | 0 | 0 |
| `0x00475F4F` | After `fn_00476F3B`, for each slot whose byte at `0x004ABBE0` changed | Each player slot 0 to 5 | 9 | The slot that changed | 0 | 0 |

- The hire tests run in the order sector full, cash, roster slot, so one offer
  records at most one of types 7, 6 and 8.
- In the Crackdown branch all type-1 calls for a sector come before its type-3
  call, and the type-3 call is followed by the store of -1 into the owner byte
  (`0x00473735`). When the owner byte already holds -1 the recorder returns at
  once.
- In the Control pass the previous owner is read before the owner byte is
  overwritten (`0x00475732`). When there was no previous owner the type-3 call
  passes -1 as the recipient and records nothing.
- The type-9 calls visit the changed slots in ascending order and, for each,
  the recipients 0 to 5.
- Values the resolver loads as signed bytes (the sector, target and offer
  bytes) reach the recorder sign-extended to 16 bits.

## Interpretation

The report record is ten bytes: an occupied flag, one byte of padding that is
never written, the report type and three arguments. The table and the count are
cleared by different functions, but both before the first report of a
resolution. A report always names what the panel needs to caption it: the
sector for sector events, the sector and site slot for a completed site, the
item for Research, the failed order in `a1` of a cash failure, the gang
definition for hire failures and the eliminated player for an elimination.

The Equip cash failure stores the byte at gang record `+1`, which FMT-STATE-001
names `definition` from an outside source only, where the other Equip values
might lead one to expect the item.

## Alternatives

- The byte at record `+1` might be written by code outside the recorder and the
  wrapper. No instruction that addresses the table does so; a save file restores
  whatever the saving game held there.
- Whether `fn_0046A7CB` and `fn_0040CED0` fill the table in network play was
  not read.

## How to reproduce

Open `fn_00477748` and read the two early-return tests and the five stores into
`0x004AAE08`, `0x004AAE0A`, `0x004AAE0C`, `0x004AAE0E` and `0x004AAE10` with
the index `count * 10 + player * 0x140`. List the call sites of `fn_00477748`
(twelve, all in `fn_00472775`) and read the pushed arguments at each. Open
`fn_004726C0` for the loop that stores 0 at `0x004AAE08`, and the start of
`fn_00472775` for the store of 0 into `0x004ABCA8` and the presence table.
