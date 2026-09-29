---
id: EXP-SETUP-004
title: Do the six name modifiers change a new local game as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, the installed executable run elevated with the compatibility layers the registry names for it, full screen switched off in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-SETUP-004.json
---

## Question

With Kill 'Em All, the Criminal Mentality and six humans, each named with a
different one of the six name modifiers, does the new-match scan set each
player's flag and apply the extra gangs, the permanent Crackdown and the
starting cash as RULE-SETUP-001 and RULE-SETUP-004 to RULE-SETUP-007 give,
and set the hire Force flag of FND-HIRE-005?

## Setup

As EXP-SETUP-001. Before pressing Begin the probe wrote 4 into the scenario
dword `0x004ABBE8` and its preference byte `0x00487858` and 1 into
`mentality` at `0x00487850`, and filled the live roster (FND-SETUP-013) with
six humans: player type 0 and portraits 0 to 5 in slot order. It set the name
of slot `n`, the 12-byte length-prefixed string at `0x004A2588 + 12 * n`, to
the modifier string the new-match scan tests in position `n` (FND-SETUP-015):
`modifier_name_right_hands`, `modifier_name_visibility`,
`modifier_name_hire_force`, `modifier_name_elite`, `modifier_name_islands`
and `modifier_name_cash`. The probe read each string from the running
executable; the fixture records only the flags they set.

## Procedure

1. Start the executable and take it to the full local setup screen as in
   EXP-SETUP-001.
2. Write the settings above, then press and release the left button inside
   Begin at (416, 397).
3. When no roll has been made for eight seconds, copy the `.data` section and
   read the state out of it (`Rechaos.OriginalProbe new-game --scenario 4
   --mentality 1 --humans 0:right_hands,1:visibility,2:hire_force,3:elite,4:islands,5:cash`,
   then `extract`).

## Observations

The seeds were 2835 and 28839. Nothing was drawn before Begin.

| Call instruction | Bound | Run 1 | Run 2 | What RULE-SETUP-004 and its callees say they are |
|---|---|---|---|---|
| `0x0046DC83` | 4 | 6 | 6 | one reaction per slot |
| `0x00476191`, `0x0047619F` alternately | 32 | 80 | 80 | the density centres |
| `0x004764C1` | 21 | 207 | 201 | the site proposals, with their redraws |
| `0x00476777` | 6 | 10 | 11 | the headquarters permutation, with redraws |
| | | 303 | 298 | in all |

No portrait was drawn, since no slot was empty, and no hire offer: the copy
holds `0x9C` in all three offer bytes of slot 0. In both copies each slot's
flag of its own position in the scan was set and no other. Slots 0 and 3 had
six gangs each, all at Force 10, and the other slots one; slot 5 had $1,500
and the others $20; every one of the 58 unowned sectors had 100 in its
police presence byte.

## Results

Every result is the one RULE-RNG-002 computes from the recorded seed.
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` gives each rebuild
player the modifier name whose flag the original set, starts the match with
the same seed and settings, and finds the same generator position and the
same state, compared field by field as in EXP-SETUP-001, together with the
hire Force flag.

## Conclusion

All six name modifiers are matched and applied as the spec gives for these
seeds, with every modifier in the slot of its own position in the scan.
Whether a modifier behaves the same in another slot, or when two players
carry the extra-gang modifiers in the other order, is not tested. The
planning phase of a game with several humans opens on the Ready card
(RULE-SETUP-008) before it refills the hire offers, so the offers stay vacant
in the copy.
