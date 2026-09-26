---
id: FND-RNG-003
title: The bounded wrapper makes three raw draws and returns a value from 1 to n
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045D227
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_0045D227` takes one integer argument. If it is below 1, it is replaced by
1. The function then calls `rand` (`fn_00478CD0`) three times. It uses the
third result as a selector: when the selector is greater than `0x3FFE` it keeps
the first result, and otherwise the second. It returns the kept result modulo
the argument, plus 1.

Ghidra lists 61 direct calls to `fn_0045D227`, from 24 functions. All of them
have been assigned to a caller group:

- 46 calls in 15 functions of the computer players: the command dispatcher,
  the family handlers, and the sector-target and placement helpers
  (FND-RNG-004);
- 13 calls in 8 functions of the simulation: setup portraits and reactions,
  city, site and headquarters generation, hire-offer refill and starting
  Force, the shared dice helper, and the whole-turn resolver;
- 2 calls in the preference loader `fn_0046439A` at startup (FND-RNG-001).

## Interpretation

The game asks for a random whole number from 1 to `n` and always spends
exactly three raw draws on it, including when `n` is below 1. No other game
code calls `rand`, so this wrapper is the only way the game uses the generator.

## Alternatives

None known. The control flow and constants are unambiguous; runtime output has
not been compared with a prediction.

## How to reproduce

Go to `0x0045D227`, or reach it as the only caller of `fn_00478CD0`. The three
calls to `fn_00478CD0`, the comparison with `0x3FFE`, and the final modulo and
increment follow one another in the function. List the
references to `fn_0045D227` to get the 61 calls.
