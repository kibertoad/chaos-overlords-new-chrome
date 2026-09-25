---
id: RULE-COMLINK-002
title: Comlink Send opens only when another human player can receive a message
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-COMLINK-003, SRC-MANUAL-GOG]
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

`active_player`, `player_active`, `controller`.

## Procedure

```text
let any = false
for p in 0..6:
    comlink_eligible[p] = player_active[p] and (controller[p] == 0 or controller[p] == 3)
comlink_eligible[active_player] = 0
for p in 0..6:
    if comlink_eligible[p]:
        any = true
if not any:
    return
show SCR-COMLINK-002
```

## Outputs

No return value. Fills `comlink_eligible`. Opens SCR-COMLINK-002 when at least
one other player can receive a message, and otherwise leaves the panel closed
and plays the rejected-input sound (SCR-COMLINK-002, Sounds).

## Edge cases

- In a match with one human player the panel never opens.
- A computer player or an empty slot is never eligible.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Comlink), says messages go to the other
Overlords, that the same message can go to several of them, and that the
button works only in games with more than one human player.

## Differences between builds

None known.

## Open questions

- The finding names an enabled array and `controller` as the inputs of the
  eligibility test without giving the array's address or the exact test. The
  procedure's use of `player_active` and of the values 0 and 3 is an
  assumption to be checked.
- Whether opening the panel clears `comlink_selected`, `comlink_draft` and the
  text cursor is not recorded.
