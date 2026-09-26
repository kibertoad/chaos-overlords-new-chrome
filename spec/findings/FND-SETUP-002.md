---
id: FND-SETUP-002
title: On Begin, local setup turns every empty slot into a computer player with a random unused portrait and that portrait's name
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040E0A0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00468C8E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046D1F7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00466673
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AB638
tool: Ghidra 12.1.3
environment: null
---

## Observation

The full local setup handler `0x0040E0A0` keeps six player-type dwords at
`0x004AB638`: -1 for an empty setup slot, 0 for a local human and 1 for a
computer player. When Begin is accepted it scans slots 0 to 5 in order. Each
slot holding -1 is set to 1, is given a portrait by `0x00468C8E`, and is given
the default name of that portrait by `0x0046D1F7`.

The portrait helper calls the bounded random wrapper `0x0045D227` with 15,
subtracts one, and draws again while any of the six slots already has that
portrait. Portrait 15 is the image of an empty slot and the helper never
returns it.

`0x0046D1F7` takes the name from the executable's string table, resource
`Chaos Overlords.exe#STRING/62` for portrait 0 up to `#STRING/76` for
portrait 14, and passes an exact length of 10 to `0x00466673`, which copies it
into the player's 12-byte name record.

All six slots are therefore filled before the fresh-game initializer
`0x0046DC10` generates the city, the headquarters permutation and the six
Right Hands.

## Interpretation

The number of players chosen on the local setup screen is the number of local
humans. Every slot the player leaves empty becomes a computer player, in
ascending slot order, and its portrait draws are the first random draws of the
new match (FND-RNG-005).

## Alternatives

What an empty slot's portrait byte holds while the helper runs (15 is the
likely value) is not recorded, nor the address of the portrait array. Whether
the 10-character copy pads or terminates a shorter name is not recorded.

## How to reproduce

In `0x0040E0A0`, find the Begin branch that loops over the six dwords at
`0x004AB638`, compares each with -1, stores 1 and calls `0x00468C8E` and
`0x0046D1F7`. In `0x00468C8E` find the call to `0x0045D227` with 15, the
subtraction of one and the loop over six portrait bytes. In `0x0046D1F7` find
the string resource load at 62 plus the portrait and the call to `0x00466673`
with 10.
