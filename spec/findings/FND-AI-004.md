---
id: FND-AI-004
title: The AI Mentality is one signed byte, set from the preferences and setup, read by two AI functions
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487850..0x00487851
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045519D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046439A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00438DA5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046381A
tool: Ghidra 12.1.3
environment: null
---

## Observation

The executable's string table maps resource IDs 46, 47, 48 and 49 to the four
Mentality names, in the order Goon, Criminal, Crime Lord, Homicidal Maniac.
The setup presenter `0x0045519D` loads the name shown, at `0x00455502`, with
resource ID `46 + (signed byte at 0x00487850)`. In the state-query function
`0x00402D70`, selector `0x36` returns the same signed byte. It is a different
global from the scenario at `0x004ABBE8` and from the values of selectors
`0x2F` and `0x31`.

The six writes to `0x00487850`:

- `0x00464618`, in the preference loader `0x0046439A`, copies the low byte of
  the registry value `prefsDiff` read with `RegQueryValueExA`.
- `0x00439542`, in the setup panel's apply path: `0x00438DA5` copies the
  global into a local, changes the local from the setup hit regions, and writes
  it back with the selected scenario and duration.
- `0x00461BF6` and `0x00461FEA`, two commit paths of one UI event handler. The
  first path first copies `0x00487850` to the staging byte `0x0049833C`; both
  paths later copy the staging byte back.
- `0x00463BC7` and `0x00463BF1` restore the staging byte after `0x0046381A`
  reads or writes it with neighbouring setup fields, under two different
  four-byte format markers.

There are eight calls with selector `0x36`: at `0x0040A734` and `0x0040A7B8` in
`0x0040A1A7`, and at `0x0043466A`, `0x004346D3`, `0x00434DA6`, `0x00434DD9`,
`0x00435090` and `0x004350C3` in the family-1 handler `0x00434080`. The call
at `0x0040950F` is not one: its selector argument is `0x21`, and `0x36`
appears only in a comparison before it.

Other selectors of `0x00402D70` read by the same functions:

- selector 3 returns the active player's cash, the per-player value the turn
  resolver `0x00472775` compares with action and equipment costs, lowers by
  them, and raises by income;
- selector 4 returns the sector's signed Tolerance byte (sector stride `0x24`),
  the byte the sector panel `0x004120EF` draws on its Tolerance row;
- selector `0x21` returns the sector's owner, or -2 for a disabled sector;
- selector `0x35` tests whether that owner's controller type is 0 or 3;
- selector `0x3C` returns the gang's Force byte, `0x3D` its planned action
  byte, and `0x51` its effective Heal (gang record +24);
- selector `0x2A` returns nonzero while the gang's current sector has a
  Crackdown in force;
- selector `0x2C` is a strict single-gang Control test. It rejects a disabled
  sector, an unavailable one, and one the player already owns. For a neutral
  sector it tests whether gang Force + Control is greater than sector Income +
  Support. For another player's sector it adds each defending gang's Force +
  Control to that sum first, where selector `0x91` lists only the defending
  gangs whose visibility byte for the querying player is nonzero. Control is
  gang record +23, the byte the Control resolver in `0x00472775` also reads.

The gang record bytes at `0x00498DBA` and `0x00498DBB` (offsets +18 and +19)
are effective Combat and Defense: the attack resolver adds the first to Force
at `0x00473DD3` and uses the second as Defense at `0x00473B17`.

## Interpretation

`0x00487850` is the AI Mentality chosen at setup, 0 for Goon to 3 for
Homicidal Maniac, kept for the whole match and carried through the setup
staging byte and its serialization. It starts from the saved preference. Only
the player-pair pass (FND-AI-018) and the family-1 handler (FND-AI-020) read
it through the query function; the attitude and difficulty tables read it at
new-match initialization (FND-AI-006, FND-AI-007).

## Alternatives

Whether the two four-byte formats around `0x0046381A` are save files, local
messages or network envelopes is not settled. Selector `0x2A`'s exact test (a
nonzero or a positive Crackdown byte) is not recorded. The Income and Support
bytes selector `0x2C` reads are not given offsets here.

## How to reproduce

Find the references to `0x00487850`; the six writes are listed above. The eight
selector-`0x36` calls are the pushes of `0x36` before calls to `0x00402D70`
inside `0x0040A1A7` and `0x00434080`. The string resources 46 to 49 are in the
executable's string table.
