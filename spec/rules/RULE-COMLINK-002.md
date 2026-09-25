---
id: RULE-COMLINK-002
title: Comlink Send opens only when another human player can receive a message
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-COMLINK-003, FND-COMLINK-007, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [SCR-COMLINK-002]
---

## Summary

A player can send Comlink messages only to other human players. With no other
human in the match, the Send button is refused.

## When it runs

When the active player presses the lower (Send) half of the Comlink control on
the main console.

## Parameters

None.

## Inputs

`active_player`, `player_active`, `players_human`, `elapsed_turns`.

## Procedure

```text
let any = false
for p in 0..6:
    comlink_eligible[p] = player_active[p] != 0 and players_human[p] != 0
comlink_eligible[active_player] = 0
for p in 0..6:
    if comlink_eligible[p]:
        any = true
if not any:
    return
for p in 0..6:
    comlink_selected[p] = 0
comlink_draft.occupied = 1
comlink_draft.read = 0
comlink_draft.turn = elapsed_turns
comlink_draft.sender = active_player
for i in 0..160:
    comlink_draft.text[i] = 0x20
comlink_draft_row = 0
comlink_draft_column = 0
show SCR-COMLINK-002
```

## Outputs

No return value. Fills `comlink_eligible`. Opens SCR-COMLINK-002 when at least
one other player can receive a message, with no recipient selected, a blank
draft stamped with the low 16 bits of `elapsed_turns` and the active player, and the cursor at
the top left. Otherwise leaves the panel closed and plays the rejected-input
sound (SCR-COMLINK-002, Sounds). `players_human` is 1 for a slot whose
`controller` was 0 or 3 at setup [FND-COMLINK-007].

## Edge cases

- In a match with one human player the panel never opens.
- A computer player or an empty slot is never eligible, and neither is a
  network player whose slot has been handed to the computer.
- Nothing typed in an earlier opening of the panel survives into the next.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Comlink), says messages go to the other
Overlords, that the same message can go to several of them, and that the
button works only in games with more than one human player.

## Differences between builds

None known.

## Open questions

None known.
