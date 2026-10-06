---
id: EXP-UI-001
title: What does the original draw on the city screen and console at the first planning entry of a new Greed match?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-UI-001.json
---

## Question

At the first planning entry of a new local match with one human in slot 0, do
the city screen and its console (SCR-UI-003) and the Hire dock (SCR-HIRE-002)
show what their entries give, at the positions their Position columns give?

## Setup

As EXP-SETUP-001, from the copy EXP-TURN-001 describes, with scenario 0
(Greed) and a 26-turn limit written before Begin. The two runs differ only in
the seed written over the argument of `srand`: 52421 and 1001.

## Procedure

1. Run `Rechaos.OriginalProbe new-game --executable <copy> --scenario 0
   --turns 26 --seed <seed> --capture` once for each seed. The probe presses
   Begin and, once the human's first planning phase waits for input, copies
   the writable sections.
2. Then it copies the 640-by-460 drawing area (RULE-GFX-002) twice from the
   window's device context with BitBlt, without asking the window to repaint.
   It keeps the copies when they agree byte for byte and the Overlord bar's
   marker counter (FND-UI-038) and the pump's counter (FND-UI-017) held still
   across both, and notes the marker frame.
3. Run `extract --screens SCR-UI-003,SCR-HIRE-002` over both run directories.
   For each element the probe's screen files list, with the rectangle its
   entry's Position column gives for player 0, it writes the xxh3 of the
   rectangle's red, green and blue bytes and the number of its pixels that are
   exact white. The bitmaps are kept with the maintainer's copy of the game as
   `GAME_DIR/captures/<xxh3>`.

## Observations

Begin made 310 calls of `roll` for seed 52421 and 307 for seed 1001. Both captures show marker
frame 6 and pump counter 4. The original reported a 16-bit display while the
window's device context reported 32 bits.

| Seed | Capture xxh3 | Selected sector | Exact-white pixels |
|---|---|---|---|
| 52421 | `c0cc11047db3c31a12d3adc59d298f5b` | E2 | 5089 |
| 1001 | `380e6d86c57b334eaea6c482b4f295a6` | G7 | 5089 |

In both captures the selected sector, the human's headquarters, is a solid
white 48-by-46 square over the sector's tile, the kind of white area
FND-UI-041 describes on Windows 11. All but one of the other exact-white
pixels surround the grid tabs along the map's four edges, where the tab art
leaves the map showing through; the remaining one is inside a portrait. The
fixture gives the digest and the white count of every element.

The same first configuration was recorded again with DDrawCompat v0.7.1 as
`ddraw.dll` beside the copy, with and without the compatibility layers. It
made the same 310 rolls, and its capture differed from the one above only in
the marker, which was at frame 5: the same 5089 pixels were exact white.

## Results

Every result is the one RULE-RNG-002 computes from the recorded seed, and a test
of the rebuild reaches the same state, compared as in EXP-SETUP-001. No planning
pass has run yet, so the original holds 0 in every auxiliary record and combat
record and -1 in each player's `hire_role`; the replay leaves those out as it
does for records no pass has written.

A test of the rebuild replays each run, draws its endpoint at marker frame 6 and
compares every element with the capture. No element differs. Outside the masks
of DEV-UI-006 (the cash row) and DEV-UI-023 (the key line), every pixel matches
except 4816 pixels of the map that the original drew exact white and the rebuild
does not: the whole selected sector, where the rebuild draws the tile in the
owner's colour, and the corners around the grid tabs, where it draws the map.
Those pixels are unverified.

## Conclusion

At the first planning entry of both seeds the rebuild draws the city screen,
the console and the Hire dock as the original does, at the positions
SCR-UI-003 and SCR-HIRE-002 give, apart from the selected sector's tile and the
corners around the grid tabs, which the original left white, and the rows the
two deviations draw. The runs cover
one scenario, the first planning entry and one marker frame; they show no
panel, no later turn and no hire or snub mark. A DirectDraw wrapper does not
remove the white areas, since the windowed original draws with GDI only
(FND-GFX-004).
