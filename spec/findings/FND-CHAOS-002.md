---
id: FND-CHAOS-002
title: The Chaos rolls read sector offset 0x04, the Crackdown test sums each player's successes with a band-2 owner's cut by a quarter, and the payout skips gangs that died in combat and also raises cash_earned
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047281C..0x004728E1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00473188..0x004733CD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047340D..0x004737D3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00474E57..0x00475091
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475E16..0x00475ECD
tool: Ghidra 12.1.3
environment: null
---

## Observation

All addresses are in `fn_00472775` (range in FND-EXE-004). Sector records are
`0x004A08E8 + sector * 0x24` (FMT-STATE-002); gang record offsets are those of
FND-STATE-002.

Presence table, `0x0047281C..0x004728E1`, at the start of resolution: a local
byte array indexed `sector * 6 + player`, set to 0 and then to 1 for every
sector that one of the player's 81 records names in its sector byte.

Chaos rolls, `0x00473188..0x004733CD`. `0x00473188..0x004731DE` clears a
local 6 by 64 array of 32-bit totals, one per player and sector. The scan
`0x004731F3` visits player slots 0 to 5 and roster slots 0 to 80 and handles a
record only when its sector byte is not 100 and its action byte is 3
(`0x0047326A`). The pool is the sector byte loaded at `0x004732A7` with
displacement `0x004A08EC`, offset `0x04` of the gang's sector record, plus the
gang's Chaos byte (offset `0x16`, `0x004732AF`) plus its Force byte
(`0x004732B8`). For band 0 the pool is lowered by a fifth, rounded toward
zero (`0x004732D5`), and the dice routine is called with threshold 5
(`0x004732EA`); band 1 uses the full pool with 5 (`0x00473324`) and band 2
the full pool with 4 (`0x0047335E`). The successes are stored in a local
32-bit array per gang (`0x004733C0`) and added to the total of the gang's
player and sector (`0x004733CD`). No test of police presence is made.

Sector pass, `0x0047340D..0x004737D3`, sectors 0 to 63 in order. For every
sector, first each history slot (`0x004ABCC0`, `0x004ABCC2` + `sector * 4`)
that is not -100 and is less than the `INT32` at `0x0049CA68` minus 5 is set
to -100 (`0x00473435..0x004734B1`). Then the sector total is summed over the
six players (`0x004734BB..0x00473572`): a player that owns the sector and
whose band is 2 contributes its summed total minus a quarter of it, rounded
toward zero (`0x0047352A..0x00473555`); every other player contributes its
total unchanged. The sector's byte at offset `0x05` is loaded at `0x0047358D`,
and the pass goes on to the next sector (`0x0047359B`) when that byte is
greater than or equal to the total. Otherwise, for each player 0 to 5 in turn
(`0x004735A1`), it sets the stored successes of every one of that player's 81
records whose sector byte is the sector to 0 (`0x004735F1..0x00473629`),
whatever their action, and, when the presence table holds 1 for the sector
and player (`0x00473642`), calls the report recorder with type 1 and the
sector (`0x0047366F`). The history fill, the neutralization and the only
change to police presence follow (FND-POLICE-004): the turn stored is the
same `0x0049CA68`, the three bytes the neutralization clears are offsets
`0x08`, `0x0A` and `0x0C` (`0x00473746..0x00473768`), the progress bytes of
the three site slots, and the type 3 report (`0x00473724`) is passed the
owner byte as it stands, which the recorder drops when it is -1
(`0x0047774E`).

Payout, `0x00474E57..0x00475091`, after the transaction pass. The totals are
cleared (`0x00474E57..0x00474EAD`) and rebuilt by a scan of every player's
81 records (`0x00474EC2..0x00474F73`) that adds a gang's stored successes to
its player's total for the sector in its sector byte, only when that byte is
not 100 and the action byte is 3 (`0x00474F39`). Then, for each sector 0 to
63 and, inside it, each player 0 to 5 (`0x00474FB3`, `0x00474FD5`), a total
whose player is not the sector's owner (`0x00475000`) is divided by 2 with a
signed division (`0x00475026..0x00475045`), and the total is added to the
player's `cash` at `0x004A25E8` (`0x0047506B`) and to `cash_earned` at
`0x004A27E0` (`0x00475091`).

End of resolution: the presence countdown `0x00475E16..0x00475E81` (the
decrement at `0x00475E74`, FND-POLICE-001) runs before the call of the
elimination helper `fn_00476F3B` at `0x00475ECD`.

## Interpretation

- The Chaos pool reads `income`, offset `0x04` of the sector record, as does
  Control (FND-CONTROL-003); it does not read `cash_yield` at `0x03`.
- The history window and the stored turn use `elapsed_turns`.
- The Crackdown test is per sector, strict (a total equal to the Tolerance
  does not trigger), and uses each player's total, not each gang's roll, when
  it cuts a band-2 owner's Chaos by a quarter.
- A sector already under police presence still pays Chaos when it does not
  trigger this turn.
- The Crackdown reports go out player by player in ascending slot order,
  interleaved with the zeroing of that player's gangs.
- A Chaos gang killed in this turn's combat has sector 100 at payout time and
  is not paid. A gang is paid for the sector it is in after combat, which is
  its ordered sector, since Move comes later.
- The payout counts toward `cash_earned` as well as `cash`, sector by sector
  and player by player within a sector.

## Alternatives

None known. The stored successes of gangs whose action is not Chaos are never
initialized; they are read only for gangs whose action is 3.

## How to reproduce

In `fn_00472775`, read the scan from `0x004731F3` with its load at
`0x004732A7` and the three dice calls, the sector loop from `0x0047340D` with
its compare at `0x0047358D` and report call at `0x0047366F`, and the payout
loops from `0x00474EC2` to the two adds at `0x0047506B` and `0x00475091`.
