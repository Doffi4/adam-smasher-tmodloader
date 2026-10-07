param([string]$TmlInstall = $env:TML_PATH, [string]$ExistingLabWorld = '')
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runtime = Join-Path $workspace 'lab/runtime'
$profile = Join-Path $workspace 'lab/profile'
$mods = Join-Path $profile 'Mods'
$harness = Join-Path $workspace 'verification/Task1Harness'
$env:APPDATA = Join-Path $workspace 'lab/appdata'
$env:DOTNET_CLI_HOME = Join-Path $workspace 'lab/dotnet-home'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:ADAM_SMASHER_LAB_ROOT = $workspace
& (Join-Path $PSScriptRoot 'build.ps1') -TmlInstall $TmlInstall
& (Join-Path $PSScriptRoot 'prepare-lab.ps1') -TmlInstall $TmlInstall
& dotnet build (Join-Path $harness 'Task1Harness.csproj') '-p:BuildMod=false' "-p:TmlInstall=$TmlInstall" "-p:RestoreConfigFile=$(Join-Path $workspace 'NuGet.Config')" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Harness compilation failed' }
Push-Location $runtime
try {
    & dotnet tModLoader.dll -server -nosteam -steamworkshopfolder none -tmlsavedirectory $profile -modpath $mods -build $harness -eac (Join-Path $harness 'bin/Debug/net8.0/Task1Harness.dll')
    if ($LASTEXITCODE -ne 0) { throw 'Harness packaging failed' }
} finally { Pop-Location }
Set-Content -LiteralPath (Join-Path $mods 'enabled.json') -Value '["CalamityModMusic","CalamityMod","AdamSmasher","Task1Harness"]'
# A unique world/result for each invocation avoids reusing an already defeated world.
$runId = [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
$world = Join-Path $profile "Worlds/Task1-$runId.wld"
if ($ExistingLabWorld) {
    $world = [IO.Path]::GetFullPath($ExistingLabWorld)
    if (!$world.StartsWith($profile + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Existing world must be inside lab/profile' }
}
$result = Join-Path $workspace 'verification/task1-runtime.txt'
if (Test-Path -LiteralPath $result) { Move-Item -LiteralPath $result -Destination (Join-Path $workspace "verification/task1-runtime-$runId.previous.txt") }
for ($pass = 1; $pass -le 2; $pass++) {
    $out = Join-Path $workspace "verification/task1-server-$pass.stdout.txt"
    $err = Join-Path $workspace "verification/task1-server-$pass.stderr.txt"
    $arguments = @('tModLoader.dll', '-server', '-nosteam', '-noupnp', '-ip', '127.0.0.1', '-steamworkshopfolder', 'none', '-tmlsavedirectory', ('"' + $profile + '"'), '-modpath', ('"' + $mods + '"'), '-world', ('"' + $world + '"'), '-autocreate', '1', '-worldname', [IO.Path]::GetFileNameWithoutExtension($world), '-seed', 'SmasherTask1', '-port', '7779')
    $process = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList $arguments -WorkingDirectory $runtime -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err -PassThru
    try {
        if (!$process.WaitForExit(180000)) { throw "Lab server timeout (PID $($process.Id))" }
        if ($process.ExitCode -ne 0) { throw "Lab server failed: $($process.ExitCode), see $out" }
        Copy-Item -LiteralPath (Join-Path $runtime 'tModLoader-Logs/server.log') -Destination (Join-Path $workspace "verification/task1-server-$pass.log") -Force
        if (!(Test-Path -LiteralPath $world)) { throw 'Autocreate did not save the expected world path' }
    } finally {
        if (!$process.HasExited) { Stop-Process -Id $process.Id -Force }
        $process.Dispose()
    }
}
Get-Content -LiteralPath $result
