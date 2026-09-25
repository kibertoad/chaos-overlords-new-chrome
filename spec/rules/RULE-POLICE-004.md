---
id: RULE-POLICE-004
title: Crackdown reports go to the players who had a gang in the sector when resolution began
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-CHAOS-002, FND-EXE-004, FND-POLICE-002]
conflicting: []
split_with: []
related: [FMT-STATE-001]
---

## Summary

At the start of resolution the game notes which players have a gang in each
sector. When a sector cracks down later in the turn, each of those players is
told, whether or not they took part in the Chaos, and nobody else is. The
owner who loses a sector to a third Crackdown is told separately
(RULE-POLICE-002).

## When it runs

At the start of `resolution`, before `instant_phase`, in the resolver
`fn_00472775` at `0x0047281C..0x004728E1` (range in FND-EXE-004)
[FND-POLICE-002, FND-CHAOS-002].

## Parameters

None.

## Inputs

Each gang's `sector`.

## Procedure

```text
for i in 0..384:
    sector_presence[i] = 0
for each player in turn_order:
    for slot in 0..81:
        let g = gangs[player * 81 + slot]
        for s in 0..64:
            if g.sector == s:
                sector_presence[s * 6 + player] = 1
```

## Outputs

No return value. Fills `sector_presence`, which RULE-CHAOS-001 reads to choose
the recipients of `CrackdownReport`. No draws.

## Edge cases

- An inactive gang's sector is 100 and matches no sector.
- A gang that moves or dies later in the turn still counts, and a gang hired
  later in the turn does not; the table is built once, before any action.

## What the sources say

None known. The manual does not say who is told about a Crackdown.

## Differences between builds

None known.

## Open questions

None known. The table is a local byte array of the resolver, indexed
`sector * 6 + player` [FND-CHAOS-002].
