---
id: FND-COMBAT-006
title: Nothing between order entry and resolution merges two gangs that attack each other
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043B290
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046DC10
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E766
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00471F06
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004716EB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046CF38
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004726C0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046A7CB
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The attack block of `0x00472775` runs for a gang only when the gang is
  active and its action is 1. It reads the target's action only to test for
  Hide. It writes an array that marks gangs as fighting, and nothing reads
  that array as a condition for an attack.
- The Attack picker `0x0043B290` writes only the ordering gang's own target
  bytes (gang record offsets `0x08` and `0x09`) and never reads the target's
  orders.
- The other writers of the action byte outside the planners, the record
  initializer `0x0046DC10` and the copy of the recurring action in the outer
  turn function `0x0046E766`, do not compare two gangs' targets.
- `0x00471F06`, `0x004716EB` and `0x0046CF38`, which run between planning and
  resolution, do not touch the action or target bytes.
- `0x004726C0` calls the resolver directly in local games, and the connected
  session host `0x0046A7CB` calls the same resolver before sending out its
  results.

## Interpretation

Two gangs that attack each other resolve as two separate attacks, each with its
own retaliation roll, in every mode of play.

## Alternatives

An earlier reading merged such a pair into one attack and one retaliation. No
finding backed it, and the code above leaves no place where a merge could
happen.

## How to reproduce

List the writes to the gang record's action byte (offset `0x07`) and target
bytes (`0x08`, `0x09`) across the executable, and the callers of `0x00472775`.
