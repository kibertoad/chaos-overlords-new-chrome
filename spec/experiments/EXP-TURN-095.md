---
id: EXP-TURN-095
title: Which order menu does each press open, which items does it grey, and what does each choice write?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-095.json
---

## Question

On the detailed sector screen, where do the gang cards' strips and the group
order strip stop opening their popup menus, which items does each menu grey,
and what do the menus' choices write into the gangs' order bytes when a gang
already holds a one-off or recurring order?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-071, then after the dump the steps of `--order-steps`:

1. Press the Exit control of a result panel (`(161, 304)`) twice, because the
   run ends with Combat Results open.
2. Post a press, a release, a double-click and a release at the centre of city
   sector 19, which opens the sector view (FND-UI-015, FND-UI-020).
3. Press card 0 at card points `(4,12)`, `(5,12)`, `(36,12)`, `(37,12)`,
   `(38,12)`, `(68,12)`, `(69,12)`, `(20,7)`, `(20,8)`, `(20,16)` and
   `(20,17)`, and the window at `(252,68)`, `(253,68)`, `(367,68)`,
   `(368,68)`, `(404,68)`, `(405,68)`, `(300,60)`, `(300,61)`, `(300,76)` and
   `(300,77)`, closing each menu with no choice.
4. Give orders through the menus, in this order: card 0 at `(40,12)` command
   4, card 1 at `(10,12)` command 2, card 2 at `(10,12)` command 15, card 0 at
   `(10,12)` command 3, the window at `(390,68)` command 3, at `(300,68)`
   command 3 and at `(390,68)` command 4, card 1 at `(50,12)` command 8, card
   2 at `(10,12)` command 13, the window at `(300,68)` command 13 and at
   `(390,68)` command 7, and card 0 at `(50,12)` command 1.
5. Press the back control at `(20,425)`.

A press that opens a menu reaches the popup helper's `TrackPopupMenu` call at
`0x00425715` (FND-UI-021). There the probe reads the helper's menu argument at
`[ebp+8]`, and the command and the greyed and disabled bits of each item of the
menu handle, the call's first argument, through `GetMenuItemID` and
`GetMenuState`. It then skips the call: it sets EAX to the step's command (0
for no choice), removes the call's seven arguments from the stack and resumes
at `0x0042571B`, where the helper stores the result, so no menu is shown and
the game receives the command a player's choice would return. After each step
the probe keeps the view byte `0x00487B88`, the six card slots at `0x004ABC68`
(FND-UI-015), and the order bytes of every gang of the active player in use
and of roster slot 80 (FMT-STATE-001).

## Observations

The run made the same 1182 calls of `roll` with the same bounds and results
as EXP-TURN-071. Sector 19 has no owner and `crackdown_turns` 3, no other
player's gang is seen there, and the human has no items. The sector view lists
roster slots 0, 1 and 2 on cards 0 to 2, with Force 10, 5 and 3; slots 0 and
2 hold the recurring Chaos and slot 1 no order.

The presses of step 3 opened menu 1 for card x 5 to 37, menu 2 for card x 38
to 68, and neither at x 4 or 69; card y 8 and 16 opened a menu, 7 and 17 did
not. The group strip opened menu 3 for x 253 to 367, menu 5 for x 368 to 404,
and neither at x 252 or 405; y 61 and 76 opened a menu, 60 and 77 did not.

Each menu listed the commands FND-UI-021 gives it. Menu 1 greyed Attack,
Control, Give, Influence and Sell, and Heal for the gang at Force 10 only;
Bribe, Chaos, Equip, Hide, Move, Research, Snitch, None and Terminate stayed
enabled. Menu 2 greyed Control and Influence, and Heal for the gang at Force
10. Menu 3 greyed Attack, Control and Influence, menu 5 Control and Influence.
None was enabled in every menu, for a gang with no order too.

The action and repeat_action of slots 0, 1 and 2 after each choice of step 4:

| Choice | Slot 0 | Slot 1 | Slot 2 |
|---|---|---|---|
| card 0, menu 2, Hide | 8, 8 | 0, 0 | 3, 3 |
| card 1, menu 1, Bribe | 8, 8 | 2, 0 | 3, 3 |
| card 2, menu 1, None | 8, 8 | 2, 0 | 0, 0 |
| card 0, menu 1, Chaos | 3, 0 | 2, 0 | 0, 0 |
| menu 5, Heal | 3, 0 | 7, 7 | 7, 7 |
| menu 3, Chaos | 3, 0 | 3, 0 | 3, 0 |
| menu 5, Hide | 8, 8 | 8, 8 | 8, 8 |
| card 1, menu 2, None | 8, 8 | 0, 0 | 8, 8 |
| card 2, menu 1, Snitch | 8, 8 | 0, 0 | 13, 0 |
| menu 3, Terminate | 14, 0 | 14, 0 | 14, 0 |
| menu 5, None | 0, 0 | 0, 0 | 0, 0 |
| card 0, menu 2, Chaos | 3, 3 | 0, 0 | 0, 0 |

Every target, target_2 and repeat_target stayed 0. Roster slot 80 took each
group choice's action and repeat_action and kept them through the card
choices that followed. The back control showed the city again.

## Results

A test of the rebuild replays the run to the dump and takes each step through
the rebuild: the card and group strip hit tests, the panel's lists of orders
numbered as the original's menus number them, the orders the panel offers for
the gang or the sector's gangs, and the chosen order given to the gang, or to
the sector's gangs through the group order path. The menus, the offered orders
and each gang's action and repeat_action are the same after every step.

## Conclusion

The run agrees with RULE-TURN-005 for the choices made: a one-off order
clears a recurring one, a recurring order sets both bytes, None clears both, a
group order reaches every gang of the sector, the recurring Heal only those
below Force 10, and roster slot 80 serves as the group order's scratch record.
It agrees with SCR-UI-004 and FND-UI-021 on the strips' extents and the
greyed items for this sector.
