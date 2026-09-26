---
id: FND-AI-012
title: The AI tries to hire only below a gang limit set by territory, cash and scenario, with scenario time gates
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458FA0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00481010
tool: Ghidra 12.1.3
environment: null
---

## Observation

Each scenario branch of the hire switch in `0x00458FA0` is entered only past a
gate:

| Scenario | Gate |
|---|---|
| 0 | active gangs at most the limit, and turns remaining strictly greater than the integer `match length / 8` |
| 1, 2, 3 | active gangs at most the limit, and more than two turns remaining |
| 4, 5, 6, 7, 9 | active gangs at most the limit |
| 8 | no gate |

The limit is computed in this order. Selector `0x22` of `0x00402D70` returns 1
when any sector is neutral and not under a Crackdown; selector `0x23` counts
the sectors the player owns. When no such neutral sector remains, the limit is
80 if the player's cash is strictly above 300 and `active gangs + owned
sectors` otherwise. While such a sector remains, the limit is
`owned sectors * 1.5` truncated to an integer in scenario 0,
`owned sectors * 2` in scenarios 1, 2 and 3, and `owned sectors * 4` in
scenarios 4 to 9. Every result is then capped at 80. The 1.5 is the double at
`0x00481010`; the x87 conversion truncates, and the count is never negative.

## Interpretation

A computer player hires while it has fewer gangs than it can use: up to one and
a half, two or four gangs per owned sector while neutral land remains, and up
to 80 (the roster size) once the city is taken and it is rich. Greed stops
hiring in the last eighth of the match, and Power, Acceptance and Dominance in
the last two turns.

## Alternatives

"Active gangs" is read as the count of the player's roster slots whose sector
is not 100; the selector that counts them has not been named. What "under a
Crackdown" tests (a nonzero Crackdown byte or a positive one) is not recorded.

## How to reproduce

In `0x00458FA0`, before each scenario case of the hire switch, find the calls
to `0x00402D70` with selectors `0x22` and `0x23`, the comparison of cash with
300, the load of the double at `0x00481010`, the multiplications by 2 and 4,
and the cap at 80.
