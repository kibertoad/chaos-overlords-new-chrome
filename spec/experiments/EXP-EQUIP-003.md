---
id: EXP-EQUIP-003
title: Which items does the Equip list offer late in a match, with most research done?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-EQUIP-003.json
---

## Question

With most items researched, how long are the Equip lists, and does the Tech
Level test still leave items out?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-026, with `--equip-lists` added
(`--scenario 9 --mentality 1 --seed 5490 --end-turns 28 --equip-lists`).
The lists are read as in EXP-EQUIP-001.

## Observations

The run made the same 14892 calls of `roll` with the same bounds and results
as EXP-TURN-026, and every value of EXP-TURN-026's end state is the same; the
end state also holds fields recorded after EXP-TURN-026 was.

The human has one gang, of Tech Level 7, and 520 cash. Its four lists hold 9,
8, 10 and 13 items.

## Results

`TheEquipListOffersTheOriginalsItems` compares the lists as in
EXP-EQUIP-001, and they are the same.

Every item of the four categories is researched here. The rebuild's item
table rules out two to four items per category, all by Tech Level, and each
list holds one or two items whose Tech Level equals the gang's.

## Conclusion

The run agrees with RULE-EQUIP-004 when research leaves nothing out. No list
comes near the sixteen entries the builder writes.
