---
id: FND-AUDIO-001
title: Music plays one of three CD track programs, restarts each when it ends, and pauses while the window is inactive
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458B43
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458C10
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458C5F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458E68
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458F3E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004642BD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464385
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004652A0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487868..0x00487869
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487878..0x0048787C
tool: Ghidra 12.1.3
environment: null
---

## Observation

- `mciSendCommandA` is referenced by seven helpers from `0x00458B43` to
  `0x00458F3E`. The play helper `fn_00458B43` first sends `MCI_STATUS` for the
  length of the last track of its range, then sends `MCI_PLAY` with the `FROM`,
  `TO` and `NOTIFY` flags.
- The selector `fn_004642BD` maps mode 0 to tracks 2 to 2, mode 1 to tracks 9
  to 9, and mode 2 to tracks 3 to 8, both ends included. When the requested
  mode differs from the global `g_00487878` it stops playback through
  `fn_00464385`. It then records the new mode, which is not negative, and starts
  that mode's range whenever music is enabled.
- The selector has eleven callers:
  - mode 0 at `0x004615D5`, on first entry to the title, and at `0x004617BA`,
    `0x00461A33`, `0x00461BE9`, `0x00461ED6` and `0x00462098`, when the local,
    network and load paths return to the title loop;
  - mode 2 at `0x0046EB22`, on entry to the outer game and turn function, and at
    `0x0046F5CE`, when a player's endgame screen returns to a later human
    player;
  - mode 1 at `0x0042B9F9` and `0x0042C40E`, on entry to the two endgame and
    awards presentation functions;
  - the current mode at `0x00462AE8`, when the main event pump sees that MCI
    playback has stopped.
- No selector call sits on the paths from the title to setup, from setup back
  to the title, into or out of Options or Help, or between the panels of a
  game.
- The pump's deactivation branch calls the pause helper `fn_00458C10` and its
  activation branch the resume helper `fn_00458C5F`. Shutdown closes the MCI
  device through `fn_00458F3E`.
- The Options helper `fn_004652A0` reads the byte at `0x00487868` as a music
  level from 0 to 10. Level 0 clears the music-enabled flag and stops playback.
  Any other level calls `fn_00458E68`, which writes `level * 25 * 256` into
  both 16-bit channels of the auxiliary volume. The initialized level is 5,
  which gives 32000 of 65535 in each channel; level 10 gives 64000.

## Interpretation

Track 2 is the music of the title and setup screens, tracks 3 to 8 play in
order during a game, and track 9 plays over the endgame. Each program starts
again from its first track when its last track ends, with no shuffle. Music
pauses while the window is inactive and resumes when it becomes active again.
Opening Options or Help does not change the music. The music level scales the
auxiliary volume in 11 steps, and the loudest step stays just below full scale.

The GOG build serves these CD audio requests from `MUSIC/Track02.ogg` to
`MUSIC/Track09.ogg` through its replacement `winmm.dll`. The executable itself
asks for CD tracks.

## Alternatives

- The "records the new mode, which is not negative" wording leaves open what a
  negative mode does. No caller passes one.
- Whether a change from level 0 to a nonzero level sets the music-enabled flag
  again and restarts the program has not been read.
- Which auxiliary device the volume call addresses has not been recorded.

## How to reproduce

Follow the references to `mciSendCommandA` in the `WINMM.dll` imports
(FND-PLATFORM-006) to the helpers at `0x00458B43..0x00458F3E`. The selector at
`0x004642BD` compares its argument with `g_00487878`; list its callers for the
eleven call sites. The Options helper at `0x004652A0` reads `0x00487868` and
multiplies by 25.
