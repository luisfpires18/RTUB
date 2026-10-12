<#
.SYNOPSIS
    Audits this machine's RTUB user-secrets against scripts/config/required-settings.json. Never prints a value.

.DESCRIPTION
    Lists the user-secrets of src/RTUB.Web/RTUB.csproj, compares them with the manifest (falling back to the
    committed appsettings and code defaults), and writes, under .local/config-audit/ (git-ignored):
      local-user-secrets.audit.md / .json   presence, source, length and SHA-256 fingerprint per setting
      set-local-user-secrets.template.ps1   dotnet user-secrets commands for the missing settings, placeholders only
    Run from anywhere inside the repository:
      powershell -File scripts/config/audit-local-user-secrets.ps1

.PARAMETER UserSecretsListFile
    Optional: read the output of `dotnet user-secrets list` from a file instead of running it (used by the tests).
    Keep such a file outside the repository or under .local/.
#>
[CmdletBinding()]
param(
    [string]$Project = 'src/RTUB.Web/RTUB.csproj',
    [string]$OutputDirectory = '.local/config-audit',
    [string]$ManifestPath,
    [string]$UserSecretsListFile
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'config-audit-common.ps1')

$root = Get-RtubRepoRoot
if (-not $ManifestPath) { $ManifestPath = Join-Path $root 'scripts/config/required-settings.json' }
$manifest = Read-RtubManifest $ManifestPath
$outDir = Resolve-AuditOutputDirectory $OutputDirectory $root
$projectPath = if ([IO.Path]::IsPathRooted($Project)) { $Project } else { Join-Path $root $Project }

# --- user-secrets (values stay in this dictionary; nothing below prints them) ---
if ($UserSecretsListFile) {
    $lines = Get-Content -Encoding UTF8 -Path $UserSecretsListFile
} else {
    $lines = & dotnet user-secrets list --project $projectPath 2>$null
    if ($LASTEXITCODE -ne 0) { throw "dotnet user-secrets list failed for $projectPath (exit $LASTEXITCODE)." }
}
$secrets = @{}
foreach ($line in @($lines)) {
    $i = ([string]$line).IndexOf(' = ')
    if ($i -le 0) { continue }
    $secrets[([string]$line).Substring(0, $i).Trim() -replace ':', '__'] = ([string]$line).Substring($i + 3)
}

# --- committed appsettings (non-secret fallbacks) ---
function Add-Flattened($Node, [string]$Prefix, [hashtable]$Into) {
    if ($Node -is [System.Management.Automation.PSCustomObject]) {
        foreach ($p in $Node.PSObject.Properties) {
            $key = if ($Prefix) { "${Prefix}__$($p.Name)" } else { $p.Name }
            Add-Flattened $p.Value $key $Into
        }
    } elseif ($Node -is [System.Array]) {
        for ($k = 0; $k -lt $Node.Length; $k++) { Add-Flattened $Node[$k] "${Prefix}__$k" $Into }
    } elseif ($null -ne $Node) {
        $Into[$Prefix] = [string]$Node
    }
}
$appsettings = @{}
foreach ($f in @('appsettings.json', 'appsettings.Development.json')) {
    $p = Join-Path (Split-Path $projectPath -Parent) $f
    if (Test-Path $p) { Add-Flattened (Get-Content -Raw -Encoding UTF8 $p | ConvertFrom-Json) '' $appsettings }
}

# --- compare ---
$rows = @(); $warnings = @(); $missingRequired = @(); $missingOptional = @()
$known = @{}
foreach ($s in $manifest.settings) {
    $known[$s.name] = $true
    if ($s.local -eq 'n/a') { continue }
    $source = 'missing'; $value = $null
    if ($secrets.ContainsKey($s.name)) { $source = 'user-secrets'; $value = $secrets[$s.name] }
    elseif ($appsettings.ContainsKey($s.name)) { $source = 'appsettings fallback'; $value = $appsettings[$s.name] }
    elseif (Get-Prop $s 'codeDefault') { $source = 'code default' }

    $state = 'ok'
    if ($source -eq 'missing') {
        if ($s.local -eq 'required') { $state = 'MISSING'; $missingRequired += $s; $warnings += "Missing required local setting $($s.name)." }
        else { $state = 'not set'; $missingOptional += $s }
    } elseif ($s.secret -and $source -eq 'appsettings fallback') {
        $state = 'WARN'; $warnings += "$($s.name) is a secret but comes from a tracked appsettings file; move it to user-secrets."
    }
    $rows += [pscustomobject]@{
        name = $s.name; group = $s.group; secret = [bool]$s.secret; requirement = $s.local; source = $source; state = $state
        length = $(if ($null -ne $value) { $value.Length } else { $null })
        fingerprint = (Get-ValueFingerprint $value)
    }
}
$unknown = @()
foreach ($k in ($secrets.Keys | Sort-Object)) {
    if ($known.ContainsKey($k)) { continue }
    $reason = Test-DeprecatedName $manifest $k
    if ($reason) { $warnings += "Deprecated user-secret $k : $reason" }
    $unknown += [pscustomobject]@{ name = $k; length = $secrets[$k].Length; fingerprint = (Get-ValueFingerprint $secrets[$k]); deprecated = $reason }
}

