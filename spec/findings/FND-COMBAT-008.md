---
id: FND-COMBAT-008
title: The combat phase of the resolver, instruction by instruction - attack and retaliation, police scan, a damage cap of 10, the record and row fill, then damage and deaths
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004737D8..0x00473FE4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00473FE9..0x00474250
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00474255..0x00474895
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A8888..0x004AAE07
tool: Ghidra 12.1.3
environment: null
---

## Observation

All addresses are in the whole-turn resolver `fn_00472775`, whose range is in
FND-EXE-004. The gang record offsets are those of FND-STATE-002: player
`0x00`, definition `0x01`, sector `0x02`, Force `0x03`, weapon `0x04`, action
`0x07`, target `0x08`, target_2 `0x09`, Combat `0x12`, Defense `0x13`,
Stealth `0x14`, Detect `0x15`, Martial Arts `0x1F`. `difficulty_band` is the
`INT32` array at `0x004A2570`.

Before the attack block, `0x004737D8..0x0047386F` clears a local array of 486
bytes, one per gang (`player * 81 + slot`), and a local 6 by 64 array of
32-bit counters, one per player and sector. The 486 local 32-bit damage
totals, one per gang, were cleared at the very start of resolution
(`0x00472935`), and nothing writes them between there and the attack block.

Attack block, `0x00473884..0x00473FE4`: a scan of player slots 0 to 5 and
roster slots 0 to 80. A record copy is processed only when its sector byte is
not 100 and its action byte is 1 (`0x004738FB`). Nothing tests the target's
sector or whether the target is active. For such a gang, in this order:

1. It sets the byte of the attacker (`0x0047391C`) and the byte of element
   `target * 81 + target_2` (`0x00473930`) in the local byte array.
2. It stores -1 in the gang's entry of a local array of opening damage
   (`0x0047396D`) and 0 in its entry of a local array of retaliation damage
   (`0x00473990`).
