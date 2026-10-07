param([string]$TmlInstall = $env:TML_PATH)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = Join-Path $workspace 'AdamSmasher'
$runtime = Join-Path $workspace 'lab/runtime'
$profile = Join-Path $workspace 'lab/profile'
$mods = Join-Path $profile 'Mods'
$dist = Join-Path $workspace 'dist'
$environmentNames = @('DOTNET_CLI_HOME','NUGET_PACKAGES','APPDATA','DOTNET_SKIP_FIRST_TIME_EXPERIENCE','DOTNET_GENERATE_ASPNET_CERTIFICATE','DOTNET_CLI_TELEMETRY_OPTOUT')
$previousEnvironment = @{}
foreach ($name in $environmentNames) { $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
$env:DOTNET_CLI_HOME = Join-Path $workspace 'lab/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $workspace 'lab/nuget'
$env:APPDATA = Join-Path $workspace 'lab/appdata'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& dotnet build (Join-Path $source 'AdamSmasher.csproj') '-p:BuildMod=false' "-p:TmlInstall=$TmlInstall" "-p:RestoreConfigFile=$(Join-Path $workspace 'NuGet.Config')" --nologo
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $LASTEXITCODE" }
if (!(Test-Path -LiteralPath (Join-Path $runtime 'tModLoader.dll'))) { & (Join-Path $PSScriptRoot 'prepare-lab.ps1') -TmlInstall $TmlInstall }
# Verified in local tML sources: -build, -eac, -modpath and -tmlsavedirectory.
# Logging uses relative tModLoader-Logs, so the CWD MUST be this runtime copy.
Push-Location $runtime
try {
    & dotnet 'tModLoader.dll' -server -nosteam -steamworkshopfolder none -tmlsavedirectory $profile -modpath $mods -build $source -eac (Join-Path $source 'bin/Debug/net8.0/AdamSmasher.dll')
    if ($LASTEXITCODE -ne 0) { throw "Packaging failed: $LASTEXITCODE" }
} finally { Pop-Location }
[IO.Directory]::CreateDirectory($dist) | Out-Null
Copy-Item -LiteralPath (Join-Path $mods 'AdamSmasher.tmod') -Destination (Join-Path $dist 'AdamSmasher.tmod') -Force
Write-Host "Package: $(Join-Path $dist 'AdamSmasher.tmod')"
} finally {
    foreach ($name in $environmentNames) {
        if ($null -eq $previousEnvironment[$name]) { Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue }
        else { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process') }
    }
}
