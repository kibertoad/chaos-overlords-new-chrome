---
id: BUG-COMBAT-001
title: The game is reported to freeze while presenting a battle with Detailed Combat on
status: unknown
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: hang
intent: unintended
player_reliance: not-relied-on
evidence: []
conflicting: []
split_with: []
related: [SCR-COMBAT-002, RULE-COMBAT-004]
---

## Symptom

With the Detailed Combat option on, the game stops responding while it
presents a battle. The same battle completes when the option is off and the
simple presentation is used.

## Trigger conditions

Reported for this build whenever a battle is presented with Detailed Combat
on. Which battles, systems or settings trigger it is not known.

## Mechanism

Not known. Candidates are the timeline's tick handling on timer slot 0, the
panel's message loop, waiting on a sound, or the presentation's reading of the
combat records.

## Frequency

Not known.

## Player reliance

None known; a freeze gives the player nothing to rely on.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- The report has not been reproduced on a known installation, and no finding
  or experiment records it. Whether it happens on the systems the game was
  made for, or only on modern Windows, is not known.
