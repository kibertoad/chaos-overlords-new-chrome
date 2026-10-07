# serial_number

The number the game keeps as its serial number. Any other value the game
keeps: a DWORD at `0x00487870`, initialized to 0 and read from the registry
value `serialNum`; when it is 0 after the read, two draws build a new one that
stays in memory for the session [FND-OPTIONS-001, FND-OPTIONS-003].
