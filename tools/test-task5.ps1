param([switch]$SkipProductionBuild)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runtime = Join-Path $workspace 'lab/runtime'
$profile = Join-Path $workspace 'lab/profile'
$mods = Join-Path $profile 'Mods'
$harness = Join-Path $workspace 'verification/Task5Harness'
$env:APPDATA = Join-Path $workspace 'lab/appdata'
$env:DOTNET_CLI_HOME = Join-Path $workspace 'lab/dotnet-home'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:ADAM_SMASHER_LAB_ROOT = $workspace

if (!$SkipProductionBuild) { & (Join-Path $PSScriptRoot 'build.ps1') }
& dotnet build (Join-Path $harness 'Task5Harness.csproj') '-p:BuildMod=false' "-p:RestoreConfigFile=$(Join-Path $workspace 'NuGet.Config')" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Harness compilation failed' }
Push-Location $runtime
try {
    & dotnet tModLoader.dll -server -nosteam -steamworkshopfolder none -tmlsavedirectory $profile -modpath $mods -build $harness -eac (Join-Path $harness 'bin/Debug/net8.0/Task5Harness.dll')
    if ($LASTEXITCODE -ne 0) { throw 'Harness packaging failed' }
} finally { Pop-Location }
$enabledPath = Join-Path $mods 'enabled.json'
$previousEnabled = [IO.File]::ReadAllText($enabledPath)
$process = $null
try {
    Set-Content -LiteralPath $enabledPath -Value '["CalamityModMusic","CalamityMod","BossChecklist","AdamSmasher","Task5Harness"]'
    $world = Join-Path $profile 'Worlds/Task1Lab5.wld'
    if (!(Test-Path -LiteralPath $world)) { throw 'Existing isolated world missing' }
    $arguments = @('tModLoader.dll', '-server', '-nosteam', '-noupnp', '-ip', '127.0.0.1', '-steamworkshopfolder', 'none', '-tmlsavedirectory', ('"' + $profile + '"'), '-modpath', ('"' + $mods + '"'), '-world', ('"' + $world + '"'), '-port', '7781')
    $process = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList $arguments -WorkingDirectory $runtime -WindowStyle Hidden -RedirectStandardOutput (Join-Path $workspace 'verification/task5-server.stdout.txt') -RedirectStandardError (Join-Path $workspace 'verification/task5-server.stderr.txt') -PassThru
    Write-Host "Task5 lab PID $($process.Id)"
    if (!$process.WaitForExit(60000)) { throw "Task5 lab timeout PID $($process.Id)" }
    Copy-Item -LiteralPath (Join-Path $runtime 'tModLoader-Logs/server.log') -Destination (Join-Path $workspace 'verification/task5-server.log') -Force
    $resultName = 'task5-runtime.txt'
    Get-Content (Join-Path $workspace "verification/$resultName")
    if ($process.ExitCode -ne 0) { throw "Task5 failed exit $($process.ExitCode)" }
} finally {
    if ($process -and !$process.HasExited) { Stop-Process -Id $process.Id -Force }
    if ($process) { $process.Dispose() }
    [IO.File]::WriteAllText($enabledPath, $previousEnabled)
}



