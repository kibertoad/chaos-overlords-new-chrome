---
id: RULE-UI-003
title: Panels slide in from the right and out to the right
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-011, FND-AUDIO-002, FND-OPTIONS-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-AUDIO-005]
---

## Summary

With Slide Panels on, a panel slides in from the right edge of the screen with
a sound, and slides back out with another. The slide takes about a quarter of a
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
    let step = travel / (blit_benchmark_count / 4)
    return max(step, 16)

let travel = 344
if alternate != 0:
    travel = 320
if pref_slide_panels == 0:
    if opening != 0:
        emit PanelSlideDrawn(0)
    else:
        emit PanelSlideDrawn(travel)
    return
if opening != 0:
    play_effect(0)
else:
    play_effect(1)
let step = slide_step(travel)
let offset = 0
if opening != 0:
    offset = travel
    while offset > 0:
        offset = max(offset - step, 0)
        emit PanelSlideDrawn(offset)
else:
    while offset < travel:
        offset = min(offset + step, travel)
        emit PanelSlideDrawn(offset)
```

## Outputs

No return value. Emits `PanelSlideDrawn(offset)` for each copy, where `offset`
is how far right of its final place the panel's left edge is: 0 is the panel in
place, and `travel` is the panel gone. The final place is `(104,124,344,209)`
for a primary panel and `(128,124,320,209)` for an alternate one. With Slide
Panels on, plays slot 0 (`DATA/SND00200`) before sliding in or slot 1
(`DATA/SND00201`) before sliding out.

## Edge cases

- The step is never below 16 pixels, so on a slow machine the slide takes fewer
  copies and less than a quarter of a second.
- The slide blocks input until it ends.

## What the sources say

SRC-MANUAL-GOG, page 10, says Slide Panels is checked by default and that
unchecking it makes panels pop out instead of sliding, which helps slow
computers. It agrees with the executable.

## Differences between builds

None known.

## Open questions

- How the last partial step is taken, and whether the whole panel moves or it
  is uncovered from its left edge; the procedure's clamping is an assumption.
- What happens when `blit_benchmark_count` is below 4, which makes the divisor 0.
- Whether the slide-out draws the screen underneath as it goes, and from which
  buffer.
