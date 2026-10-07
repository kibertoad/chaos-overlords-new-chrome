# attitude

How each player regards each other player, from -10 to +10; a negative value
makes the observer treat the other player as hostile. Any other value the game
keeps: `INT32LE[36]` at `0x004AB590`, element `observer * 6 + other`
[FND-AI-006]. The resolver `fn_00472775` writes it in three places: the
recovery at `0x0047280B`, the decrement after an attack at `0x00473F7D` and the
decrement at a Control takeover at `0x00475781` [FND-AI-047].
