---
id: FND-HIRE-006
title: A hire skips the cash test for a zero-cost gang, fills slots 0 to 79 only, and writes a complete record with no equipment, no orders and the definition's statistics
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004759AE..0x00475A15
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475A1B..0x00475BDA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475BDB..0x00475C2D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475C33..0x00475D29
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A2800..0x004A5ED7
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the hire block of `fn_00472775` (FND-HIRE-001, FND-HIRE-002), after the
sector count has passed:

- Cash test, `0x004759AE..0x00475A15`. It loads the signed 16-bit field at
  `0x004A287A + definition * 0x9C`, where the definition is the offer byte at
  `0x004ABBC0 + player * 3 + slot`. When that value is 0 (`TEST` at
  `0x004759D7`, `JZ` at `0x004759D9`) it jumps straight to the record build at
  `0x00475A1B`. Otherwise it loads the same field again, compares it with the
  player's cash as `CMP cost, [0x004A25E8 + player * 4]` at `0x00475A0E`, and
  takes the failure path on a signed `JG` at `0x00475A15`.
- Record build, into the resolver's local 32-byte gang copy, `0x00475A1B..0x00475BDA`,
  by record offset:

| Offset | Value | Instruction |
|---|---|---|
| `0x00` | the hiring player slot | `0x00475A21` |
| `0x0C..0x11` | 0 for all six, then 1 for the hiring player | `0x00475A4F`, `0x00475A62` |
| `0x02` | the order's sector | `0x00475A80` |
| `0x01` | the offer's definition | `0x00475A9C` |
| `0x03` | 10 when the player's byte at `0x004A5EF0` is set, otherwise the bounded draw `fn_0045D227(5)` plus 4 | `0x00475AB8`, `0x00475AD1` |
| `0x04`, `0x05`, `0x06` | `0xFF` each | `0x00475AD7..0x00475AE5` |
| `0x12`, `0x13`, `0x14`, `0x15` | the low bytes of the definition fields at `+0x7E`, `+0x80`, `+0x84`, `+0x86` | `0x00475B04..0x00475B5E` |
| `0x07..0x0B` | 0 each | `0x00475B64..0x00475B80` |
| `0x16..0x1F` | the low bytes of the ten definition fields `+0x88` to `+0x9A`, in order | `0x00475BCF` |

- Free-slot search, `0x00475BDB..0x00475C2D`. A counter starts at 0; the loop
  stops when the record's sector byte is 100 (`0x00475C05`) or the counter
  reaches 80 (`0x00475C0E`). A counter of 80 or more afterwards
  (`0x00475C26`) takes the roster-full path.
- Success, `0x00475C33..0x00475D29`: the copy is written into the free record
  (eight dwords, `0x00475C58`), the gang's entry of `0x00498990` is set to 1
  (`0x00475C71`), cash spent gains the cost (`0x00475C88..0x00475CA8`), cash
  loses it (`0x00475CBA..0x00475CE4`), the offer byte is negated
  (`0x00475CF1..0x00475D0A`) and the order is set to -1 (`0x00475D29`).
- The definition table read here starts at `0x004A2800` with stride `0x9C`:
  the Upkeep scan of `fn_0046E766` reads `+0x7C` of the same records
  (`0x0046F07A`), and the offer drawing of `fn_004716EB` takes the portrait
  from `+0x1E` (FND-HIRE-007). The block does not read `+0x82`.

## Interpretation

The hire price is the field at `+0x7A` of the gang definition (FMT-DATA-002),
the one the computer players also compare with cash (FND-AI-008). A gang whose
price is 0 is hired whatever the player's cash, even in debt; any other price
fails only when it is greater than cash, so exact cash is enough. The shipped
`DATA/Gangs` has such a zero-price definition inside the offered range 1 to 89,
so the case arises in play.

A new gang has no equipment, no action, no recurring action and no targets,
is visible only to its owner, and starts with the definition's own
statistics, with neither site bonuses nor items: its effective statistics are
rebuilt before the next planning phase like everyone's (FND-GANG-001). The
definition field at `+0x82`, which FMT-DATA-002 calls the Tech Level, is not
a statistic of the gang record.

The search uses roster slots 0 to 79. Slot 80 is never filled by a hire, so a
player can hold at most 80 hired gangs.

## Alternatives

None known.

## How to reproduce

In `fn_00472775`, read from the count test at `0x004759A8` to the order store
at `0x00475D29`. The displacement `0x4A287A`, indexed by the definition times 39 times 4, is the
price; the loop limit 80 is at `0x00475C0E`.
