---
id: FND-AI-081
title: A computer player's planned Equips are all carried out, with no cash set aside at planning
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: dynamic
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047592B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A25E8..0x004A25FF
tool: tools/Rechaos.OriginalProbe (`new-game --trace-hires`)
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
---

## Observation

On 2026-10-06 the hash-verified BLD-GOG-EN-1.1 executable replayed the
configurations of EXP-TURN-113 (Kill 'Em All, seed 21) to the 116th Done press
and of EXP-TURN-112 (Siege, seed 13) to the 96th, with the human's gang hiding
in every turn as in those runs. A breakpoint at `0x0047592B`, where the hire
phase tests one offer's hire order, read the six players' `cash`
(`0x004A25E8 + player * 4`) each time it was reached, with the number of
`roll` calls made so far. Both runs made the same draws as their fixtures over
their length: the first 96,039 of EXP-TURN-113, and all 52,052 of
EXP-TURN-112.

The rebuild's replay of the same runs, with a temporary trace of each
player's cash as its hire phase begins, gave the same six values at every
hire phase the breakpoint was reached in, up to:

- EXP-TURN-113, the hire phase of the 114th turn (after roll 94,940): player 2
  held 14 in the original and 17 in the rebuild. In that turn's planning the
  rebuild's player 2, holding 5, planned an Equip of a cost-3 item for roster
  slot 1 and then left out the Equip of another cost-3 item that roster slot
  35 had planned, because the first had used up its set-aside cash. Slot 1's
  gang was killed in that turn's combat, so its Equip was never carried out;
  in the original slot 35's Equip was carried out and the cash was 3 lower.
  The difference stayed (11 against 10, then 9 against 8, in the 115th and
  116th turns) and left the rebuild's player 2 with 0 cash at the 116th
  turn's planning, where the original's player 2 hires a gang that costs 1.
- EXP-TURN-112, the hire phase of the 94th turn (after roll 50,538): player 5
  held 53 in the original and 57 in the rebuild. In that turn's planning the
  rebuild's player 5 left out roster slot 19's Equip of a cost-4 item for the
  same reason. With 4 more cash at the 95th turn's planning, the rebuild's
  family-14 gangs of player 5 in roster slots 8 and 16 chose a weapon that
  costs 18 where the original's chose one that costs 16 (RULE-AI-005), and in the 96th turn's
  planning player 5's roster slot 19 still had cash for an armor Equip in the
  rebuild, where in the original it Influenced site 2 of its sector instead
  (RULE-AI-024). That Influence rolls the 9 dice of the original's 96th turn
  that the rebuild did not roll: Influence 2 plus Force 9 is 11, less a fifth
  for band 0, 9 dice (RULE-INFLUENCE-001).

With the rebuild planning every command its handlers write and leaving the
cash test to the transaction pass, its replays of both fixtures make every
draw of the original, 108,911 in EXP-TURN-113 and 52,052 in EXP-TURN-112.

## Interpretation

The computer players' handlers write each planned action into the gang record
(FND-AI-074), and nothing between planning and resolution drops one for cash.
The transaction pass tests each Equip against the cash the player holds when
the gang's turn comes (RULE-EQUIP-001), so an Equip planned by a gang that
dies in combat costs nothing and leaves the cash to the Equips after it.

## Alternatives

The 3 and 4 of cash could have come from a payment the rebuild leaves out
elsewhere in the same turns. The rebuild's cash agrees with the original's at
every earlier hire phase of both runs, the two differences equal the cost of
the one Equip each player withheld, and removing the set-aside alone makes
both replays agree to their last draw, so no other payment differs.

## How to reproduce

Run `Rechaos.OriginalProbe new-game --executable <copy> --out <dir>
--scenario 4 --mentality 0 --seed 21 --end-turns 116 --orders 1:0:8:0:0:1
--trace-hires`, and the same with `--scenario 6 --seed 13 --end-turns 96`.
Each `hire check after roll N` note of `trace.json` gives the six cash values.
