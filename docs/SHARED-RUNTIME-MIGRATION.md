# Shared runtime migration

Validated save promotion, cooperative writer leases and PCM conversion.

Dependencies:

- [Portable paths](https://github.com/kibertoad/refurbished-dinosaurs-toolkit/pull/86)
- [Input snapshots and bindings](https://github.com/kibertoad/refurbished-dinosaurs-toolkit/pull/87)
- [Recoverable persistence](https://github.com/kibertoad/refurbished-dinosaurs-toolkit/pull/88)
- [PCM/audio and streaming WAVE](https://github.com/kibertoad/refurbished-dinosaurs-toolkit/pull/89)

The migration uses `1.1.0-preview.shared-runtime`, built locally from the combined toolkit
candidates. This is a validation-only version and is not available on nuget.org. The PR remains
draft until the required toolkit slices are released. Replace candidate references with the
actual published version, regenerate any NuGet locks against nuget.org, run the canonical fast
gate and mark ready only after those checks pass. Candidate hashes identify local builds only.

Game save payloads, version admission, slot naming, defaults, audio routing, fades, voice limits
and control policies remain local. Synthetic checks establish migration behavior, not parity
with the original game or live device behavior. No original assets entered this change.
