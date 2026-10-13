# DEV-UI-019

- Departs from: SCR-UI-009, SCR-UI-002, SCR-UI-001, RULE-UI-013
- Replaces: SCR-UI-009
- Reason: The rebuild has no menu bar. Its commands are reached elsewhere: saving, loading and
  quitting from the Escape menu (DEV-UI-011), the options from the Options screen, Help Topics
  with F1 (DEV-HELP-001), full screen with F11 (DEV-OPTIONS-003), and About, which shows the
  credits screen, with Shift+F1. On the title screen, buttons for New Game, Load, Online, Options,
  Help, Intro and Quit stand in for the menu, drawn over the title art with a box for notices left
  by the previous screen. Each button is the face of the setup screen's Begin button, its edges and
  blank inside repeated to the button's size, with its label at the top left in the yellow
  lettering of the setup and console buttons, cut letter by letter from their labels; the Q, which
  none of them has, is their O with a tail. The buttons stand in two stone housings cut from the one that
  holds Begin and Cancel, one around New Game, Load and Online and a wider one under it around the
  bottom row. The buttons and the copyright line of the title art sit 13 pixels left
  of centre, with as much space between the logo and the top row of buttons as between the bottom
  row and the copyright line. The lower right corner carries the Refurbished Dinosaurs logo under
  a "restored by" line, its foot on the copyright line's baseline; the logo turns gold under the
  pointer, and a click on it opens https://dinorefurb.com/ in the player's browser. Ctrl+H and
  Ctrl+J open the Online screen, where hosting and joining happen, and Enter and F9 also start a
  new game and open a saved one. A left press on the title outside the buttons does nothing, where
  the original's title loop takes a press anywhere as New Game.
- Setting: None
- Default: mandatory
- Justification: Every command of the menu bar stays reachable, and the drawing area is drawn
  without the Windows frame above it (DEV-GFX-001), where a menu bar would have no place. The
  title buttons are the only way to Online and Options before a match, and the clicks that skip
  the intro movies land on the title; if a press anywhere started a new game, those clicks would
  carry a player past the menu into setup. New Game stays one button or one key away. The rebuild
  draws none of the original's Windows controls (DEV-UI-029).
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.cs, tests/Rechaos.Tests/GameMenuLayoutTests.cs, tests/Rechaos.Tests/ProgramShellParityTests.cs, tests/Rechaos.Tests/TitleButtonArtTests.cs
- Dropped: no
