---
id: FND-RESEARCH-001
title: Research rolls only while the item still needs research, and an earlier gang's completion stops later rolls in the same phase
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
tool: Ghidra 12.1.3
environment: null
---

## Observation

Inside the instant-phase switch of the whole-turn resolver `0x00472775`, the
case for action 11 (Research) reads the player's remaining research for the
selected item from the array at `0x004A2608` (element `item * 6 + player`).
It enters the dice calculation only while that value is not 0. After the dice,
it subtracts the gang's successes from the value at once and clamps it at 0,
before the scan goes on to the next gang. The case sits inside the scan of
player slots 0 to 5 and roster slots 0 to 80.

## Interpretation

Several gangs may research the same item in one turn. Once an earlier gang,
in player and roster order, brings the item to 0, every later gang researching
it makes no roll and uses no random draws. The order the orders were given in
does not matter.

## Alternatives

None known. The size of the dice pool and the success threshold are recorded
in FND-AI-007 and FND-GANG-001.

## How to reproduce

In `0x00472775`, find the instant-phase switch; case 11 indexes `0x004A2608`
with `item * 6 + player` and tests the byte against 0 before calling the dice
helper `0x00475F70`. The instruction addresses of the case have not been
written down.
