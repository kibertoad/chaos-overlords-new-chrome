# Static research and spec checks

Status: maintained canonical procedure

Part of the [validation procedure](../VALIDATION.md).

## Static binary research

`tools/ghidra/` holds bounded, clean-room Ghidra scripts for navigating the
owned executable, and [GHIDRA.md](../GHIDRA.md) documents the headless workflow.
Fingerprint the executable before analysis, work from facts (constants, data
references, branches, state offsets and call relationships), keep neutral names
until behaviour confirms a meaning, and write each result up as a finding in
`spec/findings/` with its addresses, tool version and a way to find the place
again. Decompiler line numbers are never locations. Decompiler output, raw
analysis databases and executable material stay outside the repository. The
static work still open is listed in
[static_validation_plan.md](../../static_validation_plan.md).

## Spec checks

`node tools/check-documentation.mjs` runs the toolkit's standard checker
(`@scientific-method/standard-checker`, pinned in the root `package.json`)
with the documentation standard's
[checks](https://dinorefurb.com/documentation-standard/#checks) over `spec/`,
`parity/` and `deviations/`. `PARITY.md` and the indexes in `spec/index/` are
updated on main only, by `.github/workflows/nightly-generated.yml`, and the
check fails a branch that edits them (`--regenerate` writes them). It also
fails when a code comment gives an address, or the code uses one as a number
or inside a string, that no entry cited by the comment on its line or the
nearest comment above it records, in its locations or text or in the
evidence of an entry it cites, and when a spec line names a file of the
rebuild in `src/`, `tests/` or `multiplayer/`. Comments are read from `.cs`, `.ts`, `.js`, `.mjs` and `.ps1` files, so `//` inside a string or
a regular expression is not a comment and `/* … */` is; a neutral name (`fn_…`,
`g_…`) is always an address, and a plain `0x…` value is one only inside an
image given with `--images` (the script passes the executable's,
`0x00400000..0x004C9000`, from FND-DATA-005), so colours, masks and offsets are
left alone. A range larger than `--max-range` (64 KiB by default), such as a
whole section, records only its two ends, nothing inside it. When a comment
fails, cite the finding that records the address, or write one.
The fast gate runs it with `--check`, and `.githooks/pre-commit` runs it before
each commit once a clone enables the hook. The hook copies the index to a
temporary directory and checks that, so it judges what is being committed, not
unstaged edits, and lets a missing or stale `VALIDATION.md` record through.
It compiles the Kaitai definitions when `kaitai-struct-compiler` (or the path
in `KSC`) is on the path, skips them when it is not, and in CI requires it.
Until the patch tool is published with the standard's spec package, an
experiment that uses a save patch also gives each write as a byte offset and
a value in its Setup section.
