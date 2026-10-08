# registry_dword

The DWORD a registry value holds. A value from outside the game: read with
`RegQueryValueExA` from `HKLM\SOFTWARE\Stick Man Games\Chaos Overlords\1.0`,
once at startup; `registry_dword[index]` is the value the loader queries
`index`th [FND-OPTIONS-001].
