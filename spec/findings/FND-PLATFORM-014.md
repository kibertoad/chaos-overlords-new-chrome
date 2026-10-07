---
id: FND-PLATFORM-014
title: On a 32-bit desktop the keyed copies key nothing, and the white they should drop is drawn
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: dynamic
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427C84
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427CB8
tool: tools/Rechaos.OriginalProbe and synchronized window BitBlt capture
environment: Windows 11 with a 32-bit desktop, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed by the probe with a 640-by-460 drawing area
---

## Observation

On 2026-10-05 the hash-verified BLD-GOG-EN-1.1 executable ran EXP-SETUP-001's
setup with seed 52421 to the first planning entry, and the probe took a
capture there as docs/validation/screen-captures.md describes. The original's display depth
`0x0048787C` (FND-PLATFORM-009) held 16; the probe's window device context
reported 32.

- The capture held 5089 exact-white pixels. Among them, the selected sector,
  which the human owns, showed the selected-sector frame of `PX00129` around a
  solid white interior where the owned-sector interior of `PX10001` belongs
  (SCR-UI-003, FND-UI-033), and the edge tabs of the grid were white
  triangles without their letters and digits.
- The keyed mask compositor `0x00427A09` (FND-PLATFORM-008) reached both of its
  `SetBkColor` calls during the run. The call at `0x00427C84` passed
  `RGB(255,252,255)` (`0x00FFFCFF`) and the call at `0x00427CB8` passed
  `RGB(255,255,255)`.
- With the probe writing `RGB(255,255,255)` over the colour argument of the
  call at `0x00427C84` each time it passed `RGB(255,252,255)`, the same setup
  drew one exact-white pixel, at `(250,16)`. The selected sector showed the
  `PX10001` interior inside its frame, with its markers over it, and the edge
  tabs showed their labels. The run made the same 310
  rolls with the same bounds and results, and every one of the 2760 values of
  EXP-SETUP-001's end state was the same.
- EXP-TURN-041's configuration (scenario 0, 26 turns, seed 52421, Done pressed
  26 times) recorded with the same write made the same 10647 rolls, left the
  generator in the same state, pressed Done at the same roll counts, and every
  one of the 4331 values of the fixture's end state and its events were the
  same.
- Without the write, the first capture was the same, 5089 exact-white pixels
  and the same white selected sector, with the compatibility layers above, with
  16BITCOLOR added, with DWM8And16BitMitigation or WINXPSP2 left out, with only
  640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, and with no layer. Every one of
  those runs made the same 310 rolls.
- A separate program on the same machine ran the GDI calls of the pattern fill
  of FND-GFX-006 with bitmaps 143, 146 and 147 loaded from the executable, into
  bitmaps compatible with the 32-bit screen, and drew the expected patterns.

## Interpretation

The surfaces are bitmaps compatible with the window's device context
(FND-GFX-004), so on a 32-bit desktop they hold 32-bit pixels, and the RGB555
white `0x7FFF` of a `PX16` image is held there as `RGB(255,255,255)`. The
16-bit key `RGB(255,252,255)` of FND-PLATFORM-008 matches no pixel of such a
surface, so every mode-1 copy is opaque and draws the white it should leave
out. The solid white areas the original leaves on Windows 11, such as those
FND-UI-041 mentions, are these copies; in the captured planning entry they are
all of them. The game's state does not depend on the key: the colour only
reaches the drawing.

## Alternatives

The second call at `0x00427CB8` already passes `RGB(255,255,255)`; what it sets
the colour for was not read. FND-PLATFORM-008 reads a single `SetBkColor` call
in the drawing code and takes `RGB(255,255,255)` for the 8-bit key; this run,
at 16-bit depth, reached two calls, so that `RGB(255,255,255)` may come from the
second call and not be a key, and the 8-bit key needs a new static reading. On
a real 16-bit display the key names `0x7FFF` and the copies leave it out as
RULE-GFX-003 describes; no run on such a display was made, and how a driver
expands 5-bit channels there is still the open question of FND-PLATFORM-008.
What draws the exact-white pixel left at `(250,16)` with the write was not
examined. The planning entry is the only capture with the write recorded here.

## How to reproduce

Stage the executable as docs/validation/experiments.md describes and run the probe's
`new-game --seed 52421 --capture`, then the same with `--white-key`, which
makes the write above. Count the exact-white pixels of each `capture-blt.bmp`
and extract both runs with `extract --experiment EXP-SETUP-001` to compare
their end states with the fixture.
