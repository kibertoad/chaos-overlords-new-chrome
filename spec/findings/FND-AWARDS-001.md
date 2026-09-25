---
id: FND-AWARDS-001
title: The award builder takes five categories in a fixed order with fixed starting thresholds and keeps every tied player, but only three awards per row are drawn
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042B9E0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042CE61
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A27A8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A5ED8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A25D0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0049CA78
tool: Ghidra 12.1.3
environment: null
---

## Observation

The award builder `0x0042B9E0` scans all six player slots for each category,
without reading the players' active bytes. It takes the categories in the
order Fist, Skull, Big Fat Chicken, Dollar Sign, Safe.

The first three start their running maximum at 5 for Overthrows
(`0x004A27A8`), 50 for damage inflicted by the player's own attacks
(`0x004A5ED8`) and 10 for Hide actions carried out (`0x004A25D0`); a player
whose value is below the starting maximum gets nothing. Dollar Sign uses Cash
Spent (`0x0049CA78`) with a running maximum that starts at 0. Safe uses the
same array with a running minimum that starts at 999,999. After finding the
extreme value, a second pass writes the award to every player whose value
equals it, in ascending slot order.

The per-player award table can hold all five awards, but the renderer
`0x0042CE61` reads only the first three entries of each displayed player's
row.

## Interpretation

Fist goes to the most Overthrows, Skull to the most damage, Big Fat Chicken to
the most hiding, Dollar Sign to the most cash spent and Safe to the least. A
player at exactly the starting threshold qualifies, since the second pass
compares for equality with a maximum that stays at the threshold. Ties all get
the award. Eliminated players are eligible. A player who earns four or five
awards is shown only the first three, in the builder's order.

## Alternatives

The address and layout of the per-player award table and the codes it stores
for each category are not recorded. The first pass's comparison is read as
strictly greater (strictly less for Safe); a greater-or-equal comparison gives
the same result. An earlier reading, from the manual, gave no award at zero
activity and showed five icons; the executable shows otherwise.

## How to reproduce

In `0x0042B9E0`, find the five category blocks, their starting constants 5,
50, 10, 0 and 999,999 (`0xF423F`), their loops over six slots reading the four
arrays named above, and the equality pass that appends to the award table. In
`0x0042CE61`, find the loop that reads three award entries per row.
