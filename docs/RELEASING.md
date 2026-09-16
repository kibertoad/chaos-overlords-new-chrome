# Building and releasing installers

## Local package builds

Create an SDK-free Windows package with:

```powershell
./tools/Publish-Windows.ps1
```

Build the versioned Windows installer with the pinned Inno Setup 7.1.0 compiler:

```powershell
./tools/Build-WindowsInstaller.ps1 -Version 0.1.0
```

Build Linux x64 and macOS arm64/x64 installers on their native hosts:

```powershell
./tools/Build-LinuxInstaller.ps1 -Version 0.1.0
./tools/Build-MacInstaller.ps1 -Version 0.1.0 -Runtime osx-arm64
./tools/Build-MacInstaller.ps1 -Version 0.1.0 -Runtime osx-x64
```

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

Run the manual-only `Release installers` workflow, enter a tag such as `0.1.0`,
and select `windows`, `no-mac-x64`, or `all`. The default builds Windows x64 only;
`no-mac-x64` adds Linux x64 and macOS arm64, while `all` also adds macOS x64.
Each preset requires all of its selected artifacts.
The workflow creates the tag and GitHub Release only after tests and all selected
builds succeed. It runs the fast validation tier; the repeated long-running AI
campaign matrix is exercised by the daily `Nightly observable AI campaigns`
workflow and remains available through manual dispatch. Installer artifacts used
to assemble the release are retained in Actions for one day; the durable downloadable
copies are the assets attached to the resulting GitHub Release. The release workflow
has no scheduled or push trigger.

The Windows release job uses the `release-signing` GitHub environment and
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
