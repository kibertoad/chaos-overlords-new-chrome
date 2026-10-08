---
id: EXP-UI-040
title: What does the original draw over an earlier glyph for a number cell at column -2, and for a red cell at the StretchBlt column 32764?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200 on an NVIDIA GeForce RTX 4080 at a 1920x1080 32-bit desktop, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe; the original recorded a 16-bit display and the window's device context reported 32 bits
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-040.json
---

## Question

EXP-UI-038 drew a red cell at column -2 and `StretchBlt` cells at 32766
and, in EXP-UI-039, 32762 (FND-UI-065). What does the original draw over a
cell that already holds a glyph for a green cell at column -2, and for a
red cell at 32764, the last `StretchBlt` column?

## Setup

As EXP-UI-002, with one Done press (`--end-turns 1`) and seed 52421. At the
first call of the planning-entry function `fn_0046FD80` (FND-UI-040) the probe
wrote 80000 to player 0's score at `0x004A2790` and cash at `0x004A25E8`; at
its second call, the human's second planning entry, it wrote 218290000 to the
score and -272900000 to the cash
(`--draw-values 0x004A2790=80000/218290000,0x004A25E8=80000/-272900000`).

In five cells the first quotient is the value divided by 10000 (RULE-UI-004).
At the first entry it is 8 in both rows, a green `8` in both first cells. At
the second it is 21829 for the score, glyph 21845 at source column -2 from the green
row, and 27290 for the cash, glyph 27306 at column 32764 from the red row
at y 8. The other four cells of each row are `0`, green
in the score and red in the cash.

## Procedure

1. Run `Rechaos.OriginalProbe new-game --executable <copy> --scenario 0
   --turns 26 --seed 52421 --end-turns 1 --capture` with the writes above.
   The function was called after 307 and 409 rolls.
2. At the second planning entry the probe copies the writable sections and
   the drawing area, as EXP-UI-001 describes.
3. `extract` the run. The capture is kept as
   `GAME_DIR/captures/06b0dccb4dc2e33f7f5bf188a658f603`.

## Observations

The score's first cell, `(550, 24, 6, 7)`, holds the pixels of the `8` the first planning entry drew there in its
two left pixel columns and black in its four right pixel columns in all
seven rows, the colour of `PX00129` at columns 0 to 3, rows 0 to 6. The
cash's first cell, `(550, 42, 6, 7)`, holds exactly the pixels of the `8` the first planning entry drew there.
The other cells read `0`, green in the score row and red in the cash row.

## Results

The green cell at column -2 draws as the red one of EXP-UI-038 does: its
four inside pixel columns land in the cell's four right pixel columns. The
red `StretchBlt` from column 32764 leaves the earlier glyph in place.

## Conclusion

Column -2 clips the same way from either row, and none of the three
`StretchBlt` columns 32762, 32764 and 32766 changes the cell, from either
row (EXP-UI-038, EXP-UI-039). The run is one system and one seed.
