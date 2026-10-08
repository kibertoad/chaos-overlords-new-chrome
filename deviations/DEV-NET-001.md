# DEV-NET-001

- Departs from: SCR-NET-001, SCR-NET-002, SCR-NET-003, SCR-NET-004, SCR-NET-005, RULE-COMLINK-001, FMT-SAVE-001, SCR-UI-003
- Replaces: SCR-NET-001, SCR-NET-002, SCR-NET-003, SCR-NET-004, SCR-NET-005
- Reason: The original's network play, its lobbies, its protocols and its WinSock, TAPI and serial
  paths are not reproduced. Online play uses a new coordination server, so no Comlink message is
  sent to or received from another computer in the original's form, and the network form of the
  save file has no counterpart. An online match that ends opens the awards without the final view
  of the city the original gives each player at its own computer. A host may let people without a
  seat watch the match some turns behind; a spectator sees the city screen's art, Overlord bar,
  map and status console for a seat of their choosing, with a panel over the command buttons that
  says how far behind the view is held and holds the way out, and gives no orders. The in-match
  menu of an online match lists the spectators where a local match offers to save.
- Setting: None
- Default: mandatory
- Justification: The original's WinSock, TAPI and serial paths cannot reach anything a current
  player can connect to, so there is nothing to keep, and the coordination server is what makes
  online play possible at all. The spectator view adds a way to watch that the original never had,
  and changes nothing a player in the match can do or what the rules produce.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.Persistence.cs,
  tests/Rechaos.Tests/SpectatorViewTests.cs, tests/Rechaos.Tests/MultiplayerSpectatorWatchTests.cs
- Dropped: no

Decided 2026-09-10 ("Networking scope").
