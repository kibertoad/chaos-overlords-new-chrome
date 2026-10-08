---
id: EXP-UI-055
title: Does the setup screen New Game first opens after a GOG installation look the same in the rebuild?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-055.json
---

## Question

GOG's installer stores the objective 4, Kill 'Em All, the Mentality 0, Goon,
and the planning limit 0 (SRC-INSTALLER-GOG). Does the setup screen New Game
first opens with those preferences draw the same pixels in the rebuild as in
the original?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 0 --mentality 1 --time-limit 0
--seed 3 --end-turns 0 --white-key --setup-capture`, then `extract
--experiment EXP-UI-055` over the run directory. When the preference loader
`0x0046439A` has returned, at the breakpoint after its call (FND-RNG-001), the
probe writes 4 to the objective `0x00487858`, 0 to the Mentality `0x00487850`
and 0 to the planning limit `0x00487854`, the installer's values, because the
registry this unelevated copy reads holds others. The fixture lists the writes
as the setup input `preferences 4:0:0`. The probe then holds the left button
until the title art has loaded (FND-UI-055), posts New Game, and copies the
drawing area two seconds after the setup screen opens, before the run writes
its own settings. The rest of the run is EXP-UI-015's without its setup steps
and only gives the fixture its setup rolls.

## Observations

The copy shows the setup screen with Kill 'Em All's title and its description
in four lines, the light on Kill 'Em All, no time limit lit, Goon and no
planning limit lit, and one human player in slot 0 with a red bar and a green
name. An earlier run on the same day, which wrote the same three values once
the title art had loaded and passed no settings of its own, copied the same
pixels. The run then made 325 calls of `roll`.

## Results

A test of the rebuild compares the copy with the rebuild's setup screen, drawn
with `--reference-frame setup --setup-preferences 4:0:0`, and no element
differs. The rebuild's fresh preferences hold the same scenario, Mentality and
planning limit, which a test of its defaults checks. A replay of the run's match
from the same seed and settings makes the same 325 rolls and reaches the same
end state.

## Conclusion

The run supports SCR-SETUP-001 for the screen as New Game first opens it after
a GOG installation: Kill 'Em All lights no time limit, as FND-SETUP-013 reads
for a scenario from 4 up, and the Mentality light stands on Goon. It supports
RULE-SETUP-002 and RULE-OPTIONS-001 for the screen those preferences give; the
preferences themselves were written by the probe, so the run does not show the
loader reading them from the installer's registry key.
