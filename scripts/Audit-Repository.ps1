[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    $paths = @(git -c core.quotepath=false ls-files)
    if ($LASTEXITCODE -ne 0 -or $paths.Count -eq 0) { throw 'Run this audit inside an initialized repository with tracked or staged files.' }
    $rules = @{
        'GitHub credential' = 'gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,}'
        'AWS access key' = '\b(?:AKIA|ASIA)[A-Z0-9]{16}\b'
        'Private key' = '-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----'
        'Personal absolute path' = '(?i)[A-Z]:\\Users\\(?!Public\\|Default\\)[^\\\r\n]+\\|/Users/[^/\r\n]+/|/home/[^/\r\n]+/'
    }
    $findings = @()
    foreach ($path in $paths) {
        if ($path -match '(?i)(^|/)(bin|obj|\.vs|artifacts|work)/|\.(pfx|p12|pem|key|zip|log|tmp)$|(^|/)\.env(?:\.|$)') { $findings += "Forbidden repository file: $path" }
        if ($path -match '\.(png|ico)$') { continue }
        $text = [IO.File]::ReadAllText((Join-Path $repoRoot $path))
        foreach ($rule in $rules.GetEnumerator()) {
            # Do not report literal scanner regexes as credentials.
            if ($path -eq 'scripts/Audit-Repository.ps1') { continue }
            if ($text -match $rule.Value) { $findings += "$($rule.Key) detected in $path" }
        }
    }
    if ($findings.Count) { $findings | Write-Output; throw 'Repository audit failed. Review findings without printing secret values.' }
    Write-Output "Repository audit passed for $($paths.Count) files. Pattern scanning is supplemented by source review."
} finally { Pop-Location }
