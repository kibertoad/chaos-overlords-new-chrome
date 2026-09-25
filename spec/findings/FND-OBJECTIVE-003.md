---
id: FND-OBJECTIVE-003
title: The scenario values run Greed 0 to Armageddon 9 in the order of the string table, and the end evaluator tests each by its own switch arm
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00438B35..0x00438DA4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00438DA5..0x00439562
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004384F4..0x00438B34
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047712A..0x00477747
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00476857..0x00476A93
tool: Ghidra 12.1.3
environment: null
---

## Observation

The scenario global is the dword at `0x004ABBE8`. The setup description drawer
`0x00438B35` loads string resource `scenario + 1` of the executable's string
table as the scenario's title (`0x00438BF5`) and `scenario + 95` as its
description (`0x00438C45`), through the `LoadStringA` wrapper `0x00466673`.
String resources 1 to 10 are, in order, the titles Greed, Power, Acceptance,
Dominance, Kill 'Em All, The Big 40, Siege, Eliminate, Big Man and Armageddon.

The setup handler `0x00438DA5` tests ten scenario buttons `k` = 0 to 9 and, on
a release inside button `k`, stores `k + 4` for `k` below 6 and `k - 6`
otherwise (`0x00438E92`). Button `k` is the one at column `k % 2` and row
`k / 2` of the scenario panel (the rectangles are in FND-SETUP-013). The light
drawer `0x004384F4` places the selection light of values 4 and 5 on the top
row, 6 and 7 on the second, 8 and 9 on the third, 0 and 1 on the fourth and 2
and 3 on the fifth, the even value on the left, which agrees with the handler.

The score builder `0x0047712A` switches on the scenario (`0x00477631`):

- 0: the player's cash (`0x004A25E8`, one dword per slot).
- 1, 5 and 9: the number of sectors whose owner byte (offset 0 of the 36-byte
  sector record at `0x004A08E8`) is the player.
- 2: over the player's sectors and their three site slots, the sum of the
  site definition's Support (the 16-bit field at `0x004AB680 + 62 *
  definition`) for each slot whose progress byte equals the definition's
  Resistance (`0x004AB67E + 62 * definition`).
- 3: cash times a factor, plus a per-sector weight for each sector owned, plus
  the same Support sum times a Support weight, the total then divided by 10
  (signed integer division, `0x0047750E`). The weights come from a switch on
  the dword at `0x004A5EF8` (`0x004771C9`): 26 gives cash factor 1, sector
  weight 30 and Support weight 10; 52 gives 1, 100 and 30; 104 gives 1, 250
  and 75; 208 gives 1, 1000 and 300. The switch has no default arm, so any
  other value leaves the three weights as whatever the stack held.
- 4 and 7: the number of player slots whose active byte (`0x004ABBE0`, one
  byte per slot) is 0.
- 6: how many of the six sectors listed at `0x00494818` the player owns.
- 8: the score is not reset (the reset at `0x004772BE` skips scenario 8); one is
  added for each of the sectors 27, 28, 35 and 36 whose owner byte
  (`0x004A0CB4`, `0x004A0CD8`, `0x004A0DD4`, `0x004A0DF8`) is the player.

Each score is a dword at `0x004A2790 + 4 * slot`. A slot with active byte 0
gets -32000 (`0x00477694`), and the standing byte `0x004ABC08 + slot` counts
the slots with a strictly greater score, then becomes 0xFF for an inactive
slot.

The end evaluator `0x00476857` calls `0x0047712A`, counts the active slots,
and sets the byte at `0x004ABBD4` to 1 when the count is exactly 1
(`0x004768AA`). It then switches on the scenario (`0x00476A5B`) whatever the
count was:

- 0 to 3: sets `0x004ABBD4` when the dword at `0x004A5EF8`, minus 1, equals
  the dword at `0x0049CA68` (`0x004768C3`).
- 4 and 7: no test.
- 5: for each of the six slots, active or not, counts the sectors it owns and
  sets `0x004ABBD4` when the count is above 39 (`0x0047693D`).
- 6: for each of the six slots sets `0x004ABBD4` when it owns all six sectors
  listed at `0x00494818` (`0x004769B3`).
- 8: sets it when any slot's score is above 39 (`0x004769EF`).
- 9: sets it when any slot's score equals 64 (`0x00476A2A`).

Nothing in the evaluator clears `0x004ABBD4`. Its only caller is the
whole-turn resolver `0x00472775` (`0x00475F61`). The outer match loop
`0x0046E766` sets `0x0049CA68` to 0 for a new match (`0x0046EB56`) and adds
one to it (`0x0046F930`) only after the resolution call and the end-of-match
handling of that round.

## Interpretation

The values of `scenario` are 0 Greed, 1 Power, 2 Acceptance, 3 Dominance,
4 Kill 'Em All, 5 Big 40, 6 Siege, 7 Eliminate, 8 Big Man and 9 Armageddon.
The title strings, the button mapping and the score and end switches agree on
this: cash for Greed, sectors for Power, Big 40 and Armageddon, completed-site
Support for Acceptance, a timed end for the four timed scenarios, the six
headquarters for Siege, and eliminated opponents for Kill 'Em All and
Eliminate, which have no end test of their own. The top-left scenario button,
the first on screen, is Kill 'Em All with value 4.

`0x004A5EF8` is `turn_limit`, `0x0049CA68` is `elapsed_turns`, `0x004ABBD4` is
`match_over` and `0x004ABBE0` is `player_active`. A timed scenario ends at the
evaluation where `elapsed_turns` is one less than `turn_limit`; since the
counter still holds the number of turns resolved before this one, the match
ends with the resolution of turn number `turn_limit`, counted from 1. The
manual's Dominance weights are the executable's.

Acceptance and Dominance do not read the sector's own Support field: they
sum the Support of the completed sites from the site slots at the moment of
the evaluation.

## Alternatives

FND-SETUP-009 and FND-SETUP-012 read the value 0 as Kill 'Em All because the
first button was lit on a fresh setup; this reading makes the first button
value 4. FND-SETUP-013 explains the lit button. FND-AI-005 gives the same
score arms as here, with 6 labelled Eliminate; the end tests and the title
strings make 6 Siege.

`0x0046A115` also writes `0x0049CA68` (`0x0046A1B7`) and the scenario
(`0x0046A158`); this finding does not follow that path.

## How to reproduce

List the executable's string table (resource type 6) and read strings 1 to
10. In Ghidra, open `0x00438B35` and find the two calls of `0x00466673` with
the scenario plus 1 and plus 95. Open `0x00438DA5`: the loop over ten
rectangles stores `k + 4` or `k - 6` into the local that is written to
`0x004ABBE8` at `0x0043952A`. Open `0x0047712A` and `0x00476857` and read their
switches on `0x004ABBE8`.
