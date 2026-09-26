---
id: RULE-COMLINK-007
title: When a player finishes planning, the read messages at the front of the inbox are dropped
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-COMLINK-006]
conflicting: []
split_with: []
related: [RULE-COMLINK-001, RULE-COMLINK-004, FMT-STATE-005]
---

## Summary

Comlink does not keep old mail from turn to turn. When a player ends their
planning, the messages they have read are removed from the oldest end of the
inbox until an unread message or an empty record is reached, and View starts
at the first message the next time.

## When it runs

In `planning_phase`, once for each player whose planning ends at this
computer, right after that player's planning loop and before the next player's
turn.

## Parameters

- `player`: the player whose planning has ended.

## Inputs

`comlink_messages`, `comlink_count`, `comlink_cursor`.

## Procedure

```text
let base = player * 16
while comlink_messages[base].occupied and comlink_messages[base].read:
    comlink_count[player] = comlink_count[player] - 1
    for i in 1..16:
        comlink_messages[base + i - 1] = comlink_messages[base + i]
    comlink_messages[base + 15].occupied = 0
    comlink_messages[base + 15].read = 1
comlink_cursor[player] = 0
```

## Outputs

No return value. Removes the leading read messages of `player`, lowers
`comlink_count` by the number removed and sets `comlink_cursor` to 0.

## Edge cases

- A read message that comes after an unread one is kept, and so are the
  messages after it.
- An empty record is not `occupied` and has `read` set: the match loop gives
  every record those values when it starts, and this rule gives them to the
  record it frees. A fully read inbox therefore empties completely, and the
  loop stops at the first empty record.
- The cursor goes back to 0 even when nothing was removed.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Comlink), says Comlink stores up to 16 of the
most recent messages. It does not say that read messages are discarded.

## Differences between builds

None known.

## Open questions

- Whether this also runs for a player at another computer in a network game,
  where that player's inbox is kept on their own computer, was not read.
