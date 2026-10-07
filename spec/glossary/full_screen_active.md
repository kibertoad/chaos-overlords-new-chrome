# full_screen_active

Whether this run uses full screen. Any other value the game keeps: a byte at
`0x00498354`, copied from `pref_full_screen` when the options are read at
start and not changed afterwards; the Full Screen menu item changes only the
preference. Before `WinMain` runs, the static initializer `fn_00460CA0` copies
the preference's image value 1 into it [FND-PLATFORM-009, FND-GFX-004,
FND-UI-020, FND-UI-028].
