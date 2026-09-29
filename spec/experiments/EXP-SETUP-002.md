---
id: EXP-SETUP-002
title: Does a new local Armageddon game with two humans draw and start as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, the installed executable run elevated with the compatibility layers the registry names for it, full screen switched off in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-SETUP-002.json
---

## Question

With Armageddon, the Goon Mentality and humans in slots 0 and 3, do the draws
of a new local game and the state its first planning phase starts from match
RULE-SETUP-001, RULE-SETUP-003, RULE-SETUP-004 and RULE-CITY-002 as the spec
gives them for that scenario: $500 each, no research left, and no site of
definitions 4 and 8?

## Setup

As EXP-SETUP-001, with the breakpoints and the dump described there. Before
pressing Begin the probe wrote what the setup screen's controls commit
(FND-SETUP-013): 9 into the scenario dword `0x004ABBE8` and its preference
byte `0x00487858`, 0 into `mentality` at `0x00487850`, and the live roster:
player type 0 and portraits 0 and 1 for slots 0 and 3, type -1 and portrait
15 for the other four (`0x004AB638`, `0x004A5F00`). The names were left as
the screen opened them.

## Procedure

1. Start the executable and take it to the full local setup screen as in
   EXP-SETUP-001.
2. Write the settings above, then press and release the left button inside
   Begin at (416, 397).
3. When no roll has been made for eight seconds, copy the `.data` section and
   read the state out of it (`Rechaos.OriginalProbe new-game --scenario 9
   --mentality 0 --humans 0,3`, then `extract`).

## Observations

The seed was 27834. Nothing was drawn before Begin. Begin made 338 calls of
`roll`:

| Calls | Call instruction | Bound | What RULE-SETUP-004 and its callees say they are |
|---|---|---|---|
| 6 | `0x00468C99` | 15 | the portraits of the four empty slots, with two redraws |
| 6 | `0x0046DC83` | 4 | one reaction per slot |
| 80 | `0x00476191`, `0x0047619F` alternately | 32 | the density centres |
| 234 | `0x004764C1` | 21 | the site proposals, with their redraws |
| 12 | `0x00476777` | 6 | the headquarters permutation, with six redraws |

No hire offer was drawn: the copy holds `0x9C` in all three offer bytes of
slot 0. The copy held 9 as the scenario, 0 as the Mentality and 65535 in
`turn_limit`. Every slot had $500 and no research left on any item, slots 0
and 3 had type 0 and portraits 0 and 1, the four computer players had
difficulty band 0 and the humans 1, and none of the 192 sites was of
definition 4 or 8.

## Results

Every result is the one RULE-RNG-002 computes from the recorded seed.
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` starts the rebuild's
match with the same seed and settings and finds the same generator position
and the same state, compared field by field as in EXP-SETUP-001.

## Conclusion

The Armageddon branches of the new-match setup and the city generator, a
second human in a later slot and the Goon difficulty bands agree with the
spec for these seeds. The planning phase of a game with two humans opens on
the Ready card (RULE-SETUP-008) before it refills the hire offers, so the
offers stay vacant in the copy and RULE-HIRE-002 is not reached.
