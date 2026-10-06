---
id: EXP-UI-022
title: When does the original show the hourglass during a local match with one human?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-022.json
---

## Question

In a local match of one human in slot 2 and five computers, which pointer
shape does the original select around each stretch of work, and does any
call of `roll` happen under the arrow once the city is being set up?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --humans 2 --seed 5151 --end-turns 3
--pointer`.

The probe sets a breakpoint on the cursor helper `fn_00465BC8(shape, force)`
(FND-UI-034) and records each call with the number of `roll` calls and Done
presses before it, its two arguments and the address of the call. It posts
its clicks to the window and never moves the real pointer, so Windows sends
no `WM_SETCURSOR` and the window procedure's own calls are not made. The
state is dumped at the fourth planning entry.

## Observations

The run made 641 calls of `roll`, and the Done presses came after 323, 421
and 533 of them. The helper was called 51 times, always with force 1, and
only with shape 0 (the arrow) or 4 (the hourglass):

| Call at | Shape | When |
|---|---|---|
| `0x00461321` | arrow | Once, before the first roll |
| `0x0046E794` | hourglass | After 6 rolls, before the city is set up |
| `0x0046ED23` | arrow | After the city is set up, 313 rolls |
| `0x0046F4C9` | hourglass | Before each computer's planning |
| `0x0046F4F8` | arrow | After each computer's planning |
| `0x0046FD9B` | hourglass | Before the human's planning entry |
| `0x00470255` | arrow | At the human's planning entry, after its hire offers are drawn |
| `0x004726CD` | hourglass | Before the turn is resolved |
| `0x00472763` | arrow | After the turn is resolved |

In turn 1 the computers in slots 0 and 1 plan after the setup, then the human
plans. After each Done press the computers in slots 3, 4 and 5 plan, the turn
is resolved, and the computers in slots 0 and 1 and the human's planning entry
follow. Each arrow that ends a stretch of work is followed by the hourglass of
the next stretch with no `roll` between them, up to the arrow at the human's
planning entry, which is the last call before each Done press. The first call
after each Done press is the hourglass. All 635 rolls from the setup's
hourglass on were made while the hourglass was the last shape selected.

## Results

`TheHourglassCoversTheWorkBetweenThePlanningEntries` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.Pointer.cs` checks these
observations from the fixture and checks that, at the same points of the
replay, the rebuild shows the arrow at the human's planning entry and the
hourglass from the Done press on.

## Conclusion

The run supports RULE-UI-007 for a city setup and the turns of a local match
with one human: the human plans under the arrow, and everything from the Done
press to the next planning entry runs under the hourglass, with arrows between
the stretches that no `roll` separates. The run cannot tell whether a message
is dispatched between such an arrow and the next hourglass, which would let a
moving pointer show the arrow for that moment. Loading a game was not reached.
