---
id: FND-NET-004
title: 118 functions make up the network code, and every call into them from local code is skipped or does nothing in a local game
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00401EF0..0x00402D67
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040B9C0..0x0040DAD4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041BE20..0x0041FEEE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004211E0..0x004252C6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00456F80..0x00458154
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004677F0..0x004688C9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00468E12..0x0046CF37
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00471F06..0x004726BB
tool: Ghidra 12.1.3
environment: null
---

## Observation

Import users. Through the import thunks, WinSock (`WSOCK32.dll`) is called
from 13 game functions, TAPI (`TAPI32.dll`) from 21, and the serial-port calls
of `KERNEL32.dll` (`GetCommState`, `SetCommState`, `SetCommTimeouts`,
`GetCommTimeouts`, `GetCommMask`, `GetCommProperties`, `PurgeComm`,
`SetCommMask`, `WaitCommEvent`, `ClearCommError`, `SetCommConfig`,
`CommConfigDialogA`) from 9. Adding every function whose direct callers all
belong to the set, repeated until nothing changes, gives 69 functions and
27,868 bytes of body (FND-EXE-004 sizes). The closure misses the TAPI helper
`0x0041DA86`, which calls itself, and `0x0041E175`, which it calls.

Network screens and session paths. The same closure, started also from the
three lobby and session screens `0x004677F0`, `0x0040B9C0`, `0x00456F80`, the
join path `0x0040DAB9`, the two connected-session resolvers `0x0046A7CB` and
`0x0040CED0`, the packet dispatcher `0x0046BA84`, the connection closer
`0x00421A2D`, the startup initializer `0x004211E0`, the socket and host-lookup
message handlers `0x00423C81` and `0x00424A0D`, the session leaver
`0x0046D00D`, the packet senders `0x004688CA` and `0x00468B1A`, the sync bar
renderer `0x0046D77B`, the network end-of-turn screen `0x00471F06` and the TAPI
callbacks `0x0041D19E`, `0x0041D2FB` and `0x0041D33C` (the first is passed as a
pointer to `lineInitialize` at `0x0041BE6F`) and the recursive helper
`0x0041DA86`, gives 118 functions and 71,050 bytes. They are:

`0x00401EF0` to `0x00402B33` (13 functions, serial), `0x0040B9C0`,
`0x0040C4C5`, `0x0040CBA5`, `0x0040CED0`, `0x0040D3C0`, `0x0040D72F`,
`0x0040D9E2`, `0x0040DAB9`, every function from `0x0041BE20` to `0x0041F808`
(45 functions, TAPI and its dialogs),
`0x004211E0`, `0x004214BA`, `0x004215BB`, `0x004217C0`, `0x00421A2D`,
`0x00421B9E`, `0x00421D1A`, `0x0042202D`, `0x004222C6`, `0x00422554`,
`0x004225FC`, `0x00422729`, `0x0042329C`, `0x00423326`, `0x0042364F`,
`0x004236CC`, `0x00423749`, `0x00423A23`, `0x00423C81`, `0x00423F80`,
`0x004240B5`, `0x00424A0D`, `0x00424A25`, `0x00424A3D`, `0x00424A55`,
`0x00424AE5`, `0x00424E1D`, `0x00424E55`, `0x00424FB4`, `0x00432926`,
`0x00449DD3`, `0x00449E26`, `0x00456F80`, `0x00457AF3`, `0x00457B7B`,
`0x00457EED`, `0x0045D286`, `0x004665A7`, `0x004677F0`, `0x004688CA`,
`0x0046892E`, `0x004689B6`, `0x00468B1A`, `0x00468E12`, `0x0046913D`,
`0x0046981D`, `0x0046A115`, `0x0046A7CB`, `0x0046BA84`, `0x0046D00D`,
`0x0046D77B` and `0x00471F06`. Each one's range is in FND-EXE-004.

Calls into the set from other game code, with what stops them in a local
game. `network_host` below is the byte `0x00487B58` and `network_join` the
byte `0x00482178`; both are 0 in a local game.

