# Building and development

The ready-to-play release bundles its runtimes. These instructions are for source
contributors on **Windows x64**, not steps that every player must complete.

## ROM-free checks

Install the **.NET 10 SDK** and Python 3, then clone the repo:

```powershell
git clone https://github.com/rages4calm/zelda-companion.git
cd zelda-companion
python scripts/audit_public.py --source .
dotnet build Companion/Companion.csproj -c Release
dotnet build Relay/Relay.csproj -c Release
dotnet run --project Tests/Tests.csproj -c Release
```

The workflow runs these checks on Windows without a ROM, generated assets or
private credentials. `Companion.Core` and `Relay` target .NET 10; the UI uses
Windows WinForms. `global.json` allows later .NET 10 feature bands.

## Build a usable development launcher

Extract the clean Windows release into a separate folder **without opening it or
choosing a ROM**. It is the runtime dependency template, including Python/Pillow/
PyYAML extractor dependencies, SDL, shaders and the pinned cloudflared helper.

```powershell
.\scripts\Build.ps1 -RuntimeRoot C:\Tools\ZeldaCompanion-0.3.1-win-x64
```

The script validates the template manifest and refuses initialized templates.
It publishes the launcher and relay self-contained into `.build/` and copies the
clean runtime dependencies into `.build/Companion`. Open
`.build/Companion/Zelda Companion.exe` and provide your own ROM there. This is a
private development installation; do not publish that initialized folder.

Without `-RuntimeRoot`, Build.ps1 compiles the C# software only. That output is
useful for code checks but lacks native game/extractor dependencies. No compiler
or global Python is needed by players using the published clean ZIP.

## Rebuild the native C engines

Use a Windows x64 Tiny C Compiler distribution and the **SDL 2.28.2 VC development
archive**. The SDL folder must contain `include/` and `lib/x64/`, including SDL2.dll.
The code was compiled with the TCC/SDL toolchain described in `UPSTREAMS.txt`.

```powershell
.\scripts\Build.ps1 `
  -RuntimeRoot C:\Tools\ZeldaCompanion-0.3.1-win-x64 `
  -TccRoot C:\Tools\tcc `
  -SdlRoot C:\Tools\SDL2-2.28.2
```

The modified `engine/src/main.c` builds online.exe. The retained pinned original
`engine/src/main-offline.c.txt` builds the solo engine in a separate build copy.
Native builds do not need a ROM. This repo does not provision your compiler/SDK.
The standalone `build-engine.ps1` builds only the online engine.

## Additional local tests

These paths must refer to private, owner-generated data, not committed fixtures:

```powershell
dotnet run --project Tests/Tests.csproj -c Release -- `
  --shuffle C:\PrivateGame\Game\zelda3_assets.dat C:\PrivateTests\shuffle

python run-native-tests.py --tcc-root C:\Tools\tcc `
  --sdl-root C:\Tools\SDL2-2.28.2 --sdl-dll C:\Tools\SDL2-2.28.2\lib\x64\SDL2.dll
```

Windows owned-window diagnostics and focused MSU/parser harnesses are documented
in [USER-GUIDE.md](USER-GUIDE.md#build-and-validation). Some historical native
randomizer/audio validation scripts retain the original repair workspace layout;
they are evidence harnesses, not portable provisioning commands. Adjust their
private paths for your environment and keep results outside tracked source.
Never upload ROMs, generated data, saves or audio into GitHub Actions.

## Package a clean build

Use a fresh uninitialized release template and freshly built C# binaries:

```powershell
python scripts/package.py --runtime-root C:\Tools\ZeldaCompanion-0.3.1-win-x64 --version 0.3.1
```

The destination under `.build/packages` must not already exist. The script copies
runtime files by the template's validated manifest, uses tracked source files,
creates a new SHA-256 manifest and ZIP checksum, and runs the forbidden-payload
audit. It never packages your installed/initialized portable folder. Rebuilt
native binaries, when available, replace the template's corresponding software.

The public v0.3.1 binary release is the tested local build recorded in
`distribution/SHA256-manifest-v0.3.1.json`; byte-for-byte reproducibility across
different compiler/runtime installations is not asserted.
