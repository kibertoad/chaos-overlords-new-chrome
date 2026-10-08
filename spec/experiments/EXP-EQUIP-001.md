---
id: EXP-EQUIP-001
title: Which items does the Equip list offer gangs carrying items, with little research done?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-EQUIP-001.json
---

## Question

No recorded run reads the item list the Equip panel builds. For gangs of
different Tech Levels, one of which carries a weapon, which items does the
list builder offer in each category?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-030, with `--equip-lists` added
(`--seed 4 --end-turns 4 --orders 1:0:5:0:0:0,2:1:5:1:0:0,3:0:6:1:1:0,3:1:6:1:0:0,4:0:6:1:2:0,4:1:6:1:2:0,4:2:5:12:0:0 --hires 1:1:33,2:1:33 --equip-lists`).

After the end state is dumped, the probe stops the original at the next
`PeekMessageA` call of the message pump (FND-UI-020), saves the thread
context and calls the list builder `fn_0043F136` of FND-EQUIP-012 once for
each category 0 to 3 of each living gang of the human, passing the category,
the Tech Level field of the gang's definition, the player and the roster slot,
as the Equip panel does. After each call it reads the sixteen entries at
`0x004948A8` and keeps those that are not -1, in entry order. It then
restores the saved context.

## Observations

The run made the same 785 calls of `roll` with the same bounds and results as
EXP-TURN-030, and every value of EXP-TURN-030's end state is the same; the
end state also holds fields recorded after EXP-TURN-030 was.

The human has three gangs, in roster slots 0 to 2, of Tech Levels 7, 2 and 5,
and 4 cash. The slot-2 gang carries item 0, a weapon. Each of the twelve
lists holds one or two items.

## Results

A test of the rebuild replays the run and, for each list, compares the rebuild's
legal Equip orders of the gang in that category with the recorded items. They
are the same items in the same order, and the gang's Tech Level is the one the
builder was passed.

Within each category the rebuild's item table rules out items by research
alone (up to eleven in one list), by research and Tech Level together, and,
for the slot-2 gang's weapons, item 0 because the gang carries it. One list
offers an item whose Tech Level equals the gang's.

## Conclusion

The run agrees with RULE-EQUIP-004 for the research test, the carried-item
test and an item at the gang's own Tech Level.
