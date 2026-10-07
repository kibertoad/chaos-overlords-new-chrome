# turn_end

The last step of `resolution`. Police presence counts down in every sector
(the decrement at `0x00475E74`) [FND-POLICE-001]; then eliminated players are
found (the call of `fn_00476F3B` at `0x00475ECD`) and reported, and then the
end of the match is evaluated (the call of `fn_00476857` at `0x00475F61`)
[FND-TURN-003, FND-TURN-008].
