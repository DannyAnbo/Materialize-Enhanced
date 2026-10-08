# Enhancement source and build

This directory contains the active runtime source, opt-in real-player checks, translation data, avatar resource and dnlib patcher for the Windows Materialize 1.78 release. All enhancement source is GPL-3.0, as described in the root LICENSE and NOTICE.md.

## Requirements

- Windows 64-bit, PowerShell, .NET SDK 9 and Windows .NET Framework 4 compiler.
- An original Windows Materialize 1.78 installation from Bounding Box Software. The untouched `Assembly-CSharp.dll` SHA-256 must be `AAB0F80D825B4C2809131F34CA8D75C0BACE623416CE8200BE73E9F36F96DA36`.
- Network access for the dnlib 4.5.0 NuGet restore on the first build.

Close Materialize, then run from this directory:

```powershell
.\build.ps1 -InstallDir 'D:\Apps\Materialize_1.78'
.\build.ps1 -InstallDir 'D:\Apps\Materialize_1.78' -Deploy
```

The first command writes the helper and patched English/Chinese assemblies to `artifacts/`. The second deploys them and preserves the untouched main assembly in `languages/en/` when needed. Back up your installation and projects before upgrading. The in-app language switch uses runtime translation and does not restart the player.

`EnhanceBuild.cs` replaces existing method bodies and redirects selected calls. Do not add methods or fields to the original MonoBehaviour types: old Unity scene metadata depends on their layouts and identities. Plain serialized `ProjectObject` gains whitelisted enhancement fields. The runtime helper uses only assemblies supplied by the installed player.

## Verification

Use a disposable installation and output directory. The opt-in check changes the current scene and test configuration; do not run it with an unsaved personal project.

```powershell
.\Materialize.exe --enhance-selftest 'D:\Temp\materialize-check'
python -m pip install Pillow numpy
python .\verify_images.py 'D:\Temp\materialize-check'
```

Run the executable command from the installation directory, and run the Python command from this source directory. Successful runtime completion ends with `COMPLETE failures=0`. Deliberately invalid XML, missing files and failed-save cases produce expected diagnostics. Independent image checks decode exported files, verify packed channels without alpha premultiplication, and verify selected custom and 4K dimensions. Release v1.78-enhanced.6 passed 202 runtime checks and 29 independent image checks, including execution from a Chinese installation directory, actual smoothness generation and Unicode file/directory imports. See `runtime-tests.txt` and `independent-image-verification.json`.

The release archive excludes personal projects, recent history, diagnostic logs, generated test images and build caches. Chinese UI/docs are supported; public file and directory names use ASCII.
