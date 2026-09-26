---
id: RULE-AUDIO-010
title: The startup drive check always passes and the game never looks for its disc
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-PLATFORM-012, FND-AUDIO-007, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-AUDIO-001, RULE-VIDEO-001]
---

## Summary

At startup the game runs a drive check left over from a test for its disc. It
asks Windows for the type of a two-character drive string, starting from `.\`,
and stops at the first fixed drive. The check never fails and never looks at a
CD drive, so the game starts and plays without the disc. Its one lasting effect
is the prefix it leaves in front of the movie paths. A search of the CD drives
for a file `\CHAOS\CDTrack` exists in the executable but is never called. CD
music uses whatever CD audio device MCI opens by type, and is silent when there
is no disc.

## When it runs

Once in the title initialization, before the timers are started, and again in
the planning function after a human's planning ends. The second run can only
repeat the first result.

## Parameters

None.

## Inputs

`drive_type`, the answer of the Windows drive-type query for a drive string.

## Procedure

```text
define startup_drive_check() -> INT32:
    let letter = 46
    # 46 is the character code of "."; the string tested is the letter
    # followed by a backslash, with no colon
    while letter < 90:
        if drive_type(letter) == 3:
            # DRIVE_FIXED; the volume information is read and not compared
            movie_path_prefix = letter
            return 1
        letter = letter + 1
    movie_path_prefix = 0
    return 1
```

## Outputs

Returns 1 on every path. Sets `movie_path_prefix`: the first character of the
first drive string reported as fixed, followed by a backslash, or an empty
prefix when none is (RULE-VIDEO-001).

## Edge cases

- The failure branches of both callers, which would shut the game down at
  startup or offer to save after planning, are never reached.
- With the current directory on a fixed drive, `.\` is the first string tested
  and the movies load from `.\Data\`.
- When no string up to `Y\` is reported as fixed, the prefix is empty and the
  movie names become empty strings, so no movie opens.
- Without a disc or a CD audio device every MCI command fails silently and no
  music plays; the effects, the movies and the game are unaffected
  (FND-AUDIO-007).

## What the sources say

SRC-MANUAL-GOG does not describe a disc check.

## Differences between builds

None known.

## Open questions

- How the Windows versions of the time answered the drive-type query for `.\`
  and the other two-character strings has not been observed; on Windows 11 with
  the game on a fixed drive the answer for `.\` is 3.
