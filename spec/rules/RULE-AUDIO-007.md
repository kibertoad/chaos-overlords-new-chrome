---
id: RULE-AUDIO-007
title: The Comlink alert plays slot 6 through the effects gate
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-012, FND-COMLINK-004]
conflicting: []
split_with: []
related: [RULE-AUDIO-005, RULE-AUDIO-008, RULE-SETUP-008, RULE-COMLINK-001]
---

## Summary

Each time the game sounds the Comlink alert it plays `DATA/Snd00205` from
effect slot 6, and only while sound effects are on.

## When it runs

Each time `ComlinkAlert` is emitted: when a player with an unread message starts
planning (RULE-SETUP-008), when a message for the active player arrives
(RULE-COMLINK-001), every 24 presentation ticks while a message is unread
(RULE-AUDIO-008), and when the event pump enters the city screen with
`comlink_pending` set.

## Parameters

None.

## Inputs

`effects_enabled` through `play_effect`.

## Procedure

```text
play_effect(6)
```

## Outputs

No return value. Plays slot 6 when effects are on.

## Edge cases

The Comlink View panel marks the message it shows as read and then sets
`comlink_pending` from a scan of all 16 records, so the alert stops only when
the last unread message has been viewed [FND-AUDIO-012].

## What the sources say

SRC-MANUAL-GOG, page 23, says the Comlink button blinks when the player has
mail; it does not describe a sound.

## Differences between builds

None known.

## Open questions

- When the city entry path plays slot 6 relative to the planning entry, and
  whether a player can hear both.
