---
id: RULE-GFX-002
title: The display is a 640-by-480 window or screen whose drawing area of 640 by 460 sits directly under the menu bar and is copied from an off-screen surface
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-GFX-004, FND-PLATFORM-009, FND-PLATFORM-007, FND-PLATFORM-008, FND-UI-020]
conflicting: []
split_with: []
related: [RULE-UI-013, RULE-UI-014]
---

## Summary

The game draws with GDI into off-screen surfaces and copies what changed to the
window. In a window, the window is sized for a 640-wide client with the menu
bar above the drawing area. In full screen the display is switched to 640 by
480 at the depth of the image set, and the menu font is made smaller so that
the menu bar fits in the 20 rows above the 460-row drawing area. The drawing
area's top-left corner is the client area's top-left corner in both modes.

## When it runs

The setup runs once at start (RULE-UI-013) and its undo once at exit. The
copies to the window run whenever a screen or panel changes and when the window
is repainted.

## Parameters

- `want` (`INT32`): the depth asked for, 8, 16 or 0.

## Inputs

`full_screen_active`, `system_menu_height`, `desktop_depth`, `display_mode_set`.

## Procedure

```text
if full_screen_active == 0:
    # a captioned window with a system menu and a minimize box, not resizable,
    # at 32, 32; the frame is computed for a client rectangle of 643 by
    # 488 + system_menu_height and the adjusted right and bottom edges are
    # passed as the window's width and height
    display_depth = min(max(desktop_depth, 8), 16)
else:
    # a borderless window of 640 by 480 at 0, 0, with exclusive full-screen
    # DirectDraw access
    # when DirectDraw cannot be created no mode is set and the depth stays 0
    display_depth = 0
    if display_mode_set(640, 480, want) != 0:
        display_depth = want
    else if want == 16 and display_mode_set(640, 480, 8) != 0:
        display_depth = 8
    # the menu bar height is set to 18 and its font to an 8-pixel sans serif
    # font for the whole system until exit
# surface 0 is the window; surfaces 1 (640 by 460), 2, 3, 5, 6, 7 and 11 are
# memory bitmaps at display_depth, filled white when created (RULE-UI-013)

define present(rect):
    # screens and panels draw into surface 1 or straight into the window, and
    # copy each changed rectangle from surface 1 to the same place in the window
    emit WindowAreaCopied(rect)

# a repaint (event 7) in a loop that reads events presents the whole area,
# top 0, left 0, bottom 460, right 640
```

## Outputs

`display_depth` is the depth the surfaces are created at. `WindowAreaCopied`
is emitted for each copy from the off-screen surface to the window; the
rectangle is in drawing-area coordinates, which are the window's client
coordinates. At exit the display mode, the menu metrics and the window's palette
are put back.

## Edge cases

- A copy between rectangles of different sizes is stretched with
  `COLORONCOLOR`, and any transparent copy mode is dropped (FND-PLATFORM-008).
- Nothing replaces the Windows system colours, although the code to do it is
  present behind a flag that is never set.
- In a window the client area is a few rows taller than 460 on usual system
  metrics; the rows below the drawing area show the black window background.
- When no display mode can be set, or DirectDraw cannot be created, the depth
  is 0 and the program ends (RULE-UI-013).
- A repaint that arrives while a loop only dispatches messages is validated
  without drawing, so the window is not redrawn until that loop's own copies.

## What the sources say

None of the sources describes the display.

## Differences between builds

None known.

## Open questions

- The exact client height of the window depends on the running system's
  metrics and needs a run of the original.
