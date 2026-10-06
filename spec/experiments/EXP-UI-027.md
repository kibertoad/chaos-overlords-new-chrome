---
id: EXP-UI-027
title: What does the original draw for a number cell whose source column is negative, and for a red cell partly outside the glyph sheet's bitmap?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200 on an NVIDIA GeForce RTX 4080 at a 1920x1080 32-bit desktop, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe; the original recorded a 16-bit display and the window's device context reported 32 bits
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-027.json
---

## Question

EXP-UI-002 recorded green cells at source columns 510 and 516 over a cell
that already held a glyph. What does the original draw at the first draw of a
row for a cell whose column `int16(6 * (16 + q))` is -4 (the cell's two right
pixel columns inside the 512-pixel bitmap), and for a red cell at column 510?

## Setup

As EXP-UI-002, with no Done press (`--end-turns 0`) and seed 52421. At the
first call of the planning-entry function `fn_0046FD80` (FND-UI-040) the probe
wrote 109060000 to player 0's score at `0x004A2790` and -690000 to the cash at
`0x004A25E8` (`--draw-values 0x004A2790=109060000,0x004A25E8=-690000`), so nothing
had been drawn in either row before. In five cells the first quotient is the
value divided by 10000 (RULE-UI-004): 10906 for the score, source column
`int16(65532)`, -4 from the green row, and 69 for the cash, column 510 from the red
row at y 8. The other four cells of each row are `0`, green in the score and red in the cash.

## Procedure

1. Run `Rechaos.OriginalProbe new-game --executable <copy> --scenario 0
   --turns 26 --seed 52421 --end-turns 0 --capture` with the writes above.
   The function was called after 307 rolls.
2. The probe copies the writable sections and the drawing area at that
   planning entry, as EXP-UI-001 describes.
3. `extract` the run. The capture is kept as
   `GAME_DIR/captures/7091b0051519e529813b2a2125480909`.

## Observations

The score's first cell, `(550, 24, 6, 7)`, is black in all 42 pixels, the
black of the console behind it. The cash's first cell, `(550, 42, 6, 7)`,
holds (90, 99, 206) in its two left pixel columns in all seven rows, the colour
of `PX00129` at columns 510 and 511, rows 8 to 14, and black in the other four.
The other cells read `0`, green in the score row and red in the cash row.

## Results

A red cell at column 510 draws its two left pixel columns from the sheet and
leaves the other four as they were, as the green cell of EXP-UI-002 did. The
cell at column -4 shows black throughout. Its two right pixel columns would
come from sheet columns 0 and 1 of the green row, which are the black space
glyph, so the capture cannot tell a clipped copy from no copy at all for this
column.

## Conclusion

The red row clips as the green row does. A negative column draws nothing
visible on the black console; -4 is the only negative column a cell can
partly overlap, and its inside part is black in the sheet, so on any
background the result looks the same as a clipped copy. The run is one
system and one seed.
