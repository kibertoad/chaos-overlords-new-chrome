---
id: FND-UI-066
title: Each of the 23 calls of the panel-open helper sits in a different panel handler, so its return address names the handler that opened the panel
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041953E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042E22D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043B9F3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043DEA5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00440716
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00441A94
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004429AA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004444A0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00446133
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044878C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00448EED
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044ACEB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044BE32
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044CC7A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044E2B5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044EEBD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044F3D1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00451B68
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00452146
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00454D25
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004556E5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00456944
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045D744
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045F178
tool: Ghidra 12.1.3
environment: null
---

## Observation

The panel-open helper `fn_0041953E` (FND-UI-011, FND-UI-056) has 23 direct
call sites and no other reference. Each is a 5-byte `call` with a 32-bit
relative target, so the return address it pushes is the call's address plus 5.
The instruction before each call pushes the helper's one argument, the mode
byte: 1 for the alternate form, 0 for the primary form.

Each call lies in a different function, and none of those functions calls the
helper a second time. The table names each function by the handler an earlier
finding describes, and gives the panel image resource numbers the function
contains as constants.

| Return address | Call | Function | Handler | Image resource | Mode |
|---|---|---|---|---|---|
| `0x0042E232` | `0x0042E22D` | `fn_0042E040` | Detailed Combat (FND-COMBAT-009) | 5014 | 0 |
| `0x0043B9F8` | `0x0043B9F3` | `fn_0043B290` | Attack picker (FND-ATTACK-002) | 5003 | 0 |
| `0x0043DEAA` | `0x0043DEA5` | `fn_0043DAD9` | Equip panel (FND-EQUIP-005) | 5004 | 0 |
| `0x0044071B` | `0x00440716` | `fn_0043F692` | Influence picker (FND-TURN-009) | 5005 | 0 |
| `0x00441A99` | `0x00441A94` | `fn_004413EF` | Move panel (FND-MOVE-002) | 5006 | 0 |
| `0x004429AF` | `0x004429AA` | `fn_004427FA` | Research panel (FND-EQUIP-005) | 5007 | 0 |
| `0x004444A5` | `0x004444A0` | `fn_00443BBD` | Sell panel (FND-EQUIP-004) | 5013 | 0 |
| `0x00446138` | `0x00446133` | `fn_00445A4F` | Give panel (FND-EQUIP-003) | 5015 | 0 |
| `0x00448791` | `0x0044878C` | `fn_00448718` | two-choice panel of the idle-gang warning (FND-OPTIONS-002, FND-OPTIONS-003) | 5020 | 0 |
| `0x00448EF2` | `0x00448EED` | `fn_00448E32` | Search (FND-SEARCH-001) | 5024 | 0 |
| `0x0044ACF0` | `0x0044ACEB` | `fn_00449E80` | live-gang panel (FND-GANG-004) | 5000 | 0 |
| `0x0044BE37` | `0x0044BE32` | `fn_0044B699` | Item Information (FND-UI-015) | 5001 | 1 |
| `0x0044CC7F` | `0x0044CC7A` | `fn_0044C476` | Site Information (FND-UI-011) | 5002 | 1 |
| `0x0044E2BA` | `0x0044E2B5` | `fn_0044D1BB` | City and Sector Financial (FND-FINANCE-001) | 5008, 5019 | 1 |
| `0x0044EEC2` | `0x0044EEBD` | `fn_0044E6ED` | Gangs in Sector (FND-UI-002) | 5009 | 0 |
| `0x0044F3D6` | `0x0044F3D1` | `fn_0044F2FC` | Last Turn Events (FND-EVENT-001) | 5010 | 0 |
| `0x00451B6D` | `0x00451B68` | `fn_004518D9` | Player Rankings (FND-UI-059) | 5011 | 0 |
| `0x0045214B` | `0x00452146` | `fn_00451F80` | Combat Results (FND-COMBAT-007) | 5012 | 0 |
| `0x00454D2A` | `0x00454D25` | `fn_004546C5` | Hire comparison (FND-UI-011) | 5016 | 1 |
| `0x004556EA` | `0x004556E5` | `fn_0045519D` | Game Information (FND-UI-003) | 5021 | 1 |
| `0x00456949` | `0x00456944` | `fn_00455B6B` | Gang Definition Information (FND-GANG-002) | 5022 | 1 |
| `0x0045D749` | `0x0045D744` | `fn_0045D61A` | Comlink View (FND-COMLINK-002) | 5017 | 0 |
| `0x0045F17D` | `0x0045F178` | `fn_0045EAB1` | Comlink Send (FND-COMLINK-001) | 5018, 5023 | 0 |

The six calls with mode 1 are the six alternate-form callers FND-UI-011 lists.

## Interpretation

Within one run, the return address on top of the stack when `fn_0041953E` is
entered identifies the handler that opened the panel. Of the 23 handlers, 21
contain one panel image resource, so for them it also names the panel that
slid in. City and Sector Financial `fn_0044D1BB` (5008, 5019) and Comlink Send
`fn_0045EAB1` (5018, 5023) each contain two, and the return address alone does
not say which of the two slid in. A probe that reads it at the helper's entry
can attribute each recorded slide to its handler without relying on the
travel, which only separates the two forms.

## Alternatives

None for the attribution. Indirect calls of the helper through a register or a
table were not seen: Ghidra lists no reference to `0x0041953E` other than the
23 calls.

## How to reproduce

List the references to `0x0041953E`. For each, note the call's address and
length, the immediate pushed by the instruction before it, the function that
contains it, and the scalars from 5000 to 5030 that function uses. Compare the
functions with the handlers named in the findings the table cites.
