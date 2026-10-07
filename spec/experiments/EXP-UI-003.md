---
id: EXP-UI-003
title: With the 32-bit white key, does the rebuild draw the selected sector and the grid tabs as the original does at the first planning entry?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-UI-003.json
---

## Question

EXP-UI-001 left 4816 pixels of the map unverified: the original drew the
selected sector and the corners around the grid tabs exact white, because its
keyed copies key nothing on a 32-bit desktop (FND-PLATFORM-014). With the key
colour the probe's `--white-key` writes, does the rebuild draw those pixels as
the original does?

## Setup

As EXP-UI-001.

## Procedure

As EXP-UI-001 with `--white-key` added
(`new-game --scenario 0 --turns 26 --seed <seed> --capture --white-key` for
seeds 52421 and 1001), then `extract --screens SCR-UI-003,SCR-HIRE-002` over
both run directories. The bitmaps are kept with the maintainer's copy of the
game as `GAME_DIR/captures/<xxh3>`.

## Observations

Begin made 310 calls of `roll` for seed 52421 and 307 for seed 1001, as in
EXP-UI-001. Both captures show pump counter 4; the first shows marker frame 6
and the second marker frame 5.

| Seed | Capture xxh3 | Exact-white pixels |
|---|---|---|
| 52421 | `3eda7566beb5c0551e5873febd1f87b9` | 1 |
| 1001 | `386a4ea133908d4bc56188bdd2ddba72` | 1 |

The map holds no exact-white pixel in either capture. In the first, the
selected sector shows the owned-sector tile inside its frame and the grid tabs
show their labels with the map around them, as FND-PLATFORM-014 describes.
The one exact-white pixel of each capture is inside an Overlord bar portrait,
that of player 3 at `(250,16)` for seed 52421 and of player 2 at `(180,16)`
for seed 1001. The first is the pixel FND-PLATFORM-014 found left white with
the write and did not examine.

## Results

A test of the rebuild reaches the same state for both runs, compared as in
EXP-UI-001.

Another test replays each run, draws its endpoint at the capture's
marker frame and compares every element with the capture. The fixture lists the
setup input `key_colour`, so the test compares exact white like any other colour
(docs/VALIDATION.md). No element differs: outside the masks of DEV-UI-006 (the
cash row) and DEV-UI-023 (the key line) every pixel matches, the selected sector
and the corners around the grid tabs included, and the rebuild draws the
exact-white portrait pixel white as well.

## Conclusion

At the first planning entry of both seeds the rebuild draws the selected
sector and the grid tabs as the original does once the original's key colour
matches its surfaces' white, so the city screen, the console and the Hire dock
match the original everywhere the two deviations do not draw. The runs cover
the same scenario and planning entry as EXP-UI-001, and a second marker frame.
