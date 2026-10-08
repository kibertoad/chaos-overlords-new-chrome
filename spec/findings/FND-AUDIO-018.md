---
id: FND-AUDIO-018
title: An unread Comlink message sounds slot 6 on arrival and once at planning entry, and repeats it every 24 timer ticks until read
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045D2F0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045D488
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462579
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046331D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00463713..0x00463775
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FD80
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00470340..0x0047037F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045E04D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048781C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487804..0x00487809
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004.

- The Comlink recorder `fn_0045D2F0`, when it appends a message for the active
  player, sets the pending byte at `0x0048781C`, plays slot 6 through the
  wrapper `fn_00464290` at `0x0045D488`, and sets the repeat counter at
  `0x00487808` to 0.
- The planning entry path in `fn_0046FD80` recomputes the pending byte by
  scanning the active player's 16 Comlink records for one that is occupied and
  not read. After it has shown the Last Turn Events panel (`fn_0044F2FC`, called
  at `0x00470356` when its local flag is set), it reads the pending byte at
  `0x00470360`; when the byte is set it plays slot 6 once at `0x0047036F` and
  sets the repeat counter to 0 at `0x00470377`.
- In the event pump `fn_00462579` the pending byte is read twice. The read at
  `0x0046331D` chooses how the Comlink light is drawn and plays nothing. The
  read at `0x00463761` belongs to the repeat: on each tick of timer slot 0
  (FND-UI-001) the pump increments the dword at `0x00487804`, and when it
  reaches 8 it sets it to 0 and increments the 16-bit repeat counter at
  `0x00487808`, setting that to 0 when it reaches 3. When the repeat counter is
  then 0 and the pending byte is set, it plays slot 6 at `0x00463770`.
- These three calls at `0x0045D488`, `0x0047036F` and `0x00463770` are the only
  places that push slot 6 to `fn_00464290`. The repeat counter is written only
  at `0x00463730`, `0x00463747`, `0x00470377` and `0x0045D490`, always as a
  16-bit word.
- The message-view helper `fn_0045E04D` marks the message on screen read and
  scans all 16 read bytes again, so the pending byte clears once the last unread
  message has been viewed, not when the Comlink panel opens.

## Interpretation

`SND00205`, in slot 6, is the Comlink alert. It sounds when a message for the
player at the screen arrives, once when a player with unread mail starts
planning, after the Last Turn Events panel closes, and then every 24 ticks of
timer slot 0 (four seconds at the nominal 6 Hz) until every message has been
read. The planning entry resets the repeat counter but not the eight-step counter,
so the first repeat comes 17 to 24 ticks after the entry, depending on where
the eight-step counter stood.

## Alternatives

- Whether the eight-step counter and the repeat counter advance only on the
  city and sector screens or on every screen the pump serves has not been
  recorded.
- The writes of the pending byte at `0x0046E8AD`, `0x00470A12`, `0x00471FC4`
  and `0x00472693` were not read further; none of them is next to a play of
  slot 6.

## How to reproduce

List the references of `0x0048781C` and `0x00487808`. Read the instructions
from `0x00463713` to `0x00463775` in `0x00462579` and from `0x00470340` to
`0x0047037F` in `0x0046FD80`. List the calls of `0x00464290` and the constant
each pushes.
