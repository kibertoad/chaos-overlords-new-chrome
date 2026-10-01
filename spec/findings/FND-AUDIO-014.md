---
id: FND-AUDIO-014
title: The shipped GOG CD wrapper rejects MCI_PAUSE and treats MCI_PLAY without MCI_FROM as a successful no-op
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: winmm.dll
    address: 0x6AB41360..0x6AB41541
tool: Capstone 5.0.7 and pefile 2024.8.26
environment: null
---

## Observation

The installed `winmm.dll` is a 39424-byte PE32 image, with preferred base
`0x6AB40000` and xxh3 `80f620dfdda54ba6ab48431bc6623e8d`. Its export
`mciSendCommandA` has RVA `0x1360`, placing its dispatcher at `0x6AB41360`.
This is the replacement DLL listed with BLD-GOG-EN-1.1, not the operating
system's multimedia library and not an executable function in FND-EXE-004.

For the virtual CD device ID `0xBEEF` (or device ID 0), the dispatcher compares
commands with `0x80D` (set), `0x804` (close), `0x806` (play), `0x808` (stop),
and `0x814` (status). There is no comparison with `0x809` (pause). An unmatched
command reaches `0x6AB41477`, which returns `0x105` without changing playback
state. Commands for other nonzero device IDs can be forwarded to the system
library; that path is outside this observation.

The play branch at `0x6AB414B0` tests bit 2 (`MCI_FROM`, value 4) of the flags.
When it is clear, the branch goes directly to `0x6AB41402`, which returns 0.
It does not call the playback loader, create a worker, or write the playback
flag. When the bit is set, the separate path reads the start field and starts
playback; its remaining range semantics have not been established here.

## Interpretation

FND-AUDIO-007 records the executable sending pause with command `0x809`, and
resume with command `0x806` and `MCI_NOTIFY` alone (value 1). On the shipped
GOG virtual-device path, the former is unsupported and the latter does
nothing successfully. The executable's focus handlers therefore request a
pause and resume without the wrapper implementing either operation. The
inactive-window flag still suppresses the executable's restart poll.

## Alternatives

This is static evidence for the dispatcher, not a recording of audible
playback or a complete account of the wrapper's decoder and worker. No
retail system CD device or another replacement DLL was inspected.

## How to reproduce

Check the DLL against its build-entry size and xxh3. Read its PE export table
with pefile, and disassemble from RVA `0x1360` with Capstone in x86 32-bit mode,
using the image's preferred base. Follow the comparisons for virtual device
ID `0xBEEF`, the unmatched-command return, and the flag test in the play
branch. Compare the calls the executable makes in FND-AUDIO-007.

A separate x86 helper attempted to load the trusted DLL and issue these
commands with its internal gain muted. The helper stalled in LoadLibraryEx
before producing any MCI results and was cancelled. That attempt provides no
dynamic confirmation and is not used as evidence for this finding.
