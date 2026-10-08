# app_deactivated

Whether the program has lost the focus and has not been activated again. Any
other value the game keeps: a byte at `0x00487890`, set by the deactivation
event and cleared by the activation event in the event step `fn_00462579`
[FND-UI-020].
