---
id: RULE-AWARDS-002
title: The endgame lists players by standing, ties in slot order, eliminated players last, and shows a victory splash first when one player is left
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AWARDS-004, FND-OBJECTIVE-004, FND-AWARDS-003, FND-AI-005, FND-AWARDS-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-AWARDS-001, SCR-AWARDS-001, SCR-AWARDS-002]
---

## Summary

When a match ends with exactly one player still active, human or computer,
the endgame opens on a victory splash for that player in place of the awards
view. When several players are still active, as at the end of a timed
scenario, it opens on the awards view. The results list the players from the
best standing down, tied players in slot order, and the eliminated players
after them in slot order.

## When it runs

Once, when `match_over` has been set by RULE-OBJECTIVE-001 and every local
human has had a final view of the city (FND-OBJECTIVE-004). A local game that
ends because no local human is left shows no endgame.

## Parameters

None.

## Inputs

`player_active`, `scenario_standing`.

## Procedure

```text
call RULE-AWARDS-001()

let active = 0
let survivor = -1
for player in 0..6:
    if player_active[player]:
        active = active + 1
        survivor = player

# rebuilt each time the screen is drawn
endgame_rows = []
for standing in 0..6:
    for player in 0..6:
        if player_active[player] and scenario_standing[player] == standing:
            append(endgame_rows, player)
for player in 0..6:
    if scenario_standing[player] == 0xFF:
        append(endgame_rows, player)

# SCR-AWARDS-001 opens on its Awards tab; while active is 1 that tab shows
# SCR-AWARDS-002 for survivor instead of the table
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

The splash is shown whoever the survivor is, so a computer player's win shows
its splash to the humans. With one survivor the table can be seen only on the
Stats tab, and pressing the Awards tab brings the splash back. Done leaves at
once, from either view.

## What the sources say

SRC-MANUAL-GOG, page 46, says the Overlords' pictures are posted on the left
in order of place, first on top and last at the bottom.

## Differences between builds

None known.

## Open questions

- Whether a timed scenario can end with exactly one player active, which
  would show the splash for a player who did not have the best score, is not
  recorded beyond the order of the tests in RULE-OBJECTIVE-001.
