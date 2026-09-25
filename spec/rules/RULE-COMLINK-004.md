---
id: RULE-COMLINK-004
title: Comlink View opens at the oldest unread message and refuses an empty inbox
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-COMLINK-002, FND-COMLINK-006, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [SCR-COMLINK-001, FMT-STATE-005]
---

## Summary

The View button opens the player's Comlink messages at the oldest one not yet
read. When every message has been read it opens at the message shown last
time. With no messages it does not open.

## When it runs

When the active player presses the upper (View) half of the Comlink control on
the main console.

## Parameters

None.

## Inputs

`active_player`, `comlink_count`, `comlink_messages`, `comlink_cursor`.

## Procedure

```text
let base = active_player * 16
if comlink_count[active_player] == 0:
    return
for i in 0..16:
    let m = comlink_messages[base + i]
    if m.occupied and not m.read:
        comlink_cursor[active_player] = i
        break
show SCR-COMLINK-001
```

## Outputs

No return value. May set `comlink_cursor`. Opens SCR-COMLINK-001, or with an
empty inbox leaves it closed and plays the rejected-input sound
(SCR-COMLINK-001, Sounds).

## Edge cases

- When no message is unread, the cursor keeps its old value, which
  RULE-COMLINK-001 keeps pointing at the same message when older ones are
  dropped.
- Between planning visits RULE-COMLINK-007 removes the read messages at the
  front and sets the cursor to 0, so in a later turn View opens at the first
  message kept.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Comlink), says View shows the messages the
other Overlords have sent and that the panel does not open when the player has
no messages.

## Differences between builds

None known.

## Open questions

None known.
