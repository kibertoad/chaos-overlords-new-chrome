---
id: RULE-SETUP-009
title: A press on a setup player card selects it first, then works its portrait arrows or name, and a drag moves or swaps whole players
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SETUP-005, FND-AUDIO-002, FND-AUDIO-010, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [SCR-SETUP-001]
---

## Summary

On the local setup screen, clicking another player's card only selects it.
Once a card is selected, clicking its left or right edge changes the portrait
and clicking its bottom strip opens the name editor. Dragging a face onto
another card moves that player to the other colour, or swaps the two players.

## When it runs

When the left mouse button is released after a press inside one of the six
64-by-68 card cells of SCR-SETUP-001.

## Parameters

- `card`, the slot (0 to 5) of the card cell the press landed in.
- `offset_x` and `offset_y`, the press position relative to that cell's
  top-left corner.
- `dragged`, true when the pointer left the box from -2 to +1 pixels around
  the press point before the button was released.
- `drop_card`, the slot of the card cell the release landed in, or -1 when it
  landed outside every card.

## Inputs

`selected_card`, `controller`, `portrait`, `player_names`.

## Procedure

```text
if dragged:
    if drop_card >= 0:
        let c = controller[card]
        controller[card] = controller[drop_card]
        controller[drop_card] = c
        let f = portrait[card]
        portrait[card] = portrait[drop_card]
        portrait[drop_card] = f
        let n = copy(player_names[card])
        player_names[card] = player_names[drop_card]
        player_names[drop_card] = n
        selected_card = drop_card
    return
if card != selected_card:
    if controller[card] != -1:
        selected_card = card
    return
if controller[card] != 0:
    return
if offset_y < 58:
    if offset_x < 16:
        fn_00468D87(card)
    else if offset_x > 48:
        fn_00468CFC(card)
else:
    # the name editor: dialog Chaos Overlords.exe#DIALOG/139, at most ten
    # characters; accepting an empty editor keeps the old name
    fn_0040F63D(card)
```

## Outputs

No return value. Changes `selected_card`, or swaps the three records of two
slots, or changes one slot's `portrait` or name. Makes no draws.

## Edge cases

A release outside every card after a drag changes nothing. A drag onto an
empty card moves the player there and leaves its old slot empty. A press in
the middle of a selected card, between the two portrait bands and above the
name band, does nothing.

## What the sources say

SRC-MANUAL-GOG, page 15, says the face is changed with the two arrows beside
it, the name (up to 10 characters) by clicking on it, and the colour by
dragging the face onto another one. It does not say that a card must be
selected first.

## Differences between builds

None known.

## Open questions

- `fn_00468D87` and `fn_00468CFC` step the card's portrait down and up; how
  they treat the ends of the range and portraits other players hold is not
  recorded.
- `fn_0040F63D` is the name editor; its dialog's layout is not described.
- What a press or a drag that starts on an empty card does is not recorded.
- The address of `portrait` is not recorded.
