---
id: RULE-UI-003
title: Panels slide in from the right and out to the right
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-011, FND-UI-023, FND-AUDIO-002, FND-OPTIONS-001, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-AUDIO-005]
---

## Summary

With Slide Panels on, a panel slides in from the right edge of the panel area,
x 448, with a sound, and slides back out with another. The panel shows more and
more of its left part as it comes in and is cut off at x 448. The slide takes about a quarter of a
second, measured against how fast the machine copied images at startup. With
Slide Panels off, panels appear and disappear at once, silently.

## When it runs

Whenever one of the 23 panel handlers opens or closes its panel.

## Parameters

- `opening` (`INT32`): 1 to slide in, 0 to slide out.
- `alternate` (`INT32`): 1 for the six panels that use the 320-pixel alternate
  crop (Item Information, Site Information, City and Sector Financial, the Hire
  comparison, Game Information and Gang Definition Information), 0 for the
  others.

## Inputs

`pref_slide_panels`, `blit_benchmark_count`.

## Procedure

```text
define slide_step(travel) -> INT32:
    let divisor = blit_benchmark_count / 4
    if divisor < 1:
        divisor = 1
    let step = travel / divisor
    if step < 16:
        step = 16
    return step

let travel = 344
if alternate != 0:
    travel = 320
let step = slide_step(travel)
if opening != 0:
    if pref_slide_panels != 0:
        play_effect(0)
        let shown = step
        while shown < travel:
            emit PanelSlideDrawn(travel - shown)
            shown = shown + step
    emit PanelSlideDrawn(0)
else:
    if pref_slide_panels != 0:
        play_effect(1)
        let left = travel - step
        while left > 0:
            emit PanelSlideDrawn(travel - left)
            left = left - step
    emit PanelSlideDrawn(travel)
```

## Outputs

No return value. Emits `PanelSlideDrawn(offset)` for each copy, where `offset`
is how far right of its final place the panel's left edge is: 0 is the panel in
place, and `travel` is the panel gone. At each step only the panel's left
`travel - offset` columns are drawn, ending at x 448, over rows 124 to 333.
As the panel slides out, the strip it uncovers is redrawn from the screen's
backing copy, or from the panel it was opened over when the caller asks for
that; the last copy restores the whole area the same way. The final place is `(104,124,344,209)`
for a primary panel and `(128,124,320,209)` for an alternate one. With Slide
Panels on, plays slot 0 (`DATA/SND00200`) before sliding in or slot 1
(`DATA/SND00201`) before sliding out.

## Edge cases

- The step is never below 16 pixels, so on a slow machine the slide takes fewer
  copies and less than a quarter of a second.
- The last partial step is left to the final full copy: the loop stops at the
  last whole step below the travel.
- With `blit_benchmark_count` below 4 the divisor is 1 and the step is the whole
  travel, so the slide is the final copy alone, still with its sound.
- The slide blocks input until it ends; no message is handled during it.

## What the sources say

SRC-MANUAL-GOG, page 10, says Slide Panels is checked by default and that
unchecking it makes panels pop out instead of sliding, which helps slow
computers. It agrees with the executable.

## Differences between builds

None known.

## Open questions

None.
