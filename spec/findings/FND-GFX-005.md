---
id: FND-GFX-005
title: The size the executable passes for each numbered image matches the file data except PX06008, which it reads as 242 by 158
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464108..0x0046428F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004273D5..0x0042773D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042C53B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042D045
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045060C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045075D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045EB75
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges of the functions named here are in FND-EXE-004.

`fn_00464108(surface, number, r1, r2)` calls the numeric loader
`fn_00464155` (FND-PLATFORM-002) and, when it returns 0, beeps
(`fn_00449C8E`) and shows dialog 137 through `fn_00465CEC`; it then returns
and the caller goes on. `fn_00464155` passes the two rectangle dwords on to
`fn_004273D5`, which sets the bitmap width to the difference of the high
words (right minus left) and the height to the difference of the low words
(bottom minus top), reads `bfSize - bfOffBits` bytes of pixel data from the
file into a block of that size, uploads `height` rows with `SetDIBits` and
copies the result into the rectangle of the destination surface. The callers
build the rectangle with `fn_00425EDF(top, left, bottom, right)`.

`fn_00464108` has 97 call sites. The image numbers and rectangles they pass,
read from the pushed constants (a number written as `N + x` is computed at
the call from `x`):

| Images | Width x height | Rectangle (top, left, bottom, right) | Call sites |
|---|---|---|---|
| `PX00100` | 640 x 460 | 0, 0, 460, 640 | `0x00464DF6` |
| `PX00128`, `PX00130`, `PX00131`, `PX00143` to `PX00146` | 640 x 460 | 0, 0, 460, 640 | `0x00418F56`, `0x0042CB7C`, `0x004615CB` and seven more for `PX00130`, `0x0046542F`, `0x0040E150`, `0x00467B06`, `0x0040BC2E`, `0x00457295` |
| `PX00129` | 512 x 646 | 0, 0, 646, 512 | `0x0046162C` |
| `PX00132` | 108 x 164 | 100, 266, 264, 374 | `0x004397B4` |
| `PX00137`, `PX00139` | 220 x 72 | 0, 0, 72, 220 | `0x0040CFDB`, `0x0040D7FA`, `0x0046D33F`, `0x0046A90C` |
| `PX00138` | 720 x 48 | 72, 0, 120, 720 | `0x0040D023`, `0x0040D836`, `0x0046D387`, `0x0046A960` |
| `PX00140` | 312 x 282 | 0, 0, 282, 312 | `0x0045724A`, `0x0040BA9B`, `0x0040E105`, `0x00467ABB` |
| `PX00150` | 220 x 56 | 0, 0, 56, 220 | `0x004128A8`, `0x00448ED5` |
| `PX00200` | 428 x 410 | 25, 106, 435, 534 | `0x0042C4F0`, `0x0042CEB0` |
| `PX00201` | 320 x 240 | 0, 0, 240, 320 | `0x0042BDF7`, `0x0042C786` |
| `PX00202`, `PX00203` | 311 x 393 | 30, 110, 423, 421 | `0x0042D045`, `0x0042C53B` |
| `PX00300` | 324 x 64 | 0, 0, 64, 324 | `0x0043033A` |
| `PX02000` | 120 x 1408 | 0, 0, 1408, 120 | `0x00418FD1` |
| `PX03000` | 640 x 576 | 0, 0, 576, 640 | `0x00418F95`, `0x0046501C` |
| `PX04000 + item` | 720 x 48 | 0, 48 or 96 to 48 more, 0 to 720; one at 353 | 20 sites in `fn_0042EE46`, `fn_0042F98B`, `fn_00443BBD`, `fn_00445A4F`, `fn_00449E80`, `fn_0044B699`, `fn_0044FD6C` |
| `PX04999` | 20 x 1280 | 0, 120, 1280, 140 | `0x00419010` |
| `PX05000` to `PX05022`, `PX05024` | 344 x 209 | 144, 0 or 344, 353, 344 or 688 | 25 sites, one image number each |
| `PX05023` | 344 x 209 | 144, 0, 353, 344 | `0x0045EB75` |
| `PX06004`, `PX06005` | 242 x 158 | 144, 344, 302, 586 and 155, 94, 313, 336 | `0x0045037B`, `0x00450488` |
| `PX06000 + kind` | 242 x 158 | 155, 94, 313, 336 | `0x0045060C`, `0x0045075D` |
| `PX07000 + n`, `PX07100 + n`, `PX07200 + n`, `PX07300 + n`, `PX07228`, `PX07301`, `PX07320` | 512 x 64 | 0 or 64, 0, 64 or 128, 512 | `fn_0042E040`: `0x0042E81A`, `0x0042E875`, `0x0042EB49`, `0x0042EBA7`, `0x0042ECD5`, `0x0042EC7E`, `0x0042EC25` |
| `PX10000 + map`, `PX10001 + player` | 432 x 416 | 0, 0, 416, 432 | `0x00439637`, `0x004124C0` |

- `PX06000 + kind` is loaded by `fn_0044FD6C` for a Last Turn report whose
  kind is neither 4 nor 5 (those two load `PX06004` and `PX06005`) nor 9
  (which loads `PX06009` through the first of the two calls). Every `PX06`
  image, `PX06008` included, is therefore read at 242 by 158.
- `PX05023` is loaded only when the byte `0x0048735C` is nonzero
  (`0x0045EB22`); no instruction writes that byte, and it is 0 in the file.
  No `PX05023` file exists in either image set.
- `PX00131` exists only in `DATA/PX16/`; at 8 bits its load fails and shows
  dialog 137.

Against the file data (FND-GFX-003): every size above equals the width and
height the files' pixel data give, apart from `PX06008`, whose data holds 241
by 157. Its `DATA/PX16` file has `image_size` 75,988, which is 157 rows of 484
bytes; a 242-pixel row is also 484 bytes, so the rows line up, the extra
column is the row's two padding bytes, and the 158th row, the top row of the
picture, is read from the 484 bytes after the end of the block the loader
allocated. Its `DATA/PX08` file's RLE8 data codes 157 lines of 241 pixels
and then ends.

## Interpretation

The images carry no size, and the sizes in the code agree with the files
everywhere except `PX06008`. There the original draws one column and one row
more than the picture holds: at 16 bits the extra column is black (the padding
bytes are 0) and the top row shows whatever memory follows the pixel data; at
8 bits the extra pixels are the ones the RLE8 data never sets. `PX05023` is
never loaded because its branch is dead.

## Alternatives

What `SetDIBits` leaves in the pixels an RLE8 bitmap does not set, and what the
memory after the 16-bit block holds, are not settled by reading the
executable; a run of the original at each depth would show the drawn row and
column. The report kinds that reach `PX06000 + kind` are listed by the event
findings, not here.

## How to reproduce

List the calls of `fn_00464108` and read, before each, the image number pushed
second and the four values pushed to the most recent `fn_00425EDF` call, whose
result is pushed as the last two arguments. The branch that loads `PX06000 +
kind` is in `fn_0044FD6C` after the load of the report kind at `0x004500FF`.
