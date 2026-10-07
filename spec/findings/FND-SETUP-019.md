---
id: FND-SETUP-019
title: The setup screen breaks the scenario description at the last space at or before the 37th character and starts the next line after it
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00438C3C..0x00438D9B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048772C
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the description routine `0x00438B35` (FND-SETUP-013), after the title is
drawn, `0x00438C50` loads string `scenario + 95` as a length-prefixed string
`s`, with its characters at `s[1]` to `s[len]`. Then:

- `0x00438C61` sets `total = len + 1`, `0x00438C67` sets `start = 1` and
  `0x00438C6E` clears a `last` flag. A loop runs `line` from 0 to 4.
- Each pass sets `end = start + 0x24`; when `total < end` it sets
  `end = total` and `last = 1`.
- `0x00438CD6` copies the 36 bytes at `0x0048772C` into a line buffer and
  `0x00438CDE` ends it after 36 characters. The executable holds 36 spaces
  there.
- While `last` is 0, `end > start` and `s[end]` is not a space (`0x20`),
  `0x00438D1C` decrements `end`.
- `0x00438D43` copies `end - start` characters from `s[start]` over the start
  of the line buffer, and `0x00438D84` draws it with `fn_00413FD5` at
  `(0x54, 0x34 + 8 * line)`.
- `0x00438D93` sets `start = end + 1`.

## Interpretation

Each line is the text from `start` up to, and not including, the last space at
or before `start + 36`, padded to 36 characters with spaces. The next line
starts one character after that space, so where the break falls on two spaces
the line keeps the first as its last character and ends at the second. The
line that holds the end of the text is not broken. The passes after it draw 36
spaces, so all five rows are drawn whatever the text's length.

## Alternatives

None known.

## How to reproduce

In `0x00438B35`, find the second call of `0x00466673`, with `scenario + 0x5F`,
and follow the loop after it to the `jmp` at `0x00438D96`.
