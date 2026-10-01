# Test audio

`silence.ogg` is synthesized silence from the native playback smoke test. It
contains no original-game content. The soundtrack regression test opens it as
three separate songs to exercise native completion and thread ownership.

The validation gate starts the test process with `ALSOFT_DRIVERS=null`, so native
completion is exercised without audio hardware. When invoking the tests directly,
set that variable in the parent shell before starting `dotnet test`.

long-silence.ogg is 2.5 seconds of synthesized stereo silence at 44,100 Hz. The EOF regression uses it to require streaming beyond the initially prepared buffer. It contains no original-game content.

Generation: ffmpeg -f lavfi -i anullsrc=r=44100:cl=stereo -t 2.5 -c:a libvorbis -q:a 0 long-silence.ogg
