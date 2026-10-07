#requires -Version 7.2
param([switch]$Build, [string]$TmlInstall=$env:TML_PATH)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dist=Join-Path $workspace 'dist'
if($Build) { & (Join-Path $PSScriptRoot 'build.ps1') -TmlInstall $TmlInstall }
$tmod=Join-Path $dist 'AdamSmasher.tmod'
if (!(Test-Path -LiteralPath $tmod)) { throw 'Build AdamSmasher.tmod first' }
# Same TMOD header/table layout as the installed tML TmodFile reader.
$stream=[IO.File]::OpenRead($tmod); $reader=[IO.BinaryReader]::new($stream)
try {
    if([Text.Encoding]::ASCII.GetString($reader.ReadBytes(4)) -ne 'TMOD') { throw 'Invalid TMOD header' }
    $loader=$reader.ReadString(); $reader.ReadBytes(20)|Out-Null; $reader.ReadBytes(256)|Out-Null; $reader.ReadInt32()|Out-Null
    $name=$reader.ReadString(); $version=$reader.ReadString(); $count=$reader.ReadInt32(); $entries=@()
    for($i=0;$i -lt $count;$i++) { $entries+=$reader.ReadString(); $reader.ReadInt32()|Out-Null; $reader.ReadInt32()|Out-Null }
} finally { $reader.Dispose(); $stream.Dispose() }
if($name -ne 'AdamSmasher') { throw 'Wrong internal mod name' }
foreach($entry in $entries) {
    if($entry -match '(^|/)(lab|verification|Libraries|bin|obj|\.git|\.codex|\.agents)(/|$)|Harness|Recon|\.wld$|\.twld$|\.plr$|\.dll$' -and $entry -ne 'AdamSmasher.dll') { throw "Forbidden TMOD entry: $entry" }
    if($entry.Contains('..') -or $entry.Contains(':')) { throw "Unsafe TMOD entry: $entry" }
}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $workspace 'AdamSmasher') -Filter '*.png' -Recurse -File) {
    $relative=[IO.Path]::GetRelativePath((Join-Path $workspace 'AdamSmasher'),$file.FullName).Replace('\','/')
    $raw=[IO.Path]::ChangeExtension($relative, 'rawimg').Replace('\','/')
    if($relative -notin $entries -and $raw -notin $entries) { throw "Missing packaged texture: $relative" }
}
Copy-Item -LiteralPath (Join-Path $workspace 'README.md') -Destination (Join-Path $dist 'README.md') -Force
if(Test-Path -LiteralPath (Join-Path $workspace 'verification/RESULTS.md')) { Copy-Item -LiteralPath (Join-Path $workspace 'verification/RESULTS.md') -Destination (Join-Path $dist 'RESULTS.md') -Force }
Copy-Item -LiteralPath (Join-Path $workspace 'LICENSE.txt'),(Join-Path $workspace 'ATTRIBUTION.txt') -Destination $dist -Force
if(Test-Path -LiteralPath (Join-Path $workspace 'WEAPONS_UPDATE.md')) { Copy-Item -LiteralPath (Join-Path $workspace 'WEAPONS_UPDATE.md') -Destination $dist -Force }
if(Test-Path -LiteralPath (Join-Path $workspace 'BOSS_UPDATE.md')) { Copy-Item -LiteralPath (Join-Path $workspace 'BOSS_UPDATE.md') -Destination $dist -Force }
$files=@(Get-ChildItem -LiteralPath (Join-Path $workspace 'AdamSmasher') -Recurse -File | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs','.csproj','.png','.hjson','.txt')
})
foreach($relative in @('README.md','LICENSE.txt','ATTRIBUTION.txt','NuGet.Config','tools/build.ps1','tools/prepare-lab.ps1','tools/package.ps1')) { $files+=Get-Item -LiteralPath (Join-Path $workspace $relative) }
if(Test-Path -LiteralPath (Join-Path $workspace 'WEAPONS_UPDATE.md')) { $files+=Get-Item -LiteralPath (Join-Path $workspace 'WEAPONS_UPDATE.md') }
if(Test-Path -LiteralPath (Join-Path $workspace 'BOSS_UPDATE.md')) { $files+=Get-Item -LiteralPath (Join-Path $workspace 'BOSS_UPDATE.md') }
Add-Type -AssemblyName System.IO.Compression
$zipPath=Join-Path $dist 'AdamSmasher-source.zip'
# Replace only the specifically named output inside this workspace.
if(Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath }
$zipStream=[IO.File]::Create($zipPath); $archive=[IO.Compression.ZipArchive]::new($zipStream,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach($file in ($files | Sort-Object FullName)) {
        $relative=[IO.Path]::GetRelativePath($workspace,$file.FullName).Replace('\','/')
        $entry=$archive.CreateEntry($relative,[IO.Compression.CompressionLevel]::Optimal)
        $target=$entry.Open(); $input=[IO.File]::OpenRead($file.FullName)
        try { $input.CopyTo($target) } finally { $input.Dispose(); $target.Dispose() }
    }
} finally { $archive.Dispose(); $zipStream.Dispose() }
$archive=[IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $expected=@($files | ForEach-Object { [IO.Path]::GetRelativePath($workspace,$_.FullName).Replace('\','/') })
    if($archive.Entries.Count -ne $expected.Count) { throw 'ZIP entry count mismatch' }
    foreach($entry in $archive.Entries) {
        if($entry.FullName -notin $expected -or $entry.FullName -match '\.dll$|\.pdb$|\.wld$|\.twld$|\.plr$|(^|/)(lab|verification|bin|obj|\.git)(/|$)') { throw "Forbidden ZIP entry: $($entry.FullName)" }
        $sourceStream=[IO.File]::OpenRead((Join-Path $workspace $entry.FullName)); $entryStream=$entry.Open()
        try {
            $sha=[Security.Cryptography.SHA256]::Create()
            $a=[Convert]::ToHexString($sha.ComputeHash($sourceStream)); $b=[Convert]::ToHexString($sha.ComputeHash($entryStream)); $sha.Dispose()
            if($a -ne $b) { throw "ZIP bytes mismatch: $($entry.FullName)" }
        } finally { $sourceStream.Dispose(); $entryStream.Dispose() }
    }
} finally { $archive.Dispose() }
$hashes=foreach($path in @($tmod,$zipPath)) { $hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant(); "$hash  $([IO.Path]::GetFileName($path))" }
[IO.File]::WriteAllLines((Join-Path $dist 'SHA256SUMS.txt'),$hashes)
Write-Host "PASS: TMOD $name $version / tML $loader; $count entries; textures present; own DLL only"
Write-Host "PASS: source ZIP $($files.Count) allowlisted files; decompressed byte hashes equal source; no game DLLs/lab/saves/test harness"
foreach($path in @($tmod,$zipPath)) { Write-Host "$([IO.Path]::GetFileName($path)): $((Get-Item -LiteralPath $path).Length) bytes" }
$hashes | Write-Host
