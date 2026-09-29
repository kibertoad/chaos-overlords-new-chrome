---
id: EXP-SETUP-001
title: What does a new local game draw from the generator, and what state does its first planning phase start from?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, the installed executable run elevated with the compatibility layers the registry names for it (DWM8And16BitMitigation, WINXPSP2, DISABLEDWM and RUNASADMIN for the machine; 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE as well for the user), full screen switched off in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-SETUP-001.json
---

## Question

Does the generator give the sequence RULE-RNG-001 and RULE-RNG-002 predict
from the seed the process start passes it, do the draws of a new local game
come in the order RULE-SETUP-003 and RULE-SETUP-004 give, and what state does
the first planning phase start from?

## Setup

The game's registry key held the values the install left there, among them
`prefsObjective` 4, `prefsDiff` 2, `prefsTimeLimit` 0 and a nonzero
`serialNum`, so the preference loader made no draw (RULE-OPTIONS-001). The
probe started `Chaos Overlords.exe` from the install directory as a debugged
process and set breakpoints on:

- the runtime `srand` at `0x00478CC0`, recording its argument, the seed
  (FND-RNG-001);
- the bounded wrapper `roll` at `0x0045D227`, recording its argument, and on
  the return address of each call, recording the result and the call
  instruction (FND-RNG-003, FND-RNG-006);
- the instruction after the preference loader's call at `0x00460D14`, where
  it wrote 0 to `pref_full_screen` and its copy at `0x0048786C` and
  `0x00498354`, so the game ran in a window;
- the local setup handler `0x0040E0A0`, to see the setup screen open.

## Procedure

1. Start the executable and post a left button press and release at (320,
   240) and the command `0x8101` (File, New Game) to its window every 1.5
   seconds, which ends the logos and intro movies and then leaves the title,
   until the setup handler runs.
2. Wait two seconds and press and release the left button at (416, 397),
   inside Begin (SCR-SETUP-001), with the setup screen's defaults: one local
   human in slot 0 and five empty slots.
3. When no roll has been made for eight seconds, the human's planning phase is
   waiting for input. Copy the executable's `.data` section,
   `0x00482000` to `0x004AD110`, from the running process.
4. Read the state out of the copy with the layouts of FMT-STATE-001,
   FMT-STATE-002 and FMT-STATE-004 and the glossary's addresses
   (`Rechaos.OriginalProbe extract`).

## Observations

The seed was 52421. Nothing was drawn before Begin. Begin made 310 calls of
`roll`, in this order:

| Calls | Call instruction | Bound | What RULE-SETUP-004 and its callees say they are |
|---|---|---|---|
| 9 | `0x00468C99` | 15 | the portraits of the five empty slots, with four redraws of a portrait in use |
| 6 | `0x0046DC83` | 4 | one reaction per slot |
| 80 | `0x00476191`, `0x0047619F` alternately | 32 | the X and Y of the 40 density centres |
| 204 | `0x004764C1` | 21 | the site proposals, with their redraws |
| 8 | `0x00476777` | 6 | the headquarters permutation, with two redraws |
| 3 | `0x0047172A` | 89 | the human's three hire offers at its planning entry |

The copy then held Kill 'Em All (4) as the scenario, Crime Lord (2) as the
Mentality, 65535 in `turn_limit` and 0 in `elapsed_turns`. The fixture lists
every value that was read.

## Results

Every one of the 310 results is the one RULE-RNG-002 computes from the state
RULE-RNG-001 gives the seed 52421, three draws per call. The call order is the
order of RULE-SETUP-003, RULE-SETUP-004, RULE-CITY-001 to RULE-CITY-003 and
RULE-HIRE-002.

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` checks every roll
against the generator, then starts the rebuild's match from the same seed,
scenario and Mentality with one human in slot 0 and compares: the generator's
state and the number of draws, each slot's portrait, cash, reaction,
difficulty band, attitudes and remaining research, the human's hire offers,
each sector's owner, Income, base and current Tolerance, Support, Cash, police
presence, sites and their progress, and each gang's definition, sector, Force,
items, fourteen statistics and visibility.

## Conclusion

The generator, the call order of a new local game and the starting state
agree with the spec for this seed and these settings. The run covers one
human, no name modifier and a Mentality other than Homicidal Maniac; the
branches for Armageddon, the name modifiers and Homicidal Maniac were not
reached.

`turn_limit` held 65535, where the glossary gives 52 when the setup screen
opens and the timed lengths otherwise; Kill 'Em All has no time limit.
