# Enhancement source and build

This directory contains the active runtime source, opt-in real-player checks, translation data, avatar resource and dnlib patcher for the Windows Materialize 1.78 release. All enhancement source is GPL-3.0, as described in the root LICENSE and NOTICE.md.

## Requirements

- Windows 64-bit, PowerShell, .NET SDK 9 and Windows .NET Framework 4 compiler.
- An original Windows Materialize 1.78 installation from Bounding Box Software. The untouched `Assembly-CSharp.dll` SHA-256 must be `AAB0F80D825B4C2809131F34CA8D75C0BACE623416CE8200BE73E9F36F96DA36`.
- Python 3 and UnityPy 1.25.4: `python -m pip install --target .\tools\unity-assets -r .\tools\requirements.txt`.
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

Run the executable command from the installation directory, and run the Python command from this source directory. Successful runtime completion ends with `COMPLETE failures=0`. Deliberately invalid XML, missing files and failed-save cases produce expected diagnostics. Independent image checks decode exported files, verify packed channels without alpha premultiplication, and verify selected custom and 4K dimensions. Release v1.78-enhanced.7 passed 267 runtime checks, 29 original independent image checks and 6 additional pixel/file checks. Actual UI checks covered per-map tiling, current-map editing, selective export and native Ctrl+N after project-file startup. See the included verification reports.

The release archive excludes personal projects, recent history, diagnostic logs, generated test images and build caches. Chinese UI/docs are supported; public file and directory names use ASCII.

## Shader patching

`tools/patch_shaders.py` updates the Windows 1.78 D3D11 shader programs for edge falloff and diffuse mask powers. It validates untouched asset hashes, preserves Unity program binding tables, replaces only identified upper clamps, retains the lower bound, recomputes the DXBC MD5 variant checksum and asks the Windows D3D compiler to validate each changed program. Ordinary color and PBR output clamps remain.

The build preserves original `resources.assets` and `sharedassets0.assets` alongside the original DLL in `languages/en/`; subsequent builds use these preserved originals. The matching editable ShaderLab source is updated under `Assets/Shaders/`. Existing MonoBehaviour layouts remain unchanged.

The source-cache file `texture_sources.txt` stays on the user's computer and is excluded from release packages. Projects embed reload originals but do not serialize source paths.
