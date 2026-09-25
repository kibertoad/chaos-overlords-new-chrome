---
id: FND-AI-044
title: The strategic refresh fills per-sector records at 0x0048E310, player-pair records at 0x0048F810 and new gangs' auxiliary records at 0x0048C0B0, in that order
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040A1A7..0x0040AA64
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048E310..0x0048F80F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048F810..0x0048FB4F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048C0B0..0x0048DB43
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048E2E0..0x0048E2F7
tool: Ghidra 12.1.3
environment: null
---

## Observation

`0x0040A1A7` (range in FND-EXE-004) takes one player. It is called from
`0x00458FA0` at `0x0045936F` and from `0x00409F47` at `0x0040A195`. Its body
runs four parts in this order.

1. `0x0040A1B0..0x0040A288`: for every observer 0 to 5 and every other player
   0 to 5, it clears the 24-byte record at
   `0x0048F810 + observer * 0x90 + other * 0x18`: the 16-bit values at +0 and
   +2, the 32-bit values at +4, +8, +12 and +16, and the byte at +20. All 36
   records are cleared, not only the planning player's row. At `0x0040A2A0` it
   writes 0 to the 32-bit value at `0x0048E2E0 + player * 4`.
2. `0x0040A2AB..0x0040A6E0`, for each sector 0 to 63, in the 14-byte record at
   `0x0048E310 + player * 0x380 + sector * 14`: +0 receives the sector's owner
   byte (`0x0040A2EA`), +2 the result of selector `0x90` for the player and
   sector (`0x0040A320`), +4 the result of selector `0x5E` (`0x0040A353`); and
   the 32-bit value at `0x00489950 + player * 0x100 + sector * 4` is set to 0
   (`0x0040A361`). When the owner is 0 or more and is not the player, it clears
   +4..+16 of the pair record (player, owner), adds 1 to its +0, then adds the
   gang byte +0x12 (Combat) and +0x13 (Defense) of each gang selector `0xB1`
   lists (the owner's gangs in the sector that the player sees, counted by
   selector `0xB0`) to +4 and +8, and those bytes of each of the player's own
   gangs in the sector (selectors `0x5E` and `0x47`) to +12 and +16. It then
   computes `(+4) - (+16)` and `(+12) - (+8)`; when the first is smaller
   (`JGE` at `0x0040A69C`) and selector `0x5E` again returns a count above 0
   (`0x0040A6B8`), it adds 1 to +2 (`0x0040A6D8`).
3. `0x0040A6E5..0x0040A878`: the hostility test of FND-AI-018 for each other
   player, reading +0 and +2 of the pair record and writing +20 and the
   attitude at `0x004AB590 + player * 0x18 + other * 4`.
4. `0x0040A87D..0x0040AA56`: for each roster slot 0 to 80 whose gang sector is
   not 100, it adds 1 to `0x0048E2E0 + player * 4` and to
   `0x00489950 + player * 0x100 + sector * 4`. When selector `0x48` returns 1
   for the slot (byte +1 of the planning record, FND-AI-042), it fills the
   14-byte record at `0x0048C0B0 + player * 0x46E + slot * 14`: +0 Combat
   (selector `0x4B`, gang byte +0x12), +2 Research (`0x53`, +0x1A), +4 Chaos
   (`0x4F`, +0x16), +6 Control (`0x50`, +0x17), +8 Influence (`0x52`, +0x19),
   and -1 in the 16-bit values at +10 and +12.

The references to the per-sector block show these readers: +2
(`0x0048E312`) is read by selector `0xAF` (`0x00406A5B`, the cached weight of
one sector), selector `0x9A` (`0x00406A1C`, the n-th sector whose value is 10)
and selector `0x6C` (`0x00406146`, `0x004061C7`, `0x0040627B`); +8
(`0x0048E318`) is read by selector `0x30` (`0x004038DD`, `0x004038FC`). No
instruction reads +0, +4, +6, +10 or +12 of it. Of the auxiliary record, +0
to +8 are written only here and read by no instruction; +10 (`0x0048C0BA`) and
+12 (`0x0048C0BC`) are written by the dispatcher and most family handlers and
read by `0x00402D70` and `0x00436C70`.

The pair records are 24 bytes: +0 the count of the other player's sectors,
+2 the count of those the player out-fights, +4, +8, +12 and +16 the
per-sector sums, and +20 the flag. Six observers of 0x90 bytes end at
`0x0048FB4F`. The per-sector block of six players (0x380 bytes each) ends at
`0x0048F80F`, directly before the pair records. The auxiliary block of six
players (0x46E bytes each) ends at `0x0048DB43`.

None of the three blocks, nor `0x0048E2E0`, is referenced by the save and load
functions `0x00463CC5` and `0x0046381A`.

## Interpretation

`sector_weight` is +2 of the per-sector record, a signed 16-bit value at
`0x0048E312 + player * 0x380 + sector * 14`. `combat_advantage` is +20 of the
pair record at `0x0048F810 + observer * 0x90 + other * 0x18`. The value at
`0x0048E2E0` is the player's count of active gangs, which the hire gate
compares with its limit (FND-AI-003). The auxiliary records are
`0x0048C0B0 + player * 0x46E + slot * 14`: the `focus` value of FND-AI-015 is
+10 and the `coverage_sector` value is +12; the five statistics at +0..+8 are
a snapshot of a new gang that nothing uses.

Because the weights are computed in part 2 and the hostility in part 3, a
hostility set by this pass changes the sector weights only at the player's next
pass. The advantage count needs at least one of the player's own gangs in the
sector: a sector with no own gang never counts, whatever the defenders sum to.
`(theirs Combat - ours Defense) < (ours Combat - theirs Defense)` is the same
test as ours Combat + Defense greater than theirs, as long as the sums do not
overflow 32 bits.

The blocks are not saved: after a load the per-sector and pair records are
rebuilt at the next pass (and the site sums by FND-AI-045), and the auxiliary
records of every gang whose record is not flagged keep whatever the running
program holds, which after loading into a fresh program is 0.

## Alternatives

The count of own gangs in part 2 is read through selector `0x5E` both for the
list loop and for the final test; the two calls return the same value, since
nothing in between moves a gang.

## How to reproduce

Read `0x0040A1A7` from its entry: the double loop clearing `0x0048F810`, the
sector loop with calls to selectors `0x90`, `0x5E`, `0xB0`, `0xB1` and `0x47`,
the loop over six players with selector `0x36`, and the roster loop with
selector `0x48` followed by five selector calls and two stores of `0xFFFF`.
List the references to `0x0048E310..0x0048F80F` and `0x0048C0B0..0x0048DB43`.
