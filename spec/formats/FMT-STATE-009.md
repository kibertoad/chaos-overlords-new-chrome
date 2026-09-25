---
id: FMT-STATE-009
title: Input event record
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: []
byte_order: little
size: 16
text: false
definition: fmt_state_009.ksy
evidence: [FND-UI-020, FND-UI-021]
conflicting: []
split_with: []
related: []
---

## Layout

The window procedure writes one record, `input_event`, at `0x00498360` while it
handles a message, and the event step copies it to the loop that asked for input
(RULE-UI-014) [FND-UI-020]. The step clears it before it waits.

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 4 | `INT32LE` | `type` | The kind of event, 0 when none arrived; see below | supported | FND-UI-020 |
| `0x04` | 4 | `INT32LE` | `a` | For a command, bits 8 to 15 of the command ID (the menu group); for a key, the character; for a button, paint or close event, the surface slot of the window, always 0 | supported | FND-UI-020 |
| `0x08` | 4 | `INT32LE` | `b` | For a command, bits 0 to 7 of the command ID (the item); for a key, the Windows virtual key; for a button, the client x | supported | FND-UI-020 |
| `0x0C` | 4 | `INT32LE` | `c` | For a button, the client y; otherwise unused | supported | FND-UI-020 |
| `0x10` | | | | Total size 16 | | |

## Enumerations and flags

### type

| Value | Name | Meaning | Status | Evidence |
|---|---|---|---|---|
| 0 | `EVENT_NONE` | No event within the wait | supported | FND-UI-020 |
| 1 | `EVENT_COMMAND` | A menu command or accelerator key | supported | FND-UI-020, FND-UI-021 |
| 2 | `EVENT_KEY` | A key pressed | supported | FND-UI-020 |
| 3 | `EVENT_LEFT_DOWN` | Left button pressed, or the second press of a double click that counts as a single press | supported | FND-UI-020 |
| 4 | `EVENT_LEFT_UP` | Left button released | supported | FND-UI-020 |
| 5 | `EVENT_LEFT_DOUBLE` | Left button double click, on every other double click | supported | FND-UI-020 |
| 6 | `EVENT_CLOSE` | The window's close command; the event step turns it into File, Exit | supported | FND-UI-020 |
| 7 | `EVENT_PAINT` | The window needs repainting | supported | FND-UI-020 |
| 8 | `EVENT_DEACTIVATE` | The program lost the focus | supported | FND-UI-020 |
| 9 | `EVENT_ACTIVATE` | The program got the focus back | supported | FND-UI-020 |
| 16 | `EVENT_DESTROY` | The window is destroyed; the event step turns it into File, Exit | supported | FND-UI-020 |
| 17 | `EVENT_RIGHT_DOWN` | Right button pressed, or the second press of a double click that counts as a single press | supported | FND-UI-020 |
| 18 | `EVENT_RIGHT_UP` | Right button released | supported | FND-UI-020 |
| 19 | `EVENT_RIGHT_DOUBLE` | Right button double click, on every other double click | supported | FND-UI-020 |

The command groups in `a` are `0x81` File, `0x84` Options, `0x85` Comm and
`0x80` Help, with 6 and 7 for the Music and Sound Effects levels; the items in
`b` are those of SCR-UI-009 [FND-UI-021].

## Differences between builds

None known.

## Coverage

A memory structure: nothing has been decoded against a dump of the running
original.

## Open questions

None known.
