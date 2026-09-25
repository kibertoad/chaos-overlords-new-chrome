---
id: RULE-OBJECTIVE-001
title: At the end of each turn the scores are rebuilt, a lone surviving player ends the match, and then the scenario's own condition is tested
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TURN-003, FND-AI-005, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-OBJECTIVE-002, RULE-OBJECTIVE-004]
---

## Summary

After every turn's resolution, once eliminated players have been found, the
game recomputes every player's score toward the scenario. If only one player
is left in the match, the match is over, whatever the scenario. Otherwise the
scenario's own end condition decides.

## When it runs

In `turn_end`, as the last step of `resolution`: after RULE-OBJECTIVE-003 has
cleared the active flags of eliminated players and their elimination reports
have been recorded (RULE-EVENT-003).

## Parameters

None.

## Inputs

`player_active`, and everything RULE-OBJECTIVE-002 and RULE-OBJECTIVE-004 read.

## Procedure

```text
call RULE-OBJECTIVE-002()
let active = 0
for each player in turn_order:
    if player_active[player]:
        active = active + 1
if active == 1:
    match_over = 1
    return
if call RULE-OBJECTIVE-004():
    match_over = 1
```

## Outputs

No return value. Sets `scenario_score` and `scenario_standing` through
RULE-OBJECTIVE-002, and sets `match_over` when the match ends. Makes no draws.

## Edge cases

The sole-survivor test comes before the scenario test, so it ends every
scenario, timed or not. In Big Man the scores are rebuilt, and so grow, before
the 40-point test.

## What the sources say

SRC-MANUAL-GOG, pages 12 to 14, says the four timed scenarios end at the
chosen time limit and the six objective scenarios when a player meets the
objective, and that in the objective scenarios play goes on until one
Overlord meets it. It does not mention the sole-survivor rule, which the
executable applies in every scenario.

## Differences between builds

None known.

## Open questions

- What happens when no player is active after an elimination (the count is 0)
  is not recorded.
- The address of `match_over` and how the winners are recorded are not
  recorded.
- `player_active` has no recorded address.
