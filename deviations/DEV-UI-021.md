# DEV-UI-021

- Departs from: SCR-UI-004
- Reason: The order menus of a gang card and of the group order strip are a panel the rebuild
  draws at (248,50,174,400), listing the same orders, where the original opens a Windows popup
  menu at the card's corner or at (290,65). An order the rules refuse for the gang is refused when
  it is chosen, with a message on the console. The panel opens and closes without sliding and
  without a sound, as the popup does (FND-UI-057), and the order panel chosen from it slides in
  with the panel-open sound (RULE-UI-003).
- Setting: None
- Default: mandatory
- Justification: The rebuild draws the whole game in its own window without the Windows frame
  (DEV-GFX-001), where a native popup menu would appear outside the game's picture and ignore its
  scaling. The panel offers the orders the original's menus offer, for the same gangs, so the
  player can do nothing new, and there is no original form to switch back to.
- Dropped: no
