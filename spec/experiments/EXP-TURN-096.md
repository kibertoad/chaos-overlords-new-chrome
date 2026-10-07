---
id: EXP-TURN-096
title: Whose gangs do the sector view's cards list after each Overlord portrait press?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-096.json
---

## Question

On the detailed sector screen, whose gangs do the cards list after a press on
each portrait of the Overlord bar, for players with and without a gang seen
in the sector, and does a card of another player's gang open an order menu?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-026, then after the dump the steps of `--order-steps`:

1. Press the Exit control of a result panel three times; a press with no
   result panel open is skipped.
2. Post a press, a release, a double-click and a release at the centre of city
   sector 33, which opens the sector view (FND-UI-015, FND-UI-020).
3. Press the window at `(183,21)`, the portrait of player 2, then card 0 at
   `(20,12)`, then the portraits of players 4, 5, 1, 3 and 0 at `(323,21)`,
   `(393,21)`, `(113,21)`, `(253,21)` and `(43,21)`.
4. Press the back control at `(20,425)`.

After each step the probe keeps the player whose gangs the cards list,
`0x00487B8C`, the view byte `0x00487B88` and the six card slots at
`0x004ABC68` (FND-UI-015), with the popup menu a press opened as EXP-TURN-095
keeps it.

## Observations

The run made the same 14892 calls of `roll` with the same bounds and results
as EXP-TURN-026. The human, player 0, has one gang in sector 33, in roster
slot 0. Players 2, 4 and 5 have gangs the human sees there; players 1 and 3
have none.

| Step | Listed player | Card slots |
|---|---|---|
| open sector 33 | 0 | 0 |
| portrait of player 2 | 2 | 22, 23, 25, 26 |
| card 0 | 2 | 22, 23, 25, 26 |
| portrait of player 4 | 4 | 1 |
| portrait of player 5 | 5 | 25 |
| portrait of player 1 | 5 | 25 |
| portrait of player 3 | 5 | 25 |
| portrait of player 0 | 0 | 0 |

No step opened a popup menu, the press on player 2's card included, and no
step changed the human's orders. The back control showed the city again.

## Results

A test of the rebuild replays the run to the dump and takes each portrait press
through the rebuild's portrait hit test and portrait handling. The listed player
and the card slots, as roster slots of that player, are the same after every
step, and the card of player 2's gang opens no menu in the rebuild either.

## Conclusion

The run agrees with RULE-UI-010: the cards list the viewed player's gangs in
the sector in roster order, a portrait switches them only to a player with a
gang seen there, and the active player's portrait restores the active
player's own.
