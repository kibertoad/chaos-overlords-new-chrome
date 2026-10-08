---
id: EXP-TURN-001
title: Does the first turn of a new local game, ended with no orders, draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen, Warn if Idle Gangs and Detailed Combat switched off in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-TURN-001.json
---

## Question

When the human ends the first planning phase at once, do the five computer
players plan, the turn resolve and the next planning phase open with the draws
and the state RULE-TURN-001, RULE-HIRE-001, RULE-HIRE-002 and RULE-AI-001 to
RULE-AI-010 give?

## Setup

As EXP-SETUP-001, with the setup screen's defaults: one human in slot 0, the
registry's scenario 4 and Mentality 2. At the instruction after the preference
loader's call the probe also wrote 0 to Warn if Idle Gangs at `0x00487860` and
to Detailed Combat at `0x0048785C` (FND-OPTIONS-001, FND-OPTIONS-002), so the
turn ran with nothing waiting for input.

## Procedure

1. Start the copy. Every half second, write 1 to `left_button_down` at
   `0x004985A4` and post the command `0x8101` (File, New Game) until the
   setup handler runs, then write 0 there. RULE-VIDEO-001 ends each movie at
   the first tick that sees the button held.
2. Wait two seconds and press and release the left button inside Begin at
   (416, 397).
3. When no roll has been made for eight seconds, note the number of rolls so
   far and press and release the left button inside Done at (550, 306)
   (SCR-UI-003).
4. When `elapsed_turns` at `0x0049CA68` has reached 1 and no roll has been
   made for eight seconds, copy the `.data` section and read the state out of
   it (`Rechaos.OriginalProbe new-game --executable <copy> --end-turns 1`,
   then `extract`).

## Observations

The seeds were 47974 and 2328. Begin made 306 and 320 calls of `roll`, in the
order EXP-SETUP-001 gives. Done made 100 more in each run:

| Calls | Call instruction | Bound | What FND-RNG-006 says they are |
|---|---|---|---|
| 16, 15 | `0x0047172A` | 89 | the computer players' vacant offers at their planning entries, with redraws |
| 5, 5 | `0x00475AC6` | 5 | the Force of each gang hired in the hire phase |
| 79, 80 | `0x00475FBB` | 6 | the resolver's dice |

The copies held 1 in `elapsed_turns`. The fixture lists every value read.

## Results

Every result is the one RULE-RNG-002 computes from the recorded seed. A test of
the rebuild starts the rebuild's match with the same seed and settings, ends the
human's planning with no orders, lets the computer players plan and the turn
resolve up to the human's next planning entry, and refills the human's offers
there. The rebuild makes the same calls in the same order with the same bounds
and results, and reaches the same generator position and the same state,
compared as in EXP-SETUP-001.

## Conclusion

The first turn with five computer players agrees with the spec for both seeds.
Each computer player's vacant offers are drawn at its planning entry, before
any draw of its planning pass (RULE-TURN-001, RULE-HIRE-002). The runs do
not say which rules the resolver's dice belong to; they only show that the
rebuild rolls the same dice in the same order.
