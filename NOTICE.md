# Notices

Materialize was created by Bounding Box Software and released under GNU GPL version 3. This fork preserves the original license, source and Git history. Enhancement code and build tools in `Enhancements/` are modifications for the Windows Materialize 1.78 distribution, released under the same GPL-3.0 terms, without warranty.

Enhancement maintainer: DannyAnbo, https://github.com/DannyAnbo

Homepage: https://space.bilibili.com/413324822

The maintainer supplied the avatar for inclusion in the About screen and this distribution. It is a reduced, metadata-stripped version of the supplied image. The avatar is not a general-purpose stock-image asset.

The patch builder uses dnlib 4.5.0 (MIT): https://github.com/0xd4d/dnlib. It is restored from NuGet during a build. Python image verification uses Pillow (HPND) and NumPy (BSD-3-Clause); these verification libraries are not bundled in the portable application.

The portable application includes the existing Unity runtime and original Materialize dependencies, including FreeImage and upstream clipboard helpers. Their original notices/source remain in the upstream project and dependency distributions; this fork does not claim authorship of them. Unity is a trademark of Unity Technologies. The original Windows 1.78 installation is needed to reproduce the patched build; Unity runtime binaries are not compiled by the enhancement build script.
