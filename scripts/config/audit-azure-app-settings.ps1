<#
.SYNOPSIS
    Read-only, redacted audit of the rtub-dev and rtub App Service settings against scripts/config/required-settings.json.

.DESCRIPTION
    Reads both apps' settings with `az webapp config appsettings list` (read-only; requires `az login`), keeps the
    values in memory only, and writes under .local/config-audit/ (git-ignored):
      dev-app-settings.audit.md / .json, prod-app-settings.audit.md / .json
          present / missing per setting, slot setting, secret flag, value length, SHA-256 fingerprint prefix,
          DEV-vs-PROD match / differ checks and warnings
      set-dev-app-settings.template.ps1, set-prod-app-settings.template.ps1
          `az webapp config appsettings set` commands with placeholders only (missing settings active, the rest
          commented out); they refuse to run while a <PLACEHOLDER> is left
    No value is printed, written or compared in clear. Nothing in Azure is changed.

      powershell -File scripts/config/audit-azure-app-settings.ps1

.PARAMETER IncludeValues
    Deliberately not implemented: exporting real values is never needed for the audit. Passing it stops the script.

.PARAMETER DevSettingsFile / ProdSettingsFile
    Optional: read a settings list in the `az ... -o json` shape from a file instead of calling az (used by the
    tests with synthetic values). Keep real exports out of the repository.
#>
[CmdletBinding()]
param(
    [string]$ResourceGroup = 'rtub_group',
    [string]$DevApp = 'rtub-dev',
    [string]$ProdApp = 'rtub',
    [switch]$IncludeValues,
    [string]$OutputDirectory = '.local/config-audit',
    [string]$ManifestPath,
    [string]$DevSettingsFile,
    [string]$ProdSettingsFile
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'config-audit-common.ps1')

if ($IncludeValues) {
    throw 'IncludeValues is deliberately not implemented: the audit compares fingerprints and never exports values. Read a single value in the Azure portal if you really need it.'
}

$root = Get-RtubRepoRoot
if (-not $ManifestPath) { $ManifestPath = Join-Path $root 'scripts/config/required-settings.json' }
$manifest = Read-RtubManifest $ManifestPath
$outDir = Resolve-AuditOutputDirectory $OutputDirectory $root

# Values live only in these hashtables; nothing below prints or writes them.
function Get-AppSettings([string]$App, [string]$File) {
    if ($File) { $json = Get-Content -Raw -Encoding UTF8 -Path $File }
    else {
        if (-not (Get-Command az -ErrorAction SilentlyContinue)) { throw 'Azure CLI (az) not found. Install it and run az login, or pass -DevSettingsFile / -ProdSettingsFile.' }
        # Windows PowerShell turns az's stderr warnings into errors under 'Stop'; judge by the exit code instead.
        # stderr is discarded (it never carries values; stdout, which does, is only parsed).
        $previous = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
        try { $json = (& az webapp config appsettings list --resource-group $ResourceGroup --name $App --output json 2>$null) -join "`n" }
        finally { $ErrorActionPreference = $previous }
        if ($LASTEXITCODE -ne 0) { throw "az webapp config appsettings list failed for '$App' (exit $LASTEXITCODE). Check az login and the resource group." }
    }
    $map = @{}
    # Assigned first: Windows PowerShell 5.1 emits a JSON array as one object, foreach then enumerates it.
    $parsed = $json | ConvertFrom-Json
    foreach ($item in $parsed) {
        $map[[string]$item.name] = [pscustomobject]@{ value = [string]$item.value; slot = [bool](Get-Prop $item 'slotSetting') }
    }
    return $map
}

$envs = @(
    [pscustomobject]@{ key = 'dev'; label = 'DEV'; prefix = 'DEV'; app = $DevApp; settings = (Get-AppSettings $DevApp $DevSettingsFile) },
    [pscustomobject]@{ key = 'prod'; label = 'PROD'; prefix = 'PROD'; app = $ProdApp; settings = (Get-AppSettings $ProdApp $ProdSettingsFile) }
)

function Get-EffectiveDefault($Setting, [string]$EnvKey) {
    if ($EnvKey -eq 'prod' -and (Get-Prop $Setting 'productionDefault')) { return [string]$Setting.productionDefault }
    if (Get-Prop $Setting 'committedDefault') { return [string]$Setting.committedDefault }
    return (Get-Prop $Setting 'codeDefault')
}

$stamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
$known = @{}; foreach ($s in $manifest.settings) { $known[$s.name] = $true }
$results = @{}

