# DEV-UI-010

- Departs from: SCR-UI-005, SCR-UI-006, SCR-UI-007, SCR-UI-008, SCR-OPTIONS-001, SCR-GANG-001, SCR-GANG-002, SCR-FINANCE-001, SCR-EQUIP-001, SCR-GIVE-001, SCR-SELL-001, SCR-MOVE-001, SCR-RESEARCH-001, SCR-INFLUENCE-001, SCR-HIRE-001, SCR-OBJECTIVE-001, SCR-SEARCH-001, SCR-EVENT-001, SCR-COMLINK-002, SCR-UI-003, SCR-UI-004, SCR-HIRE-002, SCR-SETUP-001
- Reason: Panels accept keyboard navigation, and Escape, Backspace and the right mouse button
  cancel them; the arrow keys cycle gangs on the gang information panel, pick a cell on
  the Move panel and a row on the Equip panel, and the keys 1 to 3 toggle items on the Give and
  Sell panels. A right press also lets go of a held console tile, Hire offer or reject cross, or
  setup portrait without acting on it, and backs out of the setup screen to the title.
  The original's panels take Enter and Execute and, on the idle-gang warning, Escape, and every
  screen but the About credits, the console and the detailed sector screen ignores the right
  button (FND-UI-063).
- Setting: None
- Default: mandatory
- Justification: It adds keys and ways to cancel; the original's Enter and Execute work as before,
  and where the original gives the right button a meaning (the console tiles and the detailed
  sector screen) the rebuild keeps it. A right press that does nothing is not something a player
  plans around, so the original's behaviour is not worth a setting.
- Dropped: no
