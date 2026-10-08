---
id: EXP-UI-038
title: What does the original draw over an earlier glyph for a number cell at the StretchBlt column 32766, and for a red cell at column -2?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200 on an NVIDIA GeForce RTX 4080 at a 1920x1080 32-bit desktop, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe; the original recorded a 16-bit display and the window's device context reported 32 bits
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-038.json
---

## Question

EXP-UI-028 drew a cell at source column 32766, where the copy goes to
`StretchBlt` with a source width of -65530 spanning the whole bitmap, over
the black console, where drawing nothing and drawing black look the same.
EXP-UI-027 drew a cell at column -4 and no run has drawn one at -2, the
column whose four right pixel columns come from sheet columns 0 to 3
(FND-UI-065). What does the original draw at those two columns over a cell
that already holds a glyph?

## Setup

As EXP-UI-002, with one Done press (`--end-turns 1`) and seed 52421. At the
first call of the planning-entry function `fn_0046FD80` (FND-UI-040) the probe
wrote 80000 to player 0's score at `0x004A2790` and cash at `0x004A25E8`; at
its second call, the human's second planning entry, it wrote 54450000 to the
score and -218290000 to the cash
(`--draw-values 0x004A2790=80000/54450000,0x004A25E8=80000/-218290000`).

In five cells the first quotient is the value divided by 10000 (RULE-UI-004).
At the first entry it is 8 in both rows, a green `8` in both first cells. At
the second it is 5445 for the score, glyph 5461 at source column 32766 from the green
row, and 21829 for the cash, glyph 21845 at column -2 from the red row at
y 8. The other four cells of each row are `0`, green
in the score and red in the cash.

## Procedure

1. Run `Rechaos.OriginalProbe new-game --executable <copy> --scenario 0
   --turns 26 --seed 52421 --end-turns 1 --capture` with the writes above.
   The function was called after 307 and 409 rolls.
2. At the second planning entry the probe copies the writable sections and
   the drawing area, as EXP-UI-001 describes.
3. `extract` the run. The capture is kept as
   `GAME_DIR/captures/a35ea04e16343b73288b5c6c575640d4`.

## Observations

The score's first cell, `(550, 24, 6, 7)`, holds exactly the pixels of the `8` the first planning entry drew there.
The cash's first cell, `(550, 42, 6, 7)`, holds the `8`'s pixels in its
two left pixel columns and black in its four right pixel columns in all
seven rows, the colour of `PX00129` at columns 0 to 3, rows 8 to 14, where
the `8` had green pixels in the first three of them. The other cells read
`0`, green in the score row and red in the cash row.

## Results

The `StretchBlt` from column 32766 leaves the earlier glyph in place, so it
changes no pixel of the cell. The red cell at column -2 copies the four
pixel columns that lie inside the bitmap, sheet columns 0 to 3, into the
cell's four right pixel columns, and leaves the two left ones as they were.

## Conclusion

At column 32766 the copy draws nothing, over a glyph as over the black
console (EXP-UI-028). At column -2 the part inside the bitmap is drawn at
its place in the cell, two pixels in, and the rest keeps the earlier draw,
as at -4 (EXP-UI-027) and at 510 (EXP-UI-002). The run is one system and
one seed.
