---
id: FND-AI-008
title: The AI ranks its three hire offers by a role mode, then refuses an unaffordable winner without a fallback
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004078D9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABBC0..0x004ABBD2
tool: Ghidra 12.1.3
environment: null
---

## Observation

`0x00458FA0` calls `0x004078D9` after choosing a role for the next hire. The
helper takes the player and a ranking mode. It scans exactly the three offer
bytes at `0x004ABBC0 + player * 3`, picks one offer slot, and only then
compares that offer's gang-definition field at +0 (read through selector
`0x8D`) with the player's cash. When the winner costs more than the cash it
returns -1; it does not try another offer. When the requested mode is 0, the
player's cash is strictly above 200 and the scenario is not 0, the helper uses
mode 3 instead.

| Mode | Ranking and eligibility |
|---:|---|
| 0 | Lowest Upkeep, among offers with Upkeep at most 3 and Control at least 0. A later offer with an equal value replaces the earlier one |
| 1 | Highest Heal, starting from a best of 0. In scenario 0 the offer must also have Upkeep at most 3. Later equal values win |
| 2 | Highest Research, starting from 0. In scenario 0 the offer must also have Upkeep at most 3. Later equal values win |
| 3 | Highest Combat plus each of Blade, Ranged, Fighting and Martial Arts that is above 0, starting from 0. Later equal values win |
| 4 | Highest Stealth + Strength. Scenario 0 requires Upkeep at most 4, Strength at least 0 and Stealth above 3, and later equal values win. Other scenarios require Strength at least 0 and replace the best only on a strictly greater value, so the first maximum wins |
| 5 | Highest Detect, starting from a best of 10 that an offer must reach. Later equal values win |

The fields are read from the 156-byte gang definition records: the field at
+0 of the statistics block, Upkeep at +2, and Combat through Martial Arts at +4
through +30. Modes 1, 2 and 4 had to be read from the instructions, because the
decompiler reuses the player parameter as an accumulator and prints misleading
code for them.

## Interpretation

This helper picks which offer a computer player hires. The field at +0 of the
statistics block is the price compared with cash, and "later equal values win"
is a `>=` or `<=` comparison in the scan. The mode 0 override sends a rich
player (outside Greed) to the Combat ranking.

## Alternatives

The field compared with cash is called Force in the decoded gang definition
file. The finding reads it as the price because it is compared with cash; the
hire resolver's own price field has not been compared with it. Whether "Upkeep
at most 3" in mode 0 is an eligibility test or only the starting best value is
read from the instructions as an eligibility test.

## How to reproduce

Find the call from `0x00458FA0` to `0x004078D9`. Inside it, the loop of three
over `0x004ABBC0 + player * 3` holds one comparison block per mode; the cash
comparison and the -1 return follow the loop. The rich-player override compares
cash with 200 and selector 0 (scenario) with 0 before the loop.
