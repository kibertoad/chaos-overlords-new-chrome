---
id: FND-NET-001
title: The network packet dispatcher reads 16-byte headers and handles sixteen packet types, one set on the joining side and one on the hosting side
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046BA84..0x0046CF37
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498980..0x0049898F
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_0046BA84` occupies `0x0046BA84..0x0046CF37` (5,157 bytes, FND-EXE-004). It
takes no arguments. It is called twice from the event reader `fn_00462579`,
once from `fn_00456F80` and once from `fn_004677F0`, so incoming packets are
handled whenever a screen reads its next event.

Every packet starts with a 16-byte header read into `0x00498980`: four 32-bit
fields (type, then three arguments, the first usually a player slot), each
passed through the byte-order helper `fn_00449E26`.

Joining side. When the byte at `0x00487B5C` is 0, either `0x00482178` or
`0x00487B64` is set, and connection 0 has data (`fn_00423F80(0)`), it reads one
header from connection 0 and acts on its type:

| Type | Action |
|---|---|
| 0 | Appends the slot to the list at `0x004905A8` (count at `0x004905A4`) and sets its `controller` to 0; past four entries it removes the last one again and answers with type 1 |
| 2 | Reads the six portrait bytes at `0x004A5F00` and six bytes at `0x004A27C0`, then redraws through `fn_0040C4C5` |
| 5 | Sets `0x004905A0` and `0x0048FB70`, calls `fn_0040D72F`, answers type 5 with -2 |
| 7 | Sends the slot's 81 gang records and three bytes each from `0x004ABBC0` and `0x004A27C8` as `0xA26` bytes, followed by a four-byte checksum, updating the progress display `fn_0040D3C0` |
| 8 | Receives the match: gang records addressed by player and roster slot until -1, sector records until -1, then fixed blocks at `0x004A25E8`, `0x004ABBC0`, `0x004A27C8`, `0x004A2608`, `0x004ABC08`, `0x004A2790`, `0x004ABBE0`, `0x004ABBD4` and `0x004AAE08`, and six more 24-byte arrays when `0x004ABBD4` is set; it clears the combat results table at `0x004A8888`, reads sector rows of it until -1, reads 10-byte records into `0x004A11E8` addressed by a 16-bit index below `0x1E6` until `0xFFFF`, reads six bytes at `0x004ABC58` and compares a four-byte checksum with the running sum at `0x004988F0`, calling `fn_0040D72F` on a mismatch |
| 9 | Sets `0x004905A0`, clears `0x0048FB70` and calls `fn_00421A2D(0)` |
| 10 | Records a Comlink message through `fn_0045D2F0(0, slot)` |
| 11 | Sets the slot's byte at `0x004ABC88` and redraws with `fn_0041B4EA` |
| 12 | Calls `fn_00421A2D(0)` |
| 14 | As type 7 without the checksum |
| 15 | Sets `0x00487770` and reads the six 12-byte name records at `0x004A2588` |
| 16 | Calls `fn_00449C8E`, sends menu command `0x8F` and calls `fn_00421A2D(0)` |

Hosting side. When `0x00487B58` or `0x00487B60` is set it polls connections 0
to 11 and reads a header from each that has data. Types 0, 1, 3, 4 and 9 act
only while `0x00487770` is 0: 0 finds a free slot and portrait
(`fn_00468BBC`, `fn_00468C0E`), answers type 0 and plays slot 3; 1 frees the
sender's slot (portrait 15, `controller` -1, connection -1); 3 and 4 step the
sender's portrait with `fn_00468CFC` and `fn_00468D87`; 9 answers type 9. Type 2
answers with the portraits; 5 exchanges the names; 7 and 14 receive a slot's
gang records into the gang table, 7 with a checksum and 14 setting the slot's
byte at `0x00494BF4`; 10 records a Comlink message; 11 sets the slot's byte at
`0x004ABC88` and relays type 11 to every connection whose `controller` is 3;
12 calls `fn_00421A2D` for the connection; 13 stores the first argument at
`0x00498938 + 4 * connection`.

## Interpretation

This is the whole message layer of network play. The host keeps the lobby
(slots, portraits, names) and the match state, a joining computer uploads its
players' orders as whole gang records (types 7 and 14), the host sends the
resolved match back (type 8), and type 11 marks a player's end of planning.
Only type 10, the Comlink message, reaches game code shared with local play.
The spec describes network play only as far as its lobby screens and file
transfers; the table records what the dispatcher does so that it is placed.

## Alternatives

- The meaning of the per-slot bytes at `0x004ABC88`, `0x00494BF4` and
  `0x004A27C0`, and of the functions `fn_0040D72F`, `fn_00421A2D` and
  `fn_00449C8E`, is inferred from where they are called; none has been read.

## How to reproduce

In `0x0046BA84`, find the 16-byte read into `0x00498980` followed by four
calls to `0x00449E26`, and the two switches on the type field: one after the
read from connection 0, one inside the loop over twelve connections.
