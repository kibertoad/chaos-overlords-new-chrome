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
All release installers are currently unsigned.

The Windows installer scans GOG and Windows uninstall records plus common GOG
paths, accepts a manually selected source, and imports the required assets. Its
silent options are `/ORIGINAL="C:\path\to\Chaos Overlords"` and `/NOIMPORT=1`.
Every portable package and installed application includes the filled-in project
`NOTICE` and canonical GPLv3 `LICENSE`; the Windows Setup wizard displays both
before installation.

## GitHub release workflow

Run the manual-only `Release installers` workflow, enter a tag such as `0.1.0`,
and select `windows` or `all`. The default builds Windows x64 only. `all`
requires matching Windows x64, Linux x64, macOS arm64, and macOS x64 artifacts.
The workflow creates the tag and GitHub Release only after tests and all selected
builds succeed. It has no scheduled or push trigger.

## Continuous integration

The manual-only `Continuous integration` workflow runs build, test, startup,
and installer checks on Windows, Linux, and both macOS architectures. Ordinary
pushes do not start it. The separate zizmor workflow runs automatically for
pull requests and can also be invoked manually.
