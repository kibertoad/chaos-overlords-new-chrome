---
id: FND-EQUIP-006
title: Equip checks cash at its place in the transaction pass, not in the picker, and equal cash is enough
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00474998
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00474A05
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00474A0C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00474A22
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00474A35
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00474BF3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047506B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475A0E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475A15
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475CE4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043F136
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043DAD9
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the Equip branch (action 5) of the whole-turn resolver `fn_00472775`:

- `0x00474998` loads the signed item Cost and `0x004749E1` onward applies the
  Factory division (FND-EQUIP-001).
- `0x00474A05` compares the buying player's 32-bit cash word in the array at
  `0x004A25E8` with the adjusted price, and the signed `JL` at `0x00474A0C`
  takes the failure path when cash is lower.
- On success `0x00474A22` subtracts the price from the same cash word and
  `0x00474A35` adds it to the player's cash spent.

Elsewhere in the same resolver, the Sell branch credits the cash word at
`0x00474BF3`, within the same ascending gang scan; the Chaos payout credits it
at `0x0047506B`; and the Hire branch compares the contract cost with the cash
word at `0x00475A0E`, takes the failure path on the signed `JG` at
`0x00475A15`, and debits on success at `0x00475CE4`.

The Equip list builder `fn_0043F136` examines the 64 item records and tests
category, Tech Level, research state, and whether the item is already in one of
the acting gang's three equipment slots. It computes and draws the
Factory-adjusted price but does not read the cash array or compare the price
with cash. The Equip picker `fn_0043DAD9` writes the chosen list entry into the
gang's queued item on keyboard confirmation and on pointer confirmation;
neither branch tests cash or the cost of other queued purchases.

## Interpretation

Items the player cannot afford are listed and can be ordered. A purchase is
paid from the cash the player has when the gang's turn in the transaction pass
comes, so equal cash buys the item. A Sell by an earlier roster slot can pay
for a later Equip; a Sell by a later roster slot, the Chaos payout and the next
Upkeep cannot. Hire runs after the Chaos payout, so all of those except the
next Upkeep can pay for a hire. The list leaves out an item the acting gang
already carries, whether or not it is affordable.

## Alternatives

- Which field of the gang record holds the queued item, and what the failure
  path does besides skipping the purchase, are not recorded.
- The exact comparisons of the list's Tech Level and research tests are not
  recorded.

## How to reproduce

In `fn_00472775`, find the case for action 5 and step from `0x00474998` through
the comparison with the cash array at `0x004A25E8` at `0x00474A05`. Cross-
references to `0x004A25E8` inside the resolver give the Sell, Chaos and Hire
sites. In `fn_0043F136` and `fn_0043DAD9`, no reference to `0x004A25E8`
appears.
