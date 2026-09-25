---
id: RULE-SETUP-002
title: A fresh local setup selects the stored scenario preference, which is Kill 'Em All when nothing is stored
status: established
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SETUP-009, FND-SETUP-012, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [SCR-SETUP-001]
---

## Summary

When the full local setup screen opens, its selected scenario is the one the
player's preferences hold, and Kill 'Em All if there is no stored preference.

## When it runs

When the setup initializer prepares the full local setup screen, before the
screen is first drawn.

## Parameters

None.

## Inputs

`preferred_scenario`.

## Procedure

```text
scenario = preferred_scenario
show SCR-SETUP-001
```

## Outputs

No return value. Sets `scenario`, then shows the setup screen. Makes no draws.

## Edge cases

`preferred_scenario` starts at 0, Kill 'Em All, and changes only when the
preference loader finds a stored value.

## What the sources say

SRC-MANUAL-GOG, page 12, describes the Scenario Selection Control Panel and
its ten scenarios, and gives no default.

## Differences between builds

None known.

## Open questions

- The values of `scenario` other than 0 and 6 to 9 are not recorded, so a
  stored preference of 1 to 5 cannot yet be named.
- The address of the registry value's stored form, and whether a value
  outside 0 to 9 is accepted, are not recorded.
