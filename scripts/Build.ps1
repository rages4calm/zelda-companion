param(
    [string]$RuntimeRoot,
    [string]$TccRoot,
    [string]$SdlRoot
)
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$outputRoot=Join-Path $projectRoot '.build'
if($RuntimeRoot){$RuntimeRoot=(Resolve-Path -LiteralPath $RuntimeRoot).Path}
if($TccRoot){$TccRoot=(Resolve-Path -LiteralPath $TccRoot).Path}
if($SdlRoot){$SdlRoot=(Resolve-Path -LiteralPath $SdlRoot).Path}
Push-Location $projectRoot
try {
    & dotnet publish Companion/Companion.csproj -c Release -r win-x64 --self-contained true -o .build/Companion
    if($LASTEXITCODE -ne 0){throw 'Companion publish failed'}
    & dotnet publish Relay/Relay.csproj -c Release -r win-x64 --self-contained true -o .build/Relay
    if($LASTEXITCODE -ne 0){throw 'Relay publish failed'}
    if($RuntimeRoot){
        $runtime=(Resolve-Path -LiteralPath $RuntimeRoot).Path
        if(Test-Path -LiteralPath (Join-Path $runtime 'UserData')){throw 'Use a fresh, uninitialized release folder for runtime dependencies'}
        if(Test-Path -LiteralPath (Join-Path $runtime 'Game/zelda3_assets.dat')){throw 'Runtime template contains generated game data'}
        $manifest=Get-Content -LiteralPath (Join-Path $runtime 'SHA256-manifest.json') -Raw | ConvertFrom-Json
        foreach($entry in $manifest.PSObject.Properties){
            $file=Join-Path $runtime $entry.Name
            if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value){throw "Runtime template changed: $($entry.Name)"}
        }
        foreach($folder in @('Game','Tools')){
            $target=Join-Path $outputRoot "Companion/$folder"
            New-Item -ItemType Directory -Path $target -Force | Out-Null
            Get-ChildItem -LiteralPath (Join-Path $runtime $folder) -Recurse -File | ForEach-Object {
                $relative=$_.FullName.Substring((Join-Path $runtime $folder).Length+1)
                $destination=Join-Path $target $relative
                New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
                Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
            }
        }
    }
    if($TccRoot -or $SdlRoot){
        if(-not $TccRoot -or -not $SdlRoot){throw 'Native rebuild requires both TccRoot and SdlRoot'}
        & (Join-Path $projectRoot 'build-engine.ps1') -TccRoot $TccRoot -SdlRoot $SdlRoot
        $tcc=(Resolve-Path -LiteralPath (Join-Path $TccRoot 'tcc.exe')).Path
        $sdl=(Resolve-Path -LiteralPath $SdlRoot).Path
        $offlineRoot=Join-Path $outputRoot ('native-offline-'+[guid]::NewGuid().ToString('N'))
        Copy-Item -LiteralPath (Join-Path $projectRoot 'engine') -Destination $offlineRoot -Recurse
        Copy-Item -LiteralPath (Join-Path $offlineRoot 'src/main-offline.c.txt') -Destination (Join-Path $offlineRoot 'src/main.c') -Force
        Push-Location $offlineRoot
        try {
            & $tcc -o zelda3.exe -DCOMPILER_TCC=1 -DSTBI_NO_SIMD=1 -DHAVE_STDINT_H=1 -D_HAVE_STDINT_H=1 -DSYSTEM_VOLUME_MIXER_AVAILABLE=0 ('-I'+$sdl+'/include') ('-L'+$sdl+'/lib/x64') -lSDL2 -lws2_32 -lgdi32 -luser32 -I. src/*.c snes/*.c third_party/gl_core/gl_core_3_1.c third_party/opus-1.3.1-stripped/opus_decoder_amalgam.c
            if($LASTEXITCODE -ne 0){throw 'Offline engine build failed'}
        } finally {Pop-Location}
        $game=Join-Path $outputRoot 'Companion/Game';New-Item -ItemType Directory -Path $game -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $projectRoot 'engine/online.exe') -Destination (Join-Path $game 'online.exe') -Force
        Copy-Item -LiteralPath (Join-Path $offlineRoot 'zelda3.exe') -Destination (Join-Path $game 'zelda3.exe') -Force
        Copy-Item -LiteralPath (Join-Path $sdl 'lib/x64/SDL2.dll') -Destination (Join-Path $game 'SDL2.dll') -Force
    }
    Write-Output "Build complete: $outputRoot/Companion. A playable development folder also requires the clean runtime template."
} finally {Pop-Location}
