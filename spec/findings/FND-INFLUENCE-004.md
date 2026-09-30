---
id: FND-INFLUENCE-004
title: At difficulty band 0 the Influence resolver keeps the dice pool in the local that holds the site's progress
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472D40..0x00472EC1
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the resolver `fn_00472775`, the Influence case loads the progress byte of
the ordered site slot (`0x004A08F0 + sector * 36 + slot * 2`) into the local
`[EBP-0x1894]` at `0x00472D5A` and compares it with the site's Resistance in
`[EBP-0x1A80]`; equal values skip the action. It then branches on the player's
difficulty band (`0x004A2570 + player * 4`).

The band-0 branch (`0x00472D8A`) writes the gang's Force plus Influence into
the same local `[EBP-0x1894]`, subtracts the local divided by 5 (`IDIV`, so
toward zero) and passes the result to the dice helper `0x00475F70` with
threshold 5 (`0x00472DC3`). The band-1 branch (`0x00472DD6`) and the band-2
branch (`0x00472DFC`) compute Force plus Influence on the stack and pass it
straight to the helper, with thresholds 5 and 4, leaving the local alone.

All three join at `0x00472E53`, which adds the helper's success count to the
local. When the local is at least the Resistance it is set to the Resistance
and the completion report is emitted (`0x00472E98`). The low byte of the local
is then stored back into the progress byte (`0x00472EBA`).

The Research case (`0x00472F17`) keeps its band-0 pool in `[EBP-0x1A80]`,
apart from the remaining-points local it lowers, and the Heal case
(`0x00472C0E`) reduces no pool.

## Interpretation

At band 0 an Influence sets the site's progress to the reduced pool plus the
successes, capped at the Resistance, whatever the progress was: one Influence
can complete a site, a site with more progress than that can lose some, and a
pool below 0 (a negative Influence) leaves progress below 0. At bands 1 and 2
the progress grows by the successes. Only computer players at Goon are in band
0 (RULE-AI-018).

## Alternatives

None. EXP-TURN-023 shows the band-0 result.

## How to reproduce

Run `ReportInstructionWindow.java 0x00472D40 80` and follow `[EBP-0x1894]`
from its load at `0x00472D5A` through the band branches to the store at
`0x00472EBA`.
