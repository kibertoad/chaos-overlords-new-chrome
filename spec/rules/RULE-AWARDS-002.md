---
id: RULE-AWARDS-002
title: The endgame lists players by standing, ties in slot order, eliminated players last, after a victory splash when one human plays
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AWARDS-003, FND-AI-005, FND-AWARDS-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-AWARDS-001, SCR-AWARDS-001, SCR-AWARDS-002]
---

## Summary

When a match ends, a single human player first sees a victory splash; with
several humans the game goes straight to the shared results. The results list
the players from the best standing down, tied players in slot order, and the
eliminated players after them in slot order.

## When it runs

Once, when the match has ended (`match_over` set by RULE-OBJECTIVE-001) and
the player has not been eliminated.

## Parameters

None.

## Inputs

`controller`, `player_active`, `scenario_standing`.

## Procedure

```text
call RULE-AWARDS-001()

let humans = 0
for each player in turn_order:
    if controller[player] == 0 or controller[player] == 3:
        humans = humans + 1

endgame_rows = []
for standing in 0..6:
    for each player in turn_order:
        if player_active[player] and scenario_standing[player] == standing:
            append(endgame_rows, player)
for each player in turn_order:
    if scenario_standing[player] == 0xFF:
        append(endgame_rows, player)

if humans == 1:
    show SCR-AWARDS-002
show SCR-AWARDS-001
```

## Outputs

No return value. Fills `player_awards` through RULE-AWARDS-001, sets
`endgame_rows`, and shows the screens. Makes no draws.

## Edge cases

A standing counts every player with a higher score, so a standing can skip
values (two players at 0 leave no player at 1); the row loop still visits
every standing from 0 to 5. A standing can never exceed 5, since at most five
other players can score higher, so every active player is listed.

## What the sources say

SRC-MANUAL-GOG, page 46, says the Overlords' pictures are posted on the left
in order of place, first on top and last at the bottom.

## Differences between builds

None known.

## Open questions

- Whether the controller counts network humans (controller 3) as well as
  local ones is not recorded.
- Whether the victory splash appears when the lone human did not win (for
  example a timed scenario won by a computer player) is not recorded.
- Where `endgame_rows` is kept is not recorded; it may be computed by the
  renderer each time it draws.
