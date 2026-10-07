---
id: EXP-COMLINK-002
title: Are Comlink Send and View both refused when the only human has no one to write to and no messages?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs, Detailed Combat and Slide Panels switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-COMLINK-002.json
---

## Question

RULE-COMLINK-002 has the Send half of the Comlink control refuse to open when
no other human player can take a message, before it prepares a draft. Does a
match with one human refuse it, and View with it?

## Setup

As EXP-COMLINK-001, with one human in slot 0 and computer players in slots 1
to 5 (`--humans 0`).

## Procedure

As EXP-COMLINK-001, with the script the fixture lists: wait for the human's
planning, keep the Comlink state, press the Send part and then the View part of
the console's Comlink control, and keep the state again.

## Observations

Both presses called their handler, which played slot 4 and returned without
opening its panel. The Send panel's selection bytes and its draft buffer stayed
all 0, as the executable's data holds them, so the handler returned before it
stamped a draft. The count, cursor and records of the human stayed empty, and
`comlink_pending` stayed 0.

## Results

A test of the rebuild replays the steps as in EXP-COMLINK-001: the rebuild finds
no recipient for the human, so Send does not open, and an empty inbox, so View
does not open. The original never played slot 6, and the human's planning entry,
with no unread message, does not sound the alert in the rebuild either.

## Conclusion

The run agrees with RULE-COMLINK-002 and RULE-COMLINK-004: with one human,
Send and View are both refused with the rejected-input sound, and the refused
Send leaves its draft untouched. It agrees with RULE-AUDIO-007 that a planning
entry with no unread message plays no alert.
