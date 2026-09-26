---
id: FND-MOVE-006
title: The Move repair loop has no bound, and some order sets keep it running for ever
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00476A94..0x00476F3A
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges of the functions named here are in FND-EXE-004. The loop is the one
FND-MOVE-003 describes in `fn_00476A94(player)`.

- The loop's only control is the flag byte set to 1 at `0x00476AA0` and tested
  at `0x00476AC0`. Each round clears it at `0x00476AC8` and sets it at
  `0x00476C7F` when the scan finds a sector whose two counts sum above 6. No
  counter, limit or timer is read anywhere in the function, and it has no exit
  other than the return at `0x00476F3A` after a round that found no such
  sector.
- A round changes state only through one of the three stores into a mover's
  offset `0x08` byte (`0x00476D96`, `0x00476EB8`, `0x00476F03`). All three sit
  inside loops over the mover list that match only a mover whose offset `0x08`
  byte equals the crowded sector (`0x00476CD4`, `0x00476E0C`). When the crowded
  sector's count is made only of gangs that are not moving, both loops match
  nothing, the round stores nothing, and the next round recounts the same
  numbers.
- The first repair loop tests the mover's own sector with the projected count
  (both arrays, in which the mover itself counts at its destination) against
  6 at `0x00476D3F`. The fallback takes the first mover in roster order whose
  destination is the crowded sector, with no test of its source.

## Interpretation

The loop ends only when a recount finds no sector above six, and nothing
bounds the number of rounds. It can run for ever in two ways.

- A sector holding more than six of the player's gangs that are not moving is
  never repaired and the loop never ends. The Hire check keeps a sector at six
  (RULE-HIRE-001), so this needs a state normal play does not reach.
- A mover can be passed between its own sector and a neighbour for ever. The
  first loop skips a mover whose source already counts six, and the fallback
  always takes the earliest mover, so if the earliest mover into each of these
  sectors is the same gang, it is sent back to its source, then given a random
  neighbour, then sent back again. When every neighbour it can draw also
  counts six and every other mover into it comes from a sector counting six,
  every draw leads back to the same state.

An example for one player, with sectors numbered row times 8 plus column and
gang M earlier in the roster than every other mover. Sector 0 holds five
gangs that stay and M, ordered to Move to 8. Sector 1 holds five that stay and
N, ordered to 0. Sector 2 holds five that stay and Q, ordered to 1, and one
gang in sector 3 is ordered to 2. Sector 8 holds five that stay, and F in
sector 16 is ordered to 8. Sector 16 holds five that stay besides F, and one
gang in sector 24 is ordered to 16. Sector 9 holds six that stay. The first
round finds sector 8 at seven, skips M (sector 0 counts six) and F (sector 16
counts six), and sends M back to 0. Sector 0 then counts seven; N's source
counts six, so the fallback takes M, whose destination is its own sector, and
draws 1, 8 or 9. Each of them now counts seven, the other mover into it (Q, F
or none) comes from a sector counting six, and the fallback sends M back to 0.
The state repeats whatever is drawn. This example has Moves into sectors that
already hold six of the player's gangs. The same cycle can be built with every
Move going into a sector holding at most five, by keeping each sector at a
projected six with four stayers and two incoming Moves from sectors that
nothing reaches; it takes about 55 gangs.

The random neighbour draw inside the selector (FND-MOVE-003) ends with
certainty, since every sector has at least three neighbours on the map.

## Alternatives

- The Move panel or the computer players may refuse the orders the example
  needs. Whether the panel refuses a sector that already holds six is left
  open by SCR-MOVE-001; the second construction does not depend on it. The
  computer players' order writers were not checked against it.
- The hang has not been produced in a run of the original.

## How to reproduce

In `fn_00476A94`, find the flag stored at `0x00476AA0` and tested at
`0x00476AC0`, and confirm that nothing else leaves the loop. Follow the three
stores into offset `0x08` and the tests of the crowded sector that guard them.
Work the example through the recount at `0x00476B61..0x00476C2F` by hand.
