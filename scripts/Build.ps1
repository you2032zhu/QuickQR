[CmdletBinding()]
param([switch]$Interactive)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    dotnet restore QuickQR.sln --configfile NuGet.Config --locked-mode -m:1
    if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed.' }
    dotnet build QuickQR.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    $testArgs = @('QuickQR.Tests/bin/Release/net10.0-windows/QuickQR.Tests.dll')
    if ($Interactive) { $testArgs += @('--clipboard', '--windows') }
    dotnet @testArgs
    if ($LASTEXITCODE -ne 0) { throw 'Automated tests failed.' }
} finally { Pop-Location }