foreach ($e in $envs) {
    $other = $envs | Where-Object { $_.key -ne $e.key }
    $rows = @(); $warnings = @(); $removals = @()
    $groupsMissing = @{}

    foreach ($s in $manifest.settings) {
        $req = [string](Get-Prop $s $e.key)
        $has = $e.settings.ContainsKey($s.name)
        $value = if ($has) { $e.settings[$s.name].value } else { $null }
        $default = Get-EffectiveDefault $s $e.key
        $effective = if ($has) { $value } else { $default }
        $state = if ($has) { 'present' } elseif ($null -ne $default) { 'default' } else { 'missing' }
        $checks = @(); $fix = $false

        if ($req -eq 'required' -and $state -eq 'missing') {
            $checks += 'missing required'; $fix = $true; $groupsMissing[$s.group] = $true
        }
        if ($req -eq 'absent' -and $has) {
            $checks += "must not be set on $($e.label)"; $removals += $s.name
        }
        $expected = Get-Prop (Get-Prop $s 'expected') $e.key
        if ($expected -and $null -ne $effective -and -not [string]::Equals([string]$effective, [string]$expected, [StringComparison]::OrdinalIgnoreCase)) {
            $checks += "not the expected value '$expected'"; $fix = $true
        }
        $vc = [string](Get-Prop (Get-Prop $s 'valueCheck') $e.key)
        if ($vc.StartsWith('startsWith:') -and $has) {
            $prefix = $vc.Substring(11)
            if (-not $value.StartsWith($prefix, [StringComparison]::Ordinal)) { $checks += "does not start with '$prefix'"; $fix = $true }
        }

        # Compared on effective values (Azure value, else the committed / code default), so two apps that both
        # fall back to the same default (e.g. the production bucket) are caught too.
        $comparison = $null
        $otherEffective = if ($other.settings.ContainsKey($s.name)) { $other.settings[$s.name].value } else { Get-EffectiveDefault $s $other.key }
        if ($null -ne $effective -and $null -ne $otherEffective -and ($has -or $other.settings.ContainsKey($s.name) -or $s.devProd -eq 'differ')) {
            $same = (Get-FullHash $effective) -eq (Get-FullHash $otherEffective)
            $comparison = if ($same) { "same as $($other.label)" } else { "differs from $($other.label)" }
            if ($s.devProd -eq 'differ' -and $same) { $checks += "shares its value with $($other.label) (must differ)" }
            if ($s.devProd -eq 'match' -and -not $same) { $checks += "differs from $($other.label) (should match)" }
        }
        foreach ($c in $checks) { $warnings += "$($s.name): $c." }

        $rows += [pscustomobject]@{
            name = $s.name; group = $s.group; secret = [bool]$s.secret; requirement = $req; state = $state
            slotSetting = $(if ($has) { $e.settings[$s.name].slot } else { $null })
            length = $(if ($has) { $value.Length } else { $null })
            fingerprint = (Get-ValueFingerprint $value)
            devProd = $s.devProd; comparison = $comparison; checks = $checks; needsFix = $fix
        }
    }

    $labels = @{ webpush = 'WebPush VAPID settings incomplete (push will not work)'; r2 = 'Cloudflare R2 settings incomplete (storage routes answer 500)'; email = 'EmailSettings incomplete (no outgoing email)'; core = 'Core runtime settings incomplete' }
    foreach ($g in $groupsMissing.Keys) { if ($labels.ContainsKey($g)) { $warnings = @("$($e.label): $($labels[$g]).") + $warnings } }

    $unknown = @()
    foreach ($name in ($e.settings.Keys | Sort-Object)) {
        if ($known.ContainsKey($name)) { continue }
        $reason = Test-DeprecatedName $manifest $name
        if ($reason) { $warnings += "${name}: deprecated - $reason"; $removals += $name }
        $unknown += [pscustomobject]@{ name = $name; length = $e.settings[$name].value.Length; fingerprint = (Get-ValueFingerprint $e.settings[$name].value); slotSetting = $e.settings[$name].slot; deprecated = $reason }
    }

    $results[$e.key] = [pscustomobject]@{ env = $e; rows = $rows; warnings = $warnings; unknown = $unknown; removals = $removals }
}

