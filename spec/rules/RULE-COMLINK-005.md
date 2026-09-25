---
id: RULE-COMLINK-005
title: Showing a Comlink message marks it read and dates it from its turn
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-COMLINK-004, FND-COMLINK-006, FND-COMLINK-007, FND-AUDIO-002, FND-AUDIO-011]
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
    if not comlink_messages[base + i].read:
        comlink_pending = 1
let page = sprintf("%02d", comlink_cursor[active_player] + 1)
let count = sprintf("%02d", comlink_count[active_player])
let year = sprintf("%d", 2050 + m.turn / 52)
let week = sprintf("%02d", m.turn % 52 + 1)
```

## Outputs

No return value. Sets `read` of the message shown and sets `comlink_pending`
to whether any message is still unread. The panel then shows `page`, `date`,
the name and portrait of player `m.sender`, and the four 40-character rows of
`m.text`. The page number and the count, and the year and the week, are drawn
as separate fields at the positions SCR-COMLINK-001 gives; any separator
between them is part of the panel art.

## Edge cases

- Turn 0 is dated year 2050, week 01; turn 51 week 52 of 2050; turn 52 week 01
  of 2051. `turn` is the number of turns completed when the message was
  written (RULE-COMLINK-002), and the division is signed.
- The rescan does not test `occupied`. An empty record always has `read` set
  (FND-COMLINK-006), so empty records never count as unread.

## What the sources say

SRC-MANUAL-GOG does not describe when a message counts as read.

## Differences between builds

None known.

## Open questions

None known.
