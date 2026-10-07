---
id: RULE-AI-008
title: A computer player ranks its three hire offers by the mode of its hire role
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-064, FND-EXE-004, EXP-TURN-003, FND-EQUIP-006, FND-HIRE-001, FND-HIRE-002, FND-HIRE-006, FND-PLATFORM-003, FND-UPKEEP-001]
conflicting: []
split_with: []
related: []
---

## Summary

A computer player looks at its three hire offers and picks the one that best
fits the job it is hiring for: cheap gangs, or gangs strong in Control,
Influence, fighting, Tech Level and Research, or Stealth. If it cannot pay for the one it picks, it hires nothing
this turn.

## When it runs

When RULE-AI-010 calls `rank_offer`, at the end of a computer player's
planning pass.

## Parameters

None.

## Inputs

`hire_offers`, `gang_definitions` (`hire_cost`, `upkeep`, `chaos`, `control`,
`influence`, `combat`, `strength`, `blade`, `range`, `fighting`,
`martial_arts`, `tech_level`, `research`, `stealth`), `cash` and `scenario`.

## Procedure

```text
# The offer slot the player hires with ranking mode mode, or -1
define rank_offer(player, mode):
    if mode == 0 and cash[player] > 200 and scenario != 0:
        mode = 3
    let choice = -1
    let best = 0
    if mode == 0:
        best = 3
    if mode == 5:
        best = 10
    for k in 0..3:
        let d = gang_definitions[hire_offers[player * 3 + k]]
        if mode == 0:
            if d.upkeep <= best and d.chaos >= 0:
                best = d.upkeep
                choice = k
        else if mode == 1 or mode == 2:
            let v = d.control
            if mode == 2:
                v = d.influence
            if v >= best and (scenario != 0 or d.upkeep <= 3):
                best = v
                choice = k
        else if mode == 3:
            let v = d.combat + max(d.strength, 0) + max(d.blade, 0) + max(d.range, 0)
            v = v + max(d.fighting, 0) + max(d.martial_arts, 0)
            if v >= best:
                best = v
                choice = k
        else if mode == 4:
            let v = d.tech_level + d.research
            if scenario == 0:
                if d.upkeep <= 4 and d.research >= 0 and d.tech_level > 3 and v >= best:
                    best = v
                    choice = k
            else if d.research >= 0 and v > best:
                best = v
                choice = k
        else if mode == 5:
            if d.stealth >= best:
                best = d.stealth
                choice = k
    if choice == -1:
        return -1
    if gang_definitions[hire_offers[player * 3 + choice]].hire_cost > cash[player]:
        return -1
    return choice
```

## Outputs

Returns the offer slot, 0 to 2, or -1 when no offer qualifies or the chosen
offer costs more than the player's cash. Changes no state and makes no draw.

## Edge cases

An equal value from a later offer replaces an earlier one in every mode except
mode 4 outside Greed, where the first of the best offers wins. A player with
more than 200 cash outside Greed ranks mode 0 requests by fighting strength
instead. An unaffordable winner is not replaced by the next-best offer.

EXP-TURN-003 reaches mode 0 with an offer whose Chaos is at least 0 and whose
Control is below 0: the original hires it.

## What the sources say

SRC-MANUAL-GOG does not describe how computer players choose whom to hire.

## Differences between builds

None known.

## Open questions

- The field compared with cash is `hire_cost`; that it is the price is
  inferred from the comparison with cash and from the hire resolution
  (FND-AI-064, FND-HIRE-006).
- Whether a winner costing exactly the player's cash is hired (`> cash`
  fails) is assumed.
- The starting best of mode 4 is not recorded; 0 is assumed. The starting
  bests of modes 1 to 3 (0), 0 (3) and 5 (10) are recorded.
- What the helper does with an offer slot that holds no gang (a negative
  element of `hire_offers`) is not recorded; offers are refilled at each
  planning entry, before this rule runs.
- No recorded run requests mode 5 (Stealth) or runs mode 1 in Greed. These
  rest on FND-AI-064. Until a run reaches them, the entry stays `supported`.
