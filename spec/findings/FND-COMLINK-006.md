---
id: FND-COMLINK-006
title: Comlink messages are stored only on the recipient's computer, cleared when the match loop starts, and read messages at the front of an inbox are dropped when its player finishes planning
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045D2F0..0x0045D619
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046C6B2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046CD6E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045F10B..0x0045F14F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00460560
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00460391..0x004604A6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E7EE..0x0046E88F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498120..0x004981C5
tool: Ghidra 12.1.3
environment: null
---

## Observation

The recorder `fn_0045D2F0` (range in FND-EXE-004) takes a source and a
recipient slot.

- With the source -1 it copies the 166-byte global message buffer at
  `0x00498120` into a local record. Otherwise the source is a network
  connection number: it reads 166 bytes from that connection with
  `fn_00423A23` and swaps the two bytes of the word at record `+2`.
- In a game with `network_game` (`0x00487B58`) set and `controller` 3 for the
  recipient, it sends a packet of type 10 with the recipient in its header to
  the recipient's connection (`0x004A08D0 + recipient * 4`), then the 166 bytes
  with the word at `+2` byte-swapped for the send and swapped back after it.
  With the flag at `0x00482178` set and a `controller` other than 0 for the
  recipient, it sends the same packet to connection 0. Either way it then
  stores nothing.
- Otherwise it stores the message. When the recipient is `active_player`
  (`0x004ABC84`) it first sets `comlink_pending` (`0x0048781C`) to 1, plays
  general-effect slot 6 and stores 0 in `comlink_alert_repeat` (`0x00487808`),
  in that order. Then it appends or compacts as FND-COMLINK-001 describes, and
  last sets `g_004877D0` to 1 when `g_004877CC` is set and the recipient is
  `active_player`.
- The network dispatcher `fn_0046BA84` calls the recorder for packet type 10 at
  `0x0046C6B2` with source 0 and at `0x0046CD6E` with the connection the
  packet came from; both pass the header's recipient (`0x00498984`). The
  "message ID 0 or the ID received" of FND-COMLINK-001 is this connection
  number.

The Send handler `fn_0045EAB1` prepares the global buffer each time the panel
opens (`0x0045F10B..0x0045F14F`): byte `+0` = 1, byte `+1` = 0, the word at
`+2` = the low 16 bits of `elapsed_turns` (`0x0049CA68`), byte `+4` =
`active_player`, and each of the four 40-byte rows from `+5` copied from 40
spaces at `0x004877D4`. No instruction addresses `+0xA5` (`0x004981C5`), which
holds 0 in the executable's data. When Send completes, the handler calls
`fn_00460560`, which returns 1 when any of the 160 text bytes differs from a
space, and calls the recorder only in that case.

The outer match function `fn_0046E766`, on entry and before it tests its
argument, sets both `comlink_cursor` and `comlink_count` of every player to 0
and gives each of the 96 records `occupied` 0 and `read` 1
(`0x0046E7EE..0x0046E88F`). In the same place it stores 0 in the 132 Search
bytes at `0x004A24E8`.

The function `fn_00460391`, given a player, repeats while that player's record
0 has both `occupied` and `read` set: it subtracts 1 from `comlink_count`,
copies records 1 to 15 down one place and gives record 15 `occupied` 0 and
`read` 1. It then sets `comlink_cursor` to 0. Its two callers are
`fn_0046FD80` at `0x004708FF` and `fn_00471F06` at `0x00472684`, each right
after the player's planning loop ends and before the next player's turn.

## Interpretation

A message leaves the sender's computer once per remote recipient and is stored
only where its recipient plays; a host relays a message from one client to
another by calling the recorder again. The alert sounds before the message is
stored, and only for the player at this computer.

A message's turn is the number of turns completed when it was written. The
text is 160 printable bytes padded with spaces, with no terminator, and the
last byte of a record is always 0. A message of spaces only is never sent.

Comlink keeps only unread messages across turns: when a player finishes
planning, the read messages ahead of the oldest unread one are removed, and
View starts at the first message next time. A read message behind an unread one
stays. Every empty record counts as read, which is why the unread scan of
FND-COMLINK-004 can ignore `occupied`.

## Alternatives

- The flag at `0x00482178` is read as `local_game` in the glossary; its use
  here, sending to connection 0 for every recipient not at this computer, fits
  a network client better. Neither reading is settled.
- Whether a loaded game enters `fn_0046E766` again, and so starts with empty
  inboxes, was not traced.

## How to reproduce

Open `fn_0045D2F0`: read the branch on -1 with the copy from `0x00498120`, the
two send branches with `0x004AB638`, the store of 1 into `0x0048781C` before
the count test at `0x004981E0`. In `fn_0045EAB1`, read the stores into
`0x00498120`..`0x00498124` and the four copies from `0x004877D4`, and the call
of `fn_00460560` before the recorder loop. Open `fn_00460391` and list its
callers. In `fn_0046E766`, read the loops at `0x0046E7EE`.
