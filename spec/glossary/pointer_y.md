# pointer_y

The pointer's vertical position on the 640-by-480 screen. A value from outside
the game: taken from the Windows mouse messages; it changes whenever the mouse
moves [FND-UI-032]. The window procedure keeps the client point, y in the high
16 bits, at `0x0049859C` [FND-UI-020].
