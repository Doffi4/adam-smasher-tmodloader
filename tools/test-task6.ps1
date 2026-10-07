param([switch]$SkipHarnessBuild, [int]$TimeoutSeconds = 60)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runtime = Join-Path $workspace 'lab/runtime'
$profile = Join-Path $workspace 'lab/profile'
$mods = Join-Path $profile 'Mods'
$harness = Join-Path $workspace 'verification/Task6Harness'
$env:APPDATA = Join-Path $workspace 'lab/appdata'
$env:DOTNET_CLI_HOME = Join-Path $workspace 'lab/dotnet-home'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:ADAM_SMASHER_LAB_ROOT = $workspace
$enabledPath = Join-Path $mods 'enabled.json'
$previousEnabled = [IO.File]::ReadAllText($enabledPath)
$process = $null
try {
if (!$SkipHarnessBuild) {
    & dotnet build (Join-Path $harness 'Task6Harness.csproj') '-p:BuildMod=false' "-p:RestoreConfigFile=$(Join-Path $workspace 'NuGet.Config')" --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Harness compilation failed' }
    Push-Location $runtime
    try {
        & dotnet tModLoader.dll -server -nosteam -steamworkshopfolder none -tmlsavedirectory $profile -modpath $mods -build $harness -eac (Join-Path $harness 'bin/Debug/net8.0/Task6Harness.dll')
        if ($LASTEXITCODE -ne 0) { throw 'Harness packaging failed' }
    } finally { Pop-Location }
}
    Set-Content -LiteralPath $enabledPath -Value '["CalamityModMusic","CalamityMod","AdamSmasher","Task6Harness"]'
    $world = Join-Path $profile 'Worlds/Task1Lab5.wld'
    $arguments = @('tModLoader.dll', '-server', '-nosteam', '-noupnp', '-ip', '127.0.0.1', '-steamworkshopfolder', 'none', '-tmlsavedirectory', ('"' + $profile + '"'), '-modpath', ('"' + $mods + '"'), '-world', ('"' + $world + '"'), '-port', '7782')
    $process = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList $arguments -WorkingDirectory $runtime -WindowStyle Hidden -RedirectStandardOutput (Join-Path $workspace 'verification/task6-server.stdout.txt') -RedirectStandardError (Join-Path $workspace 'verification/task6-server.stderr.txt') -PassThru
    Write-Host "Task6 lab PID $($process.Id)"
    if (!$process.WaitForExit($TimeoutSeconds*1000)) { throw "Task6 lab timeout PID $($process.Id)" }
    Copy-Item -LiteralPath (Join-Path $runtime 'tModLoader-Logs/server.log') -Destination (Join-Path $workspace 'verification/task6-server.log') -Force
    $resultName=if($env:ADAM_SMASHER_LAB_MODE -eq 'load') {'task6-smoke-runtime.txt'} else {'task6-combat-runtime.txt'}
    if (Test-Path (Join-Path $workspace "verification/$resultName")) { Get-Content (Join-Path $workspace "verification/$resultName") }
    if ($process.ExitCode -ne 0) { throw "Task6 failed exit $($process.ExitCode)" }
} finally {
    if ($process -and !$process.HasExited) { Stop-Process -Id $process.Id -Force }
    if ($process) { Write-Host "Exact PID $($process.Id) cleanup: exited=$($process.HasExited)"; $process.Dispose() }
    [IO.File]::WriteAllText($enabledPath, $previousEnabled)
}
