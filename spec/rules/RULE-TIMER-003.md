---
id: RULE-TIMER-003
title: The planning clock bar and its warning sounds
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TIMER-001]
conflicting: []
split_with: []
related: [RULE-AUDIO-005]
---

## Summary

A 60-pixel bar on the main console shrinks as planning time runs out, in steps
of whole percent. In the last ten seconds a short tick sounds each time the bar
is redrawn, and in the last second a longer sound.

## When it runs

In the input pump, on every call the pump makes while a timed human player is
planning.

## Parameters

None.

## Inputs

`timer_redraw_countdown`, `timer_ms`, `planning_start_ms`,
`planning_limit_ms`.

## Procedure

```text
timer_redraw_countdown = timer_redraw_countdown - 1
if timer_redraw_countdown == 0:
    timer_redraw_countdown = 6
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

No return value. Every sixth call, emits `TimerBarDrawn` with the width of the
bar in pixels, and plays slot 7 (`DATA/Snd00206`) or slot 8 (`DATA/Snd00207`)
when the remaining time is in their range. Both divisions truncate, so the width
is taken from the whole percent elapsed, not from the remaining time directly.

## Edge cases

- The warning sounds go through `play_effect` and are silent while sound effects
  are off. Nothing stops them from restarting on every redraw, so the 1.189-second
  last-second sound is cut off and started again at each redraw that falls in the
  last second.
- A call that finds more time elapsed than the limit is not expected, since
  RULE-TIMER-002 ends the turn first; what it would draw is not recorded.

## What the sources say

None of the sources describes the bar or the sounds.

## Differences between builds

None known.

## Open questions

- Which pump calls count, and so how many redraws happen per second; this needs
  a measurement of the original.
- Whether the time bounds are compared in milliseconds, as written, or in whole
  seconds.
- The value `timer_redraw_countdown` holds when planning starts.
- Whether the bar is drawn when the limit is -1.
- `planning_limit_ms` and `planning_start_ms` have no recorded address.
