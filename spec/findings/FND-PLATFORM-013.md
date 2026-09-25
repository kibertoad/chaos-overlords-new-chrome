---
id: FND-PLATFORM-013
title: The ordinal imports of WSOCK32 and smackw32 by name, and the functions that call the WinSock, Telephony and serial port imports
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AE91C..0x004AE963
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AE974..0x004AE9B3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004786A2..0x0047870D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047870E..0x004787A9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00421D1A..0x00424E54
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges of the functions named here are in FND-EXE-004. Import slots were read
from the executable's import directory with a script; the smackw32 names come
from the export table of the shipped `SMACKW32.DLL`, the WSOCK32 names from the
fixed ordinals of the Windows Sockets 1.1 `wsock32.dll`.

`WSOCK32.dll`, slots `0x004AE91C..0x004AE960` in order: 10 `inet_addr`, 2
`bind`, 23 `socket`, 4 `connect`, 9 `htons`, 115 `WSAStartup`, 116
`WSACleanup`, 3 `closesocket`, 55 `getservbyname`, 19 `send`, 16 `recv`, 13
`listen`, 57 `gethostname`, 11 `inet_ntoa`, 52 `gethostbyname`, 111
`WSAGetLastError`, 1 `accept`, 101 `WSAAsyncSelect`.

`smackw32.dll`, slots `0x004AE974..0x004AE9B0` in order: 20 `_SmackGoto`, 8
`_SmackBufferOpen`, 22 `_SmackOpen`, 14 `_SmackClose`, 3 `_SmackBufferClose`,
28 `_SmackToBuffer`, 26 `_SmackSoundOnOff`, 1 `_SmackBufferBlit`, 15
`_SmackColorRemap`, 7 `_SmackBufferNewPalette`, 9 `_SmackBufferFocused`, 31
`_SmackVolumePan`, 21 `_SmackNextFrame`, 17 `_SmackDoFrame`, 29
`_SmackToBufferRect`, 32 `_SmackWait`.

WinSock. The thunks at `0x004786A2..0x00478708` are called only from:

| Function | Imports it calls |
|---|---|
| `fn_004215BB` | `WSAStartup` with version `0x101` (`0x00421657`) |
| `fn_004214BA` | `WSACleanup` |
| `fn_004225FC` | `socket(2, 1, 0)`, `bind`, `listen`, `WSAAsyncSelect`, `WSAGetLastError` |
| `fn_00422729` | `socket(2, 1, 0)`, `htons(0x10AD)` (`0x0042294F`), `connect`, `WSAAsyncSelect`, `WSAGetLastError` |
| `fn_00421D1A` | `accept`, `WSAAsyncSelect` |
| `fn_0042329C`, `fn_00423326` | `closesocket` |
| `fn_0042364F`, `fn_004236CC` | `inet_addr` |
| `fn_00423749` | `send`, `WSAGetLastError` |
| `fn_00423A23` | `recv`, `WSAGetLastError` |
| `fn_00423F80` | `recv` |
| `fn_00424AE5` | `gethostname`, `gethostbyname`, `inet_ntoa`, `htons`, `getservbyname`, `WSAGetLastError` |

Every socket is address family 2 (`AF_INET`) and type 1 (`SOCK_STREAM`).
`WSAAsyncSelect` posts message `0x401` to the window at `0x00498570`
(`0x00422662`). `fn_00424AE5` fills the port either with 4269 (`0x10AD`,
`0x00424D4D`) or with the port `getservbyname` returns for a service name
held at `0x00492F90` and the protocol `tcp` (`0x00424D94`), and shows a
message box when that lookup fails.

Telephony. The thunks at `0x0047870E..0x004787A4` are called from
`fn_0041BE20`, `fn_0041C014`, `fn_0041C0E0`, `fn_0041C36E`, `fn_0041C735`,
`fn_0041CA2F`, `fn_0041D51B`, `fn_0041DB3B`, `fn_0041DBB8`, `fn_0041DC72`,
`fn_0041DD24`, `fn_0041DDD2`, `fn_0041DEB7`, `fn_0041E0AD`, `fn_0041E37B`,
`fn_0041E3F8`, `fn_0041E8DF`, `fn_0041EB5F`, `fn_0041ED08`, `fn_0041F567` and
`fn_0041F808`.

Serial port. `fn_00424E55` opens a COM port with `CreateFileA` for overlapped
I/O, and `fn_00424FB4` opens the ports and calls `CommConfigDialogA`.
`fn_00401EF0` calls `GetCommState`, `SetCommState` and `SetCommTimeouts`;
`fn_00402140`, `fn_004021C1` and `fn_00402242` call `PurgeComm`;
`fn_0040259A` calls `PurgeComm` and `SetCommMask`; `fn_00402A8D` calls
`WaitCommEvent`; `fn_00402B33` calls `ClearCommError`.

## Interpretation

The WinSock mode is TCP/IP only: no socket of the IPX family (6) or of
datagram type is created. One side listens and accepts, the others connect, by
default on port 4269, and socket events arrive as window messages. The modem
mode is the Telephony code in `0x0041BE20..0x0041F808`, and the serial
mode is the code at the start of the executable and at
`0x00424E55..0x00424FB4`.

## Alternatives

Which service name `0x00492F90` holds when the `getservbyname` branch runs,
and which setting chooses between that branch and the fixed port, were not
followed. The TAPI thunks were matched to callers only; which Telephony call
each function makes is not listed here.

## How to reproduce

Parse the import directory and match the smackw32 ordinals to the export
table of `SMACKW32.DLL`. List the callers of each thunk in
`0x004786A2..0x004787A4`, and read the arguments pushed before the calls of
`socket`, `htons` and `WSAStartup`.
