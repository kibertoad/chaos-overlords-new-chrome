---
id: EXP-SETUP-003
title: Does a new local Greed game at Homicidal Maniac with a four-year limit draw and start as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, the installed executable run elevated with the compatibility layers the registry names for it, full screen switched off in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-SETUP-003.json
---

## Question

With Greed, a 208-turn limit, the Homicidal Maniac Mentality and one human in
slot 0, does a new local game skip the reaction draws and set the attitudes
and difficulty bands as RULE-SETUP-004, RULE-AI-014 and RULE-AI-018 give, and
does a timed scenario keep the chosen `turn_limit` (FND-SETUP-018)?

## Setup

As EXP-SETUP-001. Before pressing Begin the probe wrote 0 into the scenario
dword `0x004ABBE8` and its preference byte `0x00487858`, 208 into
`turn_limit` at `0x004A5EF8` and 3 into `mentality` at `0x00487850`
(FND-SETUP-013). The roster was left as the screen opened it: one human in
slot 0.

## Procedure

1. Start the executable and take it to the full local setup screen as in
   EXP-SETUP-001.
2. Write the settings above, then press and release the left button inside
   Begin at (416, 397).
3. When no roll has been made for eight seconds, copy the `.data` section and
   read the state out of it (`Rechaos.OriginalProbe new-game --scenario 0
   --mentality 3 --turns 208`, then `extract`).

## Observations

The seeds were 50990 and 12161. Nothing was drawn before Begin.

| Call instruction | Bound | Run 1 | Run 2 | What RULE-SETUP-004 and its callees say they are |
|---|---|---|---|---|
| `0x00468C99` | 15 | 5 | 5 | the portraits of the five empty slots, with no redraw |
| `0x00476191`, `0x0047619F` alternately | 32 | 80 | 80 | the density centres |
| `0x004764C1` | 21 | 210 | 202 | the site proposals, with their redraws |
| `0x00476777` | 6 | 12 | 10 | the headquarters permutation, with redraws |
| `0x0047172A` | 89 | 3 | 3 | the human's hire offers at its planning entry |
| | | 310 | 300 | in all |

No reaction was drawn at `0x0046DC83`. In both copies `turn_limit` held 208,
every reaction 0, every computer player's difficulty band 2 and the human's
1, and each player's six attitudes -10 toward slot 0, the human, and +10
toward the five computer players, its own cell included.

## Results

Every result is the one RULE-RNG-002 computes from the recorded seed.
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` starts the rebuild's
match with the same seed and settings and finds the same generator position
and the same state, compared field by field as in EXP-SETUP-001.

## Conclusion

The Homicidal Maniac branch of the fresh-match initializer and a timed
scenario's turn limit agree with the spec for these seeds.
