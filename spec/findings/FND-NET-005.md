---
id: FND-NET-005
title: Twenty-one serial, modem and socket helpers no entry named belong to the network code, reached only from it or from nothing
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040299E..0x00402A06
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041CEDD..0x0041CFC9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041D4CE..0x0041D51A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041DF77..0x0041E0AC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041E104..0x0041E174
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041E19F..0x0041E37A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041E683..0x0041E8DE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041F063..0x0041F566
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042306A..0x0042329B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004236E5..0x00423748
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00424148..0x004243D9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004245AC..0x00424A0C
tool: Ghidra 12.1.3
environment: null
---

## Observation

FND-NET-004 gives the network code as 118 functions, naming the serial and
TAPI ranges `0x00401EF0..0x00402B33` and `0x0041BE20..0x0041F808` as wholes.
This finding names the fifteen functions inside those ranges that no entry
described, and six more near the socket code that the list of FND-NET-004
leaves out.
Ranges are in FND-EXE-004. "Local test" says what keeps each one from running
in a local game.

Serial and modem helpers inside the ranges of FND-NET-004:

| Function | What it does | Called from | Local test |
|---|---|---|---|
| `fn_0040299E(overlapped, buffer, unused, count)` | Finishes an overlapped read on the port handle `0x00482030` with `GetOverlappedResult` without waiting; on success passes the bytes read to `fn_00402A07`, on failure other than error 6 (invalid handle) calls the hang-up `fn_0041C346`; returns 0 on failure | the serial reader `fn_0040259A` (`0x004027CE`) | serial link only |
| `fn_0041CEDD(buffer, address)` | Grows a buffer through `fn_0041DA86` to `0x71` bytes plus the address and fills it as a call-parameters block: size 16, 2, 1 and zeros in the next fields, the address's length plus 1 and offset `0x70`, then the address text at offset `0x70` (an empty text at `0x004867F0` when the address is null) | the dialler `fn_0041CA2F` (`0x0041CAB1`) | modem link only |
| `fn_0041DF77(result)` | Turns a Telephony result into a success flag: 0 gives 1, a positive request number 0. For `0x8000000E`, `0x8000001E` and `0x8000002D` it runs `fn_0041E0AD`, for `0x80000043` `fn_0041E104`, `0x80000044` `fn_0041E175`, `0x80000048` `fn_0041E19F`, `0x8000004B` `fn_0041E1C9`, `0x80000052` `fn_0041E15E` and `0x80000056` `fn_0041E134`, and returns their result; any other code gives 0 | 18 call sites in the Telephony wrappers `fn_0041C014` to `fn_0041E3F8` | modem link only |
| `fn_0041E104`, `fn_0041E134`, `fn_0041E19F` | Each shows one warning or error message box owned by the window at `0x004854D4` and returns 0 | `fn_0041DF77` | as their caller |
| `fn_0041E15E` | Shuts the Telephony line down with `fn_0041C014` and returns 0 | `fn_0041DF77` | as its caller |
| `fn_0041E1C9` | Shows a message box with Retry and Cancel (style 5) and returns whether Retry (4) was chosen | `fn_0041DF77` | as its caller |
| `fn_0041E20D` | Shows a Yes and No message box (style 4); on Yes (6) runs `fn_0041E25E` and returns 1 when it succeeded | the line openers `fn_0041BE20`, `fn_0041C36E`, `fn_0041C735` | modem link only |
| `fn_0041E25E` | Starts the modem control panel with `CreateProcessA`, closes the process and thread handles, and shows a message box when the start fails; returns 0 | `fn_0041E20D` | as its caller |
| `fn_0041E303(text)` | Copies the text to a 1,024-byte buffer, appends a fixed line when a call handle (`0x004854F4`) is held, and shows it in a warning box | the Telephony callback `fn_0041D33C`, four sites | modem link only |
| `fn_0041D4CE` | Raises the line count at `0x004854DC` to one more than its fourth argument, and posts `WM_COMMAND` `0x4B2` to the window at `0x004854E0` when there is one | the Telephony callbacks `fn_0041D19E` and `fn_0041D33C` | modem link only |
| `fn_0041E683(dialog)` | Fills combo box 1001 of the dial dialog with one name per line device up to the count at `0x004854DC`, with a fixed text for a line that cannot be opened, has no name or has an empty name, and selects the line chosen before or the first one that opens | the dial dialog procedure `fn_0041F808`, two sites | modem link only |
| `fn_0041F063(dialog)` | Enables or disables nine controls of the dial dialog by the check state of control 1008 | `fn_0041F808`, two sites | modem link only |
| `fn_0041F1CF(dialog)` | Builds the number to dial from the dialog's fields, translates it with `fn_0041DDD2` for the chosen line, and shows the results in controls 1010, 1011 and 1012 | `fn_0041F808`, ten sites | modem link only |