# --- reports ---
$stamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
$report = [ordered]@{
    kind = 'local-user-secrets'; generatedAt = $stamp; project = $Project; manifest = 'scripts/config/required-settings.json'
    note = 'Values are never recorded: presence, source, length and a SHA-256 fingerprint prefix only.'
    summary = [ordered]@{ userSecrets = $secrets.Count; missingRequired = $missingRequired.Count; missingOptional = $missingOptional.Count; warnings = $warnings.Count; unknown = $unknown.Count }
    warnings = $warnings; settings = $rows; unknownUserSecrets = $unknown
}
Write-Utf8File (Join-Path $outDir 'local-user-secrets.audit.json') ($report | ConvertTo-Json -Depth 6)

$md = New-Object Text.StringBuilder
[void]$md.AppendLine("# Local user-secrets audit").AppendLine()
[void]$md.AppendLine("Generated $stamp for ``$Project``. **Redacted:** no value is recorded - presence, source, length and a SHA-256 fingerprint prefix only.").AppendLine()
[void]$md.AppendLine("User-secrets: $($secrets.Count); missing required: $($missingRequired.Count); missing optional: $($missingOptional.Count); warnings: $($warnings.Count)").AppendLine()
if ($warnings.Count) { [void]$md.AppendLine('## Warnings').AppendLine(); foreach ($w in $warnings) { [void]$md.AppendLine("- $w") }; [void]$md.AppendLine() }
[void]$md.AppendLine('## Settings').AppendLine()
[void]$md.AppendLine('| Setting | Local | State | Source | Secret | Length | Fingerprint |').AppendLine('| --- | --- | --- | --- | --- | --- | --- |')
foreach ($r in $rows) { [void]$md.AppendLine("| ``$($r.name)`` | $($r.requirement) | $($r.state) | $($r.source) | $($r.secret) | $($r.length) | $($r.fingerprint) |") }
if ($unknown.Count) {
    [void]$md.AppendLine().AppendLine('## User-secrets not in the manifest').AppendLine()
    foreach ($u in $unknown) { [void]$md.AppendLine("- ``$($u.name)`` (length $($u.length), $($u.fingerprint))$(if ($u.deprecated) { ' - **deprecated:** ' + $u.deprecated })") }
}
[void]$md.AppendLine().AppendLine('Template for the missing settings: `set-local-user-secrets.template.ps1` (placeholders only).')
Write-Utf8File (Join-Path $outDir 'local-user-secrets.audit.md') $md.ToString()

# --- template (placeholders only) ---
$t = New-Object Text.StringBuilder
[void]$t.AppendLine("# RTUB local user-secrets template - generated $stamp by scripts/config/audit-local-user-secrets.ps1")
[void]$t.AppendLine('# PLACEHOLDERS ONLY. Replace each <LOCAL_...> value with your own, delete the guard line, then run from the repo root.')
[void]$t.AppendLine('# Required settings are active lines; optional ones are commented out. This file is git-ignored: keep it that way.')
[void]$t.AppendLine("Write-Error 'Template not edited: replace every <LOCAL_...> placeholder and remove this guard line first.'; return").AppendLine()
if (-not ($missingRequired.Count + $missingOptional.Count)) { [void]$t.AppendLine('# Nothing missing.') }
foreach ($s in $missingRequired) {
    [void]$t.AppendLine("dotnet user-secrets set --project $Project `"$($s.name -replace '__', ':')`" `"$(Get-TemplateValue $s 'local' 'LOCAL')`"")
}
foreach ($s in $missingOptional) {
    if ((Get-Prop $s 'unused') -or -not (@($s.source) -contains 'user-secrets')) { continue }
    [void]$t.AppendLine("# dotnet user-secrets set --project $Project `"$($s.name -replace '__', ':')`" `"$(Get-TemplateValue $s 'local' 'LOCAL')`"   # optional ($($s.group))")
}
Write-Utf8File (Join-Path $outDir 'set-local-user-secrets.template.ps1') $t.ToString()

Write-Host "Local user-secrets audit: $($secrets.Count) user-secrets, $($missingRequired.Count) required missing, $($missingOptional.Count) optional not set, $($warnings.Count) warning(s)."
Write-Host "Reports (redacted) and template (placeholders only) written to $outDir"
