# Continuous integration and packaging

Status: maintained canonical procedure

Part of the [validation procedure](../VALIDATION.md).

The manually dispatched continuous-integration workflow runs the [local checks](local-checks.md)
on Windows x64, Linux x64, macOS arm64, and macOS x64. It also publishes with
`IncludeOriginalAssets=false`, rejects any resulting `Assets` directory, and
runs the published `--smoke-test` entry point without an original asset pack.
Disposable installer artifacts from this workflow are retained for one day so routine validation
does not consume the repository's Actions storage for the default multi-month retention window.
After all four platforms pass, its Windows packaging job builds the clean-room
self-contained package and installer, installs with `/NOIMPORT=1`, launches the
packaged `--platform-smoke-test` entry point, uninstalls it, and uploads both
artifacts. The Windows publisher and installer gate require adjacent SDL2 and
OpenAL libraries so a metadata-only smoke test cannot mask a real launch
failure. Ordinary pushes do not dispatch this workflow.

The first complete hosted run of this matrix and installer path was GitHub
Actions run `34400362789` on 2026-09-09. The latest recorded clean-room matrix
and installer run is `34403047147`, with zizmor run `34403047115`; every
selected platform, packaging, and audit job passed. Hosted runs remain the
authority for runner-specific compatibility.

Linux and macOS installer builders run only on matching native hosted runners.
The Linux gate opens the generated `.deb` and smoke-runs its installed-layout
executable. Each macOS gate validates the generated plist, smoke-runs the app
bundle executable, builds the `.pkg`, expands it again, and confirms the game
payload. Windows additionally exercises silent install and uninstall. The
manual release gate signs and verifies Windows executables and the installer
through SSL.com eSigner, and signs the Linux `.deb` with a detached OpenPGP
signature after checking the signing configuration up front and before building;
that signature is verified against the expected key fingerprint, and rejected if
the key has been revoked or has expired, before upload;
local and continuous-integration packages remain unsigned, and macOS signing and
notarization remain deliberately out of scope.

GitHub Actions dependencies are pinned to immutable commits corresponding to
their documented latest releases. `.github/workflows/zizmor.yml` uses the
official zizmor action in non-Advanced-Security auditor mode, causing any
finding to fail its pull-request check. It also supports manual dispatch and
does not run on pushes.
