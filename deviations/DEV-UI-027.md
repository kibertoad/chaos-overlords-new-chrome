# DEV-UI-027

- Departs from: SCR-UI-003, SCR-UI-004, SCR-UI-006, SCR-GANG-002, SCR-SELL-001, SCR-GIVE-001, SCR-EVENT-001, SCR-OPTIONS-001, SCR-COMLINK-002
- Reason: With Steady Lights on, nothing on the match screens blinks or cycles while the player
  does nothing: the Events, Comlink and Done lights stay lit for as long as they would blink, the
  selected sector's frame and the Overlord bar's marker and empty-seat art hold their first
  frames, the idle gang warning's line stays drawn, the Comlink Send caret stays inverse, and the
  rotating item pictures hold their first frame. Off, they blink and turn as in the original.
- Setting: Steady Lights (On holds them still; Off is the original's drawing)
- Default: off
- Tests: tests/Rechaos.Tests/SteadyLightsTests.cs
- Dropped: no
