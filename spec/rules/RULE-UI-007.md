---
id: RULE-UI-007
title: The pointer shape
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-034]
conflicting: []
split_with: []
related: []
---

## Summary

The pointer is always a stock Windows cursor: the arrow, or the hourglass while
the game sets up a city, loads a game or resolves a turn.

## When it runs

At the 35 places that change the pointer: 20 select the arrow and 15 the
hourglass, around the work that runs without taking input.

## Parameters

None.

## Inputs

`pointer_shape`.

## Procedure

```text
define set_pointer(shape, force) -> INT32:
    let cursor_ids: INT32[5] = [0x7F00, 0x7F01, 0x7F03, 0x7F88, 0x7F02]
    if shape != pointer_shape or force != 0:
        pointer_shape = shape
        emit PointerShapeSet(cursor_ids[shape])
    return shape
```

## Outputs

`set_pointer` returns the shape. It emits `PointerShapeSet` with the Windows
cursor ID when the shape changes or `force` is set: 0 the arrow, 1 the I-beam,
2 the cross, 3 the no sign, 4 the hourglass. Only 0 and 4 are ever passed.

## Edge cases

None known.

## What the sources say

None of the sources describes the pointer.

## Differences between builds

None known.

## Open questions

- `pointer_shape` has no recorded address.
- Which calls pass `force`; the calls around setup, loading and resolution are
  known to.
