---
id: FND-SETUP-009
title: A fresh setup takes its scenario from a preference byte that starts at 0 and may be replaced from the registry
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487858
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046439A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004384C0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABBE8
tool: Ghidra 12.1.3
environment: null
---

## Observation

The initialized data byte at `0x00487858` holds 0. The preference loader at
`0x0046439A` replaces it with the registry value `prefsObjective` when that
value exists. The setup initializer at `0x004384C0` copies the byte, sign
extended, into the scenario dword at `0x004ABBE8` before it draws the full
local setup screen.

## Interpretation

A fresh setup starts on scenario 0 unless a stored preference says otherwise.
FND-SETUP-012 shows the running original opening its setup screen on Kill 'Em
All, the first scenario button, with no stored preference, so scenario 0 is
Kill 'Em All in the original's numbering.

## Alternatives

An earlier reading mapped the value 0 through a later program's own scenario
order, in which 0 is Greed; that order is not the original's and was dropped.
Where the preference loader writes the byte back, and when, is not recorded
here.

## How to reproduce

Find the registry value name `prefsObjective` in the executable's data and
follow its reference into `0x0046439A`, which stores into `0x00487858`. Follow
the references to `0x00487858` to `0x004384C0`, which loads it with sign
extension and stores it to `0x004ABBE8`.
