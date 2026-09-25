---
id: FND-UI-003
title: Game Information uses the 320-pixel alternate panel, lists all six player slots and picks its texts from string tables
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045519D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040F63D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A2589..0x004A25D1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487768..0x0048776A
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The Game Information handler `fn_0045519D` loads `PX05021` into the alternate
  backing area. Its close transition copies backing corners `(344,144)-(664,353)`
  to screen corners `(128,124)-(448,333)`. Its pointer test uses the 128-pixel
  left edge and takes the bottom face at panel-local corners `(33,169)-(82,191)`,
  screen `(161,293)-(210,315)`.
- Text is written at backing x 444 for the three header values, x 456 for the
  player names, and right-aligned to backing x 624 for each player's
  intelligence: screen x 228, x 240 and right edge 408. The headers are at
  screen y 151, 169 and 187, and the six player rows at y `214 + 9*n`.
- The roster pass always runs `n = 0..5`. It reads each 12-byte name record from
  `0x004A2589 + 12*n` and then draws a status label for the same slot. It tests
  the eliminated-state byte first and draws the eliminated label when it is set;
  otherwise it picks the computer or human label from the controller byte.
- The first header depends on the scenario. For scenarios 0 to 3 (Greed, Power,
  Acceptance and Dominance) the renderer appends the two-byte literal at
  `0x00487768`, an opening parenthesis after a space, then the string resource
  from `0x36` to `0x39` chosen by the stored duration of 26, 52, 104 or 208
  turns, then a closing parenthesis. Scenarios 4 to 9 show the scenario string
  alone.
- The second header picks string `0x2E` to `0x31` by the AI Mentality byte, and
  the third picks string `0x32` to `0x35` by the planning time limit byte. Read
  from the executable's string table with `LoadStringW`, these are the four
  Mentality names and the four limit choices (none, 30 seconds, 2 minutes, 5
  minutes). The player status labels follow at `0x3A` (human), `0x3B`
  (computer) and `0x3C` (eliminated).
- The text-entry helper `fn_0040F63D` stores at most ten characters, keeps only
  characters from `0x20` to `0x5A`, and ends the 12-byte record with a NUL.

## Interpretation

`PX05021` is a 320-by-209 panel drawn through the alternate crop, not the
344-pixel shared panel. The first header shows the scenario, followed by its
duration in parentheses for the four timed scenarios. Each of the six player
slots gets a row whether or not a person configured it, and an eliminated
player is labelled eliminated whatever its controller.

## Alternatives

- Which string resources hold the scenario names has not been recorded.
- Which controller values count as human for the row label (0 and 3, or only
  0) has not been read.

## How to reproduce

Find the load of resource 5021 in `0x0045519D` and the constants 444, 456 and
624 passed to the text routine. The `LoadStringA` calls take `0x2E`, `0x32` and
`0x36` plus an index. Extract `Chaos Overlords.exe#STRING` entries `0x2E` to
`0x3C` to read the labels.