foreach ($key in @('dev', 'prod')) {
    $r = $results[$key]; $e = $r.env
    $missing = @($r.rows | Where-Object { $_.state -eq 'missing' -and $_.requirement -eq 'required' })

    $json = [ordered]@{
        kind = 'azure-app-settings'; environment = $e.label; app = $e.app; resourceGroup = $ResourceGroup; generatedAt = $stamp
        manifest = 'scripts/config/required-settings.json'
        note = 'Values are never recorded: presence, length and a SHA-256 fingerprint prefix only. DEV/PROD comparison uses full hashes in memory.'
        summary = [ordered]@{ settingsInAzure = $e.settings.Count; missingRequired = $missing.Count; warnings = $r.warnings.Count; unknown = $r.unknown.Count }
        warnings = $r.warnings; settings = $r.rows; notInManifest = $r.unknown
    }
    Write-Utf8File (Join-Path $outDir "$key-app-settings.audit.json") ($json | ConvertTo-Json -Depth 6)

    $md = New-Object Text.StringBuilder
    [void]$md.AppendLine("# $($e.label) App Service settings audit (``$($e.app)``)").AppendLine()
    [void]$md.AppendLine("Generated $stamp from ``$ResourceGroup/$($e.app)``. **Redacted:** no value is recorded - presence, length and a SHA-256 fingerprint prefix only.").AppendLine()
    [void]$md.AppendLine("Settings in Azure: $($e.settings.Count); missing required: $($missing.Count); warnings: $($r.warnings.Count); not in manifest: $($r.unknown.Count)").AppendLine()
    [void]$md.AppendLine('## Warnings').AppendLine()
    if ($r.warnings.Count) { foreach ($w in $r.warnings) { [void]$md.AppendLine("- $(ConvertTo-MdCell $w)") } } else { [void]$md.AppendLine('None.') }
    [void]$md.AppendLine().AppendLine('## Settings').AppendLine()
    [void]$md.AppendLine("| Setting | $($e.label) | State | Secret | Slot | Length | Fingerprint | DEV/PROD rule | Comparison | Checks |")
    [void]$md.AppendLine('| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |')
    foreach ($row in $r.rows) {
        [void]$md.AppendLine("| ``$($row.name)`` | $($row.requirement) | $($row.state) | $($row.secret) | $($row.slotSetting) | $($row.length) | $($row.fingerprint) | $($row.devProd) | $($row.comparison) | $(ConvertTo-MdCell ($row.checks -join '; ')) |")
    }
    if ($r.unknown.Count) {
        [void]$md.AppendLine().AppendLine('## Settings in Azure but not in the manifest').AppendLine()
        foreach ($u in $r.unknown) { [void]$md.AppendLine("- ``$($u.name)`` (length $($u.length), $($u.fingerprint))$(if ($u.deprecated) { ' - **deprecated:** ' + $u.deprecated })") }
    }
    [void]$md.AppendLine().AppendLine("Template: ``set-$key-app-settings.template.ps1`` (placeholders only).")
    Write-Utf8File (Join-Path $outDir "$key-app-settings.audit.md") $md.ToString()

    # Template: every relevant setting, missing / wrong ones active, the rest commented out. Placeholders only.
    $t = New-Object Text.StringBuilder
    [void]$t.AppendLine("# RTUB $($e.label) App Service settings template ($($e.app)) - generated $stamp by scripts/config/audit-azure-app-settings.ps1")
    [void]$t.AppendLine('# PLACEHOLDERS ONLY - this file never holds a real value. Edit it locally; it is git-ignored, keep it that way.')
    [void]$t.AppendLine('# Active lines = missing or wrong at audit time. Commented lines = present and fine (uncomment only to change them).')
    [void]$t.AppendLine('# Replace every <...> value; the guard below refuses to run while one is left. --output none stops az from')
    [void]$t.AppendLine('# printing every setting (secrets included) back to the terminal. Setting app settings restarts the app.')
    if ($key -eq 'prod') { [void]$t.AppendLine('# PROD: never rotate WebPush__Vapid* on a live app - every existing push subscription stops working.') }
    [void]$t.AppendLine('$settings = @(')
    foreach ($row in $r.rows) {
        $s = $manifest.settings | Where-Object { $_.name -eq $row.name } | Select-Object -First 1
        $req = $row.requirement
        if ($req -eq 'absent' -or $req -eq 'n/a' -or (Get-Prop $s 'unused')) { continue }
        $hasExpected = $null -ne (Get-Prop (Get-Prop $s 'expected') $key)
        $relevant = $req -eq 'required' -or $hasExpected -or ($req -eq 'optional' -and @('email', 'r2', 'webpush') -contains $s.group)
        if (-not $relevant) { continue }
        $line = "'" + $s.name + '=' + ((Get-TemplateValue $s $key $e.prefix) -replace "'", "''") + "'"
        if ($row.needsFix) { [void]$t.AppendLine("    $line") }
        else { [void]$t.AppendLine("    # $line   # $($row.state)$(if ($req -ne 'required') { ', ' + $req })") }
    }
    [void]$t.AppendLine(')')
    [void]$t.AppendLine('$left = @($settings | Where-Object { $_ -match ''<[^>]+>'' } | ForEach-Object { ($_ -split ''='', 2)[0] })')
    [void]$t.AppendLine('if ($left.Count) { Write-Error ("Replace these placeholders first: " + ($left -join '', '')); return }')
    [void]$t.AppendLine('if (-not $settings.Count) { Write-Host ''Nothing to set.''; return }')
    [void]$t.AppendLine("az webapp config appsettings set -g $ResourceGroup -n $($e.app) --settings @settings --output none")
    if ($r.removals.Count) {
        [void]$t.AppendLine().AppendLine("# Settings that must not be on $($e.label) (stale or wrong environment). Review, then uncomment to delete:")
        [void]$t.AppendLine("# az webapp config appsettings delete -g $ResourceGroup -n $($e.app) --setting-names $(($r.removals | Sort-Object -Unique) -join ' ') --output none")
    }
    Write-Utf8File (Join-Path $outDir "set-$key-app-settings.template.ps1") $t.ToString()

    Write-Host "$($e.label) ($($e.app)): $($e.settings.Count) settings, $($missing.Count) required missing, $($r.warnings.Count) warning(s)."
}
Write-Host "Reports (redacted) and templates (placeholders only) written to $outDir"
