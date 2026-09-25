---
id: RULE-UI-008
title: The presentation timer
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-001, FND-PLATFORM-006]
conflicting: []
split_with: []
related: []
---

## Summary

A multimedia timer ticks about six times a second. Combat animation, the Comlink
alert repeat and other presentation steps advance on its ticks.

## When it runs

On each tick of the multimedia timer that the title initialization starts for
timer slot 0 with a rate of 6. The callback runs on the timer's own thread.

## Parameters

None.

## Inputs

None.

## Procedure

```text
clock presentation_tick: 1000 / 166 Hz
presentation_tick_pending = 1
```

## Outputs

No return value. Marks timer slot 0 as having ticked. The loops that use the
clock read and clear `presentation_tick_pending`.

## Edge cases

The period is `1000 / 6` in integer arithmetic, 166 ms, not 166.67 ms.

## What the sources say

None of the sources describes the timer.

## Differences between builds

None known.

## Open questions

- Whether slot 0 is a flag, as written, or a counter of ticks not yet taken, and
  so whether ticks missed by a busy loop are lost.
- `presentation_tick_pending` has no recorded address.
- What else advances on this clock: the item rotation of Item Information and
  the Send cursor of the Comlink are candidates.
