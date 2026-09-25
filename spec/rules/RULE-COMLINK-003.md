---
id: RULE-COMLINK-003
title: Sending a Comlink message stores a copy for each selected recipient
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-COMLINK-001, FND-COMLINK-003, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-COMLINK-001, FMT-STATE-005]
---

## Summary

The player picks one or more recipients by clicking their faces and sends the
message with the Send button. Each recipient gets a copy.

## When it runs

In the Comlink Send panel, when the Send control is released inside itself or
the Execute key is pressed.

## Parameters

None.

## Inputs

`comlink_selected`, `comlink_draft`.

## Procedure

```text
let any = false
for p in 0..6:
    if comlink_selected[p]:
        any = true
if not any:
    return
for recipient in 0..6:
    if comlink_selected[recipient]:
        call RULE-COMLINK-001(recipient, comlink_draft)
```

## Outputs

No return value. With no recipient selected, sends nothing and plays the
rejected-input sound before any pressed face is drawn. Otherwise stores a copy
of `comlink_draft` for each selected recipient through RULE-COMLINK-001.

## Edge cases

A recipient is selected only through its card, and a card can be selected only
while `comlink_eligible` is set for it (SCR-COMLINK-002), so every recipient is
another human player.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Comlink), says the player types the message
and clicks the faces of the Overlords to send it to, and can send the same
message to several of them.

## Differences between builds

None known.

## Open questions

- The order in which the recipients are visited is not recorded; the
  procedure assumes ascending player slot.
- How `occupied`, `read`, `turn` and `sender` of `comlink_draft` are set before
  it is copied is not recorded. `turn` is presumably the current turn and
  `sender` the active player.
- Whether the panel closes after sending, and whether the draft is cleared, is
  not recorded.
