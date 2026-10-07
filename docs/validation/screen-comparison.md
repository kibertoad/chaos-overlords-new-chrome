# Comparing screens with captures

Status: maintained canonical procedure

Part of the [validation procedure](../VALIDATION.md).

## Comparing

`ScreenCaptureTests.TheRebuildDrawsWhatTheOriginalDrew` runs once for each
capture the experiment fixtures record. It replays the run with
`OriginalNewGameExperimentTests.ReplayedMatch`, writes the endpoint as a native
save, and starts the game with

```text
Rechaos.Game --assets <pack> --reference-frame <save> <bitmap> --marker-frame <n>
    [--pump-counter <0-7>] [--selected-sector <0-63>] [--lamps <0|1>,<0|1>]
    [--item-frame <0-14>] [--idle-phase <0-7>] [--caret-phase <0-5>]
    [--clip-tick <0-21> [--clip-index <n>]]
    [--reference-clicks <x:y[:2]|x:y>x:y|'TEXT>,...]
```

which shows the save at its planning entry in a 640-by-460 window, holds the
presentation clock at zero, draws three frames and writes the third as a
bitmap before it exits. `--pump-counter` passes the capture's `frame_counter`, or its
`pump_counter` when the fixture has none, which picks the selected-sector frame
drawn (FND-UI-048). `--selected-sector` passes `selected_sector`, which the
planning entry selects in place of the sector the rebuild keeps for the player
(FND-SAVE-003); a save holds no selection (DEV-SAVE-001). `--lamps` passes the
second and fourth of the capture's `lamps`, the bytes that say the Events and
the Comlink lamp were drawn lit, which pick the blink phase of those lights in
place of the clock's (FND-EVENT-006). `--item-frame` passes `item_frame`, the
frame the rotating items of Item Information, Sell and Give are drawn at
(FND-UI-052, FND-UI-053). `--idle-phase` passes `idle_phase`, the idle gang
warning's ticks since its open modulo 8 (FND-UI-054), and `--caret-phase`
passes `caret_phase`, the Comlink caret's phase (FND-COMLINK-010).
`--clip-tick` passes `clip_tick`, the tick of the Detailed Combat clip a shot
shows (FND-COMBAT-016), and `--clip-index` passes `clip_index`: the rebuild
passes over that many clips of the presentation the clicks started and draws
the next at the tick. A shot without `clip_index` is drawn at the first clip. The blinking
and cycling parts of the screen stay at time zero however many clicks were
made: the marker is drawn at `--marker-frame`, or at its first frame without it.
`--reference-clicks` lists the presses that take the rebuild from the planning
entry to a shot step's screen, `:2` marking a double-click, and `'TEXT` text
typed into the Comlink Send panel a character at a time. They run on a clock
of their own, one button edge every 50 ms, wait while a pressed face or a flash
holds the input (RULE-TIMER-004), and the frame is drawn 20 updates after the
last one. Panels are drawn in place, without the slide. For a shot step the test
works the presses out from the order steps before it: a double-click at the
centre of the opened sector's cell for `open`, the step's point for `dbl` and
`strip`, a card's point for `card`, `(20, 425)` for `back` and the step's text
for `type`. It leaves out `wait`, which presses nothing, and `exit`, since the
reference frame does not draw the planning entry's panels.
For a `title_capture`, `credits_capture` or `setup_capture` the test passes
`title`, `credits` or `setup` in place of the save; the game draws its title
screen, the credits over it, or the local setup as New Game first opens it,
without a match. A setup step's copy passes the presses before it as
`--reference-clicks`, a drag as `x:y>x2:y2`, which the rebuild makes as a
press, a move with the button down and a release.
The rebuild's orders are a panel (DEV-UI-021): a `card` step whose menu 1
choice runs a picker (Attack, Equip, Give, Influence, Move, Research or Sell,
FND-UI-021) becomes the card press and a press on that order's row of the
panel. Any other step that opened a popup menu makes the capture
unreplayable, and the test skips it. With `RECHAOS_KEEP_FRAMES` set to a directory, the test copies
each frame the rebuild drew there as `<experiment>-<run>-<step>.bmp`, step -1
being the endpoint, and as `<experiment>-<run>-<screen>.bmp` for a screen before the match. Preferences, saves and logs of that run go to a
`rechaos-reference-frame-*` directory beside the bitmap, never to the player's.
The test then compares each element:

- An element the original drew wholly in exact white is unverified: in a
  capture taken without `--white-key`, a keyed copy on Windows 11 draws solid
  white where its image should show through (FND-PLATFORM-014), and what
  belongs there is unknown.
- With the capture under `GAME_DIR/captures/`, every pixel outside the masks is
  compared. A pixel the original drew exact white is counted as unverified
  unless the rebuild drew it white as well. The element matches when no
  compared pixel differs.
- Without the capture, an element with no white pixel and no mask is compared
  by its digest, and any other element is unverified.
- A fixture whose inputs hold the setup input `key_colour`, written by
  `--white-key`, has no white left by a keyed copy, so its exact white is
  compared like any other colour: a pixel the original drew white and the
  rebuild did not differs, and without the capture an element with white
  pixels and no mask is compared by its digest. EXP-UI-003 is compared this
  way.
- `ScreenCaptureMasks` lists, for each screen, the rectangles a deviation draws
  over, each under the ID of the deviation. All the masks of the screens a
  capture names apply to the whole frame. `EveryMaskCitesADeviationFromItsScreen`
  checks that each deviation's Departs from item, wrapped lines included,
  names the screen.

The test prints every element's verdict and fails on an element that differs.
It skips a capture that records no screen elements, and skips the rendering
when no asset pack is installed: the gate builds with
`IncludeOriginalAssets=false`, so it finds the pack only in the player's
application data (`ChaosOverlordsNewChrome/Assets`). A plain
`dotnet test --project tests/Rechaos.Tests` in a checkout with
`src/Rechaos.Game/Assets` finds it beside the test binary.
`OnlyTheMarkerFrameChangesAReplayedEndpoint` checks the reference frame itself:
two renders of the EXP-SETUP-001 endpoint at marker frames 6 and 0 differ only
inside the marker.

A screen row of `PARITY.md` lists `ScreenCaptureTests` once a capture of the
original covers its elements and they match; the elements left unverified are
named in its Notes.

## What the reference frame shows

The reference frame starts at the city screen and its console (SCR-UI-003,
SCR-HIRE-002) of the player whose planning entry the run ends at, with no
pointer, and then makes the scripted presses. With several local humans it
starts at the hand-off card (SCR-SETUP-002) and a press of its Ready goes on as
in play; for a run whose match ended it starts at the endgame (SCR-AWARDS-001),
and for one whose last resolution eliminated a local human at that player's elimination card
(SCR-OBJECTIVE-002). A run stops at the elimination card as it stops at the
endgame.
It does not draw Combat Results or Last Turn Events that the planning entry
would open first, but it closes Last Turn Events as a press of its Exit after
the first page would: the Events light stays on only while the player has another report
to see (RULE-EVENT-005). EXP-UI-007's capture, after the original's planning
entry showed its one report and the Exit closed the panel, has the light's
flag clear. A capture taken with Combat Results or Last Turn Events open, at
the final view, during a drag or with a popup menu open cannot be compared.

The selected-sector outline cycles through two frames on the pump's counter
(FND-UI-017), and the reference frame draws the one the capture's
`pump_counter` gives (FND-UI-048); without one it draws the first. It draws the
Events and Comlink lights in the lit half of their blink whenever they are on.
No capture yet shows a light lit, so which counter values the lit half covers
has not been compared.
