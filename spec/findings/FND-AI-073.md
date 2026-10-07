---
id: FND-AI-073
title: Family-10 armor and family-12 weapon and armor gates compare item cost with cash as signed values
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042A784
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00435684
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004357D2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402DD4..0x00402DE1
tool: Ghidra 12.1.3
environment: null
---

## Observation

In family 10's handler `0x0042A6E0`, the armor selected by selector `0x72`
supplies the signed cost word at item-record offset `0x7E`. The load at
`0x0042A768` sign-extends it into EBX. Selector 3 of `0x00402D70` supplies
the player's cash in EAX. The comparison at `0x0042A782` is followed by
signed `JG` at `0x0042A784`, which skips the armor Equip when cost is greater
than cash.

In family 12's handler `0x004353A0`, the weapon and armor paths load the same
cost field with sign extension at `0x00435668` and `0x004357B6`. Each calls
selector 3 for cash, compares EBX with EAX, and skips Equip on signed `JG`,
at `0x00435684` and `0x004357D2` respectively.

Selector 3 reads the full cash dword at `0x004A25E8 + player * 4`
(`0x00402DD4..0x00402DE1`), without clamping it to zero. Upkeep can leave
this value negative (FND-UPKEEP-001, RULE-UPKEEP-001).

## Interpretation

These gates accept cost at most cash as signed values. With nonnegative item
cost and negative cash they reject Equip; negative cash is a valid input.
Family 10 can reach its gate with negative cash because selector `0x72`
does not filter on cost (FND-AI-055). Family 12's weapon selector `0x61`
requires cost at most cash and armor selector `0x64` requires cost strictly
below cash (FND-AI-055), so with nonnegative costs neither supplies an
upgrade when cash is negative. Its two later comparisons therefore do not
run in that case.

## Alternatives

An unsigned comparison would treat negative cash as a large positive amount;
the signed branch instructions rule that interpretation out. This reading
does not establish behavior for negative item costs or cash overflow.

## How to reproduce

Verify BLD-GOG-EN-1.1's executable length of 664,576 bytes and SHA-256
`a1430159bbe20869e277a5000311344f4ec141ab77c96b385336617149e97d89`.
In Ghidra 12.1.3, inspect the three branch addresses and their preceding
cost loads, selector-3 calls and comparisons. Trace selector 3 to the cash
dword load at `0x00402DD7`. Compare selector `0x72` with selectors `0x61`
and `0x64` as recorded in FND-AI-055, and the unclamped upkeep arithmetic
in FND-UPKEEP-001.
