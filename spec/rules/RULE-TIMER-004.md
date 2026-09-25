---
id: RULE-TIMER-004
title: Presentation waits last until the next tick of the six-per-second clock, and only the panel slide step depends on the machine's speed
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TIMER-002, FND-UI-020, FND-UI-011, FND-PLATFORM-009]
conflicting: []
split_with: []
related: [RULE-UI-008, RULE-UI-014, RULE-UI-003]
---

## Summary

The presentation has four kinds of wait. A timed pause waits for ticks of the
six-per-second presentation clock. Every request for input waits at most 17 ms.
Two network panels wait a fixed number of milliseconds. The only timing that
depends on the speed of the machine is the panel slide step, which a one-second
copy benchmark at start sets.

## When it runs

Where a screen pauses between drawing steps: 13 places in seven functions of
the city screen, the panels and the Comlink.

## Parameters

- `ticks` (`INT32`): how many ticks of the clock to wait; every call passes 1.

## Inputs

`presentation_tick_pending`.

## Procedure

```text
presentation_tick_pending = 0
let seen = 0
while seen < ticks:
    call RULE-UI-014()
    if presentation_tick_pending != 0:
        presentation_tick_pending = 0
        seen = seen + 1
```

## Outputs

None. Events that arrive during the wait are taken by the event step and
dropped, apart from what the step itself handles.

## Edge cases

- With `ticks` 1 the wait lasts from almost 0 to 166 ms, depending on where the
  clock's period stands when it starts.
- The elimination panel of a network game closes itself after 15000 ms, and a
  network wait lasts 2000 ms, both measured with `timeGetTime`.
- `blit_benchmark_count` is measured once at start by copying the same
  rectangle for just over one second; RULE-UI-003 takes a quarter of it, at
  least 1, as the slide step.
- No wait counts processor cycles or loop iterations.

## What the sources say

None of the sources describes the waits.

## Differences between builds

None known.

## Open questions

- The real period of a 166 ms multimedia timer at 20 ms resolution on the
  machines the game was made for has not been measured.
