---
id: FND-AUDIO-016
title: The CD fade uses zero-based wait deadlines and dispatches window messages without handling game events
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458D54..0x00458E2E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045C2CD..0x0045C33A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462579..0x004637B7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004642BD..0x00464384
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004652A0..0x004653AD
tool: Ghidra 12.1.3
environment: null
---

## Observation

The executable matches BLD-GOG-EN-1.1. FND-AUDIO-007 records the helper calls;
this reads their wait-counter origin and distinguishes window dispatch from
game-event handling using FND-UI-020.

The volume-capable, playing path of `fn_00458D54` captures the volume and the
start time. Its counter starts at 0 and runs while below 32. Each iteration
subtracts the captured volume divided by 32, writes that volume, waits until
`start_time + counter * 17`, then calls `fn_0045C2CD`. Consequently the first
wait deadline is 0 ms and the last is 31 * 17 = 527 ms, not 544 ms. After the
last message dispatch it writes zero, calls the conditional stop helper and
restores the captured volume. Message handling and execution can extend the
actual completion time beyond the last deadline.

For captured high-byte volume 125, the first write is 122. After the first,
zero-deadline dispatch the second write is 119 and waits until 17 ms. In the
ideal timeline, after each dispatch the next attenuation is already written:
119 while waiting for 17 ms, 116 while waiting for 34 ms, and 29 while waiting
for 527 ms. The zero/stop/restore sequence follows the final dispatch.

`fn_0045C2CD` takes at most one window message and translates and dispatches
it. It does not call `fn_00462579`, deliver an event to a screen, or apply music
levels. The window procedure writes the event record, but the next full
`fn_0045C180` event step clears that record before waiting for a fresh event
(FND-UI-020). A command dispatched during the fade can therefore be consumed
without its game action running. The pause, resume and level application are
in `fn_00462579`, not the window procedure, so they do not run inside the fade.

The selector `fn_004642BD` checks music-enabled after the fade returns and may
play the selected program. The muting branch of `fn_004652A0` clears that flag,
calls the fade and then updates the menu checks; it does not call the selector.
A menu command cannot re-enable music through the fade's window-only dispatch.

## Interpretation

A fade blocks game input and game-event work while still dispatching window
messages. It has 32 attenuation writes but only 31 nonzero, 17 ms deadline
increments. Its initial two writes bracket the zero-deadline message dispatch.

The 32-step wording of FND-AUDIO-007 does not imply 32 waits of 17 ms. Nor does
its message-dispatch wording imply reentrant option or focus handlers. Those
handlers belong to the full event step, which is not entered during the fade.
A focus message consumed during the fade can leave the game's inactive flag
unchanged, even though the operating system's actual focus has changed.

## Alternatives

This is a static reading, not a timed run on a CD device. The ideal volume
intervals exclude the time taken by individual dispatches and auxiliary-device
calls. Messages still queued after the fade may be handled normally afterward;
this does not claim that every input arriving during the fade is consumed.

## How to reproduce

Inspect the initial counter, loop bound, subtraction, zero-based wait target
and dispatch call in `fn_00458D54`. Follow `fn_0045C2CD` and compare it with the
full event step and its option/focus branches. Check the two fade callers for
what they do after the helper returns. Consult FND-UI-020 for event-record
clearing and window-procedure routing.