| Caller | Callee | Test |
|---|---|---|
| Title loop `0x00460CCF` | `0x004211E0` at `0x00460FFB` | Runs once at startup; it only clears the connection table and the network globals |
| `0x00460CCF` | `0x004214BA` at `0x004622B2` | Runs at shutdown; it closes connections whose open byte is set and calls `WSACleanup`, the TAPI shutdown and the serial close only when their started bytes (`0x00493219` to `0x0049321B`, `0x00493220`) are set |
| `0x00460CCF` | `0x004677F0`, `0x0040B9C0` | Menu commands `0x81`/6 and `0x81`/7, the network host and join items |
| `0x00460CCF` | `0x00456F80`, `0x0040DAB9`, `0x004215BB` | Pending actions 2 and 3, set only when the load dialog reads a network save (`W04N`) or by the network paths |
| `0x00460CCF` | `0x00421A2D` at `0x00461A10`, `0x00461BD0`, `0x00462075` | After the network paths above; the callee does nothing for a connection whose open byte (`0x004906D0 + 0x350 * n`) is 0 |
| Event reader `0x00462579` | `0x0046BA84` at `0x00462AFF` and `0x00462C2E`, `0x004688CA` at `0x00462BE0`, `0x00421A2D` at `0x00462C72` | `network_host` (`0x00462AF7`) or `network_join` (`0x00462C26`) |
| `0x00462579` | `0x0046D00D` at `0x00462891` | Menu command `0x85`/1; the callee tests `network_host` and `network_join` and otherwise returns 1 at once |
| Hot-seat handoff `0x004396C0`, planning `0x0046FD80` | `0x0046D00D` | Menu commands only; same test inside the callee |
| Outer match loop `0x0046E766` | `0x00421A2D` at `0x0046F402` | `network_join` (`0x0046F3EE`) |
| `0x0046E766` | `0x00471F06` at `0x0046F679` | `network_host` or `network_join` (`0x0046F5E7`) |
| Turn resolver entry `0x004726C0` | `0x0040CED0`, `0x0046A7CB` | `network_join` (`0x0047272F`), then `network_host` (`0x0047273E`); otherwise the local resolver `0x00472775` runs |
| Session setup `0x0046D22F` | `0x0046D77B` | `network_host` (`0x0046D25B`) |
| Turn-order sender `0x0046CF38` | `0x004688CA` | `network_join` (`0x0046CF48`), and `network_host` with a slot of type 3 (`0x0046CF6D`) |
| Save routine helper `0x00458155` | `0x004688CA` at `0x004581BC` | Only for slots whose type is 3, which a local game never has |
| Comlink recorder `0x0045D2F0` | `0x00423A23` at `0x0045D312` | Only when its first argument is not -1; its local caller `0x0045EAB1` always passes -1 |
| `0x0045D2F0` | `0x004688CA`, `0x00468B1A` | `network_host` with a recipient of type 3 (`0x0045D35B`), or `network_join` (`0x0045D3EA`) |
| Window procedure `0x0045C33B` | `0x00423C81`, `0x00424A0D` | Messages `0x404` and `0x405`, posted by `WSAAsyncSelect` and the host lookup, which a local game never starts |

The send routine `0x00423749` is reached from local code only through
`0x00468B1A` and `0x0046D00D` above.

## Interpretation

The network code is the 118 functions listed, 71,050 bytes of game code. A
local game enters none of them except the startup initializer, the shutdown
cleanup and the connection closer, which touch only the network's own state
when no connection was opened. None of the local rules depends on them, so
they are out of scope under DEV-NET-001 and do not need a spec entry of their
own beyond the network screens already described.

## Alternatives

Functions reached only through pointers other than the three TAPI callbacks
may belong to the network code and are not in the list. Whether a network
save can be loaded into a local game through pending action 2 without a
connection is not recorded.

## How to reproduce

List the import thunks of `WSOCK32.dll` and `TAPI32.dll` and the serial
entries of `KERNEL32.dll` in the import table, take the game functions that
call them, and close the set over "every direct caller is in the set" with the
caller counts of FND-EXE-004. Then read each listed caller at the given
address.
