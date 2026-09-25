---
id: RULE-OBJECTIVE-005
title: An eliminated local human sees the elimination card at that player's place in the slot order, behind the Ready card when several humans play
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-OBJECTIVE-002, FND-SETUP-010, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [SCR-SETUP-002, SCR-OBJECTIVE-002, RULE-AUDIO-001]
---

## Summary

When a local human has been eliminated, the game shows that player a private
elimination card, at the point in the slot order where the player's turn
would have come, and then retires the slot for good. With several humans at
the computer, the Ready card comes first so the others do not see it.

## When it runs

In the outer match loop's walk over slots 0 to 5 that follows a turn's
resolution, for each local human slot that is inactive and not yet retired.

## Parameters

- `handoff`, true when more than one local human plays.

## Inputs

`controller`, `player_active`, `player_retired`.

## Procedure

```text
for each player in turn_order:
    if controller[player] == 0 and not player_active[player] and not player_retired[player]:
        if handoff:
            show SCR-SETUP-002
        active_player = player
        # blocks until Done is released inside its rectangle
        show SCR-OBJECTIVE-002
        player_retired[player] = 1
        # the ordinary gameplay music
        call RULE-AUDIO-001(2)
```

## Outputs

No return value. Shows the cards, sets `player_retired` of each eliminated
local human, and selects music mode 2 through RULE-AUDIO-001 after each card. Makes no draws.

## Edge cases

With one local human the Ready card is skipped, but the elimination card still
appears when that human is eliminated.

## What the sources say

SRC-MANUAL-GOG, page 46, says the Endgame Screen appears when a scenario is
finished and the player has not been eliminated, which implies a separate
presentation for an eliminated player.

## Differences between builds

None known.

## Open questions

- Where this walk sits in the turn (after resolution and before the next
  planning scan) is not recorded, nor whether it sets `active_player`.
- What follows the card for a single human (the manual and the help suggest
  a return to the title screen) is not recorded.
- The addresses of `player_retired` and `player_active` are not recorded.
