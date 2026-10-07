# DEV-GFX-001

- Departs from: RULE-GFX-002, RULE-UI-013, RULE-UI-014
- Replaces: RULE-GFX-002
- Reason: The 640-by-460 drawing area is drawn into a resizable window. The window opens at the
  largest whole multiple of the area, up to 2, that fits in nine tenths of the display; a resized
  window draws the area at the largest scale that fits, whole or not, and letterboxes it. Full
  screen is a borderless window at the desktop's mode in 32-bit colour, with no menu bar above the
  area, and it stays open when it loses focus. The original sizes a window under the Windows menu
  bar, or switches the display to 640 by 480 at 8 or 16 bits and minimizes itself when it loses
  focus.
- Setting: None
- Default: mandatory
- Justification: Every pixel of the drawing area is the original's. The window opens at a whole
  multiple, which keeps each pixel square, and only a window the player resizes scales by a
  fraction. At one to one the area is a small patch on a current display, and many current
  drivers no longer offer 640 by 480 at 8 or 16 bits, so a mode-switch setting would offer a mode
  the display may refuse. No rule depends on the window.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.cs
- Dropped: no
