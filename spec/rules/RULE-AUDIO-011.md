---
id: RULE-AUDIO-011
title: The shipped GOG CD wrapper rejects pause and ignores a play request without MCI_FROM
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-014, FND-AUDIO-007]
conflicting: []
split_with: []
related: [RULE-AUDIO-001, RULE-AUDIO-002]
---

## Summary

The executable's focus pause and resume requests do not pause or resume the
virtual CD device supplied with this GOG build. A pause request is unsupported;
a play request without a start position is a successful no-op.

## When it runs

When the executable sends the focus requests of RULE-AUDIO-002 to the virtual
CD device. This entry describes the shipped wrapper's handling, separately
from the executable's command requests.

## Parameters

- `command`: the multimedia command number.
- `flags`: the multimedia command flags.

## Inputs

The virtual CD device, whose ID is `0xBEEF`, and its playback state.

## Procedure

```text
# The virtual-device dispatcher also accepts device ID 0.
if command == 0x809:  # MCI_PAUSE
    return 0x105
if command == 0x806 and (flags & 4) == 0:  # MCI_PLAY without MCI_FROM
    return 0
```

## Outputs

Neither path changes playback state. The return value is the multimedia
command result, which the executable's focus path does not use to retry.

## Edge cases

The executable still sets its inactive-window flag on focus loss and clears
it on focus regain. Its restart poll does not run while inactive. These
requests therefore do not imply that the poll stays enabled when the wrapper
does not pause the playback.

## What the sources say

No outside source is used for the installed wrapper's behavior.

## Differences between builds

A system CD device or another replacement DLL can handle these commands
otherwise. Only the DLL inventoried with BLD-GOG-EN-1.1 was inspected.

## Open questions

- The original executable's live binding to this installed DLL has not been
  captured in this investigation.
- Audible playback across focus changes has not been recorded dynamically.
- Other play flags, endpoint handling and the decoder/worker are not specified
  by this entry.
