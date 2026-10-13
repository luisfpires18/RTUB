# Shared helpers for the RTUB configuration audits (task 039). Dot-sourced by audit-local-user-secrets.ps1 and
# audit-azure-app-settings.ps1. Works on Windows PowerShell 5.1 and PowerShell 7.
#
# Safety contract: no function here returns or prints a setting's value. Callers get presence, length and a short
# SHA-256 fingerprint only; reports and templates go to a git-ignored folder (.local/ inside the repository).

Set-StrictMode -Version 2.0

function Get-RtubRepoRoot {
    $root = $null
    try { $root = (& git -C $PSScriptRoot rev-parse --show-toplevel 2>$null) } catch { $root = $null }
    if (-not $root) { $root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }
    return [IO.Path]::GetFullPath(($root -replace '/', [IO.Path]::DirectorySeparatorChar))
}

# Resolves the output folder. Inside the repository it must be under .local/ (git-ignored), so a report or template
# can never land in a tracked path. Outside the repository (e.g. a temp folder) anything goes.
function Resolve-AuditOutputDirectory([string]$Path, [string]$RepoRoot) {
    $full = if ([IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path $RepoRoot $Path }
    $full = [IO.Path]::GetFullPath($full)
    $sep = [IO.Path]::DirectorySeparatorChar
    $rootWithSep = $RepoRoot.TrimEnd($sep) + $sep
    $localWithSep = (Join-Path $RepoRoot '.local').TrimEnd($sep) + $sep
    $cmp = [StringComparison]::OrdinalIgnoreCase
    if (($full + $sep).StartsWith($rootWithSep, $cmp) -and -not ($full + $sep).StartsWith($localWithSep, $cmp)) {
        throw "Refusing to write audit output to '$full': inside the repository it must be under .local/ (git-ignored)."
    }
    if (-not (Test-Path $full)) { New-Item -ItemType Directory -Force -Path $full | Out-Null }
    return $full
}

function Read-RtubManifest([string]$Path) {
    if (-not (Test-Path $Path)) { throw "Manifest not found: $Path" }
    return (Get-Content -Raw -Encoding UTF8 -Path $Path | ConvertFrom-Json)
}

# Short, non-reversible fingerprint for comparing values without showing them. Never log the input.
function Get-ValueFingerprint($Value) {
    if ($null -eq $Value) { return $null }
    $Value = [string]$Value
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $bytes = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value)) } finally { $sha.Dispose() }
    $hex = -join ($bytes | ForEach-Object { $_.ToString('x2') })
    return 'sha256:' + $hex.Substring(0, 12)
}

function Get-FullHash($Value) {
    if ($null -eq $Value) { return $null }
    $Value = [string]$Value
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $bytes = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value)) } finally { $sha.Dispose() }
    return -join ($bytes | ForEach-Object { $_.ToString('x2') })
}

function Get-Prop($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    $p = $Object.PSObject.Properties[$Name]
    if ($null -eq $p) { return $null }
    return $p.Value
}

# The placeholder written into templates: <LOCAL_|DEV_|PROD_ + placeholder>.
function Get-PlaceholderToken($Setting, [string]$EnvPrefix) {
    $base = Get-Prop $Setting 'placeholder'
    if (-not $base) { $base = ($Setting.name -replace '__', '_').ToUpperInvariant() }
    return '<' + $EnvPrefix + '_' + $base + '>'
}

# Value proposed by a template: the manifest's fixed expected value (never a secret), else a non-secret example,
# else a placeholder. Secrets always get a placeholder.
function Get-TemplateValue($Setting, [string]$EnvKey, [string]$EnvPrefix) {
    if (-not $Setting.secret) {
        $expected = Get-Prop (Get-Prop $Setting 'expected') $EnvKey
        if ($expected) { return [string]$expected }
        $example = Get-Prop (Get-Prop $Setting 'example') $EnvKey
        if ($example) { return [string]$example }
    }
    return (Get-PlaceholderToken $Setting $EnvPrefix)
}

function Test-DeprecatedName($Manifest, [string]$Name) {
    foreach ($d in $Manifest.deprecatedPatterns) {
        if ($Name -match $d.pattern) { return $d.reason }
    }
    return $null
}

function Write-Utf8File([string]$Path, [string]$Text) {
    [IO.File]::WriteAllText($Path, $Text, (New-Object Text.UTF8Encoding($false)))
}

function ConvertTo-MdCell([string]$Text) {
    if ($null -eq $Text) { return '' }
    return ($Text -replace '\|', '\|' -replace "`r?`n", ' ')
}
