---
id: FND-AI-017
title: The planner always passes an encoded sector to the hire destination helper, with two kinds of override
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
    address: 0x00408214
tool: Ghidra 12.1.3
environment: null
---

## Observation

All 18 direct references to `0x00408214` are inside `0x00458FA0`.

Ten of them, at `0x00459BC8`, `0x0045A08C`, `0x0045A55C`, `0x0045AAFE`,
`0x0045AF7E`, `0x0045B3FE`, `0x0045B671`, `0x0045BA56`, `0x0045BC23` and
`0x0045BFE9`, pass the encoded placement anchor (FND-AI-010) as the mode, so
each takes the `>= 0x40` path.

Seven, at `0x00459BB1`, `0x0045A075`, `0x0045A545`, `0x0045AAE7`,
`0x0045AF67`, `0x0045B3E7` and `0x0045BFD2`, belong to the hire-role-4 paths of
scenarios 0 to 5 and 9. They pass instead the first sector returned by
selector `0x9A` (the first sector holding a visible hostile human gang,
FND-AI-013), plus `0x40`.

One, at `0x0045BA3F`, belongs to hire-role slots 5 and 7 of scenario 7 and
passes the Right Hands' sector plus `0x40`.

The turn resolver `0x00472775` uses the written destination during its hire
step.

## Interpretation

AI hiring never uses a random placement. The planner reduces the choice to one
encoded sector: the anchor, or for a family-6 hire the first hostile human
sector, or in Siege on those two slots the Right Hands' sector.

## Alternatives

When selector `0x9A` finds no sector it returns 100, and 100 plus `0x40` is
164. Whether the override call is skipped then, or passes 164 so that the
destination is sector 100, is not recorded.

## How to reproduce

List the references to `0x00408214`; the addresses above are the call sites
inside `0x00458FA0`. Each override site adds `0x40` to the selector-`0x9A`
result or to the Right Hands' sector a few instructions before its call.
