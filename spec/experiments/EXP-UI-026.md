---
id: EXP-UI-026
title: When does closing the window during planning ask to save first?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-026.json
---

## Question

Do a new match, a resolved turn and a hire drag leave the saved byte at
`0x00498350` clear, does closing the window open dialog 129 exactly while that
byte is 0, and which of its answers save and quit?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 0 --mentality 1 --humans 0 --seed 16
--end-turns 1 --saved 1:1,2:1 --hire-steps exit,exit,drag:0:12 --closes
-:2,-:1c,-:1w,-:3,1:3`.

The probe reads the byte before each write, and writes 1 to it before the Done
press of turn 1 and again at the dump in turn 2's planning. After the dump it
closes the Hire panel, drags the first offer to sector 12, and then posts
`WM_CLOSE` to the window five times: four times as the game stands, then after
writing 1 to the byte. A breakpoint on the dialog opener `fn_00465CEC`
(FND-UI-022) records each dialog asked for and returns the step's answer
without showing it: 2 (cancel), 1 (save first) twice and 3 (go on without
saving). A breakpoint on the save `fn_00463CC5` counts its calls and returns
without running it, reporting the save cancelled for the first answer 1 and
written for the second. Breakpoints on File, Exit's three stores of the quit
byte `0x00487828` (`0x00470586`, `0x004705A8`, `0x004705FB`, FND-UI-058) record
which one was reached and skip it, so the game stays in planning for the next
close. After each close the probe waits up to three seconds for a store.

## Observations

The byte was 0 before the turn 1 write, in the new match's first planning, and
0 again before the write at the dump, after turn 1 resolved. After the hire
drag, which stored the order, it was 0. Each of the first four closes opened
dialog 129:

- cancel called no save and reached no store;
- save first called the save once; cancelled, it reached no store, and written,
  it reached the store at `0x00470586`;
- go on without saving called no save and reached the store at `0x004705A8`.

The fifth close, with the byte at 1, opened no dialog and reached the store at
`0x004705FB`.

Separate runs from the same start, which let the stores run, agreed: a close
that reaches a store ends the process, and with the byte written to 1 at the
dump and the Hire panel closed without a drag, a close opened no dialog and
quit.

## Results

`ClosingAsksToSaveExactlyWhileTheMatchIsUnsaved` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.Closes.cs` plays the same
steps in the rebuild and compares the saved mark before each write, and for
each close whether it asks, whether the answer opens the save browser, and
whether the game leaves.

## Conclusion

The run supports RULE-UI-015 for a local game: a new match and a resolved
turn leave the match unsaved, a hire drag clears the saved mark, a close asks
to save first exactly while the match is unsaved, and each answer saves and
quits as the static reading says. The save dialog itself was not shown; the
probe stood in for its result.
