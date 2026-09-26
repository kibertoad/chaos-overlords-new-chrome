---
id: FND-PLATFORM-004
title: Network play is built on WinSock, the Telephony API and serial ports, with no DirectPlay import
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AE91C..0x004AE964
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AE764..0x004AE7CC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AE5FC..0x004AE760
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00486EA4..0x00486EBF
tool: Ghidra 12.1.3
environment: null
---

## Observation

- `WSOCK32.dll` supplies 18 imports, all by ordinal, at
  `0x004AE91C..0x004AE964`.
- `TAPI32.dll` supplies 26 imports at `0x004AE764..0x004AE7CC`, among them
  `lineInitialize`, `lineOpen`, `lineMakeCall`, `lineDial`, `lineAnswer`,
  `lineDrop`, `lineGetID`, `lineConfigDialogEdit` and `lineTranslateDialog`.
- `KERNEL32.dll` imports (`0x004AE5FC..0x004AE760`) include serial-port
  configuration and events (`GetCommState`, `SetCommState`,
  `SetCommTimeouts`, `SetCommMask`, `WaitCommEvent`, `ClearCommError`,
  `PurgeComm`, `CommConfigDialogA`), overlapped I/O
  (`GetOverlappedResult`), threads, mutexes, events, critical sections and
  waits.
- Strings in `.data` include the device class `comm/datamodem` (six copies),
  the port names `COM1` to `COM4`, and the command line
  `CONTROL.EXE MODEM.CPL,,ADD` at `0x00486EA4`, which opens the modem Control
  Panel.
- No DirectPlay DLL is imported, and the executable holds no `dplay` string.

## Interpretation

The original's network modes are written directly on WinSock, the Telephony
API (modem) and the serial port API, sharing one set of threads and
synchronization objects. They do not use DirectPlay, even though the GOG
installation ships a DirectPlay service provider (`dpwsockx.dll`) among its
compatibility files.

## Alternatives

The WinSock ordinals have not been mapped to function names or to the IPX and
TCP/IP modes they serve. A DirectPlay object could in principle be created
through `LoadLibraryA` and `GetProcAddress`, but no string names one.

## How to reproduce

List the imports of `WSOCK32.dll` and `TAPI32.dll`, and search the file for
`dplay`, ignoring case.
