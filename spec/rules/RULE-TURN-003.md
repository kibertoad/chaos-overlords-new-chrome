---
id: RULE-TURN-003
title: The instant phase carries out Bribe, Heal, Hide, Influence, Research and Snitch gang by gang, then raises every Tolerance below 1 to 1
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TURN-001, FND-TURN-008, FND-SNITCH-001, FND-GANG-001, SRC-MANUAL-GOG, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-BRIBE-001, RULE-HEAL-001, RULE-HIDE-001, RULE-INFLUENCE-001, RULE-RESEARCH-001, RULE-SNITCH-001, RULE-TOLERANCE-002, FMT-STATE-001]
---

## Summary

The instant actions are carried out one gang at a time: the first player's
gangs in roster order, then the second player's, and so on. Each action takes
effect before the next gang acts, so two gangs influencing the same site add to
it one after the other. After all of them, any sector whose Tolerance has
fallen below 1 is set to 1.

## When it runs

At the start of `resolution`, as `instant_phase` (RULE-TURN-002).

## Parameters

None.

## Inputs

`turn_order`, each gang's `action` in `gangs`.

## Procedure

```text
for each player in turn_order:
    for slot in 0..81:
        let gang = gangs[player * 81 + slot]
        if gang.sector == GANG_INACTIVE:
            continue
        if gang.action == ACTION_BRIBE:
            call RULE-BRIBE-001(player, gang)
        else if gang.action == ACTION_HEAL:
            call RULE-HEAL-001(player, gang)
        else if gang.action == ACTION_HIDE:
            call RULE-HIDE-001(player, gang)
        else if gang.action == ACTION_INFLUENCE:
            call RULE-INFLUENCE-001(player, gang)
        else if gang.action == ACTION_RESEARCH:
            call RULE-RESEARCH-001(player, gang)
        else if gang.action == ACTION_SNITCH:
            call RULE-SNITCH-001(player, gang)
call RULE-TOLERANCE-002()
```

## Outputs

No return value. Runs the instant action of each gang in player slot and
roster slot order, with the draws each of those rules makes, and then runs
RULE-TOLERANCE-002, which sets every sector's `tolerance` below 1 to 1.

## Edge cases

An inactive record (`sector` 100) is skipped before its `action` is looked at
(FND-TURN-008).

A gang's action takes effect at once. An Influence gang that reaches a site
after an earlier gang has completed it makes no roll (FND-TURN-001).

The gangs' effective statistics are those rebuilt at the start of the turn. A
site completed during this phase adds nothing to any gang until the next turn
start (FND-GANG-001).

The Tolerance floor is applied once, after every gang has acted, so a sector can
sit below 1 between two instant actions, and Snitch's reduction is never
limited on its own (FND-SNITCH-001).

## What the sources say

SRC-MANUAL-GOG, numbered page 45, lists Bribe, Heal, Hide, Influence, Research
and Snitch as the Instant phase and says each phase is executed for all players
simultaneously. The executable carries them out one gang at a time in slot
order, and a gang's result can change what a later gang's action finds.

## Differences between builds

None known.

## Open questions

- None beyond the step rules' own questions.
