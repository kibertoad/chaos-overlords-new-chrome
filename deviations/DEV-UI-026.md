# DEV-UI-026

- Departs from: SCR-UI-003
- Reason: In a local match, F6 saves the session's journal as a replay and F10 opens the replay
  viewer over the city screen. The viewer verifies every recorded step before it shows the first
  one, then shows the recorded match on the city map, with the Overlord bar, calendar and sector
  values of the frame shown and a panel over the console's controls at `(436,108,200,350)`: the
  turn, the step and what it did, a timeline, and buttons to go to the start, back a turn, back a
  step, play or pause, on a step, on a turn, to the end, slower, faster and exit. Space, the
  arrows, Page Up, Page Down, Home and End do the same; W, A, S and D or a click on the map select
  the sector whose values are shown; Escape, Backspace or a right click close the viewer, which the
  key line along the bottom of the map (DEV-UI-023) says in place of the city's keys. While it
  is open the live match is set aside: its planning clock is paused, no computer turn runs, and
  closing the viewer puts it back as it was. The original has no replay.
- Setting: None
- Default: mandatory
- Justification: The viewer only shows a recorded history, so the player can do nothing in the
  match they could not do before and no rule produces anything different; the replay files are
  read, never written, by the viewer. The original has no replay to compare with or switch back
  to, and a setting that removed the keys would only take the feature away. F10 used to load the
  replay as the match, which gave every local match a quick load that kept the luck RULE-RNG-001
  makes a reload give up; the viewer shows the same history without that (docs/DECISIONS.md,
  2026-10-06).
- Tests: tests/Rechaos.Tests/MatchReplayPlaybackTests.cs, tests/Rechaos.Tests/ReplayViewerTests.cs
- Dropped: no
