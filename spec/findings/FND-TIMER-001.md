---
id: FND-TIMER-001
title: A human's planning turn ends by itself after 30 seconds, 2 minutes or 5 minutes, with a shrinking bar and two warning sounds
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E766
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FD80
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041B8BC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041BDD5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041B8FC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462579
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487854..0x00487858
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487898..0x0048789C
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The byte at `0x00487854` (`prefsTimeLimit`, FND-OPTIONS-001) starts at 0. The
  new-match initialization in `fn_0046E766` maps its values 0, 1, 2 and 3 to -1
  (no limit), 30000, 120000 and 300000 milliseconds.
- For a human planning entry, `fn_0046FD80` calls the start helper
  `fn_0041B8BC`, which records `timeGetTime`. Computer planning does not call
  it.
- The expiry helper `fn_0041BDD5` returns true once the elapsed milliseconds
  exceed the limit, and the human input loop then leaves as if Done had been
  accepted. The test comes after the path that opens the idle-gang warning
  (FND-OPTIONS-002), so expiry does not open the warning.
- The drawing helper `fn_0041B8FC` computes
  `elapsed_percent = (elapsed * 100) / limit` and then the visible width
  `60 - (elapsed_percent * 60) / 100`, both divisions truncating. It plays slot 7
  while the remaining time is strictly between 1 and 10 seconds and slot 8
  while it is above 0 and at most 1 second.
- In the input pump `fn_00462579`, the counter at `0x00487898` is decremented
  at `0x00462F07` before it is compared. When it reaches 0 the pump calls the
  drawing helper and sets the counter back to 6.
- The sound wrapper `fn_00464290` passes every call on to the lower helper
  `fn_0045851A`; nothing stops the warning from being requested on every sixth
  pump call.
- `SND00206` (slot 7) lasts about 0.117 seconds and `SND00207` (slot 8) about
  1.189 seconds.

## Interpretation

With a limit chosen at setup, each human planning turn runs against the clock
and ends as if Done were pressed when the limit passes, without the idle-gang
question. Computer players are not timed. The bar shrinks in whole-percent
steps. In the last ten seconds a short tick sounds, and in the last second a
longer sound, each time the pump redraws the bar.

## Alternatives

- The unit of the "1 second" and "10 seconds" bounds (milliseconds compared with
  1000 and 10000, or whole seconds) has not been recorded.
- Which pump calls count as eligible, and so how often per second the bar is
  redrawn, depends on the pump rate, which has not been measured.
- Whether the expiry helper tests the -1 of "no limit" itself or relies on its
  caller has not been recorded.
- Where the chosen limit and the start time are stored has not been recorded.

## How to reproduce

In `0x0046E766`, find the table or compares that produce 30000, 120000 and
300000. In `0x0041B8FC`, find the multiplies by 100 and 60 and the calls to the
wrapper `0x00464290` with 7 and 8. At `0x00462F07`, find the decrement of
`0x00487898` and the store of 6.
