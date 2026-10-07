# DEV-UI-024

- Departs from: SCR-ATTACK-001, SCR-COMBAT-001, SCR-COMBAT-002, SCR-COMLINK-001, SCR-EQUIP-001, SCR-EVENT-001, SCR-FINANCE-001, SCR-GANG-001, SCR-GANG-002, SCR-GIVE-001, SCR-HIRE-001, SCR-INFLUENCE-001, SCR-MOVE-001, SCR-OBJECTIVE-001, SCR-OPTIONS-001, SCR-RESEARCH-001, SCR-SEARCH-001, SCR-SELL-001, SCR-UI-003, SCR-UI-004, SCR-UI-005, SCR-UI-006, SCR-UI-007, SCR-UI-008
- Reason: Options > Keys lists every single key the rebuild's screens read, says what each does on
  every screen that reads it, and lets the player bind each to another key. Binding a key that
  another shortcut holds swaps the two, so no key triggers two shortcuts. Escape stops a key
  capture instead of becoming the binding. Text fields, the title screen's Ctrl chords and
  Alt+Enter keep the printed keys. The bindings are kept in a local, versioned
  `keybindings.json`: a file from an older version keeps its bindings and gains the shortcuts
  added since, and a newer version's file lends this build the shortcuts both have and is never
  overwritten. The original reads fixed keys: each listed screen's key table names the keys it
  answers, and a player who rebinds one presses another.
- Setting: None
- Default: mandatory
- Justification: The default map binds every shortcut to its own key, so a player who never opens
  the editor presses the keys the original reads, with the rebuild's own keys from the other
  deviations; a setting that turned the editor off would only take the choice away. Rebinding
  changes nothing in a match: the bindings never enter a save, a replay or a state hash.
- Tests: tests/Rechaos.Tests/KeyBindingTests.cs
- Dropped: no
