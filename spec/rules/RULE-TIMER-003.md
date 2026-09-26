---
id: RULE-TIMER-003
title: The planning clock bar and its warning sounds
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TIMER-001, FND-TIMER-003, FND-UI-023, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-AUDIO-005, RULE-UI-008]
---

## Summary

A 60-pixel bar on the main console shrinks as planning time runs out, in steps
of whole percent. It is redrawn about once a second, on every sixth tick of the
presentation timer. In the last ten seconds a short tick sounds at each redraw,
and in the last second a longer sound. With no time limit no bar is drawn.

## When it runs

In the event pump, on each `presentation_tick` it takes, except while a screen
function that clears the byte `g_00487830` for its duration runs, and once when
a timed human player's planning starts.

## Parameters

None.

## Inputs

`timer_redraw_countdown`, `timer_ms`, `planning_start_ms`,
`planning_limit_ms`, `planning_timed`.

## Procedure

```text
if timer_redraw_countdown > 0:
    timer_redraw_countdown = timer_redraw_countdown - 1
else:
    timer_redraw_countdown = 0
if timer_redraw_countdown == 0:
    timer_redraw_countdown = 6
    if planning_timed == 0:
        return
    let elapsed = timer_ms - planning_start_ms
    let elapsed_percent = (elapsed * 100) / planning_limit_ms
    let width = 60 - (elapsed_percent * 60) / 100
    emit TimerBarDrawn(width)
    let remaining = planning_limit_ms - elapsed
    if remaining > 1000 and remaining < 10000:
        play_effect(7)
    else if remaining > 0 and remaining <= 1000:
        play_effect(8)
```

## Outputs

No return value. Every sixth tick, while a timed turn is planned, emits
`TimerBarDrawn` with the width of the bar in pixels, and plays slot 7 (`DATA/Snd00206`) or slot 8 (`DATA/Snd00207`)
when the remaining time is in their range. Both divisions truncate, so the width
is taken from the whole percent elapsed, not from the remaining time directly.

## Edge cases

- The warning sounds go through `play_effect` and are silent while sound effects
  are off. Nothing stops them from restarting on every redraw, so the 1.189-second
  last-second sound is cut off and started again at each redraw that falls in the
  last second.
- A width below 1 draws the empty bar and a width above 59 the full bar. With a
  panel open past the limit, the width goes below 1 and the empty bar stays.
- `timer_redraw_countdown` is 6 at startup and is not reset when planning
  starts, so the first redraw after the one at the start of planning comes one
  to six ticks later.
- Ticks the pump misses are lost (RULE-UI-008), so the redraw rate can fall
  below once a second on a busy machine.
- The bar is the rectangle `(520,336)-(580,339)`: the full bar's first `width`
  columns, then the empty bar's remaining columns.

## What the sources say

None of the sources describes the bar or the sounds.

## Differences between builds

None known.

## Open questions

None.
