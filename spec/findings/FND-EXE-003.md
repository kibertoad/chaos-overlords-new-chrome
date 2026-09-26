---
id: FND-EXE-003
title: The executable was linked by Microsoft's linker 3.10 with a statically linked C runtime
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040009A..0x0040009C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00478D00
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AE000..0x004AE0DC
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The optional header's linker version bytes at `0x0040009A` are 3 and 10.
- The import directory at `0x004AE000` names ten DLLs: `KERNEL32.dll`,
  `USER32.dll`, `GDI32.dll`, `comdlg32.dll`, `ADVAPI32.dll`, `WINMM.dll`,
  `smackw32.dll`, `DDRAW.dll`, `WSOCK32.dll` and `TAPI32.dll`, and no C runtime
  DLL such as `MSVCRT.dll`.
- `KERNEL32.dll` imports include the functions a C runtime's startup and heap
  code calls: `GetVersion`, `GetCommandLineA`, `GetStartupInfoA`,
  `GetEnvironmentStrings`, `HeapCreate`, `HeapAlloc`, `TlsAlloc`, `RtlUnwind`,
  `UnhandledExceptionFilter`, `SetHandleCount`, `GetStdHandle` and the
  `LCMapString` and `GetStringType` pairs.
- The entry point at `0x00478D00` begins by installing a structured exception
  frame through `fs:[0]` (`push -1`, then two pushed addresses, then
  `mov fs:[0], esp`), reserves `0x60` bytes of stack, and then calls
  `GetVersion` through its import slot at `0x004AE6A0`.

## Interpretation

The linker version and the startup sequence match a Microsoft Visual C++ 4.x
toolchain with the C runtime linked statically, which fits the 1996 timestamp
(FND-EXE-001). The C runtime's code is therefore inside `.text`, and library
functions such as `rand` or `sprintf` appear as ordinary functions of the
executable.

## Alternatives

Another compiler that used Microsoft's linker 3.10 could produce the same
header. The entry sequence has not yet been matched byte for byte against a
known Visual C++ runtime, so the exact compiler version is open.

## How to reproduce

Read the linker version in the optional header, list the import directory, and
disassemble the entry point `0x00478D00`.
