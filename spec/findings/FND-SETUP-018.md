---
id: FND-SETUP-018
title: Every match entry sets the turn limit to 65535 when the scenario number is above 3
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004395E1..0x004395F8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046EB2E
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_00439563` (range in FND-EXE-004) writes the six headquarters candidates,
sets every sector's owner byte to -1 when its byte argument is nonzero, and
then compares `scenario` at `0x004ABBE8` with 3 at `0x004395E1`. The jump at
`0x004395E8` is a signed less-or-equal; when it is not taken, the instruction
at `0x004395EE` stores the 32-bit value `0xFFFF` (65535) into `turn_limit` at
`0x004A5EF8`. Nothing else in the function touches `turn_limit`.

Its only caller is the match entry `fn_0046E766`, at `0x0046EB2E`, passing on
its own byte argument. The call comes before `fn_0046E766` tests that argument
at `0x0046EB4E` to tell a new match from a loaded one (FND-AI-045), so it runs
for both.

The other writers of `turn_limit` are the setup initializer `fn_004384C0`,
which stores 52 at `0x004384D2` before the setup screen opens (FND-SETUP-013),
the full local setup screen `fn_00438DA5`, which stores the length it edited at
`0x0043953A`, and `fn_0046A115` at `0x0046A17E`. `fn_0046381A` and
`fn_00463CC5` pass its address at `0x004638F0` and `0x00463DF5`.

## Interpretation

Greed, Power, Acceptance and Dominance (0 to 3), the timed scenarios, keep the
length chosen at setup. Every other scenario plays with a `turn_limit` of
65535, whatever length was chosen, and a loaded match of such a scenario gets
65535 again on entry. Every rule that reads `turn_limit` or `turns_remaining`
in those scenarios reads 65535, among them the hire schedule of RULE-AI-010
(`turn_limit / 52.0`) and the objective families of RULE-AI-031 (the parity of
`turns_remaining()`).

EXP-SETUP-001 read 65535 in `turn_limit` at the first planning phase of a Kill
'Em All match.

## Alternatives

None: the compare, the branch and the store are consecutive instructions with
no other path to the store.

## How to reproduce

List the references to `0x004A5EF8` in Ghidra. The write of the constant
`0xFFFF` is at `0x004395EE` in `fn_00439563`; the two instructions before it
compare `scenario` with 3 and skip the store when it is 3 or less. List the
callers of `fn_00439563`: the only one is `0x0046EB2E` in `fn_0046E766`.
