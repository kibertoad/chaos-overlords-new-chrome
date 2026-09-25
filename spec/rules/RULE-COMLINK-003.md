---
id: RULE-COMLINK-003
title: Sending a Comlink message stores a copy for each selected recipient
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-COMLINK-001, FND-COMLINK-003, FND-COMLINK-006, FND-COMLINK-007, SRC-MANUAL-GOG]
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
let blank = true
for i in 0..160:
    if comlink_draft.text[i] != 0x20:
        blank = false
if not blank:
    for recipient in 0..6:
        if comlink_selected[recipient]:
            call RULE-COMLINK-001(recipient, comlink_draft)
# SCR-COMLINK-002 closes here, after the stores
```

## Outputs

No return value. With no recipient selected, sends nothing and plays the
rejected-input sound before any pressed face is drawn, and the panel stays
open. Otherwise closes the panel and, unless the text is all spaces, stores a
copy of `comlink_draft` for each selected recipient through RULE-COMLINK-001,
in ascending slot order. The draft's `occupied`, `read`, `turn` and `sender`
were set when the panel opened (RULE-COMLINK-002).

## Edge cases

A recipient is selected only through its card, and a card can be selected only
while `comlink_eligible` is set for it (SCR-COMLINK-002), so every recipient is
another human player.

A message of spaces only is dropped without a sound: the panel closes as if it
had been sent.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Comlink), says the player types the message
and clicks the faces of the Overlords to send it to, and can send the same
message to several of them.

## Differences between builds

None known.

## Open questions

None known. The draft is not cleared on sending; the next opening of the panel
replaces it (RULE-COMLINK-002).
