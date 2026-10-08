---
id: EXP-UI-028
title: What does the original draw for a number cell at a source column where the copy goes to StretchBlt, and for a red cell wholly outside the glyph sheet's bitmap?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200 on an NVIDIA GeForce RTX 4080 at a 1920x1080 32-bit desktop, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe; the original recorded a 16-bit display and the window's device context reported 32 bits
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-028.json
---

## Question

At source columns 32762, 32764 and 32766 the cell's right edge wraps past
the 16-bit range, and the copy goes to `StretchBlt` instead of `BitBlt`
(FND-UI-065). What does the original draw at the first draw of a row for a
cell at column 32766, and for a red cell at column 516?

## Setup

As EXP-UI-002, with no Done press (`--end-turns 0`) and seed 52421. At the
first call of the planning-entry function `fn_0046FD80` (FND-UI-040) the probe
wrote 54450000 to player 0's score at `0x004A2790` and -700000 to the cash at
`0x004A25E8` (`--draw-values 0x004A2790=54450000,0x004A25E8=-700000`), so nothing
had been drawn in either row before. In five cells the first quotient is the
value divided by 10000 (RULE-UI-004): 5445 for the score, source column
32766 from the green row, and 70 for the cash, column 516 from the red
row at y 8. The other four cells of each row are `0`, green in the score and red in the cash.

## Procedure

1. Run `Rechaos.OriginalProbe new-game --executable <copy> --scenario 0
   --turns 26 --seed 52421 --end-turns 0 --capture` with the writes above.
   The function was called after 307 rolls.
2. The probe copies the writable sections and the drawing area at that
   planning entry, as EXP-UI-001 describes.
3. `extract` the run. The capture is kept as
   `GAME_DIR/captures/527bba00e759cfe1a1b13963230ada75`.

## Observations

The score's first cell, `(550, 24, 6, 7)`, and the cash's first cell,
`(550, 42, 6, 7)`, are black in all 42 pixels, the black of the console behind
them. The other cells read `0`, green in the score row and red in the cash
row.

## Results

Neither copy changes the cell: the `StretchBlt` from column 32766 and the
red `BitBlt` from column 516 leave the black console as it was.

## Conclusion

A cell wholly outside the bitmap draws nothing visible on the black console,
through either copy and from either row, on this system. The `StretchBlt`
source width of -65530 spans the whole bitmap, so the capture cannot tell a
copy that drew nothing from one that drew black, and the run does not show
whether the `StretchBlt` leaves a glyph drawn there earlier in place, as the
`BitBlt` of EXP-UI-002 does at column 516.
