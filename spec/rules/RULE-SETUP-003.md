---
id: RULE-SETUP-003
title: Begin turns every empty setup slot into a computer player with an unused random portrait and that portrait's name
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SETUP-002, FND-RNG-005, SRC-HELP-GOG, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002, RULE-SETUP-001]
---

## Summary

When the player presses Begin on the local setup screen, every colour slot
without a local human becomes a computer player. Each gets a random portrait
that no other player has, and the name that goes with that portrait. Then the
new match starts.

## When it runs

When Begin is released inside its button on the local setup screen
(SCR-SETUP-001).

## Parameters

None.

## Inputs

`controller`, `portrait`, and the state of `rng` through `roll`.

## Procedure

```text
for each player in turn_order:
    if controller[player] == -1:
        controller[player] = 1
        let face = roll(15) - 1
        let taken = true
        while taken:
            taken = false
            for other in 0..6:
                if portrait[other] == face:
                    taken = true
            if taken:
                face = roll(15) - 1
        portrait[player] = face
        # The name is copied with an exact length of 10 into the
        # player's 12-byte name record
        player_names[player] = resource("Chaos Overlords.exe", "STRING", 62 + face)

call RULE-SETUP-001()
```

## Outputs

No return value. Sets `controller`, `portrait` and `player_names` of each
empty slot, in ascending slot order, then starts the match through
RULE-SETUP-001. Makes three draws from `rng` (one `roll`) for each portrait
tried, including the rejected ones. These are the first draws of the new
match.

## Edge cases

With six local humans no slot is empty and no draw is made. A portrait taken
by any slot, human or computer, is rejected. Since at most five slots can be
empty and there are 15 portraits, a free portrait always exists, but the
number of draws is unbounded in principle.

## What the sources say

SRC-MANUAL-GOG, page 15 (Player Selection Panel), says the setup shows the
player's face and five shadowy faces, and that the Add and Remove buttons set
how many people play; it does not say that the other slots become computer
players. SRC-HELP-GOG's setup topic says the setup starts with one local human
and that Add and Remove change their number.

## Differences between builds

None known.

## Open questions

- What an empty slot's `portrait` holds during the scan (the empty-slot image,
  15, is the likely value) is not recorded; a value from 0 to 14 there would
  change which portraits are rejected.
- The address of `portrait` is not recorded.
- Whether a name shorter than 10 characters is padded, and with what, is not
  recorded.
