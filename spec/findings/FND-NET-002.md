---
id: FND-NET-002
title: The network progress renderer draws one progress bar per remote player and two lines of status text chosen by a code
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046D77B..0x0046DC0F
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_0046D77B` occupies `0x0046D77B..0x0046DC0F` (1,142 bytes, FND-EXE-004). It
has thirteen call sites: four in `fn_0046D22F` and nine in `fn_0046A7CB`,
the connected-session branches of FND-SETUP-007. It takes one status code.

- For each slot 0 to 5 whose `controller` (`0x004AB638`) is 3, and whose
  progress value at `0x004988D8 + 4 * slot` is positive, it copies that many
  pixels of the three-row strip at `(354,0)` of surface 6 to the screen at
  `(298, 96 + 4 * slot)`. For every other slot it copies the 100-by-3 empty bar
  at `(360,6)` of surface 6 to `(298, 96 + 4 * slot)`.
- When the code is not -1 it fills surface 7 `(256,0)-(394,16)` with black and
  loads two strings from the string table: entries `0x4D + 2 * code` and
  `0x4E + 2 * code` for codes 0 to 4, entries `0x43 + 2 * code` and
  `0x44 + 2 * code` for codes 10 to 13. It draws the first centred on x 325
  (from `325 - 3 * length`) on row 0 and the second on row 8, and copies
  `(256,0)-(394,16)` to the screen at `(278,76)`.
- The switch has no branch for other codes; for them the two text buffers are
  drawn without being filled.

Call sites pass -1 six times, 12 and 13 once each, and a register value five
times.

## Interpretation

During a network session each remote human player has a 100-pixel progress
bar, one row of four pixels per slot, and the panel shows a two-line status
message: codes 0 to 4 use string-table entries 77 to 86, codes 10 to 13 use
entries 87 to 94. A slot that is not a remote human always shows an empty bar.

## Alternatives

- Which codes the five register-passed call sites can pass has not been traced,
  so whether the unfilled-buffer path is reachable is not known.

## How to reproduce

In `0x0046D77B`, find the loop over six slots testing `0x004AB638` against 3
and `0x004988D8` against 0, the copies to x `0x12A` on row `0x60 + 4 * slot`,
and the switch that adds `0x4D` or `0x43` to twice the code.
