# Building and releasing installers

## The version number

`version.txt` in the repository root holds the released version as a single
`x.y.z` line. It is the only place the number is written down: the build stamps
it onto every assembly, the game prints it on the title screen and in every bug
report, crash log, and diagnostics export, and the installer scripts name their
output after it. A build fails when the file is missing or holds anything other
than one three-part version, and the packaging scripts refuse a `-Version` that
disagrees with it, so an installer cannot be named after a version the game
inside it does not report.

Nobody types the next number. The release workflow is asked for a release
*kind* — `patch`, `minor`, or `major` — and works the number out at release
time from the `version.txt` that `main` carries just then: a patch release after
`0.8.8` is `0.8.9`, a minor one `0.9.0`, a major one `1.0.0`. It writes that
number back to `version.txt` on `main` before anything is built and every
release job builds from that commit, so one run both stamps and publishes the
same version. To see what the next release would be called without starting one:

```powershell
./tools/Get-NextVersion.ps1 -Bump patch
```

Two cases are not a plain bump. A version recorded in `version.txt` that carries
no tag never shipped — an earlier run recorded it and then failed, or branch
protection forced the bump to be landed by hand in an ordinary pull request —
so the workflow publishes *that* version rather than a number past it, and the
release kind is ignored for that run; the log says so. And a `version.txt` that
has fallen behind a version already tagged stops the release outright, because
counting on from it would land on a number that is taken: correct `version.txt`
on `main` first.

A packaged build states its own version on Linux and macOS:

```shell
./artifacts/ChaosOverlordsNewChrome-linux-x64/Game/Rechaos.Game --version
```

The Windows executable is a windowed binary and writes nowhere a pipe can see,
so read its stamp instead:

```powershell
(Get-Item ./artifacts/ChaosOverlordsNewChrome-win-x64/Game/Rechaos.Game.exe).VersionInfo.ProductVersion
```

Both packaging scripts already make that check before wrapping a package in an
installer, and the release workflow repeats it against the installed game.

## Local package builds

Create an SDK-free Windows package with:

```powershell
./tools/Publish-Windows.ps1
```

Build the versioned Windows installer with the pinned Inno Setup 7.1.0 compiler:

```powershell
./tools/Build-WindowsInstaller.ps1
```

Build Linux x64 and macOS arm64/x64 installers on their native hosts:

```powershell
./tools/Build-LinuxInstaller.ps1
./tools/Build-MacInstaller.ps1 -Runtime osx-arm64
./tools/Build-MacInstaller.ps1 -Runtime osx-x64
```

Each script takes its version from `version.txt`. Passing `-Version` is allowed
only to restate the recorded number; to build a different one, edit
`version.txt` so the package and the game inside it agree.

The Linux `.deb` installs launch and import commands. The macOS `.pkg` installs
an application bundle containing the game, extractor, and asset-import helper.
Linux and macOS release installers are currently unsigned.

The Windows installer scans GOG and Windows uninstall records plus common GOG
paths, accepts a manually selected source, and imports the required assets. Its
silent options are `/ORIGINAL="C:\path\to\Chaos Overlords"` and `/NOIMPORT=1`.
Every portable package and installed application includes the filled-in project
`NOTICE` and canonical GPLv3 `LICENSE`; the Windows Setup wizard displays both
before installation.

## GitHub release workflow

Run the manual-only `Release installers` workflow, choose how far the version
advances — `patch` (the default), `minor`, or `major` — and select `windows`,
`no-mac-x64`, or `all`. The workflow settles the version as described above,
records it in `version.txt` on `main`, and builds every installer from that
commit, which is also the commit the release tag ends up pointing at. Releases
run one at a time, since two started together would compute the same number. The
default builds Windows x64 only; `no-mac-x64` adds Linux x64 and macOS arm64,
while `all` also adds macOS x64.
Each preset requires all of its selected artifacts.
The separate `signed release` choice is `no`, `all`, or `windows only`, with
`windows only` as the default. Signing
is applied only to installers selected by the installer preset; choosing `all`
never adds a skipped installer. Windows Authenticode signing is currently the
only configured platform signer, so both non-`no` choices sign the selected
Windows installer while Linux and macOS remain unsigned.
The workflow creates the tag and GitHub Release only after tests and all selected
builds succeed. It runs the fast validation tier; the repeated long-running AI
campaign matrix is exercised by the daily `Nightly observable AI campaigns`
workflow and remains available through manual dispatch. Installer artifacts used
to assemble the release are retained in Actions for one day; the durable downloadable
copies are the assets attached to the resulting GitHub Release. The release workflow
has no scheduled or push trigger.

When signing is requested, the Windows release job uses the `release-signing` GitHub environment and
SSL.com eSigner to Authenticode-sign the project executables before Inno Setup
packages them, then signs the completed installer. Configure these environment
secrets before running a release:

- `ES_USERNAME`: SSL.com account username.
- `ES_PASSWORD`: SSL.com account password.
- `CREDENTIAL_ID`: eSigner code-signing certificate credential ID.
- `ES_TOTP_SECRET`: OAuth TOTP secret used for unattended signing.

The job uses eSigner's production environment, verifies that every signature is
valid and timestamped, and confirms after installation that the packaged game
retained its signature. Development installers produced locally or by the
continuous-integration workflow remain unsigned.

## Continuous integration

The manual-only `Continuous integration` workflow runs build, test, startup,
and installer checks on Windows, Linux, and both macOS architectures. Ordinary
pushes do not start it. The separate zizmor workflow runs automatically for
pull requests and can also be invoked manually.
