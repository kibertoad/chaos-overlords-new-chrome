# loaded_game_kind

Which kind of saved file the last load read: 0 none, 1 an `S40W` local game,
2 an `N40W` network game, 3 an `M10W` file. Any other value the game keeps: a
DWORD at `0x0048788C`, set by File, Open and by the window procedure for a file
named on the command line, and cleared by the title loop after it acts on it
[FND-PLATFORM-009, FND-UI-020, FND-SAVE-001].
