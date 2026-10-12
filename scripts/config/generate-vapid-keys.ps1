<#
.SYNOPSIS
    Generates a Web Push VAPID key pair (P-256) for one RTUB environment and prints it. Writes no file.

.DESCRIPTION
    Uses .NET's ECDsa (no Node, no network). Prints the base64url public key (65-byte uncompressed point), the
    base64url private key (32 bytes) and a WebPush__VapidSubject placeholder, in the format WebPushOptions expects.

      powershell -File scripts/config/generate-vapid-keys.ps1

    Then store them where the environment reads them (docs/configuration-runbook.md):
      local: dotnet user-secrets set --project src/RTUB.Web/RTUB.csproj "WebPush:VapidPublicKey" "<public>"
      Azure: az webapp config appsettings set -g rtub_group -n <app> --settings WebPush__VapidPublicKey=<public> ... --output none
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function ConvertTo-Base64Url([byte[]]$Bytes) {
    return [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Get-Fixed32([byte[]]$Bytes) {
    if ($Bytes.Length -eq 32) { return $Bytes }
    $out = New-Object byte[] 32
    [Array]::Copy($Bytes, 0, $out, 32 - $Bytes.Length, $Bytes.Length)
    return $out
}

$ecdsa = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
try {
    $p = $ecdsa.ExportParameters($true)
    $public = New-Object byte[] 65
    $public[0] = 4
    [Array]::Copy((Get-Fixed32 $p.Q.X), 0, $public, 1, 32)
    [Array]::Copy((Get-Fixed32 $p.Q.Y), 0, $public, 33, 32)
    $publicKey = ConvertTo-Base64Url $public
    $privateKey = ConvertTo-Base64Url (Get-Fixed32 $p.D)
} finally {
    $ecdsa.Dispose()
}

Write-Host 'WARNING: do not commit these keys or paste them into a tracked file, an issue, a PR or a workflow log.' -ForegroundColor Yellow
Write-Host 'WARNING: use one stable pair per environment (local, DEV, PROD). Rotating a live pair invalidates every' -ForegroundColor Yellow
Write-Host '         existing push subscription: every member must turn push on again in /profile.' -ForegroundColor Yellow
Write-Host ''
Write-Output "WebPush__VapidSubject=mailto:<contact-address>"
Write-Output "WebPush__VapidPublicKey=$publicKey"
Write-Output "WebPush__VapidPrivateKey=$privateKey"
