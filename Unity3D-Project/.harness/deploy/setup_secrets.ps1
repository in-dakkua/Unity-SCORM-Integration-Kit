<#
setup_secrets.ps1 - Alta de las contrasenas del keystore Android en el
Windows Credential Manager (WCM), por proyecto.

LO EJECUTA EL HUMANO en su propia terminal, nunca un agente: las
contrasenas se escriben con Read-Host -AsSecureString (no se ven, no quedan
en el historial) y van directas al WCM. Nada se escribe en disco ni en la
config: unity.config.json solo guarda el prefijo con el que se nombran.

  .harness\deploy\setup_secrets.ps1 -ProjectId cliente-miapp          # alta / actualizar
  .harness\deploy\setup_secrets.ps1 -ProjectId cliente-miapp -List    # ver cuales existen (sin valores)
  .harness\deploy\setup_secrets.ps1 -ProjectId cliente-miapp -Remove  # borrar
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9._-]+$')]
    [string]$ProjectId,
    [string]$Prefix,
    [switch]$List,
    [switch]$Remove
)

. "$PSScriptRoot\common.ps1"

if (-not $Prefix) { $Prefix = "harness/$ProjectId" }
$names = Get-HarnessSecretNames $Prefix

if ($List) {
    foreach ($kv in $names.GetEnumerator()) {
        if ([HarnessCred]::Exists($kv.Value)) { Write-Ok "$($kv.Value) existe" } else { Write-Warn2 "$($kv.Value) NO existe" }
    }
    exit 0
}

if ($Remove) {
    foreach ($kv in $names.GetEnumerator()) {
        if ([HarnessCred]::Delete($kv.Value)) { Write-Ok "$($kv.Value) borrado" } else { Write-Info "$($kv.Value) no existia" }
    }
    exit 0
}

function Read-Secret([string]$Label) {
    $a = Read-Host -AsSecureString "$Label"
    $b = Read-Host -AsSecureString "$Label (repetir)"
    $pa = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($a)
    $pb = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($b)
    try {
        $sa = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pa)
        $sb = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pb)
        if ($sa -ne $sb) { throw 'No coinciden.' }
        if ($sa.Length -lt 6) { throw 'Demasiado corta (Android exige al menos 6 caracteres).' }
        return $sa
    } finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pa)
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pb)
    }
}

Write-Host "Alta de secretos del keystore Android para '$ProjectId' en el Credential Manager."
Write-Host 'Se guardan en esta cuenta de Windows, en este equipo. No se escriben en disco.'
try {
    $ks = Read-Secret 'Contrasena del KEYSTORE'
    $same = Read-Host 'La contrasena de la CLAVE (alias) es la misma? [s/N]'
    if ($same -match '^[sSyY]') { $key = $ks } else { $key = Read-Secret 'Contrasena de la CLAVE (alias)' }
    [HarnessCred]::Write($names.keystore_pass, $ProjectId, $ks)
    [HarnessCred]::Write($names.key_pass, $ProjectId, $key)
    $ks = $null; $key = $null
    Write-Ok "Guardado: $($names.keystore_pass), $($names.key_pass)"
    Write-Info 'Comprueba con: powershell -NoProfile -ExecutionPolicy Bypass -File .harness\deploy\deploy.ps1 doctor -Platform android'
} catch {
    Write-Fail $_.Exception.Message
    exit 1
}
