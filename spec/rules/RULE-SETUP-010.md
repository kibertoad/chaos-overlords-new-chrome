---
id: RULE-SETUP-010
title: Local setup starts with one human, and Add and Remove change the number of local humans from one to six
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SETUP-005, FND-SETUP-017, FND-SETUP-013, FND-RNG-005, FND-AUDIO-002, FND-AUDIO-010, SRC-HELP-GOG, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [SCR-SETUP-001, RULE-AUDIO-005]
---

## Summary

The local setup screen opens with one human player, in slot 0 with portrait 0,
and five empty slots; every slot starts with a numbered default name.
Add puts another local human in an empty slot and selects it; Remove takes
out the selected human and selects the highest slot still occupied. There is
always at least one local human and at most six.

## When it runs

When a setup screen is about to open (the reset `fn_00410016`, called from
the title loop, the local setup and the network setup [FND-SETUP-017]), and
when Add or Remove is released inside its button on SCR-SETUP-001.

## Parameters

- `button`, 0 for the screen opening, 1 for Add, 2 for Remove.

## Inputs

`controller`, `selected_card`. The setup screen edits its own copies of
`controller`, `portrait` and `player_names`, at `0x00490680`, `0x00490678`
and `0x00490630`, and copies them into the game's lists when it opens
[FND-SETUP-013, FND-SETUP-017]; the procedure writes `controller` and
`portrait` for those copies.

## Procedure

```text
if button == 0:
    for p in 0..6:
        # player_names[p] becomes string resource 61 followed by the digit p + 1
        controller[p] = -1
        portrait[p] = 15
    controller[0] = 0
    portrait[0] = 0
    selected_card = 0
    return
let humans = 0
for p in 0..6:
    if controller[p] == 0:
        humans = humans + 1
if button == 1:
    if humans == 6:
        play_effect(4)
        return
    for p in 0..6:
        if controller[p] == -1:
            controller[p] = 0
            selected_card = p
            return
if button == 2:
    if humans == 1:
        play_effect(4)
        return
    controller[selected_card] = -1
    for p in 0..6:
        if controller[p] != -1:
            selected_card = p
```

## Outputs

No return value. Changes `controller` of one slot and `selected_card`. Plays
effect slot 4, the rejected-input sound, when the count would leave the range
one to six. Makes no
draws.

## Edge cases

Remove at one human and Add at six humans change nothing and play the
rejection cue after the push cue.

## What the sources say

FND-SETUP-017 shows the reset: slot 0 human with portrait 0, the other slots
empty with portrait 15, and every name string resource 61 followed by the
slot's number, whatever the portrait. SRC-HELP-GOG's setup topic says the
setup starts with one local human, Add adds another and Remove takes the last one away. SRC-MANUAL-GOG, page 15,
says the Add button adds a player and the Remove button removes one.
FND-SETUP-005 shows that Remove takes out the selected card rather than the
last one, and selects the highest occupied slot afterwards; FND-RNG-005 shows
that slot 0 starts with portrait 0.

## Differences between builds

None known.

## Open questions

- Which empty slot Add fills (the lowest is the reading used), what portrait
  and name the new human gets, and what Remove leaves in the removed slot's
  portrait and name are not recorded.
- Whether Add and Remove count computer slots is not recorded; before Begin
  no slot is a computer.
- Portrait 15 in an empty slot is one past the last portrait; whether any
  drawing reads it for an empty slot is not recorded.
