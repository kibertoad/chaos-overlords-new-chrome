---
id: EXP-UI-039
title: What does the original draw over an earlier glyph for a number cell at column 508, and for a red cell at the StretchBlt column 32762?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200 on an NVIDIA GeForce RTX 4080 at a 1920x1080 32-bit desktop, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe; the original recorded a 16-bit display and the window's device context reported 32 bits
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-039.json
---

## Question

At source column 508 four of a cell's six pixel columns lie inside the
512-pixel bitmap, and at column 32762 the copy goes to `StretchBlt` with a
source width of -65530 (FND-UI-065). No run has drawn a cell at either
column. What does the original draw there over a cell that already holds a
glyph?

## Setup

As EXP-UI-002, with one Done press (`--end-turns 1`) and seed 52421. At the
first call of the planning-entry function `fn_0046FD80` (FND-UI-040) the probe
wrote 80000 to player 0's score at `0x004A2790` and cash at `0x004A25E8`; at
its second call, the human's second planning entry, it wrote 219140000 to the
score and -163670000 to the cash
(`--draw-values 0x004A2790=80000/219140000,0x004A25E8=80000/-163670000`).

In five cells the first quotient is the value divided by 10000 (RULE-UI-004).
At the first entry it is 8 in both rows, a green `8` in both first cells. At
the second it is 21914 for the score, glyph 21930 at source column 508 from the green
row, and 16367 for the cash, glyph 16383 at column 32762 from the red row
at y 8. The other four cells of each row are `0`, green
in the score and red in the cash.

## Procedure

1. Run `Rechaos.OriginalProbe new-game --executable <copy> --scenario 0
   --turns 26 --seed 52421 --end-turns 1 --capture` with the writes above.
   The function was called after 307 and 409 rolls.
2. At the second planning entry the probe copies the writable sections and
   the drawing area, as EXP-UI-001 describes.
3. `extract` the run. The capture is kept as
   `GAME_DIR/captures/53331249f3cec25477a3b305838ba00a`.

## Observations

The score's first cell, `(550, 24, 6, 7)`, holds (90, 99, 206) in its four
left pixel columns in all seven rows, the colour of `PX00129` at columns
508 to 511, rows 0 to 6, and the pixels of the `8` the first planning entry drew there in its two right pixel columns.
The cash's first cell, `(550, 42, 6, 7)`, holds exactly the pixels of the `8` the first planning entry drew there.
The other cells read `0`, green in the score row and red in the cash row.

## Results

The cell at column 508 copies its four pixel columns inside the bitmap
and leaves the two right ones as they were. The red `StretchBlt` from
column 32762 leaves the earlier glyph in place.

## Conclusion

Column 508 clips as 510 does (EXP-UI-002, EXP-UI-027), and the
`StretchBlt` at 32762 draws nothing, as at 32766 (EXP-UI-038). The run
is one system and one seed.
