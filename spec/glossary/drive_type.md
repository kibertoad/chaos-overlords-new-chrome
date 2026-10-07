# drive_type

`drive_type(letter)` gives the answer of the Windows drive-type query for the
string made of the character `letter` and a backslash, 3 for a fixed drive. A
value from outside the game: `GetDriveTypeA`, called by the startup drive check
[FND-PLATFORM-012].
