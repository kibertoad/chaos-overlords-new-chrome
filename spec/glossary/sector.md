# sector

One of the 64 squares of the city, numbered 0 to 63 row by row, eight to a
row: column `sector % 8`, row `sector / 8` [FND-AI-005, FND-UI-033]. The
sectors adjacent to a sector are the up to eight whose row and column each
differ from its own by at most one, at offsets -9, -8, -7, -1, +1, +7, +8 and
+9, leaving out any that would wrap past the edge of a row or leave the city
[FND-AI-005, FND-MOVE-002]. A structure the game keeps: FMT-STATE-002, kept
as one element of `sectors` [FND-CONTROL-001, FND-UI-035].
