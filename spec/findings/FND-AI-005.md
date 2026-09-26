---
id: FND-AI-005
title: The shared AI sector selector scores sectors by mode in growing squares, and scenario standings rank players
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408642
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047712A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00476857
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A2790..0x004A27A8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABC08..0x004ABC0E
tool: Ghidra 12.1.3
environment: null
---

## Observation

`0x00408642` is the sector-target routine the family handlers share. It takes
the active player, a mode and the active gang.

Mode 0 picks one of the eight neighbours at offsets -9, -8, -7, -1, +1, +7, +8
and +9 with calls of the bounded wrapper, rejecting results that wrap past a
row or leave the city.

For other modes it clears an 8-by-8 integer score map, gets the gang's current
sector through selector `0x5A`, and examines clipped squares of growing radius
around it, from 1 to 7. Each radius rescans the whole square, not only its
edge, and the search stops after the first radius at which any candidate
scores. The current sector is removed before the final choice. Modes 1 to 5
score:

- mode 1: a neutral sector +1 when selector `0x2C` says the gang can take it
  by Control on its own;
- mode 2: a sector the player owns +1;
- mode 3: a sector another player owns +1;
- mode 4: +1 when the player-pair test `0x2D` accepts its owner;
- mode 5: a neutral sector the gang can take by Control on its own +5, an owned
  sector with no gang whose previous action is Chaos (selector `0x5B` is 0) +2,
  and a sector owned by another player +1.

Selector `0x32` counts the players whose controller type is 0 or 3. Selector
`0x2D` compares two players' positions in a six-byte player-order table,
rejecting neutral and self comparisons. Selector `0x2E` returns the one player
whose standing byte at `0x004ABC08` is 0, or -1 when no player or several
players have 0. Selector `0x5E` counts one player's occupied gang records in a
sector.

`0x0047712A` builds the scenario score at `0x004A2790`, then sets each active
player's byte at `0x004ABC08 + player` to the number of players with a
strictly greater score, and each inactive player's byte to `0xFF`. While the
standings are counted, the inactive players' scores hold -32000. The score is
cash in scenario 0; the count of owned sectors in scenarios 1, 5 and 9; the
summed current Support in scenario 2; in scenario 3, a numerator scaled by the
match length and then divided (signed, in integers) by 10; in scenarios 4 and
7 every active player gets the count of inactive player slots; in scenario 6,
the count of the six generated headquarters sectors the player owns. In
scenario 8 the scorer does not reset the score: it adds one for each of the
sectors 27, 28, 35 and 36 the player owns now.

The end-of-turn evaluator `0x00476857` calls `0x0047712A` before it tests the
count of active players and the scenario's end condition. It ends the match at
once when exactly one of the six active-state bytes is nonzero, before the
scenario switch. The Player Ranking panel (`DATA/PX08/PX05011`) path at
`0x004518D9` reads the score and standing arrays directly. The awards and
statistics path `0x0042CE61` visits standings 0 to 5 in order, player slots 0
to 5 within each standing, and then the inactive (`0xFF`) players in slot
order.

## Interpretation

Nonzero modes pick the nearest sectors of the kind the mode wants, and mode 5
prefers free land to own land to enemy land at 5 to 2 to 1. Selector `0x2E`
gives the unique current leader of the scenario. The same score and standing
table drives the end of the match, the ranking panel and the order of the
endgame screen, so ties share a place and eliminated players come last.

## Alternatives

FND-MOVE-001 describes a mode 0 call from the Move-capacity repair that scores
nothing, draws once among all 64 tied sectors and routes one step toward it;
that contradicts the neighbour draw described here. Which reading of mode 0 is
right, and how many draws the neighbour pick makes when it rejects a result,
is open. The six-byte player-order table selector `0x2D` reads has no address
here, and the Dominance numerator is not written out. An active player whose
score is below -32000 keeps a low place even though inactive rows are drawn
last.

## How to reproduce

List the references to `0x00408642` (48 direct calls, FND-AI-028). Inside it,
the mode 0 block holds the eight offsets and the wrapper calls; the nonzero
path clears 64 integers and loops the radius from 1 to 7. The scorer and the
standing loop are in `0x0047712A`; the evaluator `0x00476857` calls it first.
