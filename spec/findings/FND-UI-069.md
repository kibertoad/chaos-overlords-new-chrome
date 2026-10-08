---
id: FND-UI-069
title: The gang card draws its action strip only for the active player's gang while a match is in play
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00410130
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00410283
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00410291
tool: Ghidra 12.1.3
environment: null
---

## Observation

The card compositor `fn_00410130(player, slot)` composes the card of gang
record `0x00498DA8 + player * 0xA20 + slot * 0x20`. Its rectangles go through
`fn_00425EDF`, which packs `(top, left, bottom, right)`, and its copies through
`fn_0042773E(destination, source, source rectangle, destination rectangle)`.
In order it:

1. copies the card frame from sheet `(162,15)-(236,125)` to card
   `(0,0)-(74,110)`;
2. reads the `force` byte (record offset 3) and copies sheet
   `(354,0)-(354 + 6*force, 3)` to card `(7,3)-(7 + 6*force, 6)`;
3. reads the `action` byte (record offset 7), then at `0x00410283` compares
   `player` with the active player `0x004ABC84` and at `0x00410291` tests the
   no-match byte `0x004ABC9C` (FND-STATE-010). Only when `player` is the active
   player and the byte is 0 does it copy sheet
   `(162, 125 + 9*action)-(226, 134 + 9*action)` to card `(5,8)-(69,17)`.
   Otherwise both jumps go to `0x0041032E`, past the copy;
4. goes on with the portrait and the equipment icons as FND-UI-036 records,
   with no further test of `player`.

The detailed sector compositor `fn_00410770` passes its own player argument,
the player whose gangs the cards list (FND-UI-015, FND-UI-018), as `player`.

## Interpretation

A card shows its gang's action only on the active player's own cards during
a match. After a press on another overlord's portrait, the cards of that
overlord's gangs keep the blank band of the card frame where the strip would
be, so the original shows no other player's orders on this screen. In the
final view given after the match has ended, when `0x004ABC9C` is set again,
the active player's own cards have no strip either.

The strip sits at card offset `(5,8)` and the Force meter at `(7,3)`, which
SCR-UI-004 had measured from the art.

## Alternatives

None. Both tests are plain compares against the globals, with no other path
to the copy.

## How to reproduce

In `0x00410130`, find the multiply of the byte at `0x00498DAF` by 9 and the
add of `0x7D`. The two conditional jumps before it, after the compare with
`[0x004ABC84]` and the test of `[0x004ABC9C]`, both go to `0x0041032E`.
