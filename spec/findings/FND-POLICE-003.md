---
id: FND-POLICE-003
title: The police detect a gang on a roll of 1 to 100 against 115 minus 5 Stealth, less 20 for Hide, and attack with 25 minus Defense dice at 5 or better
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00474173..0x004741A0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004741D6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475F70
tool: Ghidra 12.1.3
environment: null
---

## Observation

- In the police scan of the whole-turn resolver, the instructions at
  `0x00474173..0x0047419B` test whether the gang's current action byte is 8
  (Hide), multiply that test's result by 20, multiply the gang's effective
  Stealth by 5, and form `115 - 5 * Stealth - (20 if Hide)`. The call at
  `0x0047419B` draws a number from 1 to 100 through the bounded random wrapper,
  and the roll is compared with that value.
- When the gang is detected, the call at `0x004741D6` passes the pool
  `25 - effective Defense` and the success threshold 5 to the shared dice
  routine `0x00475F70`.

## Interpretation

The police find a gang with a chance of `115 - 5 * Stealth - (20 if Hide)`
percent, which the roll's range of 1 to 100 limits to 0 to 100 percent while
the draw is always made. The 25 is the manual's police Force 5 plus police
Combat 20. The executable does not use the manual's table (certain through
Stealth 5) or the manual's police Detect of 12 for hidden gangs.

## Alternatives

Whether the roll must be less than, or less than or equal to, the threshold was
not recorded. With less than or equal, a visible gang is certain to be found
through Stealth 3; with less than, through Stealth 2.

## How to reproduce

In `0x00472775`, after the police presence read at `0x0047415E`, read
`0x00474173..0x004741A0` for the constants 8, 20, 5 and 115 and the call to the
bounded wrapper with 100, then the call at `0x004741D6` to `0x00475F70` with the
constants 25 and 5.
