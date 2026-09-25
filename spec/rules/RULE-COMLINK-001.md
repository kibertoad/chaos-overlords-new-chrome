---
id: RULE-COMLINK-001
title: Storing a Comlink message keeps each player's newest 16 messages
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-COMLINK-001, FND-AUDIO-002, FND-AUDIO-012, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-005]
---

## Summary

Each player's Comlink holds up to 16 messages. When a 17th arrives, the oldest
is dropped and the others move down to make room.

## When it runs

Once for each recipient of a message: when the active player sends one
(RULE-COMLINK-003), and when a message arrives from another computer in a
network game.

## Parameters

- `recipient`: the player slot the message is for.
- `message`, of type FMT-STATE-005: the message record to store.

## Inputs

`comlink_messages`, `comlink_count`, `comlink_cursor`, `active_player`.

## Procedure

```text
let base = recipient * 16
let n = comlink_count[recipient]
if n < 16:
    comlink_messages[base + n] = message
    comlink_count[recipient] = n + 1
else:
    for i in 1..16:
        comlink_messages[base + i - 1] = comlink_messages[base + i]
    comlink_messages[base + 15] = message
    comlink_count[recipient] = 16
    if comlink_cursor[recipient] > 0:
        comlink_cursor[recipient] = comlink_cursor[recipient] - 1
if recipient == active_player:
    comlink_pending = 1
    emit ComlinkAlert()
    comlink_alert_repeat = 0
```

## Outputs

No return value. Copies `message` into the recipient's next free element of
`comlink_messages`, or drops element 0, moves elements 1 to 15 down one place
and puts `message` last. Adds 1 to `comlink_count` up to 16. When a message is
dropped, moves the recipient's `comlink_cursor` back one place, stopping at 0.
When the recipient is the active player, sets `comlink_pending`, emits
`ComlinkAlert` and restarts the alert's repeat timing. For a recipient playing
on another computer the original also sends the message over the network as
packet type 10.

## Edge cases

- With a full inbox and the cursor on the oldest message (cursor 0), that
  message is dropped and the cursor stays at 0, now on the next oldest.
- The count never exceeds 16.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Comlink), says Comlink stores up to 16 of
the most recent messages sent to the player, and that the button blinks when
the player has mail. It agrees with the executable.

## Differences between builds

None known.

## Open questions

- Whether the recorder stores a message for a recipient on another computer as
  well as sending it is not recorded.
- The order of the store, the pending flag, the sound and the repeat reset
  within the recorder is not recorded beyond the store coming first in the
  finding's description.
- What a message received from the network (message ID 0, or a received ID)
  fills into the record is not recorded.
