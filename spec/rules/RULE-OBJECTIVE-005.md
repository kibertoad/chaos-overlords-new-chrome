---
id: RULE-OBJECTIVE-005
title: An eliminated local human sees the elimination card at that player's place in the slot order, behind the Ready card when several humans play
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-OBJECTIVE-004, FND-OBJECTIVE-002, FND-SETUP-010, FND-AUDIO-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [SCR-SETUP-002, SCR-OBJECTIVE-002, RULE-AUDIO-001]
---

## Summary

When a local human has been eliminated, the game shows that player a private
elimination card, at the point in the slot order where the player's turn
would have come in the round after the elimination, and then retires the slot
for good by making it an empty slot. With several humans at the computer, the
Ready card comes first so the others do not see it. When no local human is
left, the local game ends without the awards.

## When it runs

At the start of every round of the outer match loop, before any slot plans:
the marking pass, then the walk over slots 0 to 5 in which the slots plan.

## Parameters

None.

## Inputs

`controller`, `player_active`.

## Procedure

```text
# marking: -2 is a local human who is eliminated and has not seen the card
let humans = 0
for player in 0..6:
    if controller[player] == 0:
        if not player_active[player]:
            controller[player] = -2
        humans = humans + 1
let handoff = humans > 1
if humans == 0:
    # the local game ends here, with no awards
    return
for player in 0..6:
    # an active slot plans here: a local human behind the Ready card when
    # handoff is set (RULE-SETUP-008), a computer player directly
    if controller[player] == -2:
        if handoff:
            show SCR-SETUP-002
        active_player = player
        # the card starts the endgame music on entry (FND-AUDIO-001) and
        # blocks until Done is released inside its rectangle
        call RULE-AUDIO-001(1)
        show SCR-OBJECTIVE-002
        # retired: the value of an empty slot
        controller[player] = -1
        for later in player + 1..6:
            if controller[later] == 0:
                # the ordinary gameplay music
                call RULE-AUDIO-001(2)
```

## Outputs

No return value. Shows the cards, sets `controller` of each eliminated local
human to -2 and then -1, and asks RULE-AUDIO-001 for music mode 2 once for
each later local human after each card. Makes no draws.

## Edge cases

With one local human the Ready card is skipped, but the elimination card still
appears when that human is eliminated. A human counted as -2 still counts
toward `handoff`, so the Ready card comes before the elimination card when one
other local human remains. The card starts the endgame music, and only a later
local human in slot order asks for the gameplay music again, so after the card
of the last local human in slot order the endgame music keeps playing for the
humans before it until another card or a new game changes it. The round in which the last local
human retires still lets later computer players plan; the next round finds no
local human and the game ends.

## What the sources say

SRC-MANUAL-GOG, page 46, says the Endgame Screen appears when a scenario is
finished and the player has not been eliminated, which implies a separate
presentation for an eliminated player.

## Differences between builds

None known.

## Open questions

- Whether the walk's `active_player` assignment is the same global the
  planning screens use is not recorded.
