param([Parameter(Mandatory=$true)][string]$TccRoot,[Parameter(Mandatory=$true)][string]$SdlRoot)
$ErrorActionPreference='Stop'
$taskTcc=(Resolve-Path -LiteralPath (Join-Path $TccRoot 'tcc.exe')).Path
$taskSDL=(Resolve-Path -LiteralPath $SdlRoot).Path
Push-Location (Join-Path $PSScriptRoot 'engine')
try {
    & $taskTcc -o online.exe -DCOMPILER_TCC=1 -DSTBI_NO_SIMD=1 -DHAVE_STDINT_H=1 -D_HAVE_STDINT_H=1 -DSYSTEM_VOLUME_MIXER_AVAILABLE=0 ('-I'+$taskSDL+'/include') ('-L'+$taskSDL+'/lib/x64') -lSDL2 -lws2_32 -lgdi32 -luser32 -I. src/*.c snes/*.c third_party/gl_core/gl_core_3_1.c third_party/opus-1.3.1-stripped/opus_decoder_amalgam.c
    if ($LASTEXITCODE -ne 0) {throw 'Native build failed'}
} finally {Pop-Location}
