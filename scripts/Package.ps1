[CmdletBinding()]
param(
    [string]$Tag,
    [string]$OutputDirectory,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot -Parent
[xml]$props = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
$version = [string]$props.Project.PropertyGroup.Version
if (-not $Tag) { $Tag = 'v' + $version }
if ($Tag -notmatch '^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') { throw 'Tag must be a SemVer tag, for example v1.0.0.' }
if ($Tag.Substring(1) -cne $version) { throw "Tag $Tag does not match Directory.Build.props version $version." }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts/release' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') }

Add-Type -AssemblyName System.IO.Compression
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$archives = @()
Push-Location $repoRoot
try {
    foreach ($rid in @('win-x64', 'win-arm64')) {
        $name = "QuickQR-$Tag-$rid"
        $publish = Join-Path $OutputDirectory "publish/$name"
        $zipPath = Join-Path $OutputDirectory "$name.zip"
        # Refuse stale output instead of mixing files from different releases.
        if ((Test-Path -LiteralPath $publish) -or (Test-Path -LiteralPath $zipPath)) { throw "Output already exists: $name. Choose a new output directory." }
        dotnet publish QuickQR.Windows/QuickQR.Windows.csproj -c Release -r $rid --self-contained true --no-restore -p:PublishSingleFile=false -p:PublishTrimmed=false -p:UseSharedCompilation=false -o $publish
        if ($LASTEXITCODE -ne 0) { throw "Publish failed for $rid." }
        $config = Get-Content -LiteralPath (Join-Path $publish 'QuickQR.runtimeconfig.json') -Raw | ConvertFrom-Json
        if (-not $config.runtimeOptions.includedFrameworks) { throw 'Release must be self-contained.' }
        foreach ($file in @('QuickQR.exe','QuickQR.dll','coreclr.dll','PresentationFramework.dll','zxing.dll','LICENSE','licenses/Apache-2.0.txt','licenses/dotnet-LICENSE.txt','ThirdPartyNotices.txt')) {
            if (-not (Test-Path -LiteralPath (Join-Path $publish $file) -PathType Leaf)) { throw "Missing release file: $rid/$file" }
        }
        $exeBytes = [IO.File]::ReadAllBytes((Join-Path $publish 'QuickQR.exe'))
        $peOffset = [BitConverter]::ToInt32($exeBytes, 0x3c)
        $machine = [BitConverter]::ToUInt16($exeBytes, $peOffset + 4)
        $expectedMachine = if ($rid -eq 'win-x64') { 0x8664 } else { 0xaa64 }
        if ($machine -ne $expectedMachine) { throw "Incorrect PE architecture: $rid" }
        if ([BitConverter]::ToUInt16($exeBytes, $peOffset + 24 + 68) -ne 2) { throw 'Executable must use the Windows GUI subsystem.' }
        Compress-Archive -LiteralPath $publish -DestinationPath $zipPath -CompressionLevel Optimal
        $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
        try {
            $exeEntry = $archive.Entries | Where-Object FullName -eq "$name/QuickQR.exe"
            if (-not $exeEntry) { throw 'ZIP layout verification failed.' }
            # Read and compare every file, exercising the archive and verifying content hashes.
            foreach ($entry in $archive.Entries) {
                if (-not $entry.Name) { continue }
                $relative = $entry.FullName.Substring($name.Length + 1)
                $entryStream = $entry.Open()
                try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($entryStream)) }
                finally { $entryStream.Dispose() }
                if ($hash -ne (Get-FileHash -LiteralPath (Join-Path $publish $relative) -Algorithm SHA256).Hash) { throw "ZIP content mismatch: $relative" }
            }
        } finally { $archive.Dispose() }
        $archives += Get-Item -LiteralPath $zipPath
    }
    $checksums = @($archives | ForEach-Object { ((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()) + '  ' + $_.Name })
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'SHA256SUMS.txt'), ($checksums -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
    $changelog = Get-Content -LiteralPath (Join-Path $repoRoot 'CHANGELOG.md') -Raw
    $sectionPattern = '(?ms)^## \[' + [regex]::Escape($version) + '\][^\r\n]*\r?\n(.*?)(?=^## |\z)'
    $match = [regex]::Match($changelog, $sectionPattern)
    if (-not $match.Success) { throw 'Add a matching CHANGELOG section before releasing.' }
    $changes = $match.Groups[1].Value.Trim()
    $checksumText = $checksums -join "`n"
    $notes = @"
# QuickQR $Tag

$changes

## 下载与安装

- Intel / AMD 64 位电脑：下载 QuickQR-$Tag-win-x64.zip。
- Windows on Arm：下载 QuickQR-$Tag-win-arm64.zip。此包为交叉构建，尚未完成 ARM64 真机运行验证。
- 完整解压整个文件夹，双击 QuickQR.exe；无需单独安装 .NET，无需管理员权限，应用完全离线。

## 系统要求与已知限制

目标系统为 Windows 10 22H2 / Windows 11。现有实际运行验证在 Windows x64 build 26300 完成；Windows 10、ARM64、混合 DPI、多显示器和各聊天/Office 客户端仍需对应环境验收。关闭主窗口后继续托盘驻留，从托盘“退出 QuickQR”完全退出。

本版本不附带未经安装与卸载验收的安装器。发布文件未使用发布者证书签名。

## SHA256

下载后可运行 Get-FileHash 检查以下校验值，也可使用随附的 SHA256SUMS.txt。

~~~text
$checksumText
~~~
"@
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'RELEASE-NOTES.md'), $notes, [Text.UTF8Encoding]::new($false))
    $checksums | Write-Output
} finally { Pop-Location }
