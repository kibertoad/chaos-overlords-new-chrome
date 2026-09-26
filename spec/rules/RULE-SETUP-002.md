---
id: RULE-SETUP-002
title: A fresh local setup selects the stored scenario preference, which is Greed when nothing is stored, and a one-year time limit
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SETUP-013, FND-OBJECTIVE-003, FND-SETUP-009, FND-SETUP-012, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [SCR-SETUP-001]
---

## Summary

When the full local setup screen opens, its selected scenario is the one the
player's preferences hold, and Greed if the preferences key is absent. The
time limit starts at 52 turns every time, whatever was chosen before.

## When it runs

When the setup initializer prepares the full local setup screen, before the
screen is first drawn.

## Parameters

None.

## Inputs

`preferred_scenario`.

## Procedure

```text
scenario = INT32(preferred_scenario)
turn_limit = 52
show SCR-SETUP-001
```

## Outputs

No return value. Sets `scenario` and `turn_limit`, then shows the setup
screen. Makes no draws.

## Edge cases

`preferred_scenario` is 0, Greed, in the executable's data. The preference
loader reads its registry values into one shared variable, so when the key
exists but `prefsObjective` is missing, `preferred_scenario` takes the low
byte of the last earlier value that was found (FND-SETUP-013). A committed
scenario choice on the setup screen is stored back into `preferred_scenario`.
The stored byte is sign extended, and a value outside 0 to 9 leaves the
selection light and the description undefined.

## What the sources say

SRC-MANUAL-GOG, page 12, describes the Scenario Selection Control Panel and
its ten scenarios, and gives no default.

## Differences between builds

None known.

## Open questions

- A run of the original with the key absent, which would confirm Greed, has
  not been made. FND-SETUP-012 saw Kill 'Em All while the registry held a
  stored value of 4.
