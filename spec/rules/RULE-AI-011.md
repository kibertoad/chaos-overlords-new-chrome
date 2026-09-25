---
id: RULE-AI-011
title: A computer player tries to hire only below a gang limit and outside each scenario's closing turns
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-012, FND-AI-009]
conflicting: []
split_with: []
related: [RULE-AI-004, FMT-STATE-001, FMT-STATE-002]
---

## Summary

A computer player hires only while it has fewer gangs than its territory can
use. While free land remains, the limit is one and a half, two or four gangs
per owned sector depending on the scenario; once the city is taken, a rich
player may fill its roster. Greed stops hiring in the last eighth of the match,
and Power, Acceptance and Dominance in the last two turns.

## When it runs

When RULE-AI-010 calls `hire_allowed`, at the end of a computer player's
planning pass.

## Parameters

None.

## Inputs

`gangs` (`sector`), `sectors` (`owner`, `crackdown_turns`), `cash`,
`scenario`, `turn_limit` and `elapsed_turns`.

## Procedure

```text
define active_gangs(player):
    let n = 0
    for slot in 0..81:
        if gangs[player * 81 + slot].sector != GANG_INACTIVE:
            n = n + 1
    return n

define hire_limit(player):
    let owned = 0
    let free_land = false
    for s in 0..64:
        if sectors[s].owner == player:
            owned = owned + 1
        if sectors[s].owner == SECTOR_NEUTRAL and sectors[s].crackdown_turns == 0:
            free_land = true
    let limit = 0
    if not free_land:
        if cash[player] > 300:
            limit = 80
        else:
            limit = active_gangs(player) + owned
    else if scenario == 0:
        limit = INT32(owned * 1.5)
    else if scenario <= 3:
        limit = owned * 2
    else:
        limit = owned * 4
    return min(limit, 80)

define hire_allowed(player):
    if scenario == 8:
        return true
    if active_gangs(player) > hire_limit(player):
        return false
    if scenario == 0:
        return turns_remaining() > turn_limit / 8
    if scenario >= 1 and scenario <= 3:
        return turns_remaining() > 2
    return true
```

## Outputs

`hire_allowed` returns true when the player may try to hire this turn.
`hire_limit` returns the limit, 0 to 80. Makes no draw and changes no state.

## Edge cases

A player at exactly its limit may still hire, so it can reach one gang above
the limit. In Big Man there is no limit. `owned * 1.5` is computed in floating
point and truncated, so one owned sector gives a limit of 1 and three give 4.

## What the sources say

SRC-MANUAL-GOG does not describe it.

## Differences between builds

None known.

## Open questions

- Which count the gates call "active gangs" is inferred: roster slots whose
  sector is not 100.
- The "not under a Crackdown" test of selector `0x22` is taken to be a zero
  Crackdown byte.
- `turn_limit` has no recorded address (see the glossary).
- The comparison `active gangs <= limit` is recorded as inclusive for every
  gated scenario.
