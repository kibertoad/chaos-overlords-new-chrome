---
id: RULE-UI-007
title: The pointer shape
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-034, FND-UI-020, FND-UI-023]
conflicting: []
split_with: []
related: []
---

## Summary

The pointer is always a stock Windows cursor: the arrow, or the hourglass while
the game sets up a city, loads a game or resolves a turn. Every pointer
message the window receives puts the arrow back.

## When it runs

At the 35 places that change the pointer: 20 select the arrow and 15 the
hourglass, around the work that runs without taking input. The window procedure
also selects the arrow each time Windows asks it for the pointer
(`WM_SETCURSOR`), which Windows does when the pointer moves over the window.

## Parameters

None.

## Inputs

`pointer_shape`.

## Procedure

```text
define set_pointer(shape, force) -> INT32:
    let cursor_ids: INT32[5] = [0x7F00, 0x7F01, 0x7F03, 0x7F88, 0x7F02]
    if shape != pointer_shape or force != 0 or full_screen_active == 0:
        emit PointerShapeSet(cursor_ids[shape])
    return shape

define on_pointer_query():
    set_pointer(0, 0)
```

## Outputs

`set_pointer` returns the shape. It emits `PointerShapeSet` with the Windows
cursor ID unless `shape` equals `pointer_shape` while `force` is 0 and
`full_screen_active` is set: 0 the arrow, 1 the I-beam,
2 the cross, 3 the no sign, 4 the hourglass. Only 0 and 4 are ever passed.

## Edge cases

`pointer_shape` is 99 from the start and nothing writes it, so for the shapes
0 and 4 the test always passes and every call sets the cursor. Because
`on_pointer_query` sets the arrow on each pointer move while messages are
dispatched, the hourglass stays only while the pointer is still or no message
is dispatched.

## What the sources say

None of the sources describes the pointer.

## Differences between builds

None known.

## Open questions

- Which calls pass `force`; the calls around setup, loading and resolution are
  known to. It makes no difference while `pointer_shape` is never written.
- How long the hourglass is actually visible needs a run of the original.
