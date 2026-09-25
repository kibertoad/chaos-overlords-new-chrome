---
id: FND-GANG-001
title: Before planning, each active gang's fourteen statistics are rebuilt from its definition, its three items and its owned sector's completed sites
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E766
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004782C5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047781F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
tool: Ghidra 12.1.3
environment: null
---

## Observation

- `fn_0046E766`, the function that runs the start of each turn, first rebuilds
  every 36-byte sector record through `fn_004782C5`. That helper walks the
  sector's three site slots (definition and progress). For every site whose
  progress has reached the base Resistance of its definition, it adds the
  site's Support, Cash, Tolerance, all fourteen statistic modifiers and its
  special-site flag into the sector record.
- `fn_0046E766` then scans all six players and each player's 81 gang records,
  and calls `fn_0047781F` for every active gang, before planning begins. The
  same sequence runs again after a completed turn when the match goes on.
- `fn_0047781F` rebuilds each of the gang's fourteen effective statistic bytes
  on its own. Each starts from the matching field of the gang's 156-byte
  definition record, then adds the matching field of each of the three
  equipped items that is not -1, using the 166-byte stride of the item
  records. It adds the sector's matching sum only when the gang's player is the
  current owner of the gang's sector. The rebuilt 32-byte gang record is
  written back at once.
- In the whole-turn resolver `fn_00472775`, which copies each rebuilt gang
  record before dispatching its action, the Heal case reads the effective Heal
  and passes `Heal + 4` to the common dice helper, and the Research case reads
  the effective Research and passes `Force + Research`, adjusted only by the
  per-player difficulty band (FND-AI-007).
- The game keeps no per-site owner field: a site counts for whoever owns its
  sector once its progress has reached its Resistance.

## Interpretation

Item and completed-site modifiers are part of the dice pools the resolver
uses, not only numbers on the screen. A site completed during this turn's
Instant pass is missing from the sector and gang records built before
planning, so it affects nothing until the next rebuild.

## Alternatives

Which sector field receives each site field (for example whether Tolerance is
added to the Tolerance byte shown on the sector panel) has not been recorded
here. The order of the fourteen statistics in the gang, item and site records
is taken from the format entries.

## How to reproduce

Start at `fn_0046E766`. Its per-sector loop calls `fn_004782C5`, whose loop over
three site slots compares progress with the site definition's Resistance. The
following player-then-roster double loop calls `fn_0047781F`, which reads the
gang definition with a stride of 156 and the item records with a stride of 166,
and compares the sector's owner byte with the gang's player before adding the
sector sums.
