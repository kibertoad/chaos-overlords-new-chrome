# Spec entry templates

Blank entries for each kind in `spec/`, with the front matter fields and body
sections the [documentation standard](https://dinorefurb.com/documentation-standard/#entry-types)
requires, in its order. Copy one into the directory for its kind, name the file
after the ID (`spec/rules/RULE-COMBAT-007.md`), and replace every `<...>`. The
standard defines what each field and section holds; this page does not repeat
it.

A section with nothing to say is kept and says `None known.`, or `None.` where
it is certain that there is nothing. Until the check script is published in
[refurbished-dinosaurs-toolkit](https://github.com/kibertoad/refurbished-dinosaurs-toolkit),
reviewers go through the standard's list of checks by hand.

## Build

````markdown
---
id: BLD-<ALIAS>
title: <Game> <version>, <language>, <distribution>
superseded_by: []
developer: <the studio that made the game>
publisher: <the company that released the game>
publisher_version: "<version>"
distribution: <GOG, Steam, CD-ROM, ...>
languages: [<ISO 639-1 codes>]
int_width: <16 or 32>
files:
  - path: <path relative to the install directory, or CD:path>
    format: <MZ, COM, NE, PE, LE, LX, ELF, cdda or data>
    size: <bytes>
    xxh3: <32 lower-case hex digits, as `xxhsum -H2` prints>
---

## Obtaining

## Compared with other builds

## Other files
````

A build has one executable that runs the game's rules. An installation that
ships two, such as a DOS and a Windows version over the same data files, is two
builds, and both list the shared files. `Restoration.Inspect --source <dir>`
prints each file's size and `xxh3`.

## Source

````markdown
---
id: SRC-<ALIAS>
title: <title>
superseded_by: []
author: <who wrote it>
date: "<year or date>"
location: <URL or archive location>
xxh3: null
licence: null
---

## Use

## Known errors
````

## Finding

````markdown
---
id: FND-<AREA>-<NNN>
title: <one sentence>
status: recorded
builds: [BLD-<ALIAS>]
superseded_by: []
recorded_by: <GitHub username>
reproduced_by: []
method: <static or dynamic>
locations:
  - build: BLD-<ALIAS>
    file: <path from the build entry>
    address: <range in the notation for the file's format, or offset: for data files and overlays>
tool: <tool and version>
environment: null
---

## Observation

## Interpretation

## Alternatives

## How to reproduce
````

`environment` stays `null` for a static finding. A dynamic finding gives it in
the form an experiment uses.

## Experiment

````markdown
---
id: EXP-<AREA>-<NNN>
title: <the question, as a sentence>
status: recorded
builds: [BLD-<ALIAS>]
superseded_by: []
recorded_by: <GitHub username>
reproduced_by: []
environment: <OS, compatibility layer or emulator, and settings>
starting_state: <saves/EXP-<AREA>-<NNN>.patch.json, a save, new-game, or null>
recording: null
repetitions: <count>
fixture: EXP-<AREA>-<NNN>.json
---

## Question

## Setup

## Procedure

## Observations

## Results

## Conclusion
````

## Format

````markdown
---
id: FMT-<AREA>-<NNN>
title: <what the format holds>
status: unknown
builds: [BLD-<ALIAS>]
superseded_by: []
files: []
byte_order: little
size: null
text: false
definition: null
evidence: []
conflicting: []
split_with: []
related: []
---

## Layout

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|

## Enumerations and flags

None known.

## Differences between builds

None known.

## Coverage

## Open questions
````

A binary format with a status above `unknown` has a Kaitai definition next to
it, named after the ID in lower case (`fmt_data_002.ksy`), and `definition`
names it.

## Rule

````markdown
---
id: RULE-<AREA>-<NNN>
title: <what the rule decides>
status: unknown
builds: [BLD-<ALIAS>]
superseded_by: []
evidence: []
conflicting: []
split_with: []
related: []
---

## Summary

## When it runs

## Parameters

## Inputs

## Procedure

```text
```

## Outputs

## Edge cases

## What the sources say

## Differences between builds

None known.

## Open questions
````

## Bug

````markdown
---
id: BUG-<AREA>-<NNN>
title: <the symptom>
status: unknown
builds: [BLD-<ALIAS>]
superseded_by: []
impact: <crash, hang, save-corruption, rules, presentation or performance>
intent: <unintended or unclear>
player_reliance: <relied-on, not-relied-on or unknown>
evidence: []
conflicting: []
split_with: []
related: []
---

## Symptom

## Trigger conditions

## Mechanism

## Frequency

## Player reliance

## Fixes elsewhere

## Differences between builds

None known.

## Open questions
````

## Screen

````markdown
---
id: SCR-<AREA>-<NNN>
title: <screen, panel or dialog>
status: unknown
builds: [BLD-<ALIAS>]
superseded_by: []
resolution: <width>x<height>
evidence: []
conflicting: []
split_with: []
related: []
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|

## Other input

| Device | Input | Enabled when | Effect | Evidence |
|---|---|---|---|---|

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|

## Timing

## Differences between builds

None known.

## Open questions
````
