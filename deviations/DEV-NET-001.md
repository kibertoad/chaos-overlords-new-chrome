# DEV-NET-001

- Departs from: SCR-NET-001, SCR-NET-002, SCR-NET-003, SCR-NET-004, SCR-NET-005, RULE-COMLINK-001, FMT-SAVE-001, SCR-UI-003
- Replaces: SCR-NET-001, SCR-NET-002, SCR-NET-003, SCR-NET-004, SCR-NET-005
- Reason: The original's network play, its lobbies, its protocols and its WinSock, TAPI and serial
  paths are not reproduced. Online play uses a new coordination server, so no Comlink message is
  sent to or received from another computer in the original's form, and the network form of the
  save file has no counterpart. An online match that ends opens the awards without the final view
  of the city the original gives each player at its own computer.
- Setting: None
- Default: mandatory
- Justification: The original's WinSock, TAPI and serial paths cannot reach anything a current
  player can connect to, so there is nothing to keep, and the coordination server is what makes
  online play possible at all.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.Persistence.cs
- Dropped: no

Decided 2026-09-10 ("Networking scope").
