# DEV-NET-002

- Departs from: RULE-TURN-001
- Reason: In an online match, the orders a player gives in one turn travel to the server as one
  document of at most 512 operations, the coordination server's limit. The document leaves out
  what a later order makes moot: a gang's later order replaces its earlier one (RULE-TURN-005), an
  order given and cancelled in the same turn is not sent, and the hire dock, which hire
  resolution leaves with no hire or snub at the end of every turn, sends only the hire or snub it
  ends the turn with. A dock that starts a turn holding one keeps every hire op. An action that
  would take the document past the limit is refused with "TOO MANY ORDERS THIS TURN." before it
  changes anything. The original takes any number of orders in a turn. Hot-seat and
  single-player matches have no limit.
- Setting: None
- Default: mandatory
- Justification: No input the original accepts reaches the limit: a turn's document then holds at
  most one operation per gang (80), one for the hire dock and one per notification dismissed (64),
  145 in all. Leaving out the moot operations changes nothing the rules produce, because every
  client applies the same document and no rule reads the planning events or the absolute command
  sequence numbers that are all it changes. The original's network play is not reproduced
  (DEV-NET-001) and online play is outside the parity target (2026-09-10), so there is no original
  online behaviour for a setting to restore.
- Tests: tests/Rechaos.Tests/MultiplayerOrderCompactionTests.cs
- Dropped: no
