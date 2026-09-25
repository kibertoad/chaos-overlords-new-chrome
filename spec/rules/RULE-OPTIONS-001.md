---
id: RULE-OPTIONS-001
title: Reading the options from the registry at startup
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-OPTIONS-001, FND-RNG-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002]
---

## Summary

At startup the game reads its options from the registry key
`HKLM\SOFTWARE\Stick Man Games\Chaos Overlords\1.0`. Every value is read into
one shared buffer, so a missing value takes the value of the option read before
it rather than its default. When the serial number that comes out is 0, the game
makes two random draws to build a new one.

## When it runs

Once, at process start, after the random number generator is seeded and before
the title screen is shown.

## Parameters

None.

## Inputs

`registry_present`, `registry_dword`, and `rng` through `roll`.

## Procedure

```text
define next_option(index: INT32, buffer: UINT32) -> UINT32:
    # a failed query leaves the shared buffer as it was
    if registry_present[index] != 0:
        return registry_dword[index]
    return buffer

# the key is opened once with KEY_READ
let buffer: UINT32 = 0
buffer = next_option(0, buffer)
pref_thousands_colors = buffer
buffer = next_option(1, buffer)
pref_slide_panels = buffer
buffer = next_option(2, buffer)
pref_base_stats = buffer
buffer = next_option(3, buffer)
pref_detailed_combat = buffer
buffer = next_option(4, buffer)
pref_warn_idle = buffer
buffer = next_option(5, buffer)
comm_type = buffer
buffer = next_option(6, buffer)
effects_level = buffer
buffer = next_option(7, buffer)
music_level = buffer
buffer = next_option(8, buffer)
mentality = buffer
buffer = next_option(9, buffer)
planning_limit_choice = buffer
buffer = next_option(10, buffer)
objective_choice = buffer
buffer = next_option(11, buffer)
pref_full_screen = buffer
buffer = next_option(12, buffer)
serial_number = buffer
if serial_number == 0:
    let serial_low = roll(16384) - 1
    let serial_high = roll(16384) - 1
    # the two values are combined into a serial number, which the loader then
    # tries to write back through the read-only key; the write fails
    # (BUG-OPTIONS-001)
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

When the serial number is 0, makes two calls of `roll(16384)`, six draws from
`rng` in all. Otherwise makes none.

## Edge cases

- A value missing from the registry takes the value read for the option before
  it (BUG-OPTIONS-002). Only when every value from the first is missing does an
  option keep what the buffer held before the first query.
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

- What the loader does when the key cannot be opened at all: whether it returns
  at once and every option keeps its initialized value, or runs the queries.
- What the shared buffer holds before the first query; the procedure assumes 0.
- How the two draws are combined into the serial number, and whether the
  combined value is stored in `serial_number` in memory.
- Whether a registry value of another type or size changes the buffer.
- `mentality` is kept as a byte, but the loader writes four bytes at its address;
  what the three bytes after it hold is not recorded.
