---
id: EXP-TURN-010
title: Do twenty-five turns of new local games, with the human's first gang hiding throughout, draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-TURN-010.json
---

## Question

As EXP-TURN-004, over twenty-five turns, with the human's first gang on a
recurring Hide so that the human stays in the game: do the later turns, with
many more gangs, Crackdowns and police, make the draws and reach the state the
spec gives?

## Setup

As EXP-TURN-001.

## Procedure

1. As EXP-TURN-009, pressing Done twenty-five times (`--end-turns 25`), with
   one order: before the first Done press, Hide (8) in `action` and
   `repeat_action` of the human's gang in roster slot 0 (`--orders 1:0:8:0:0:1`).
2. Start the first run again with its recorded seed, twenty-four turns and
   the same order (`--seed 61038 --end-turns 24 --orders 1:0:8:0:0:1`), note
   every call of the sector selector `0x00408642` (`--trace-calls 0x00408642`)
   and copy the writable sections at the entry of call 11610 of `roll`,
   counting from 0 (`--dump-at-roll 11610`).
3. Repeat step 2 over twenty-five turns (`--end-turns 25`), copying the
   writable sections at the entry of call 12630 instead
   (`--dump-at-roll 12630`).

## Observations

The seeds were 61038 and 13847. Begin made 307 and 320 calls of `roll`, and
the twenty-five turns made 13260 and 13258 more. Over the twenty-five turns the
calls were:

| Call instruction | Bound | First run | Second run |
|---|---|---|---|
| `0x0047172A` | 89 | 137 | 136 |
| `0x00409C24` | 2 to 8 | 145 | 163 |
| `0x00409C99` | 2 to 9 in the first run; 4, 7, 8 and 258 in the second | 14 | 5 |
| `0x004737A9` | 3 | 1 | 0 |
| `0x0047419B` | 100 | 2 | 0 |
| `0x004756D9` | 2 | 0 | 1 |
| `0x00475AC6` | 5 | 110 | 115 |
| `0x00475FBB` | 6 | 12851 | 12838 |

In the first run the call at `0x004737A9` is call 11572 and the calls at
`0x0047419B` are calls 11573 and 12580, in turns 23 and 24. The second run's
call at `0x004756D9` is call 7217, in turn 18. The Last Turn Events panel
opened once, at the planning phase after the first run's last turn. The
copies held 25 in `elapsed_turns`, and the human's gang had Force 10 and
`action` and `repeat_action` 8 in both runs.

In the traced repetition of the first run, player 4's selector calls after
call 11609 of `roll` were, in order, for gangs 3, 8, 11 and 15, with mode 5
each. They returned 22, 11, 62 and 45; the call for gang 8 was entered after
11609 calls and the call for gang 11 also after 11609, so gang 8's call made
no call of `roll` and gang 11's made call 11609. At the entry of call 11610,
the `roll(7)` at `0x00409C99` of gang 15's call, the first seven pairs of
`selector_pairs` held score 1 with the sectors 0, 45, 46, 53, 55, 61 and 62.
Planning records 8 and 11 of player 4 held family 1, and 3 and 15 family 0.
Sector 12 was owned by player 0, the human, and a gang of player 4 stood in
it; player 4's `attitude` toward player 0 was -10.

At the end of the first run the six players held 0, 3, 3, 3, 3 and 2 Last
Turn reports (FMT-STATE-006): five completed sites (type 4), six completed
items (type 5), a Crackdown in sector 50 for player 2 (type 1), and player 4's
Control of sectors 11 and 14, neither owned before (type 2 with `arg2` -1). At
the end of the second run they held 0, 3, 1, 2, 1 and 1: two sites and six
items.

## Results

Every result is the one RULE-RNG-002 computes from the recorded seed.
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays both runs. The
rebuild makes both runs' calls with the same bounds and results and reaches the
same generator position and state after the twenty-five turns, and builds the
same Last Turn reports for every player.

At call 11610 of the first run the original's call for player 4's gang 8, in
sector 20, returned 11 with no draw. Sector 12, next to it, is the human's and
player 4's attitude toward the human is -10, so mode 5 gives it 1; the common
block then multiplies table element `4 * 9 + 1`, the score of sector 44, and
leaves sector 12 at 1 below sector 11's 5 (FND-AI-069). A reading in which the
common block multiplied the visited sector gave both 5 and drew `roll(2)`.

With that reading of the common block, the first call to differ was call
12630. The third run, of step 3, traced the selector over all twenty-five
turns. At call 10662 of `roll` the original's call for player 5's gang 16, in
sector 45, returned 52, and sector 52 then held six of player 5's gangs. The
Move stayed in the gang's orders and the Move repair (RULE-MOVE-002) let the
gang in, so at call 11615 its call was made from sector 52 and returned 45. A
reading in which a Move into a sector holding six of the player's gangs is
refused when it is planned left the gang in sector 45, where the call returned
38. With the Move kept, both runs replay to the end.

The other corrections FND-AI-069 makes, the owner read of -2 under a
Crackdown and the end of mode 6 after its hostile-human bonus, come from the
static reading. These runs do not single them out: a reading with them and
with the Move refused when it is planned also first differed at call 12630.

## Conclusion

The two runs agree with the spec over twenty-five turns, including a tied
Control, with RULE-AI-006 as FND-AI-069 corrects it: the common block's
multiply by five reaches another table element than the visited sector, and a
computer player's Move into a full sector is planned and left to the Move
repair.
