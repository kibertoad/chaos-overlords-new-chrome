---
id: RULE-UI-001
title: A push-button control acts only when released inside
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-032, FND-AUDIO-010, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-AUDIO-005]
---

## Summary

Pressing a push-button draws it pressed and plays the press sound. While the
button is held, the control shows pressed only while the pointer is over it.
Releasing over the control makes it act; releasing anywhere else cancels.

## When it runs

When the left mouse button goes down on a push-button control: the eight tiles
of the main console (RULE-UI-002), the four buttons of the setup screens, the
two Hire reject gates, the Ready button of the handoff panel and the three
controls of the endgame screens.

## Parameters

- `x` (`INT32`), `y` (`INT32`), `w` (`INT32`), `h` (`INT32`): the control's rectangle,
  half-open.

## Inputs

`pointer_x`, `pointer_y`, `left_button_down`.

## Procedure

```text
define pointer_in_rect(x, y, w, h) -> INT32:
    return pointer_x >= x and pointer_x < x + w and pointer_y >= y and pointer_y < y + h

emit ControlArtDrawn(1)
play_effect(2)
let shown = 1
while left_button_down != 0:
    let now = pointer_in_rect(x, y, w, h)
    if now != shown:
        emit ControlArtDrawn(now)
        shown = now
return pointer_in_rect(x, y, w, h)
```

## Outputs

Returns 1 when the button was released inside the rectangle and 0 otherwise.
Emits `ControlArtDrawn(1)` when the pressed art is drawn and
`ControlArtDrawn(0)` when the control's released art is restored, and plays
slot 2 (`DATA/SND00202`) once, through `play_effect`, at the press.

## Edge cases

The press sound plays even when the press is then cancelled.

## What the sources say

None of the sources describes the pressed state.

## Differences between builds

None known.

## Open questions

- Whether the art is restored after an accepted release, before the control
  acts.
- The Hire reject gates play slot 2 through the shared pointer helper; whether
  that helper tracks the pointer the same way has not been recorded.
