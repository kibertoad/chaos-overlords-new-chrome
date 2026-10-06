---
id: EXP-UI-002
title: What does the original draw for a number cell whose source column lies partly or wholly outside the glyph sheet's bitmap?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200 on an NVIDIA GeForce RTX 4080 at a 1920x1080 32-bit desktop, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe; the original recorded a 16-bit display and the window's device context reported 32 bits
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-UI-002.json
---

## Question

The number helpers copy a cell whose first quotient is `q` from source column
`6 * (16 + q)` of the 512-pixel-wide surface that holds `PX00129`
(RULE-UI-004). When the column is 510 (2 of the cell's 6 pixel columns inside
the bitmap) or 516 (none inside), what does the cell on screen show?

## Setup

As EXP-UI-001, with one Done press (`--end-turns 1`) and seeds 52421 and
1001. The probe set a breakpoint on the planning-entry function `fn_0046FD80`,
which draws the active player's score from `0x004A2790` and cash from
`0x004A25E8` as five cells each (FND-UI-040). At the function's first call it
wrote 80000 to player 0's score and cash; at its second call, the human's
second planning entry, it wrote 700000 to the score and 690000 to the cash
(`--draw-values 0x004A2790=80000/700000,0x004A25E8=80000/690000`).

In five cells the first quotient is the value divided by 10000 (RULE-UI-004):
8 for 80000, a green `8` at source column 144; 69 for 690000, source column
510; 70 for 700000, source column 516. The other four cells are `0` in each
case.

## Procedure

1. Run `Rechaos.OriginalProbe new-game --executable <copy> --scenario 0
   --turns 26 --seed <seed> --end-turns 1 --capture` with the writes above,
   once for each seed. The function was called once at each planning entry,
   after 307 and 409 rolls for seed 52421 and after 304 and 407 for seed 1001.
2. At the second planning entry the probe copies the writable sections and
   the drawing area, as EXP-UI-001 describes.
3. `extract` the two runs. The captures are kept as
   `GAME_DIR/captures/50c3202cfc2b7d7acac0106bf3636ff9` (seed 52421) and
   `GAME_DIR/captures/73b4ef7aa6bf17d1f69623a5c99e33ba` (seed 1001).

## Observations

In both captures the score's first cell, `(550, 24, 6, 7)`, holds exactly the
pixels of the `8` the first planning entry drew there, and the score row
reads `80000`. The cash's first cell, `(550, 42, 6, 7)`, holds in its two left
pixel columns the colour of `PX00129` at columns 510 and 511, rows 0 to 6,
(90, 99, 206) in the capture, and in its four right pixel columns the pixels
of the `8` drawn there before. The other cells of both rows are the `0` glyph.
The two captures agree in both rows.

A first run with the same writes at the first planning entry only (no Done
press) drew the same: the two left pixel columns of the 690000 cell took the
sheet's colour and the rest of both cells kept the black of the console
behind them.

## Results

On this system the copy changes only the destination pixels whose source
pixel lies inside the 512-by-646 bitmap. A cell at column 516 leaves the
screen as it was, and a cell at column 510 draws its two left pixel columns
from the sheet and leaves the other four as they were.

## Conclusion

For a source cell not wholly inside the bitmap, the original as run here
draws the part inside and leaves the rest of the destination unchanged. The
run covers columns 510 and 516 from the green row through `BitBlt`, on one
system; it does not cover a negative column, the red row or the three columns
where the copy goes to `StretchBlt`, and other versions of Windows may differ.
