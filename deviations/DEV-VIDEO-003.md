# DEV-VIDEO-003

- Departs from: RULE-VIDEO-001, SCR-UI-001
- Reason: With Intro only once switched on, the rebuild plays the two movies unattended only until
  it has recorded a showing: its preferences file keeps `IntroMoviesSeen`, set once the queue
  drains after at least one movie opened, and every later start goes straight to the title screen.
  The title screen gains an INTRO button that plays the movies again on request. The original plays
  both movies at every start that does not load a saved game.
- Setting: Intro only once
- Default: on
- Justification: Players rarely want to watch the intro again and again; one showing is plenty.
  Playing it at every start makes the player wait through or click past the same two movies each
  time before reaching the title screen, and nothing in a match depends on it. The INTRO button
  plays the movies whenever the player asks, and a player who wants the original's intro at every
  start switches Intro only once off.
- Dropped: no

Made a setting that starts off on 2026-09-25, and switched to start on on 2026-09-26.
