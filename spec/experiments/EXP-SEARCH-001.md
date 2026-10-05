---
id: EXP-SEARCH-001
title: How do the Search panel's ALL, NONE and rows change the filter table at the first planning entry?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-SEARCH-001.json
---

## Question

No recorded run presses the Search panel. Does the filter table start empty,
do ALL and NONE set and clear all 22 bytes of the active player, does a row
press flip only its own byte, and does the filter outlast the panel?

## Setup

As EXP-UI-001.

## Procedure

Run `Rechaos.OriginalProbe new-game --executable <copy> --scenario 0 --seed
52421 --search-clicks 576:266,161:151,161:183,263:153,263:228,379:303,379:228,263:228,161:151,263:198,161:183,304:314,263:228,161:304,576:266,161:304`. The probe stops at the first planning entry as
EXP-UI-001 does and dumps the state. Then it posts each click to the window as
a left-button press and release, lets the original run for half a second, and
keeps the whole 132-byte `search_filters` table (FND-SEARCH-001), the active
player and whether the Search handler `fn_00448E32` is running
(FND-SEARCH-002).

The clicks, in client coordinates with the panel at `(104,124)`, are: the
lower half of the console's Ranking/Search control; ALL; NONE; rows 0, 5, 21
and 16 and row 5 again; ALL; row 3; NONE; a point of the panel that is no
control; row 5; Done; the Search control again; Done. Row `n` is pressed at
`(206 + 116 * (n / 11) + 57, 146 + 15 * (n % 11) + 7)`. No two consecutive
presses hit the same row, so none is a double-click.

## Observations

Begin made 310 calls of `roll`, as in EXP-UI-001. The active player is 0
throughout. The first press opens the panel with every byte of the table 0.
After ALL the active player's 22 bytes are 1, and after NONE 0. Rows 0, 5, 21
and 16 each set their own byte, and row 5 pressed again clears it. ALL then
row 3 leaves every byte but byte 3 at 1. NONE clears them, and the press on no
control changes nothing. Row 5 sets byte 5, Done closes the panel, and the
reopened panel still holds byte 5 until the last Done closes it. The bytes of
players 1 to 5 stay 0 throughout.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` reaches the same state
as the original at the dump. `TheSearchPanelChangesTheOriginalsFilters` in
`OriginalNewGameExperimentTests.Presentation.cs` passes each recorded click to
the rebuild: the console's hit test opens Search at the first point, and the
Search panel's hit test and its press handling, starting from an empty
selection, give the same table after every press and the same open or closed
panel.

## Conclusion

The run agrees with RULE-SEARCH-001 for an empty start, ALL, NONE, row presses
in both directions and a press on no control, and with FND-SEARCH-002 for the
positions of the controls it pressed. It has one human and makes no
double-click and no load.
