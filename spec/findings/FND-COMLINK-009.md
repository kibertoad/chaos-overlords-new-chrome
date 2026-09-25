---
id: FND-COMLINK-009
title: A message recorded for the active player while the View panel is open makes the panel redraw the shown message with the new count
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045D5ED..0x0045D609
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045D74C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045DF38..0x0045E015
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045E05C..0x0045E0E3
tool: Ghidra 12.1.3
environment: null
---

## Observation

- `0x004877CC` has three references. The View handler `fn_0045D61A` stores 1
  at `0x0045D74C`, after it has drawn the first message and just before its
  event loop, and stores 0 at `0x0045E015`, after the loop ends. The recorder
  `fn_0045D2F0` reads it at `0x0045D5ED`.
- `0x004877D0` has three references. The recorder stores 1 at `0x0045D609`
  when `0x004877CC` is set and the recipient is `0x004ABC84`, after the
  message has been stored. The View handler reads it at `0x0045DF38`, at the
  end of each pass of its event loop, and on a set value stores 0
  (`0x0045DF45`), reads the recipient's count from `0x004981E0 + player * 4`,
  calls `fn_0045E04D(player, count)` and copies surface 7 x 0..344,
  y 144..353 to the screen at (104, 124).
- `fn_0045E04D` marks the message at the player's cursor
  (`0x004981C8 + player * 4`) read, sets `0x0048781C` to 1 when any of the
  player's 16 elements still has the read byte 0 and to 0 otherwise
  (`0x0045E05C..0x0045E0E3`), and draws that message with the cursor + 1 and
  the count passed in (FND-COMLINK-007).
- The View handler's event loop calls the event pump `fn_00462579` at
  `0x0045D755` for each event.
- The recorder has two callers: the Send handler `fn_0045EAB1` (`0x0045F2A6`,
  `0x0045F6CE`) and the network dispatcher `fn_0046BA84` (`0x0046C6B2`,
  `0x0046CD6E`). The pump calls the dispatcher at `0x00462AFF` and
  `0x00462C2E`.

## Interpretation

`0x004877CC` is set while the active player's View panel is open, and
`0x004877D0` is a request from the recorder to that panel to refresh. The
refresh keeps the message on show: the recorder moves the cursor back one
place when it drops the oldest message (FND-COMLINK-006), so the cursor still
points at the same message, except when the cursor is already 0 and the
message on show is the one dropped: then the panel shows the next one and
marks it read. Otherwise only the count changes, and the Comlink light
stays on, because the new message is unread. The new message itself is not
shown until the player steps to it.

While the panel's loop waits, the recorder can run only through the pump and
the network dispatcher, so the refresh happens for a message arriving from
another computer in a network game. The Send panel cannot be open at the same
time.

## Alternatives

- The dispatcher's other two callers (`fn_00456F80`, `fn_004677F0`) are not
  reached from the View loop as far as was read; a path through them would
  refresh the panel the same way.

## How to reproduce

List the references to `0x004877CC` and `0x004877D0`. In `fn_0045D2F0`, read
the test after the store of the message. In `fn_0045D61A`, read the store
before the loop, the test at `0x0045DF38` with the call to `fn_0045E04D` and
the copy from surface 7, and the store after the loop. In `fn_0045E04D`, read
the loop over the read bytes before the drawing starts.
