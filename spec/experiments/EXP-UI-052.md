---
id: EXP-UI-052
title: Which character does the window procedure store for each number-pad key and each main-keyboard key, with Shift held and not?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200 with the United States keyboard layout (04090409) on the game's thread, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, staged as docs/validation/experiments.md describes, windowed, the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-052.json
---

## Question

For each number-pad key, each digit and punctuation key of the main keyboard,
and each navigation key the number pad gives with Num Lock off, which
character does the `WM_KEYDOWN` branch of the window procedure (FND-UI-020,
FND-UI-064) store in the key event, with Shift held and with it released?

## Setup

A new local match with humans in slots 0 and 1, stopped at the first
planning entry.

## Procedure

`Rechaos.OriginalProbe new-game --humans 0,1 --seed 7 --end-turns 0
--white-key --order-steps strip:320:265:0,strip:576:166:0,keys:TOKENS`, where
TOKENS is the key list of the fixture's `inputs`.

After the dump the probe makes two presses, then posts `WM_KEYDOWN` and
`WM_KEYUP` to the game window for each virtual key in turn: `VK_NUMPAD0` to
`VK_DIVIDE` (`0x60` to `0x6F`) and Enter, the same with Shift, Enter again,
the main digits and the eleven punctuation keys `0xBA` to `0xC0` and `0xDB` to
`0xDE`, the same with Shift, then the eleven navigation keys Insert, End,
Down, Page Down, Left, Clear, Right, Home, Up, Page Up and Delete, without and
with Shift. Num Lock is chosen this way: Windows gives `VK_NUMPAD0` to
`VK_NUMPAD9` with Num Lock on and the navigation keys with it off, before the
key is posted.

A posted message cannot hold Shift, so a breakpoint at `0x0045CA62`, after the
`GetAsyncKeyState(VK_SHIFT)` call, sets the call's result to `0xFFFF8000` for
the keys with Shift and to 0 for the others. A breakpoint at `0x0045CC35`, the
jump every path of the branch takes once the event is stored, records the
event's type, character and key at `0x00498360`.

## Observations

Every one of the 98 keys stored one event of type 2 whose key is the virtual
key posted. The characters:

| Keys | Without Shift | With Shift |
|---|---|---|
| `VK_NUMPAD0` to `VK_NUMPAD9` | `0` to `9` | `)!@#$%^&*(` |
| `VK_MULTIPLY`, `VK_ADD`, `VK_SUBTRACT` | `*`, `+`, `-` | the same |
| `VK_SEPARATOR` | 0 | 0 |
| `VK_DECIMAL`, `VK_DIVIDE` | `.`, `/` | `>`, `?` |
| Enter | `0x0D` | not posted |
| `0` to `9` | `0` to `9` | `)!@#$%^&*(` |
| `;` `=` `,` `-` `.` `/` | the same | `:` `+` `<` `-` `>` `?` |
| `` ` `` `[` `\` `]` | the same | the same |
| `'` | `'` | `"` |
| The eleven navigation keys | 0 | 0 |

## Results

A test of the rebuild compares the rebuild's character for each key and
Shift state with the stored one, and expects no character where the original
stored 0 or a control character. A
first comparison found the rebuild typing the number-pad digit with Shift held
and nothing for the five number-pad operators.

## Conclusion

The run supports the key translation of RULE-UI-014 on a United States
layout: `MapVirtualKeyA(key, 2)` gives each number-pad digit and operator its
character, and the shift switch, which tests the character, also shifts the
number-pad digits and the number-pad `.` and `/`. The navigation keys and the
separator give 0, which no text entry takes. Whether a key on the physical
number pad ever reaches the window as `VK_NUMPAD0` to `VK_NUMPAD9` with
`GetAsyncKeyState` reporting Shift held depends on the keyboard driver and was
not recorded; the run fixes what the procedure does with each key it is
given.
