---
id: EXP-UI-030
title: Do drags between setup player cards move and swap whole players, and does the rebuild draw the cards the same afterwards?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 3
fixture: EXP-UI-030.json
---

## Question

FND-SETUP-005 reads that a press on a setup card that leaves the -2 to +1
box around the press point with the button down starts a drag, and that the
release over a card swaps the two slots' players, an empty slot included.
EXP-UI-015 found drags posted by an earlier probe unreliable, and that probe
did not write the pointer points the game reads (FND-UI-020). With a probe
that writes them, do card-face drags behave as FND-SETUP-005 reads, every
time, and does the rebuild draw the cards the same after each?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 6 --mentality 1 --time-limit 0
--seed 3 --end-turns 0 --white-key --setup-capture --setup-steps <steps>`,
three times, then `extract --experiment EXP-UI-030` over the three run
directories. The steps are posted on the setup screen before the run writes
its own settings:

| Steps | Presses |
|---|---|
| 0 to 1 | Add, shot |
| 2 to 3 | A drag from card 0 `(429, 124)` to card 1 `(512, 124)`, shot |
| 4 to 5 | A drag from card 0 to card 4 `(429, 272)`, shot |
| 6 to 7 | A drag from `(453, 262)` on card 4 to `(452, 261)`, inside the box, shot |
| 8 to 9 | A drag from card 1 to card 0, shot |
| 10 to 12 | Card 4, Remove, shot |

A `drag:x:y:x2:y2` step writes the client point `0x0049859C` and the screen
point `0x004985A0` (FND-UI-020) at the press, at each of eight steps along the
line and before the release, and posts only the button messages. A `shot`
copies the drawing area as `--capture` does (docs/VALIDATION.md).

## Observations

All three runs kept every copy, and the three runs' copies agree byte for
byte. Add put a second human in slot 1 and selected card 1. The drag from card
0 to card 1 swapped the two players and left card 1 selected. The drag from
card 0 to the empty card 4 moved the player in slot 0 to slot 4, left card 0
empty and selected card 4. The drag that stayed inside the box acted as a
press and gave card 4 the next portrait. It started 56 pixels right of card
4's origin and 20 below it and was released one pixel up and left of that,
both in the band of the next portrait, so the run does not tell whether the
press or the release point picks the band. The drag from card 1 to the empty card 0 moved
that player to slot 0. Card 4 and Remove then took out the player of slot 4,
and the last copy equals the setup screen as New Game first opened it. Each
run then made 325 calls of `roll`.

## Results

A test of the rebuild replays the steps, each drag as a press, a move to the
release point and a release, and compares the setup screen after each with the
copies of every run. No element differs.

## Conclusion

The runs agree with FND-SETUP-005 and RULE-SETUP-009: a drag released over
another card swaps the two slots, with an empty slot as well, the drop card is
selected, and a pointer that stays within the box gives a press. The
unreliable drags of EXP-UI-015 came from its probe. The runs do not start a
drag on an empty card or release one outside every card.
