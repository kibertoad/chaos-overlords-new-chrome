---
id: FND-FINANCE-003
title: The Financial panel draws its nine numbers through fn_00414187, passing each value as the third argument, from nine fixed calls in drawing order
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044DFB6..0x0044E2B0
tool: Ghidra 12.1.3
environment: null
---

## Observation

After the sums of FND-FINANCE-002, `fn_0044D1BB` draws each number with a
call of `fn_00414187` preceded by a call of `fn_00425F8C(x, y)`, whose
result is pushed as the second argument. Every call pushes, from the first
argument: 7, that position, the value, the width and a flag. The calls, in
the order the panel makes them, are:

| Call | Value | Width | Flag |
|---|---|---|---|
| `0x0044DFEB` | the upkeep sum | 4 | 0 |
| `0x0044E032` | the gang count, when it is below 10 (`0x0044DFF3`) | 1 | 1 |
| `0x0044E0AE` | the gang count, when it is 10 or more | 2 | 0 |
| `0x0044E125` | the recruits sum | 4 | 0 |
| `0x0044E162` | the equipment sum | 4 | 0 |
| `0x0044E19F` | the officials sum | 4 | 0 |
| `0x0044E1DC` | the tax sum | 4 | 0 |
| `0x0044E219` | the protection sum | 4 | 0 |
| `0x0044E256` | the Chaos sum | 4 | 0 |
| `0x0044E2AB` | the total, added at `0x0044E25E..0x0044E273` from the seven sums | 4 | 0 |

Exactly one of the two gang count calls runs, so one opening of the panel
makes nine calls of `fn_00414187` from this function. Each is followed by
`add esp, 0x14`, so the helper takes five stack arguments and the caller
removes them.

## Interpretation

A debugger that stops at `fn_00414187` while the return address lies after
one of these calls reads the drawn value at the third stack argument, so a
run of the original can record the nine numbers of each Financial panel it
opens in drawing order: upkeep, gang count, recruits, equipment, officials,
tax, protection, Chaos and total.

## Alternatives

None known.

## How to reproduce

Open `fn_0044D1BB` at `0x0044DFB6` and follow the pairs of calls of
`fn_00425F8C` and `fn_00414187` to `0x0044E2B0`, noting the stack local each
one pushes as its value and the branch on the gang count against 10.
