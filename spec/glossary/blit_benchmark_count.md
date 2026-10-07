# blit_benchmark_count

How many identical screen copies the startup benchmark managed in just over one
second. A value from outside the game: an integer measured once at startup by
`fn_00432954` [FND-UI-011]; it depends on the speed of the machine and sets the
panel slide step. Kept as a DWORD at `0x004981F8`, written once at
`0x00461313` [FND-UI-023].
