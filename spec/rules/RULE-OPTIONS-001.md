---
id: RULE-OPTIONS-001
title: Reading the options from the registry at startup
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-OPTIONS-001, FND-OPTIONS-003, FND-RNG-001, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002]
---

## Summary

At startup the game reads its options from the registry key
`HKLM\SOFTWARE\Stick Man Games\Chaos Overlords\1.0`. Every value is read into
one shared buffer, so a missing value takes the value of the option read before
it rather than its default. When the key cannot be opened, nothing is read and
every option keeps its initialized value. When the key opens and the serial
number that comes out is 0, the game makes two random draws to build a new one
and keeps it for the session.

## When it runs

Once, at process start, after the random number generator is seeded and before
the title screen is shown.

## Parameters

None.

## Inputs

`registry_key_opened`, `registry_present`, `registry_dword`, and `rng` through
`roll`.

## Procedure

```text
define next_option(index: INT32, buffer: UINT32) -> UINT32:
    # a failed query leaves the shared buffer as it was
    if registry_present[index] != 0:
        return registry_dword[index]
    return buffer

# the key is opened once with KEY_READ
if registry_key_opened == 0:
    return
let buffer: UINT32 = 0
buffer = next_option(0, buffer)
# every option but comm_type and serial_number keeps only the low byte
pref_thousands_colors = buffer % 256
buffer = next_option(1, buffer)
pref_slide_panels = buffer % 256
buffer = next_option(2, buffer)
pref_base_stats = buffer % 256
buffer = next_option(3, buffer)
pref_detailed_combat = buffer % 256
buffer = next_option(4, buffer)
pref_warn_idle = buffer % 256
buffer = next_option(5, buffer)
comm_type = buffer % 65536
buffer = next_option(6, buffer)
effects_level = buffer % 256
buffer = next_option(7, buffer)
music_level = buffer % 256
buffer = next_option(8, buffer)
mentality = buffer % 256
buffer = next_option(9, buffer)
planning_limit_choice = buffer % 256
buffer = next_option(10, buffer)
objective_choice = buffer % 256
buffer = next_option(11, buffer)
pref_full_screen = buffer % 256
buffer = next_option(12, buffer)
serial_number = buffer
if serial_number == 0:
    let serial_high = roll(16384) - 1
    let serial_low = roll(16384) - 1
    serial_number = serial_high * 65536 + serial_low
    # the loader then tries to write it back through the read-only key; the
    # write fails (BUG-OPTIONS-001)
```

## Outputs

No return value. Sets the thirteen options in this order:

| Index | Registry value | Glossary term | Initialized value |
|---|---|---|---|
| 0 | `prefsVidDeep` | `pref_thousands_colors` | 1 |
| 1 | `prefsSlide` | `pref_slide_panels` | 1 |
| 2 | `prefsBaseStats` | `pref_base_stats` | 0 |
| 3 | `prefsCombat` | `pref_detailed_combat` | 1 |
| 4 | `prefsFreeGang` | `pref_warn_idle` | 1 |
| 5 | `commType` | `comm_type` | 0 |
| 6 | `prefsVolumeSFX` | `effects_level` | 6 |
| 7 | `prefsVolumeCD` | `music_level` | 5 |
| 8 | `prefsDiff` | `mentality` | 1 |
| 9 | `prefsTimeLimit` | `planning_limit_choice` | 0 |
| 10 | `prefsObjective` | `objective_choice` | 0 |
| 11 | `prefsFullScreen` | `pref_full_screen` | 1 |
| 12 | `serialNum` | `serial_number` | 0 |

When the key opens and the serial number is 0, makes two calls of
`roll(16384)`, six draws from `rng` in all, the first giving the high half of
the new serial number. Otherwise makes none. `pref_full_screen` is also copied
to a second byte the display code reads.

## Edge cases

- A value missing from the registry takes the value read for the option before
  it (BUG-OPTIONS-002). When every value from the first is missing, the options
  take 0, the value the buffer is set to before the first query.
- Without the key the options keep the initialized values of the table and
  `serial_number` stays 0 for the session, with no draws.
- A value above 255 keeps only its low byte, above 65535 for `comm_type`.
- Since the serial number can never be written (BUG-OPTIONS-001), a key without
  `serialNum` makes the draws at every start, unless the value read before it,
  `prefsFullScreen`, is nonzero, in which case the serial number becomes that
  value and no draw is made.

## What the sources say

SRC-MANUAL-GOG, pages 9 and 10, gives the defaults of the Options menu:
Thousands of Colors checked, Music and Sound Effects Medium, Base Statistics not
checked, Detailed Combat checked, Slide Panels checked, Warn If Idle Gangs
checked. These agree with the initialized values. It does not say where the
options are kept.

## Differences between builds

None known.

## Open questions

- The size passed to each query is never reset, so a value longer than four
  bytes changes the size used by every later query. What Windows then writes
  next to the 4-byte buffer has not been observed. A value of another type
  and four bytes or less is read as its first bytes.
