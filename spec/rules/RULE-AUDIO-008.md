---
id: RULE-AUDIO-008
title: The Comlink alert repeats every 24 presentation ticks
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-012, FND-UI-001]
conflicting: []
split_with: []
related: [RULE-AUDIO-005, RULE-AUDIO-007, RULE-UI-008, RULE-SETUP-008, RULE-COMLINK-001]
---

## Summary

While a Comlink message is unread, the alert sounds again every four seconds.

## When it runs

In the main event pump, once for each tick of `presentation_tick` it takes.

## Parameters

None.

## Inputs

`comlink_blink_step`, `comlink_alert_repeat`, `comlink_pending`.

## Procedure

```text
comlink_blink_step = (comlink_blink_step + 1) % 8
if comlink_blink_step == 0:
    comlink_alert_repeat = (comlink_alert_repeat + 1) % 3
    if comlink_alert_repeat == 0 and comlink_pending != 0:
        emit ComlinkAlert()
```

## Outputs

No return value. Advances `comlink_blink_step`, every eighth tick advances
`comlink_alert_repeat`, and every 24th tick emits `ComlinkAlert`, which plays
slot 6 (RULE-AUDIO-007), while `comlink_pending` is set.

## Edge cases

RULE-SETUP-008 and RULE-COMLINK-001 set `comlink_alert_repeat` to 0 when they
sound the alert, so the next repeat comes 17 to 24 ticks later, depending on
where `comlink_blink_step` stands.

## What the sources say

None of the sources describes the repeat.

## Differences between builds

None known.

## Open questions

- Whether `comlink_blink_step` wraps at 8 as written or counts on and is tested
  modulo 8; the effect on the sound is the same.
- On which screens the pump runs this; the city and sector screens are certain,
  others are not recorded.
- `comlink_blink_step` has no recorded address.
