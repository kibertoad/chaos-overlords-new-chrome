---
id: FND-FINANCE-002
title: The Financial panel sums eight amounts from the queued orders, hires and owned sectors; the Sector variant opens PX05019 when a sector is passed and limits every sum to that sector
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044D1C7..0x0044E280
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_0044D1BB(player, sector)` (range in FND-EXE-004) clears its eight sums
(`0x0044D1C7..0x0044D203`) and branches on `sector == -1` at `0x0044D20F`.

City variant (`sector` is -1). It loads resource `0x1390` (5008,
`0x0044D262`), then:

- for each of the player's three hire slots whose queued sector byte at
  `0x004A27C8 + player * 3` is 0 or more (`0x0044D32B`): subtracts the 16-bit
  definition field at offset `0x7A` of the queued gang (`0x004A287A`) from the
  recruits sum and the field at offset `0x7C` (`0x004A287C`, Upkeep) from the
  upkeep sum, and adds one to the gang count;
- for each roster record whose sector byte is not 100 (`0x0044D3BE`): adds
  one to the gang count and subtracts the definition's Upkeep from the upkeep
  sum, then by action (`0x0044D419`):
  - 2 (Bribe): subtracts 3 from the officials sum;
  - 3 (Chaos): computes `(income + chaos + force) / 3` from the byte at
    offset `0x04` of the gang's sector record and the gang's bytes at offsets
    `0x16` and `0x03`; when it is above 0 (`0x0044D681`) it is halved when the
    sector's owner is not the player (`0x0044D6BB`) and added to the Chaos
    sum (`0x0044D6C4`);
  - 5 (Equip): subtracts the Cost of the item in offset `0x08`, reduced to
    `Cost - Cost / 3` when the gang's sector has a nonzero byte at offset
    `0x0E` and is owned by the player, from the equipment sum (`0x0044D4F0`);
  - 12 (Sell): adds `Cost / 2` of each item selected by bits 1, 2 and 4 of
    offset `0x08` to the equipment sum (`0x0044D54D`, `0x0044D5AD`,
    `0x0044D60D`);
  - 14 (Terminate): adds the Upkeep back (`0x0044D6F8`) and takes the gang
    out of the count;
- for each of the 64 sectors owned by the player: adds one to the tax sum
  (`0x0044D78F`) and, for each site slot whose progress byte has reached the
  16-bit Resistance of its site definition (`0x004AB67E + site * 0x3E`), adds
  the site's 16-bit Cash field (`0x004AB686`, site offset `0x1E`) to the
  protection sum (`0x0044D808`).

Sector variant (`sector` from 0 to 63). It loads resource `0x139B` (5019,
`0x0044D86A`), draws the sector's picture and its name as a column letter `A`
plus `sector % 8` and a row digit `1` plus `sector / 8` at buffer
`(0x18C,0xD8)` (`0x0044DA31`), then applies the same sums restricted to the sector:

- only hires queued for this sector (`0x0044DA62`);
- a gang whose action is 10 and whose offset `0x08` byte is this sector adds
  one to the count and subtracts its Upkeep (`0x0044DB18..0x0044DB6D`), before
  the test on its own sector;
- only gangs whose sector byte is this sector (`0x0044DB73`) enter the action
  switch, which is the City switch with three differences: action 10 adds the
  Upkeep back and takes the gang out of the count (`0x0044DEAC`); the Chaos
  estimate has no test for a value above 0 before the halving (`0x0044DE38`);
  and the Factory test reads the panel's sector (`0x0044DC01`);
- the tax and protection sums are taken from this sector only, when the
  player owns it (`0x0044DF2E`, `0x0044DFA7`).

Both variants then draw, at buffer x `0x262` (screen x 394), with width 4:

| Buffer y | Screen y | Sum |
|---|---|---|
| `0xAB` | 151 | upkeep, with the gang count at buffer x `0x214` (screen x 316) followed by a closing parenthesis (`0x0044DFC0..0x0044E0C0`) |
| `0xB4` | 160 | recruits (`0x0044E0FA`) |
| `0xC6` | 178 | equipment (`0x0044E137`) |
| `0xD8` | 196 | officials (`0x0044E174`) |
| `0xEA` | 214 | tax (`0x0044E1B1`) |
| `0xF3` | 223 | protection (`0x0044E1EE`) |
| `0x105` | 241 | Chaos (`0x0044E22B`) |
| `0x120` | 268 | the total of the seven sums above, the Chaos sum included (`0x0044E261`, `0x0044E280`) |

## Interpretation

Read with the manual's row order (Gang Upkeep, New Recruits, Equipment, City
Officials, Sector Tax, Site Protection, Chaos (Estimate), Cash Adjustment), the
rows are:

- Gang Upkeep: minus the Upkeep of every active gang that is not terminating
  and of every queued hire; the parenthesised number on this row is that
  count of gangs, hires included.
- New Recruits: minus the definition field at `0x7A` of each queued hire,
  the value the computer players use as the hiring price.
- Equipment: minus the Factory-adjusted price of each Equip, plus half the
  Cost of every item selected for Sale. The estimate pays for every selected
  item, which the resolver does not (BUG-SELL-001).
- City Officials: minus 3 for each Bribe.
- Sector Tax: 1 for each owned sector.
- Site Protection: the Cash of the completed sites in owned sectors.
- Chaos (Estimate): a third of `income + chaos + force` for each Chaos gang,
  halved outside the player's own sectors.
- Cash Adjustment: the sum of the seven rows.

The Sector variant is picked by the caller passing a sector instead of -1. It
counts the gangs standing in the sector and those moving into it, and gives
back the Upkeep of gangs moving out.

## Alternatives

- The row labels are in the template images, which were not read; the
  pairing with the manual's order rests on the eight rows being drawn from top
  to bottom in that order.
- The Sector variant's Chaos estimate may add a negative value; whether a
  gang's `income + chaos + force` can be negative in play was not checked.

## How to reproduce

Open `fn_0044D1BB`. The branch on the second argument against -1 selects
`0x1390` or `0x139B`. Follow the hire slot loop over `0x004A27C8`, the roster
switch on the action byte and the sector loop over `0x004A08E8`, then the
eight calls of `fn_00414187` with width 4 at buffer x `0x262`.
