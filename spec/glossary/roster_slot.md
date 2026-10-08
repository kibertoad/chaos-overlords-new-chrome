# roster_slot

A gang's position, 0 to 80, among its player's 81 elements of `gangs`. It
does not change while the gang lives. Slot 0 holds the player's Right Hands
[FND-TURN-003]. A hire copies the new gang into the first free slot of the
hiring player from 0 to 79, so slot 0 is reused once the Right Hands are dead
[FND-TURN-005, FND-HIRE-001, FND-TURN-008]. Slot 80 never holds a gang: the
command bar uses it as scratch space while a sector-wide order is chosen
[FND-TURN-009].