3. It reads the action byte of the target's record (`0x00498DA8 + target *
   0xA20 + target_2 * 0x20`), and only when that byte is 8 (`0x0047399B`)
   computes the target's Stealth plus 14 minus the attacker's Detect when the
   attacker's band is 0 (`0x004739DE`) or 1 (`0x00473A17`), or plus 10 when
   it is 2 (`0x00473A50`), and draws `fn_0045D227(20)` at `0x00473ABC`. The
   compare at `0x00473AC4` jumps past the evasion flag when the draw is
   greater than or equal to that value, so the attack is evaded when the draw
   is strictly less.
4. When not evaded (`0x00473ADF`), the pool is the attacker's Force plus the
   attacker's Combat byte (`0x00473AE7`) minus the target's Defense byte
   (`0x00473AFD`). The Defense is lowered by a quarter, rounded toward zero,
   at `0x00473B61` when the band of the player named by the target record's
   own byte `0x00` is 0 (`0x00473B25..0x00473B47`). The dice routine
   `fn_00475F70` is called with the attacker's band's threshold: 6 at
   `0x00473BC1` (band 0), 5 at `0x00473BE3` (band 1), 4 at `0x00473C05`
   (band 2). When the pool is positive and the successes are fewer than the
   pool divided by 4, the damage becomes the pool divided by 4
   (`0x00473C4F..0x00473C90`).
5. The damage is added to the target's damage total (`0x00473C9F`), to the
   attacker's player's Damage Inflicted (`0x00473CCC`, FND-COMBAT-003), and
   stored as the attacker's opening damage (`0x00473CF1`).
6. The retaliation is skipped (jump to `0x00473EFC`) when the target's action
   byte is 8 (`0x00473D1A`). Otherwise it runs when the attacker's Martial
   Arts byte is 0 (`0x00473D2A`), or when the attacker's weapon byte is not -1
   (`0x00473D39`), or when the target's Martial Arts byte is greater than 0
   (`0x00473D64`) and the target's weapon byte is -1 (`0x00473D8E`).
7. The retaliation pool is the target's Force plus the target's Combat
   (`0x00473D97..0x00473DB9`) minus the attacker's Defense (`0x00473DE3`),
   with no band adjustment of the Defense and no minimum. The threshold comes
   from the band of the player in the attacker's target byte (`0x00473DF0`):
   5 at `0x00473E18` (band 0) and `0x00473E40` (band 1), 4 at `0x00473E68`
   (band 2). The successes are halved with the signed division by 2. The
   result is added to the attacker's damage total (`0x00473ED0`) and stored as
   the attacker's retaliation damage (`0x00473EF5`).
8. `0x00473EFC..0x00473FB8` lowers an attitude cell for every attack that
   reaches this block, evaded or not (FND-AI-047).

An evaded attack skips steps 4 to 7: its opening damage stays -1, nothing is
added to any damage total or to Damage Inflicted, and there is no
retaliation. For bands other than 0, 1 and 2 no dice call is made.

Row and police setup, `0x00473FE9..0x004740AB`: for each sector, player and
entry `k` from 0 to 5, the 16-bit word at `0x004A8888 + sector * 0x96 +
player * 0x18 + k * 4` is set to -1 (`0x0047405D`); the second word of the
entry is not cleared. The six bytes at `0x004A8918 + sector * 0x96 + player`
are set to 0 (`0x00474098`), and the byte `0x004988F8 + sector` is set to 0
(`0x004740AB`).

Police scan, `0x004740B7..0x00474250`: player slots 0 to 5, roster slots 0 to
80. For every slot, active or not, byte 9 of the combat record is set to -1
(`0x0047413C`). A gang whose sector is not 100 (`0x0047414B`) and whose
sector's presence byte (offset `0x0F`, `0x0047415E`) is greater than 0 has the
value `100 - (20 if action is 8) - (5 * Stealth - 15)` computed into a
register (`0x0047416E..0x00474197`) before the draw `fn_0045D227(100)` at
`0x0047419B`. The compare at `0x004741A3` skips the gang when the value is
less than the draw (`JL`), so the gang is found when the draw is less than or
equal to the value. A found gang gets its byte in the local byte array
(`0x004741BD`), the dice call `fn_00475F70(25 - Defense, 5)` at
`0x004741D6`, the successes added to its damage total (`0x00474202`) and
stored in byte 9 of its record (`0x00474224`), and the byte `0x004A8918 +
sector * 0x96 + player` set to 1 (`0x00474243`). The band is not read.

Cap, `0x00474255..0x004742E7`: every one of the 486 damage totals above 10 is
set to 10 (`0x004742B1..0x004742D7`).

Record and row fill, `0x004742EC..0x0047476A`: for every slot, the byte
`0x00498BC0 + player * 81 + slot` is set to 0 (`0x0047434E`) and then to 1
when the gang's byte in the local array is set (`0x0047437E`). Only for such
a gang does the loop continue, with no test of the gang's sector: it sets
`0x004988F8 + sector` (`0x004743A5`) and `0x00498990 + player * 81 + slot`
(`0x004743BE`) to 1, fills bytes 0 to 8 of the combat record (FND-STATE-005;
byte 0 is loaded at `0x004743DE` from `0x00498DA9`, offset `0x01` of the gang
record, and stored at `0x004743EE`), and writes one entry of the result row:
at `0x004A8888 + sector * 0x96 + player * 0x18 + n * 4`, where `n` is the
local counter for the player and the gang's sector, the first 16-bit word
receives `player * 81 + slot` (`0x004745CE`). When the gang's action byte is 1
(`0x004745E2`) the second word receives `target * 81 + target_2`
(`0x004746B1`); otherwise it receives -1 (`0x00474728`). The counter is then
increased by one (`0x0047475E`) with no upper bound.

Bytes 4 and 5 of the record are copied from the opening and retaliation
arrays of step 2. Those two local arrays have no other writes in the function
before this loop, so for a gang that fought without attacking (a target, or a
gang the police found) the two bytes receive whatever those stack locations
held.

Damage and deaths, `0x0047476F..0x00474895`: for every slot whose sector byte
is not 100, the Force byte is replaced by Force minus the capped damage total
(`0x00474827`); when the new Force is less than 1 the sector byte is set to
100 (`0x0047486F`) and the player's casualty count is raised (`0x00474889`,
FND-GANG-005). The equipment bytes are not touched. The transaction pass
follows at `0x0047489A`.

## Interpretation

- The target of an Attack is stored as the target's player in `target` and its
  roster slot in `target_2`.
- The fight marker is a local byte per gang, set for every attacker, for every
  attack's target (evaded or not, active or not), and for every gang the
  police find, even with no damage. The resolver copies it into the global
  byte array at `0x00498BC0`.
- `phase_damage` is a local 32-bit total per gang that receives attack,
  retaliation and police damage and is capped at 10 before it is applied.
  Since Force never exceeds 10, the cap changes no Force; it only bounds the
  `force_final` byte of the record, which is Force minus the capped total and
  can be negative.
- A band-0 defender loses a quarter of its Defense against the opening attack
  only; the attacker's Defense against a retaliation is never lowered. The
  retaliation threshold is 5 for bands 0 and 1 and 4 for band 2, taken from
  the target's player.
- The Martial Arts test is `== 0` on the attacker and `> 0` on the target, so
  an unarmed attacker with a negative Martial Arts is treated like a Martial
  Artist.
- The draw order of one attack is: the evasion draw if the target's action is
  Hide, the opening dice unless evaded, the retaliation dice unless the target
  is hiding or the Martial Arts test forbids it.
- A result row entry is two signed 16-bit gang indices, `player * 81 + slot`
  of the gang and of its Attack target or -1. A gang is listed in the row of
  its own player and its own sector, in roster order. The police flag of a
  sector and player is set in the police scan.
- The damage and the deaths are applied in one loop after all records and
  rows are written.

## Alternatives

- The values of bytes 4 and 5 for a gang that fought without attacking depend
  on the stack contents left by earlier calls; no reading of the stack's
  history was made. The readers of those bytes (FND-COMBAT-010) use byte 4
  only of attackers and police entries.
- An attack on an inactive target would list the target in the row of sector
  100, past the 64 rows. Whether planning can leave such a target was not
  checked.

## How to reproduce

In `fn_00472775`, follow the scan that starts at `0x00473884` to the action
test at `0x004738FB`, the calls at `0x00473ABC`, `0x00473BC1`,
`0x00473BE3`, `0x00473C05`, `0x00473E18`, `0x00473E40` and `0x00473E68`, and
the branch chain at `0x00473D1A..0x00473D91`. Then read the police scan from
`0x004740B7`, the cap at `0x004742B1`, the loop at `0x004742EC` with its
stores into `0x004A8888` and `0x004A888A`, and the damage loop at
`0x0047476F`. List the writes to the two local arrays that feed record bytes
4 and 5.
