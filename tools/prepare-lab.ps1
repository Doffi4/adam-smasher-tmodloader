param([string]$TmlInstall = $env:TML_PATH,
      [string]$CalamityPackage = $env:CALAMITY_MOD_PATH,
      [string]$CalamityMusicPackage = $env:CALAMITY_MUSIC_PATH)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$lab = Join-Path $workspace 'lab'
$runtime = Join-Path $lab 'runtime'
$mods = Join-Path $lab 'profile/Mods'
foreach ($path in @($runtime, $mods, (Join-Path $lab 'profile/Worlds'), (Join-Path $lab 'profile/Players'))) {
    [IO.Directory]::CreateDirectory($path) | Out-Null
}
# No symlinks/junctions: every writable runtime file has its own local copy.
foreach ($name in @('tModLoader.dll', 'tModLoader.deps.json', 'tModLoader.runtimeconfig.json', 'tModLoader.runtimeconfig.dev.json')) {
    Copy-Item -LiteralPath (Join-Path $TmlInstall $name) -Destination (Join-Path $runtime $name) -Force
}
if (!(Test-Path -LiteralPath (Join-Path $runtime 'Libraries'))) {
    Copy-Item -LiteralPath (Join-Path $TmlInstall 'Libraries') -Destination $runtime -Recurse
}
Copy-Item -LiteralPath $CalamityPackage -Destination (Join-Path $mods 'CalamityMod.tmod') -Force
Copy-Item -LiteralPath $CalamityMusicPackage -Destination (Join-Path $mods 'CalamityModMusic.tmod') -Force
Write-Host "Isolated runtime: $runtime"
Write-Host "Isolated profile: $(Join-Path $lab 'profile')"
Write-Host 'Runtime/game libraries are private lab files; never redistribute lab/.'
