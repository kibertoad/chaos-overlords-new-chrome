---
id: FND-PLATFORM-005
title: Preferences live under the Stick Man Games registry key, and an App Paths key locates the installation
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AE554..0x004AE564
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487914..0x00487941
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487A10..0x00487A3D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004876B4..0x004876FC
tool: Ghidra 12.1.3
environment: null
---

## Observation

- `ADVAPI32.dll` supplies `RegQueryValueExA`, `RegOpenKeyExA`,
  `RegSetValueExA` and `RegCloseKey` at `0x004AE554..0x004AE564`.
- The key `SOFTWARE\Stick Man Games\Chaos Overlords\1.0` appears twice, at
  `0x00487914` and `0x00487A10`.
- The key
  `SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Chaos Overlords.exe`
  appears at `0x004876B4`.
- The executable opens the product key read-only, so its own attempts to write
  preferences fail. FND-OPTIONS-001 lists each value it reads and writes.

## Interpretation

The game reads its preferences from the product key and finds its installation
through the App Paths key. Preferences changed in the game are not written
back to the registry.

## Alternatives

None known.

## How to reproduce

Follow the references to the two key strings, and the callers of the four
registry imports.
