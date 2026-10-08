---
id: EXP-SEARCH-002
title: Does the Search panel change the filter bytes of the active player when the human is in slot 2?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-SEARCH-002.json
---

## Question

The filter table holds 22 bytes per player. With the only human in slot 2,
do the Search panel's presses change bytes 44 to 65 and no others?

## Setup

As EXP-UI-001.

## Procedure

As EXP-SEARCH-001 with `--humans 2`
(`new-game --executable <copy> --scenario 0 --seed 52421 --humans 2
--search-clicks 576:266,161:151,161:183,263:153,263:228,379:303,379:228,263:228,161:151,263:198,161:183,304:314,263:228,161:304,576:266,161:304`).

## Observations

Begin made 316 calls of `roll`. The active player is 2 throughout. Every
press changes the bytes of player 2 as the same press changed those of
player 0 in EXP-SEARCH-001, and the bytes of the other five players stay 0.

## Results

A test of the rebuild compares the run as in EXP-SEARCH-001, and every table is
the same.

## Conclusion

The run agrees with RULE-SEARCH-001 for a player other than 0: the panel works
on the active player's 22 bytes only.
