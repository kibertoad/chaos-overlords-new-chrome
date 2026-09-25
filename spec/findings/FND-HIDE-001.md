---
id: FND-HIDE-001
title: A gang is hidden exactly while its active action is Hide, with no separate hidden flag
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E766
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041462F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00414D8C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498DA8..0x0049CA68
tool: Ghidra 12.1.3
environment: null
---

## Observation

Each 32-byte gang record keeps its active action at offset `+7` (`0x00498DAF`
for the first record) and its recurring action at offset `+10` (`0x00498DB2`).
Hide is action 8.

In the outer turn function `fn_0046E766`, before planning, a loop over all six
players and all 81 roster slots checks the recurring actions and copies `+10`
into `+7` (FND-TURN-004).

The sector-wide command handler `fn_0041462F` writes both fields when an order
is given; its None choice writes 0 to both. The individual command handler
`fn_00414D8C` writes 0 to `+10` for a one-off choice and writes the chosen
action to `+7` (FND-TURN-002).

The combat and police code tests the same byte `+7` against 8 to decide whether
a gang is hiding.

## Interpretation

The gang record has no hidden flag of its own: a gang is hidden while its
active action is Hide. Giving the Hide order makes the gang hidden at once,
during planning. At the next turn start a one-off Hide is replaced by the
recurring action, which is None, while a recurring Hide is copied back and the
gang stays hidden. Replacing or cancelling Hide during planning changes the
active action at once and the gang stops hiding. Resolution counts a Hide every
turn a gang's action is Hide, recurring or not.

## Alternatives

None known. Whether other code reads a gang's Hide state through anything but
byte `+7` has not been searched for beyond the combat and police consumers.

## How to reproduce

In `fn_0046E766`, find the nested loops over 6 players and 81 gang records that
run before the planning loop; they end each pass by copying the byte at record
offset `+10` into `+7`. In `fn_0041462F` and `fn_00414D8C`, find the writes to
`+7` and `+10`. In the whole-turn resolver `fn_00472775`, find the comparisons
of a gang record's `+7` byte with 8 in the combat and police code.
