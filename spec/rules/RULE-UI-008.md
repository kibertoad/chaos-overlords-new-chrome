---
id: RULE-UI-008
title: The presentation timer
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-001, FND-PLATFORM-006, FND-TIMER-002, FND-UI-023, FND-EXE-004]
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

- The period is `1000 / 6` in integer arithmetic, 166 ms, not 166.67 ms. The
  timer is created with a resolution of 20 ms.
- `presentation_tick_pending` is a flag byte: a tick that comes while it is
  still set changes nothing, so ticks a busy loop misses are lost.
- A second clock, slot 1, is started at the same time at 10 per second (100 ms)
  and read by the intro; slots 2 and 3 are not used by reachable code.
- The clock is read by the event pump (the music poll, the planning bar, the
  Comlink blink and alert repeat), combat animation, the idle-gang warning's
  blinking line, the Item Information rotation, the Comlink Send panel, and the
  Last Turn Events, Sell, Give and Research panels (FND-UI-023). The pump's
  planning bar and Comlink block reads it only while the byte `g_00487830` is
  set, and the pump clears it only when its caller asks.

## What the sources say

None of the sources describes the timer.

## Differences between builds

None known.

## Open questions

None.
