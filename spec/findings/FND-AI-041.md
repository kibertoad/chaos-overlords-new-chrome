---
id: FND-AI-041
title: The dispatcher resets a flagged planning record before it assigns a family, and its post-handler block is unreachable
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00432DA0..0x00434076
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00405C5B..0x00405CD4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048DB48..0x0048E2DF
tool: Ghidra 12.1.3
environment: null
---

## Observation

The dispatcher `0x00432DA0` (range in FND-EXE-004) first reads the gang's
sector through selector `0x5A` (call at `0x00432DB5`) and keeps it in a local.
It then calls selector `0x48` (`0x00432DCC`), which returns 1 when byte +1 of
the gang's planning record is nonzero and 0 otherwise (case at `0x00405A67`).
Only when the result is 1 does it call `0x00409DE1` at `0x00432DE5`, which
clears bytes +2..+10, writes 99 to byte +0, clears byte +1 and clears the two
16-bit values at +12 and +14, and then the nested switch on scenario and hire
role of FND-AI-002 runs. The inner switches have no default case, so a pair
the table leaves blank stores nothing after the reset and the family byte stays
99. When the result is 0, no family is assigned and the record is not reset.

In the scenario 8 row, before the hire-role switch, the dispatcher calls
selector `0x2F` (the elapsed-turn counter) at `0x00433819` and, when it is 0
and the scenario is 8, writes 1 to the hire role at `0x00482128 + player * 4`
(`0x00433845`). The row's switch then reads the hire role it has just written.

Every hire-role-4 cell stores the sector read at the start into the 16-bit
value at `0x0048C0BC + slot * 14 + player * 0x46E` (for example
`0x00432EC4`), which is +12 of the 14-byte auxiliary record (FND-AI-044).

After the family switch the dispatcher calls selector `0x5D` for the gang
(`0x00433C2C`) and runs a further block only when the result is greater than 3
(`0x00433C34`) and the gang's previous action (selector `0x3E`) is neither 1
nor 10. That block holds the scenario 7 test for roster slot 0 with its mode 9
Move (`0x00433C77..0x00433D42`), a sector-weight-10 branch that writes Attack
or Hide, and a Hide for scenarios other than 6 and 8 (`0x00433FD9`).

Selector `0x5D` (`0x00405C5B`) compares the gang's Force (gang byte +3) with
the 32-bit value at `0x0048DB48 + player * 0x144 + slot * 4`. When the Force
is smaller it returns `1 - 10 * (force / value)` with an integer division
(`IDIV` at `0x00405CB7`); otherwise it returns 0. The only instruction that
stores through `0x0048DB48` is `0x00459323` in `0x00458FA0`, which writes 0
for a roster slot whose gang sector is 100. The save and load functions
(`0x00463E97`, `0x00463992`) pass the block's address to the file transfer.

## Interpretation

Byte +1 marks a record whose occupant is new (FND-AI-042). Such a record is
wiped and given the family of its player's current hire role; every other gang
keeps its family from turn to turn, and the family table is not re-applied to
it. A blank cell of the table leaves the new gang in family 99, which has no
handler.

In Big Man the first planning pass of the match gives new gangs the family of
hire role 1 instead of role 0, because the role is forced to 1 on turn 0.

The array at `0x0048DB48` is 0 in every match: nothing but the clearing store
writes it, and the save carries the zeros. With a value of 0, selector `0x5D`
returns 0 for any Force of 0 or more, and a negative Force would divide by
zero. The block after the family switch therefore never runs, and the scenario
7 Move of roster slot 0 is never ordered there. Even with a positive value the
selector returns 1 for any Force between 0 and the value, so the test against
3 could not pass without a negative Force.

## Alternatives

The array could be filled by a write through a different base address that
Ghidra did not resolve, such as a block copy; no such copy was found in the
references to the region, and the only block transfers are the save and load
calls.

## How to reproduce

In `0x00432DA0`, follow the call to `0x00402D70` with selector `0x48` and the
call to `0x00409DE1` that follows it. For the post-handler block, find the
selector `0x5D` call after the family switch and the comparison with 3; then
list the references to `0x0048DB48..0x0048E2DF`.
