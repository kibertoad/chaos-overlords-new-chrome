---
id: FND-HIDE-002
title: Four places in the resolver read whether a gang hides, and the Hide case itself only counts
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472D00
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047399B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00473CF8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047417D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004754D8
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ghidra lists 214 references to `0x00498DAF`, the action byte (`+7`) of the
first gang record. Of the reads among them, three are followed within five
instructions by a comparison with 8, all in the whole-turn resolver
`fn_00472775`:

- `0x004739B5` (test at `0x0047399B`): in the attack loop, when the target's
  action is 8 the resolver makes the Hide test, `roll(20)` at `0x00473ABC`
  (FND-RNG-006); a miss skips the attack.
- `0x00473D12` (test at `0x00473CF8`): the retaliation is made only when the
  target's action is not 8.
- `0x004754D8` (test at `0x004754E0`): when the Control block adds up the
  defending owner's strength in a sector, a gang whose action is 8 is left
  out.

The police loop works on a local copy of the record: at `0x0047417D` it sets a
flag when the copy's action is 8 and takes 20 off the chance before
`roll(100)`.

The Hide case of the instant switch, at `0x00472D00`, is an increment of the
dword at `0x004A25D0 + player × 4` followed by a jump to the copy-back at
`0x004730D5`.

Selector `0x3D` of the computer players' query function returns the action
byte; none of its 22 call sites in the game code is followed within nine
instructions by a comparison with 8. The visibility rebuild `fn_0046FA11`,
the gang rebuild `fn_0047781F` and the command handlers `fn_0041462F` and
`fn_00414D8C` contain no comparison of an action with 8.

## Interpretation

`is_hidden` has four consumers, all in `resolution`: the attack's Hide test,
the retaliation, the police chance and the defending strength in
`control_phase`. Hiding changes neither what a player sees nor the gang's
effective statistics, and the instant phase does nothing for a hiding gang
beyond counting it for `hide_count`. Other code writes 8 into the action
byte (the computer players, and the match-end branch of the outer turn
loop), but does not read it back as a hiding test.

## Alternatives

A read through a pointer or a copy that is not a direct reference of
`0x00498DAF` would not appear in the list; the police loop is one such read
and was found by reading the resolver. Other copies in the resolver were read
along with it. Code outside the resolver that copies whole records and then
tests the copy was not searched beyond the functions named above.

## How to reproduce

List the references to `0x00498DAF` and keep the reads followed by a
comparison with 8. In `fn_00472775`, read the police test at `0x0047417D` and
the Hide case at `0x00472D00`. List the pushes of `0x3D` before calls of
`0x00402D70`.
