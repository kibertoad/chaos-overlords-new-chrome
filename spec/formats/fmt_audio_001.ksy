meta:
  id: fmt_audio_001
  title: Sound effect files DATA/SNDnnnnn
  license: MIT
  endian: le
doc: |
  A Chaos Overlords sound effect: a RIFF WAVE file holding one fmt chunk and
  one data chunk of 8-bit mono PCM at 22,050 Hz.
doc-ref: FMT-AUDIO-001, FND-AUDIO-004
seq:
  - id: riff_id
    contents: RIFF
  - id: riff_size
    type: u4
    doc: 36 plus data_size; one less than the RIFF rules ask for when data_size is odd.
  - id: wave_id
    contents: WAVE
  - id: fmt_id
    contents: 'fmt '
  - id: fmt_size
    type: u4
    doc: 16.
  - id: format_tag
    type: u2
    doc: 1, PCM.
  - id: channels
    type: u2
    doc: 1, mono.
  - id: sample_rate
    type: u4
    doc: 22050.
  - id: byte_rate
    type: u4
    doc: 22050.
  - id: block_align
    type: u2
    doc: 1.
  - id: bits_per_sample
    type: u2
    doc: 8.
  - id: data_id
    contents: data
  - id: data_size
    type: u4
    doc: Number of samples.
  - id: samples
    size: data_size
    doc: Unsigned 8-bit PCM samples, 128 being silence.
  - id: pad
    size: 1
    if: data_size % 2 == 1
    doc: RIFF pad byte after an odd-length data chunk.
