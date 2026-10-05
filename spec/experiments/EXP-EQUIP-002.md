---
id: EXP-EQUIP-002
title: Which items does the Equip list offer a Tech Level 0 gang, and items the player cannot afford?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-EQUIP-002.json
---

## Question

Does the Equip list leave out an item whose research is done but whose Tech
Level is above the gang's, and does it list items when the player's cash is
below their price?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-071, with `--equip-lists` added
(`--scenario 0 --mentality 1 --seed 16 --end-turns 6 --hires 1:0:12,2:0:12 --orders 3:0:10:19:0:0,3:1:10:19:0:0,3:2:10:19:0:0,4:0:3:0:0:1,4:1:4:0:0:1,4:2:3:0:0:1 --equip-lists`).
The lists are read as in EXP-EQUIP-001.

## Observations

The run made the same 1182 calls of `roll` with the same bounds and results
as EXP-TURN-071. Every value of EXP-TURN-071's end state is the same except
`damage_dealt` and `retaliation_taken` of combat record 2 (FMT-STATE-003),
which hold 72 and 41 here and -28 and 43 there; that record's gang fought
without attacking, and FMT-STATE-003 gives both fields as undefined for such a
gang.

The human has three gangs, in roster slots 0 to 2, of Tech Levels 7, 5 and 0,
and -32 cash. The lists of the slot-2 gang hold one item or none.

## Results

`TheEquipListOffersTheOriginalsItems` compares the lists as in
EXP-EQUIP-001, and they are the same.

For the Tech Level 0 gang the rebuild's item table rules out, in each
category, one or two items that are researched or need no research and whose Tech Level is above
0, and the items it lists have Tech Level 0. Ten of the lists hold an
item whose Cost is above the human's cash.

## Conclusion

The run agrees with RULE-EQUIP-004 for the Tech Level test on its own and for
a list that ignores cash.
