---
id: RULE-COMLINK-005
title: Showing a Comlink message marks it read and dates it from its turn
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-COMLINK-004, FND-AUDIO-002, FND-AUDIO-011]
conflicting: []
split_with: []
related: [FMT-STATE-005]
---

## Summary

A message counts as read once it has been shown. The unread alert stops only
when every message has been read. The panel dates a message by the year and
week of the turn it was sent in, starting at week 1 of 2050.

## When it runs

In Comlink View, each time a message is shown: when the panel opens and after
each step to another message.

## Parameters

None.

## Inputs

`active_player`, `comlink_cursor`, `comlink_count`, `comlink_messages`,
`player_names`.

## Procedure

```text
let base = active_player * 16
let m = comlink_messages[base + comlink_cursor[active_player]]
m.read = 1
comlink_pending = 0
for i in 0..16:
    let r = comlink_messages[base + i]
    if r.occupied and not r.read:
        comlink_pending = 1
let page = sprintf("%d/%d", comlink_cursor[active_player] + 1, comlink_count[active_player])
let date = sprintf("%d.%02d", 2050 + m.turn / 52, m.turn % 52 + 1)
```

## Outputs

No return value. Sets `read` of the message shown and sets `comlink_pending`
to whether any message is still unread. The panel then shows `page`, `date`,
the name and portrait of player `m.sender`, and the four 40-character rows of
`m.text`.

## Edge cases

Turn 0 is dated `2050.01`, turn 51 `2050.52` and turn 52 `2051.01`.

## What the sources say

SRC-MANUAL-GOG does not describe when a message counts as read.

## Differences between builds

None known.

## Open questions

- The finding says the rescan reads the 16 read bytes; whether it also tests
  `occupied`, as the procedure does, is not recorded.
- The page header's exact pattern is not recorded; `"%d/%d"` stands for "the
  one-based message number and the count".
- Whether the rescan sets `comlink_pending` directly or another unread
  indicator that is copied to it is not recorded.
