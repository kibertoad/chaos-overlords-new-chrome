---
id: RULE-OBJECTIVE-001
title: At the end of each turn the scores are rebuilt, a lone surviving player ends the match, and then the scenario's own condition is tested
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-OBJECTIVE-003, FND-OBJECTIVE-004, FND-TURN-003, FND-AI-005, SRC-MANUAL-GOG]
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
for player in 0..6:
    if player_active[player]:
        active = active + 1
if active == 1:
    match_over = 1
# the scenario test runs whatever the count
if call RULE-OBJECTIVE-004():
    match_over = 1
```

## Outputs

No return value. Sets `scenario_score` and `scenario_standing` through
RULE-OBJECTIVE-002, and sets `match_over` when the match ends. Makes no draws.

## Edge cases

The sole-survivor test ends every scenario, timed or not. Nothing here clears
`match_over`; the outer match loop clears it when a match starts. In Big Man
the scores are rebuilt, and so grow, before the 40-point test.

When no player is active the count is 0 and only the scenario test can end
the match. A local game whose last local human has been eliminated ends in the
outer match loop instead, without the awards (FND-OBJECTIVE-004).

## What the sources say

SRC-MANUAL-GOG, pages 12 to 14, says the four timed scenarios end at the
chosen time limit and the six objective scenarios when a player meets the
objective, and that in the objective scenarios play goes on until one
Overlord meets it. It does not mention the sole-survivor rule, which the
executable applies in every scenario.

## Differences between builds

None known.

## Open questions

- Whether a state with no active player can arise is not recorded.
- How the winners are recorded, beyond the standings, is not recorded.
