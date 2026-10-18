[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][string]$IsccPath,
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/installer-unverified')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot -Parent
[xml]$props = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
$version = [string]$props.Project.PropertyGroup.Version
$PublishDirectory = (Resolve-Path -LiteralPath $PublishDirectory).Path
$IsccPath = (Resolve-Path -LiteralPath $IsccPath).Path
if (-not (Test-Path -LiteralPath (Join-Path $PublishDirectory 'QuickQR.exe'))) { throw 'Publish folder does not contain QuickQR.exe.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
& $IsccPath "/DAppVersion=$version" "/DPublishDirectory=$PublishDirectory" "/DOutputDirectory=$OutputDirectory" (Join-Path $repoRoot 'packaging/QuickQR.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
Write-Warning 'This candidate must pass normal-user install/uninstall tests before it can be uploaded to Releases.'
