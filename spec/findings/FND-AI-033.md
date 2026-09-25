---
id: FND-AI-033
title: The family-3 handler influences Cash sites, and AI target draws compare a gang from a different list
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00435BD0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 3's handler `0x00435BD0` switches on the previous action.

- Previous 0 (None), 4 (Control), 5 (Equip) or 7 (Heal): Heal when Force is
  below 8 and effective Heal is at least -3. Otherwise, in a sector the player
  owns, it scans the three sites in slot order and keeps the first strictly
  greatest positive Cash among sites whose remaining Resistance is positive, and
  writes Influence (9) with that site slot. With no such site it writes Control
  when selector `0x2C` accepts the current sector, and Move through mode 8
  otherwise.
- Previous 13 (Snitch): Move through mode 8.
- Previous 9 (Influence): first the family-1 equipment opportunity
  (FND-AI-021: selector `0x6C` gate, weapon before armor, cooldown of cost times
  3). Without Equip, the same Heal gate. Then it keeps the previous Influence
  site while that site is unfinished and the sector is still owned (selector
  `0x41` reads the previous target's first byte, the site slot); otherwise it
  rescans for the first strictly greatest positive Cash site, or Moves through
  mode 8 when there is none.
- Previous 1 (Attack), 8 (Hide) or 10 (Move): when the cached opponent weight
  is not 10 it returns to the owned Cash site, Control, or mode 8 Move
  sequence, without the Heal gate. At weight 10 it makes one draw. In a sector
  owned by a hostile player the actual target is drawn from the visible gangs
  of human players; otherwise from every visible opponent. Both lists run by
  player, then by gang slot.

The comparison selector `0x2B` receives the drawn ordinal but looks it up in
the list of every visible opponent, even when the actual target came from the
human-only list. It permits the attack when
`(target Force + target Combat) / 4 - attacker Defense` is at most
`attacker Force + attacker Combat - target Defense`. Failure leaves None and
clears the auxiliary target values. Success attacks the drawn target, which
need not be the gang the comparison used.

After the switch, three Moves in a row change the family to 11 in scenario 7
and to 2 in every other scenario. Last, in scenario 0 with fewer than four
turns remaining, Terminate replaces the action. Switch cases without an action
body leave None.

## Interpretation

Family 3 builds Cash: it influences the best Cash site in owned land and moves
toward owned sectors with Cash still to gain. The comparison asymmetry is a
quirk of the original: the strength test can be made on a different gang from
the one attacked.

## Alternatives

"Three Moves in a row" is read from the previous, older and new actions. The
list of cases without a body is not written out.

## How to reproduce

Open `0x00435BD0`; the four mode 8 calls, the three-site scan comparing Cash,
the selector `0x2B` call after the wrapper draw, and the final family store
comparing scenario with 7.

This evidence was recorded in the older notes under the endgame awards
heading; it concerns only the AI.
