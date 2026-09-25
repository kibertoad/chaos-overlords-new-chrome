---
id: RULE-COMLINK-001
title: Storing a Comlink message keeps each player's newest 16 messages
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-COMLINK-001, FND-COMLINK-006, FND-AUDIO-002, FND-AUDIO-012, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-005]
---

## Summary

Each player's Comlink holds up to 16 messages. When a 17th arrives, the oldest
is dropped and the others move down to make room.

## When it runs

Once for each recipient of a message: when the active player sends one
(RULE-COMLINK-003), and when a packet of type 10 arrives from another computer
in a network game. A received message is the 166 bytes of the packet, with the
two bytes of `turn` swapped back from the order they travel in
[FND-COMLINK-006].

## Parameters

- `recipient`: the player slot the message is for.
- `message`, of type FMT-STATE-005: the message record to store.

## Inputs

`comlink_messages`, `comlink_count`, `comlink_cursor`, `active_player`,
`network_game`, `local_game`, `controller`.

## Procedure

```text
if network_game and controller[recipient] == 3:
    return
if local_game and controller[recipient] != 0:
    return
if recipient == active_player:
    comlink_pending = 1
    emit ComlinkAlert()
    comlink_alert_repeat = 0
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
```

## Outputs

No return value. For a recipient on another computer, stores nothing: the
original sends the message instead as a packet of type 10, to the recipient's
own connection when `network_game` is set and to connection 0 when
`local_game` is set, with `turn` byte-swapped for the trip. Otherwise copies
`message` into the recipient's next free element of
`comlink_messages`, or drops element 0, moves elements 1 to 15 down one place
and puts `message` last. Adds 1 to `comlink_count` up to 16. When a message is
dropped, moves the recipient's `comlink_cursor` back one place, stopping at 0.
When the recipient is the active player, first sets `comlink_pending`, emits
`ComlinkAlert` and restarts the alert's repeat timing, then stores the
message.

## Edge cases

- With a full inbox and the cursor on the oldest message (cursor 0), that
  message is dropped and the cursor stays at 0, now on the next oldest.
- The count never exceeds 16.
- A host that receives a message for a player on a third computer sends it on
  through this rule, so the message is stored only where its recipient plays.
- Read messages do not stay for 16 turns: RULE-COMLINK-007 drops them from the
  front of the inbox when their player finishes planning.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Comlink), says Comlink stores up to 16 of
the most recent messages sent to the player, and that the button blinks when
the player has mail. It agrees with the executable.

## Differences between builds

None known.

## Open questions

- The glossary reads the flag at `0x00482178` as `local_game`; its use here,
  sending every message for a player not at this computer to connection 0,
  fits a network client better (FND-COMLINK-006).
- After storing a message for the active player, the recorder also sets
  `g_004877D0` when `g_004877CC` is set; what those flags do was not read.
