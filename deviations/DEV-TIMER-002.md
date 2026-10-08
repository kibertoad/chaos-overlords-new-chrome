# DEV-TIMER-002

- Departs from: RULE-TIMER-002
- Reason: With Menu Stops Clock on, the elapsed time of a timed planning turn stops while the game
  menu is open, so a turn cannot run out in the menu. The original's clock runs on while its menu
  bar is open, and a turn whose limit passes there ends when the menu closes.
- Setting: Menu Stops Clock (On; Off is the original's clock)
- Default: off
- Tests: tests/Rechaos.Tests/PlanningTimerPolicyTests.cs
- Dropped: no
