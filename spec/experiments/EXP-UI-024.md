---
id: EXP-UI-024
title: How long do the original's presentation waits last against its six-per-second clock?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-024.json
---

## Question

Does each presentation wait end at the first tick of the six-per-second clock
after it starts, and how long does a city-cell flash last?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 0 --mentality 1 --humans 0 --seed 16
--end-turns 1 --waits --hire-steps exit,exit,drag:0:12,drag:1:12`.

From the dump on, the probe records each call of the timer callback
`fn_004327C0` for slot 0, the presentation clock (FND-TIMER-002), and each call
of the wait `fn_00464CD9` with its argument, the address of the call and the
times it starts and returns, all on one clock. The hire steps drop the first and
then the second offer on sector 12, which makes the Hire handler flash that
city cell through `fn_0041ACE6` (FND-UI-017).

## Observations

The clock ticked 31 times, 165.8 ms apart on average; single periods ran from
160 to 175 ms under the debugger. The wait was called six times, each with 1: three
times for each drop, from the calls at `0x0041B314`, `0x0041B3AE` and
`0x0041B448` in turn, each starting as the one before returned. Every wait
returned 6 to 16 ms after the first tick at or after its start, and no other
tick came between. The two flashes lasted 447 and 426 ms, their first waits
having started 61 and 90 ms after a tick.

## Results

A test of the rebuild checks these observations and starts the rebuild's
city-cell flash as far past a tick of its 166 ms clock as each recorded flash
started. The rebuild's flashes end 437 and 408 ms after they start, within the
debugger's delay of the original's. The hire-step replay compares the hire
orders after each drop.

## Conclusion

The run supports RULE-TIMER-004 for the one-tick wait and the city-cell flash:
a wait lasts until the next tick, from almost 0 to 166 ms, and a flash makes
three of them back to back. The run does not reach the site and sector-cell
flashes or a pressed key face.
