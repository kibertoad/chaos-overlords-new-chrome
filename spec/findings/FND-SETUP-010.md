---
id: FND-SETUP-010
title: With more than one local human, each human's planning starts behind a Ready card, then Game Information, combat results and Last Turn Events
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E766
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004396C0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00439F7A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FD80
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045519D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00451F80
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042E040
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494870
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044F2FC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464290
tool: Ghidra 12.1.3
environment: null
---

## Observation

The outer match loop `0x0046E766` counts the active local human players before
it starts their planning visits, and enables the handoff card
`DATA/PX16/PX00132` only when there are more than one. For an ordinary active
local player it calls the card's presenter `0x004396C0`, records that player
as the current viewer, and then enters the human planning handler
`0x0046FD80`. The presenter is a blocking event loop. Its only control is
Ready, handled by the push-cue helper `0x00439F7A`, and neither its keyboard
nor its pointer path continues until Ready completes or the outer menu or quit
state interrupts it.

`0x0046FD80` then does, in this order:

1. When its once-only new-local-game flag is set, it opens the Game
   Information panel `0x0045519D`. This comes after Ready, not before the
   card.
2. It calls the simple Combat Results presenter `0x00451F80` or the Detailed
   Combat presenter `0x0042E040`, chosen by the Detailed Combat option. Called
   this way, `0x00451F80` returns at once when no sector qualifies; when there
   are results, its panel's event loop returns before planning continues.
3. Only after that call returns does it rebuild the 32-byte Last Turn Events
   read-state table at `0x00494870` and open Last Turn Events at `0x0044F2FC`.
4. It then tests the player's unread-Comlink flag and plays general sound
   slot 6 through `0x00464290`, and resets the alert repeat counter right after.

## Interpretation

The Ready card keeps one local player's turn private from the next. For a
normal hot-seat handoff the order is: Ready, Game Information once in a new
local game, the automatic combat presentation, Last Turn Events, then the
planning city. Combat and events are separate blocking presentations, and the
events never come first.

## Alternatives

When the card is shown only between players, or also before the first local
player of each turn, is read here as "for every active local player when more
than one remains"; the exact test is not recorded. The address of the
new-local-game flag and of the unread flag are not recorded.

## How to reproduce

In `0x0046E766`, find the count of controller-0 active slots compared with 1,
and the per-slot call to `0x004396C0` before the call to `0x0046FD80`. In
`0x0046FD80`, find the flag test before the call to `0x0045519D`, the branch
on the Detailed Combat option between `0x00451F80` and `0x0042E040`, the
table rebuild at `0x00494870` before the call to `0x0044F2FC`, and the call to
`0x00464290` with 6.
