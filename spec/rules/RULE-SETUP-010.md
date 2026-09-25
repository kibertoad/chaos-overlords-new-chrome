---
id: RULE-SETUP-010
title: The first local setup of a session starts with one human, later ones with the last roster begun, and Add and Remove change the number of local humans from one to six
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SETUP-005, FND-SETUP-017, FND-SETUP-013, FND-NET-004, FND-RNG-005, FND-AUDIO-002, FND-AUDIO-010, SRC-HELP-GOG, SRC-MANUAL-GOG, FND-EXE-004]
conflicting: []
split_with: []
related: [SCR-SETUP-001, RULE-AUDIO-005]
---

## Summary

The first time the local setup screen opens in a session it has one human
player, in slot 0 with portrait 0, and five empty slots; every slot starts
with a numbered default name. Begin saves the humans, portraits and names, and
the next local setup of the session opens with them; Cancel saves nothing.
Add puts another local human in the lowest empty slot with the lowest free
portrait and selects it; Remove takes out the selected human and selects the
highest slot still occupied. There is always at least one local human and at
most six.

## When it runs

The reset `fn_00410016` runs once when the title loop starts and when either
network lobby opens (the host lobby `fn_004677F0` and the joining lobby
`fn_0040B9C0`, FND-SETUP-017 and FND-NET-004); the full local setup does not
call it. Add and Remove run when released inside their buttons on
SCR-SETUP-001.

## Parameters

- `button`, 0 for the screen opening, 1 for Add, 2 for Remove.

## Inputs

`controller`, `selected_card`. The setup screen edits its own copies of
`controller`, `portrait` and `player_names`, at `0x00490680`, `0x00490678`
and `0x00490630`, and copies them into the game's lists when it opens
[FND-SETUP-013, FND-SETUP-017]; the procedure writes `controller` and
`portrait` for those copies. Begin copies the screen's lists back before the
empty slots become computer players (RULE-SETUP-003).

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
    for p in 0..6:
        if controller[p] == -1:
            # the lowest portrait no slot holds; the name is left as it was
            let face = 0
            let taken = true
            while taken:
                taken = false
                for q in 0..6:
                    if portrait[q] == face:
                        taken = true
                if taken:
                    face = face + 1
            portrait[p] = face
            controller[p] = 0
            selected_card = p
            return
    play_effect(4)
    return
if button == 2:
    if humans < 2:
        play_effect(4)
        return
    controller[selected_card] = -1
    portrait[selected_card] = 15
    selected_card = 0
    for p in 1..6:
        if portrait[p] != 15:
            selected_card = p
```

## Outputs

No return value. Changes `controller` and `portrait` of one slot and
`selected_card`. Plays
effect slot 4, the rejected-input sound, when the count would leave the range
one to six. Makes no
draws.

## Edge cases

Remove at one human and Add with no empty slot change nothing and play the
rejection cue after the push cue. A removed human's name stays in the slot and
comes back when Add reuses it. Because Begin saves the roster, a later setup
in the same session reopens with the same humans, portraits and names.

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

- Portrait 15 in an empty slot is one past the last portrait; whether any
  drawing reads it for an empty slot is not recorded.
