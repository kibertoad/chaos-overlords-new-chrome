---
id: FND-AI-064
title: The AI ranks its three hire offers by Chaos, Control, Influence, fighting strength, Tech Level and Research, or Stealth, then refuses an unaffordable winner
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
    address: 0x00402D70
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABBC0..0x004ABBD2
tool: Ghidra 12.1.3
environment: null
---

## Observation

`0x00458FA0` calls `0x004078D9` after choosing a role for the next hire. The
helper takes the player and a ranking mode. It scans exactly the three offer
bytes at `0x004ABBC0 + player * 3`, picks one offer slot, and only then reads
the chosen offer's field at record offset `0x7A` (selector `0x8D`) and compares
it with the player's cash at `0x004A25E8 + player * 4`. When the field is
greater than the cash it returns -1; it does not try another offer. When the
requested mode is 0, the cash is strictly above 200 and selector 0 (the
scenario) is not 0, the helper uses mode 3 instead.

The helper reads the offer's gang definition, the 156-byte record at
`0x004A2800 + definition * 0x9C`, either through `0x00402D70` or directly.
Each selector of `0x00402D70` from `0x79` to `0x8D` reads one signed 16-bit
field of the definition held in offer slot `param_3` of player `param_2`:

| Selector | Address read | Record offset | FMT-DATA-002 field |
|---|---|---|---|
| `0x8D` | `0x004A287A` | `0x7A` | `hire_cost` |
| `0x79` | `0x004A287C` | `0x7C` | `upkeep` |
| `0x7E` | `0x004A287E` | `0x7E` | `combat` |
| `0x7F` | `0x004A2880` | `0x80` | `defense` |
| `0x7D` | `0x004A2882` | `0x82` | `tech_level` |
| `0x80` | `0x004A2884` | `0x84` | `stealth` |
| `0x81` | `0x004A2886` | `0x86` | `detect` |
| `0x82` | `0x004A2888` | `0x88` | `chaos` |
| `0x83` | `0x004A288A` | `0x8A` | `control` |
| `0x84` | `0x004A288C` | `0x8C` | `heal` |
| `0x85` | `0x004A288E` | `0x8E` | `influence` |
| `0x86` | `0x004A2890` | `0x90` | `research` |
| `0x87` | `0x004A2892` | `0x92` | `strength` |
| `0x88` | `0x004A2894` | `0x94` | `blade` |
| `0x89` | `0x004A2896` | `0x96` | `range` |
| `0x8A` | `0x004A2898` | `0x98` | `fighting` |
| `0x8B` | `0x004A289A` | `0x9A` | `martial_arts` |

The modes, with the selectors each passes and the fields they name:

| Mode | Instructions | Ranking and eligibility |
|---:|---|---|
| 0 | `0x00407931..0x004079C3`: selector `0x79`, then `0x82`, then a direct read of `0x004A287C` | Lowest Upkeep, starting from a best of 3, among offers with Upkeep at most the best and Chaos at least 0 (`TEST EAX,EAX; JL` at `0x00407985`). A later offer with an equal value replaces the earlier one |
| 1 | selectors 0, `0x83`, `0x79` | Highest Control, starting from 0. In scenario 0 the offer must also have Upkeep below 4. Later equal values win |
| 2 | selectors 0, `0x85`, `0x79` | Highest Influence, starting from 0. In scenario 0 the offer must also have Upkeep below 4. Later equal values win |
| 3 | direct reads of `0x004A287E` and `0x004A2892` to `0x004A289A` | Highest Combat plus each of Strength, Blade, Range, Fighting and Martial Arts that is above 0, starting from 0. Later equal values win |
| 4 | selectors 0, `0x7D`, `0x86`, `0x79` | Highest Tech Level + Research. Scenario 0 requires Upkeep below 5, Research at least 0 and Tech Level above 3, and later equal values win. Other scenarios require Research at least 0 and replace the best only on a strictly greater value, so the first maximum wins |
| 5 | selector `0x80` | Highest Stealth, starting from a best of 10 that an offer must reach. Later equal values win |

The decompiler reuses the player parameter as the running best in modes 1, 2
and 4 and prints misleading code for them; the ranking above is read from the
instructions.

## Interpretation

This helper picks which offer a computer player hires. The field compared with
cash is the hire price. Mode 0 looks for a cheap gang, mode 3 for a fighter,
mode 4 for a gang that raises the Tech Level and researches, mode 5 for a
stealthy one.

This corrects the field names of FND-AI-008. That finding took the statistics
block to hold Upkeep and then Combat through Martial Arts with no Tech Level
between them, so every name it gives after Defense is the field one slot
further on: its Control is Chaos, Heal is Control, Research is Influence,
Stealth and Strength are Tech Level and Research, and Detect is Stealth. Its
mode 3 lists four skills where the helper reads five fields after Combat.

## Alternatives

The field names rest on FMT-DATA-002 placing `tech_level` at `0x82`, which
FND-HIRE-006 and FND-RESEARCH-003 support independently of this helper.

## How to reproduce

Find the call from `0x00458FA0` to `0x004078D9`. Inside it, the loop of three
over `0x004ABBC0 + player * 3` holds one comparison block per mode; list each
`PUSH` of a selector before a `CALL 0x00402D70` and each `MOVSX` from
`0x004A287A` to `0x004A289A`. The cases of `0x00402D70` for selectors `0x79`
to `0x8D` give the address each selector reads. The cash comparison and the -1
return follow the loop. The rich-player override compares cash with 200 and
selector 0 with 0 before the loop.
