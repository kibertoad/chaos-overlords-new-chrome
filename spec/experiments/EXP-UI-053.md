---
id: EXP-UI-053
title: Which name does the setup name editor give for each number-pad key and each main-keyboard key, with Shift held and not?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200 with the United States keyboard layout (04090409) on the game's thread, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, staged as docs/validation/experiments.md describes, windowed, the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-053.json
---

## Question

A setup name is typed into the edit control of dialog 139 (FND-UI-022,
FND-UI-064). What name does slot 0 hold after OK when a lower-case letter,
an underscore, or a capital followed by one number-pad, main-keyboard or
navigation key, with Shift held or not, is typed?

## Setup

The local setup screen of scenario 0, with card 0 selected.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 0 --seed 3 --end-turns 0
--setup-steps NAMES`, where NAMES is the step list of the fixture's `inputs`:
98 `name:` steps.

Each step presses the name band of card 0, which opens dialog 139, and posts
to its edit control 1007: `WM_CHAR` for `a` or `_` alone, or `WM_CHAR` for
`A` followed by `WM_KEYDOWN` and `WM_KEYUP` of one virtual key. The keys are
those of EXP-UI-052: `0x60` to `0x6F`, the eleven navigation keys, the main
digits and the punctuation keys `0xBA` to `0xC0` and `0xDB` to `0xDE`, each
once without Shift and once with it. For Shift the probe attaches its input
to the game's thread and sets `VK_SHIFT` and `VK_LSHIFT` in the shared
keyboard state for the time of the key, so the dialog's own message loop
translates the key with Shift held. The step then sends OK and reads the
12-byte name record of slot 0 at `0x004A2588`.

## Observations

`a` gave the name `A`, and `_` a single space. After the `A`:

| Keys | Without Shift | With Shift |
|---|---|---|
| `VK_NUMPAD0` to `VK_NUMPAD9` | `0` to `9` | nothing |
| `VK_MULTIPLY`, `VK_ADD`, `VK_SUBTRACT`, `VK_DECIMAL`, `VK_DIVIDE` | `*`, `+`, `-`, `.`, `/` | the same |
| `VK_SEPARATOR` | nothing | nothing |
| End, Down, Page Down, Left, Clear, Right, Home, Up, Page Up, Delete | nothing | nothing |
| Insert | nothing | not compared, see below |
| `0` to `9` | `0` to `9` | `)!@#$%` then a space, then `&*(` |
| `;` `=` `,` `-` `.` `/` | the same | `:` `+` `<`, a space, `>` `?` |
| `` ` `` `[` `\` `]` | a space each | a space each |
| `'` | `'` | `"` |

Shift with Insert pasted the text the clipboard of the machine held into the
name. That text belongs to the machine, not to the game, and is not used.

## Results

A test of the rebuild types each step's keys into the rebuild's name editor and compares the name with the record,
for every step but Shift with Insert: the rebuild's editor has no clipboard. A
first comparison found the rebuild typing Shift with a number-pad digit as the
digit, nothing for the number-pad operators, and nothing for a key whose
character lies outside space to `Z`.

## Conclusion

The run supports FND-UI-064 and the copy of FND-UI-022: the edit control
translates the key with Windows' own layout, so Shift with a number-pad digit
types nothing and Shift with minus gives an underscore; it upper-cases a
lower-case letter; and the copy into the name record turns every character
outside space to `Z` (the underscore, `^`, `` ` ``, `~` and the bracket
characters here) into a space. A letter key pressed through `WM_KEYDOWN`,
Space, and a pasted lower-case letter were not tried.