Connection helpers next to the socket code:

| Function | What it does | Called from | Local test |
|---|---|---|---|
| `fn_00424208(connection)` | For a connection 0 to 11 that is open (`+0`) and whose byte `+9` is 0, recomputes its ready byte `+8` from its type `+4`: type 1 is ready when `+0x191` or `+0x194` is set, types 2 and 3 when `+0x194` is set and `+0x197` is not; returns the byte, or 0 for any other connection | 19 call sites in 13 functions of FND-NET-004's list, and `fn_00424148` | its callers |
| `fn_004245AC(connection)` | For an open connection whose byte `+9` is set and whose type is 1, 2 or 3, returns whether `+0x194` is 0 | `fn_00456F80` and `fn_004677F0`, three sites each, and `fn_00424148` | its callers |
| `fn_00424148` | Calls both functions above for the twelve connections and then switches on the type with empty cases | none | dead |
| `fn_004236E5` | Returns the first connection whose socket `+0x18` is -1, or -1 | none | dead |
| `fn_004246C0(value, message, connection, state, reply)` | A line callback for one connection: for message 2 with state 2 it counts down the dword at `+0x348` or, when that is 0 or -1, sets `+0x193` and stores `value` at `+0x1AC`; with state `0x100` it sets `+0x190`, `+0x191` and `+0x194`, clears `+9`, `+0x193` and `+0x195`; with state `0x4000`, or for message 12 with a nonzero `reply`, it sets `+0x195` and clears `+0x190`, `+0x191`, `+0x193` and `+0x194` | none | dead |
| `fn_0042306A(number, out)` | Rewrites a telephone number: when it holds only digits, spaces, `-` and parentheses and is 9 to 12 characters long, writes it as `+1 (`, the characters up to the first `-`, `) ` and the rest; a number starting with three zeros loses its first four characters; any other number is copied unchanged | none | dead |

Offsets `+n` are into the 12 connection records of `0x350` bytes at
`0x004906D0` (FND-STATE-008). For every function marked dead, no instruction
calls or loads its address and no four-byte value in the file equals it.

Every caller named in the two tables, other than the session setup
`fn_0046D22F`, is in FND-NET-004's list of 118 or is one of these functions. A local game enters that list only through the
startup initializer `fn_004211E0`, the shutdown cleanup `fn_004214BA` and the
connection closer `fn_00421A2D` (FND-NET-004). None of the three calls a
function named here, except that `fn_004214BA` reaches `fn_0041DF77` through
the Telephony shutdown `fn_0041C014`, which it calls only when the Telephony
started byte `0x0049321A` is set (`0x00421526`), and a local game never sets
it. The one call from local
code that the table of FND-NET-004 does not list, the session setup
`fn_0046D22F` calling `fn_00424208` at `0x0046D6CF`, lies inside its
`network_host` test at `0x0046D25B`.

## Interpretation

These 21 functions are part of the network code of FND-NET-004. The six of
the second table are outside its list, which with them counts 124 functions and
73,353 bytes. Four of them are left over from an earlier connection
design: a per-connection line callback, a number formatter for North American
numbers and two connection scans that nothing calls. None runs in a local
game, and none touches match state, so they stay out of scope under
DEV-NET-001.

## Alternatives

- `fn_004246C0` matches the shape of a Telephony line callback; since nothing
  passes its address, it may have been meant for `lineInitialize` in place of
  `fn_0041D19E` in an earlier build.
- The texts of the message boxes are not reproduced; their roles are read from
  the call sites and the box styles.

## How to reproduce

List the callers of each function with the references view, and search the
file for each dead function's address as a little-endian dword. In
`0x0041DF77`, read the comparisons with the negative result codes. In
`0x0046D22F`, check that the call at `0x0046D6CF` is inside the block that the
test of `0x00487B58` at `0x0046D25B` guards.
