---
id: FND-OBJECTIVE-004
title: Each round marks eliminated local humans, walks the slots, resolves the turn, and ends the match on the evaluator's flag or when no local human is left
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046F344..0x0046F930
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABBD4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABBE0..0x004ABBE5
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the outer match loop `0x0046E766`, one round runs, after the start-of-round
refresh and the call of `0x0046FA11` (`0x0046F344`), in this order:

1. Marking (`0x0046F350`). For each slot 0 to 5 whose type dword at
   `0x004AB638` is 0: if its active byte at `0x004ABBE0` is set it is counted;
   if the byte is 0 the type is set to -2 and it is counted as well. The byte
   `0x004ABC98` becomes 1 when the count is above 1. When the count is 0 the
   loop's end byte `0x004ABC90` is set (`0x0046F40A`, `0x0046F42A`); in a
   network session (`0x00482178`) `0x00421A2D` is called with 0 first.
2. Walk (`0x0046F459`). While `0x004ABC90` and the quit byte `0x00487828` are
   both 0, for each slot in order that is active or has type -2: type 1 plans
   through `0x004716EB` and `0x00458FA0`; type 0 shows the handoff card
   `0x004396C0` when `0x004ABC98` is set and then enters `0x0046FD80`; type -2
   shows the handoff card under the same test, then the elimination card
   `0x0042C3F5`, sets the type to -1 (`0x0046F597`), and then calls the music
   selector `0x004642BD` with 2 once for every later slot whose type is 0
   (`0x0046F5A5` to `0x0046F5CE`), not at all when there is none.
3. The byte `0x00487B98` is cleared (`0x0046F67E`).
4. Resolution. The slots of type 0 are counted again, and the count is taken
   as 1 in a network game (`0x00487B58`). Only when it is not 0, and neither
   `0x00487828` nor `0x004ABC90` is set, the loop calls `0x004726C0`, which
   runs the whole-turn resolver `0x00472775`; the resolver calls the end
   evaluator `0x00476857` at `0x00475F61`.
5. End. When `0x004ABBD4` is set after resolution (`0x0046F712`), the network
   bytes are cleared, `0x004ABC9C` and `0x004ABC60` are set, the sectors and
   gangs are refreshed, and each slot of type 0 in order gets the handoff card
   (under `0x004ABC98`), then the elimination card when it is inactive, or
   `0x0046FD80` with `0x0048780C` set when it is active. Then the awards
   controller `0x0042B9E0` runs and `0x004ABC90` is set.
6. `0x0049CA68` is increased by one (`0x0046F930`), inside the same test as
   step 4.

The loop repeats while `0x00487828` and `0x004ABC90` are both 0. The active
bytes are set to 1 for all six slots when a new match starts (`0x0046EB91`),
and `0x004ABBD4` is cleared there and at `0x0046E894`.

## Interpretation

`player_retired` is not a separate flag: a retired local human is a slot whose
`controller` has become -1, the value of an empty slot, and -2 marks a local
human who is eliminated and has not yet seen the card. The marking happens at
the start of the round after the one in which the player was eliminated, so
the elimination card appears in the next round's walk at the player's place in
slot order, after the earlier slots have planned.

The music request after the card is repeated once per later local human and
is skipped when the eliminated player was the last local human in slot order.

A local game ends when no local human is left: the round in which the last
one retires still lets the later computer players plan, then the recount finds
no slot of type 0 and resolution is skipped, and the next round's marking sets
the end byte. No awards are shown in that case. A match ended by the
evaluator shows every local human a final view, then the awards.

## Alternatives

What `0x0046FD80` does differently when `0x0048780C` is set is not recorded
here. With no active player at all the evaluator does not set the flag, and
the round loop then runs on while a local human remains; whether that state
can arise is not recorded.

## How to reproduce

In Ghidra, open `0x0046E766` and read from the call of `0x0046FA11` at
`0x0046F344` to the increment at `0x0046F930`. List the references to
`0x004AB638` with the constants -2 and -1 in that range.
