---
id: FND-AUDIO-012
title: An unread Comlink message sounds slot 6 on arrival and at planning entry, and repeats it every 24 timer ticks until read
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
    address: 0x00462579
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FD80
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045E04D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048781C..0x0048781D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487808..0x0048780C
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The Comlink recorder `fn_0045D2F0`, when it appends a message for the active
  player, sets the pending byte at `0x0048781C`, plays slot 6 through the
  wrapper at once, and sets the repeat counter at `0x00487808` to 0.
- The planning entry path in `fn_0046FD80` recomputes the pending byte by scanning
  the active player's 16 Comlink records for one that is occupied and not read.
  When it finds one it plays slot 6 once and sets the repeat counter to 0. The
  city entry path in the event pump `fn_00462579` also plays slot 6 when the
  pending byte is set.
- On each tick of timer slot 0 (FND-UI-001) the event pump advances an
  eight-step animation counter. Every eighth tick it advances the repeat counter
  modulo 3, and when the counter wraps to 0 it plays slot 6 if the pending byte
  is still set.
- The message-view helper `fn_0045E04D` marks the message on screen read and
  scans all 16 read bytes again, so the pending byte clears once the last unread
  message has been viewed, not when the Comlink panel opens.

## Interpretation

`SND00205`, in slot 6, is the Comlink alert. It sounds when a message for the
player at the screen arrives, when a player with unread mail starts planning,
and then again every 24 ticks of timer slot 0 (four seconds at the nominal 6
Hz) until every message has been read.

## Alternatives

- Whether the eight-step animation counter and the repeat counter advance only
  on the city and sector screens or on every screen the pump serves has not
  been recorded.
- The repeat counter's type is taken to be at least a byte; its width has not
  been recorded.

## How to reproduce

Find the writes of `0x0048781C` in `0x0045D2F0`, `0x0046FD80` and `0x0045E04D`,
and the writes of `0x00487808` next to them. The repeat branch in `0x00462579`
compares the counter after a modulo-3 step and pushes slot 6 to the wrapper at
`0x00464290`.
