---
id: RULE-UI-015
title: File, End and File, Exit during a match offer to save first when the match changed since it was last saved or loaded
status: established
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-058, FND-UI-022, FND-NET-004, EXP-UI-026]
conflicting: []
split_with: []
related: [RULE-UI-013, RULE-UI-014, SCR-UI-009, FMT-SAVE-001]
---

## Summary

The game remembers whether the match is as it was last saved or loaded. A save
or a load marks it saved. A new match, an order given or cleared, a hire and
each resolved turn mark it unsaved. When the player ends the match or quits
while it is unsaved, dialog 129 asks first: save first, cancel, or go on
without saving.

## When it runs

When planning handles File, End or File, Exit, including the File, Exit that a
closed window becomes (RULE-UI-014).

## Parameters

None.

## Inputs

`match_saved`, `no_match_in_play`.

## Procedure

```text
# match_saved = 1 after a save or a load, and while a network game is in play
# match_saved = 0 when a new match starts, after each turn's resolution, and
# when a drop, a command box order or the clearing of an order is accepted
# on File, End (0x81, 4) or File, Exit (0x81, 9) in planning:
let leave = 1
if match_saved == 0 and no_match_in_play == 0:
    emit DialogShown(129)
    # the button pressed: 1 save first, 2 cancel, 3 go on without saving
    # 1: the save dialog opens (FMT-SAVE-001), and leave = 0 unless it wrote
    #    the match
    # 2: leave = 0
if leave == 1:
    # the network disconnect runs (FND-NET-004), which in a local game always
    # allows it; then File, End ends the match, and File, Exit sets
    # quit_requested = 1
```

## Outputs

Ends the match and, for File, Exit, the program, or leaves the player in
planning. Emits `DialogShown(129)` when it asks.

## Edge cases

- A turn resolved with no order given still leaves the match unsaved.
- A save that is cancelled or fails returns the player to planning.
- In a network game `match_saved` stays 1, so the question is never asked.

## What the sources say

None of the sources describes the question.

## Differences between builds

None known.

## Open questions

- Whether a network disconnect that is refused keeps the player in planning
  was not run: in a network game `match_saved` stays 1, and the disconnect's
  confirmations belong to the network play the rebuild replaces.
