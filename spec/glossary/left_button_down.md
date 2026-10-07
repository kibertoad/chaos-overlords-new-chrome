# left_button_down

Whether the left mouse button is held. A value from outside the game: taken
from the Windows mouse messages the event pump receives; it changes whenever
the player presses or releases the button [FND-UI-032]. The window procedure
keeps it in the byte `0x004985A4` [FND-UI-020].
