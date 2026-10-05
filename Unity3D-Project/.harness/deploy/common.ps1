# common.ps1 - Funciones compartidas por deploy.ps1 y setup_secrets.ps1.
# Windows PowerShell 5.1. Solo ASCII en este fichero (5.1 lee UTF-8 sin BOM
# como ANSI). Se carga con dot-sourcing: . "$PSScriptRoot\common.ps1"

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# --- Windows Credential Manager (CredRead/CredWrite via P/Invoke) ---------
# No hay cmdlet nativo para LEER un secreto del WCM en 5.1 y cmdkey no
# devuelve el valor, asi que se usa advapi32 directamente. Sin modulos de
# PSGallery.
if (-not ('HarnessCred' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class HarnessCred {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct CREDENTIAL {
        public int Flags; public int Type; public string TargetName; public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize; public IntPtr CredentialBlob; public int Persist;
        public int AttributeCount; public IntPtr Attributes; public string TargetAlias; public string UserName;
    }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredRead(string target, int type, int flags, out IntPtr cred);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredWrite(ref CREDENTIAL cred, int flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredDelete(string target, int type, int flags);
    [DllImport("advapi32.dll")]
    static extern void CredFree(IntPtr cred);
    const int GENERIC = 1;
    public static bool Exists(string target) {
        IntPtr p; if (!CredRead(target, GENERIC, 0, out p)) return false; CredFree(p); return true;
    }
    public static string Read(string target) {
        IntPtr p; if (!CredRead(target, GENERIC, 0, out p)) return null;
        try {
            CREDENTIAL c = (CREDENTIAL)Marshal.PtrToStructure(p, typeof(CREDENTIAL));
            if (c.CredentialBlobSize == 0) return "";
            return Marshal.PtrToStringUni(c.CredentialBlob, c.CredentialBlobSize / 2);
        } finally { CredFree(p); }
    }
    public static void Write(string target, string user, string secret) {
        byte[] b = System.Text.Encoding.Unicode.GetBytes(secret);
        CREDENTIAL c = new CREDENTIAL();
        c.Type = GENERIC; c.TargetName = target; c.UserName = user; c.Persist = 2; // LOCAL_MACHINE
        c.CredentialBlobSize = b.Length; c.CredentialBlob = Marshal.AllocHGlobal(b.Length);
        try {
            Marshal.Copy(b, 0, c.CredentialBlob, b.Length);
            if (!CredWrite(ref c, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        } finally { Marshal.FreeHGlobal(c.CredentialBlob); }
    }
    public static bool Delete(string target) { return CredDelete(target, GENERIC, 0); }
}
'@
}

# Nombres de las entradas del WCM para un proyecto. Solo nombres: los
# valores nunca se escriben en disco ni en la config.
function Get-HarnessSecretNames([string]$Prefix) {
    return [ordered]@{
        keystore_pass = "$Prefix/android_keystore_pass"
        key_pass      = "$Prefix/android_key_pass"
    }
}

# --- Salida ---------------------------------------------------------------
function Write-Ok([string]$m)   { Write-Host "[OK]    $m" -ForegroundColor Green }
function Write-Warn2([string]$m){ Write-Host "[WARN]  $m" -ForegroundColor Yellow }
function Write-Fail([string]$m) { Write-Host "[FAIL]  $m" -ForegroundColor Red }
function Write-Info([string]$m) { Write-Host "[INFO]  $m" }

# Sustituye cualquier secreto conocido por *** antes de mostrar o guardar texto.
function Protect-Text([string]$Text, [string[]]$Secrets) {
    if ($null -eq $Text) { return $Text }
    foreach ($s in $Secrets) { if ($s -and $s.Length -ge 3) { $Text = $Text.Replace($s, '***') } }
    return $Text
}

# --- Proyecto y config ----------------------------------------------------
function Get-HarnessProjectRoot {
    # .harness/deploy/ -> raiz del proyecto Unity
    return (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Get-HarnessConfig([string]$Root) {
    $path = Join-Path $Root '.harness\unity.config.json'
    if (-not (Test-Path $path)) { throw "No existe .harness/unity.config.json: ejecuta /unity-claude-code-harness primero." }
    return (Get-Content -Raw -Encoding UTF8 $path | ConvertFrom-Json)
}

function Get-Cfg($Config, [string]$Name, $Default = $null) {
    $p = $Config.PSObject.Properties[$Name]
    if ($null -eq $p -or $null -eq $p.Value -or "$($p.Value)" -eq '') { return $Default }
    return $p.Value
}

function Expand-UserPath([string]$Path) {
    if (-not $Path) { return $Path }
    if ($Path.StartsWith('~')) { $Path = $env:USERPROFILE + $Path.Substring(1) }
    return [Environment]::ExpandEnvironmentVariables($Path)
}

# True si $Child esta dentro de $Parent (normaliza / y \ y mayusculas).
function Test-PathInside([string]$Child, [string]$Parent) {
    if (-not $Child) { return $false }
    $c = [IO.Path]::GetFullPath((Expand-UserPath $Child)).TrimEnd('\').ToLowerInvariant() + '\'
    $p = [IO.Path]::GetFullPath($Parent).TrimEnd('\').ToLowerInvariant() + '\'
    return $c.StartsWith($p)
}

function Get-UnityProjectInfo([string]$Root) {
    $pv = Join-Path $Root 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path $pv)) { throw "No existe ProjectSettings/ProjectVersion.txt en $Root" }
    $ver = ((Get-Content $pv | Where-Object { $_ -match '^m_EditorVersion:' }) -replace '^m_EditorVersion:\s*', '').Trim()
    $ps = Get-Content -Raw (Join-Path $Root 'ProjectSettings\ProjectSettings.asset')
    function Pick([string]$pattern) { $m = [regex]::Match($ps, $pattern); if ($m.Success) { return $m.Groups[1].Value.Trim() } return $null }
    return [pscustomobject]@{
        UnityVersion    = $ver
        ProductName     = Pick '(?m)^\s*productName:\s*(.+)$'
        BundleVersion   = Pick '(?m)^\s*bundleVersion:\s*(.+)$'
        AndroidCode     = Pick '(?m)^\s*AndroidBundleVersionCode:\s*(\d+)'
        IosBuild        = Pick '(?ms)^\s*buildNumber:\s*\r?\n(?:\s+\w+:[^\r\n]*\r?\n)*?\s+iPhone:\s*(\S+)'
        AndroidId       = Pick '(?ms)^\s*applicationIdentifier:\s*\r?\n(?:\s+\w+:[^\r\n]*\r?\n)*?\s+Android:\s*(\S+)'
        IosId           = Pick '(?ms)^\s*applicationIdentifier:\s*\r?\n(?:\s+\w+:[^\r\n]*\r?\n)*?\s+iPhone:\s*(\S+)'
    }
}

# Localiza Unity.exe para la version EXACTA del proyecto. Nunca usa otra.
function Find-UnityEditor([string]$Version, $Config) {
    $candidates = @()
    $cfgPath = Get-Cfg $Config 'unity_editor_path'
    if ($cfgPath) { $candidates += $cfgPath }
    $candidates += "C:\Program Files\Unity\Hub\Editor\$Version\Editor\Unity.exe"
    $sec = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'
    if (Test-Path $sec) {
        $dir = (Get-Content -Raw $sec).Trim().Trim('"')
        if ($dir) { $candidates += (Join-Path $dir "$Version\Editor\Unity.exe") }
    }
    foreach ($c in $candidates) {
        if ((Test-Path $c) -and ($c -like "*$Version*")) { return $c }
    }
    return $null
}

function Test-UnityModule([string]$UnityExe, [string]$Platform) {
    $pe = Join-Path (Split-Path $UnityExe) 'Data\PlaybackEngines'
    if ($Platform -eq 'android') { return (Test-Path (Join-Path $pe 'AndroidPlayer')) }
    return (Test-Path (Join-Path $pe 'iOSSupport'))
}

function Test-UnityProjectOpen([string]$Root) {
    $lock = Join-Path $Root 'Temp\UnityLockfile'
    if (-not (Test-Path $lock)) { return $false }
    try { $fs = [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None'); $fs.Close(); return $false } catch { return $true }
}

# --- SSH al Mac -----------------------------------------------------------
$script:SshExe = Join-Path $env:SystemRoot 'System32\OpenSSH\ssh.exe'
$script:TarExe = Join-Path $env:SystemRoot 'System32\tar.exe'

function Get-MacTarget($Config) {
    $user = Get-Cfg $Config 'deploy_ios_mac_user'
    $key  = Expand-UserPath (Get-Cfg $Config 'deploy_ios_ssh_key_path' '~/.ssh/harness_mac')
    $hosts = @()
    foreach ($h in @((Get-Cfg $Config 'deploy_ios_mac_host'), (Get-Cfg $Config 'deploy_ios_mac_ip'))) { if ($h) { $hosts += $h } }
    if (-not $user -or $hosts.Count -eq 0) { throw 'Faltan deploy_ios_mac_user / deploy_ios_mac_host / deploy_ios_mac_ip en la config.' }
    return [pscustomobject]@{ User = $user; Key = $key; Hosts = $hosts; Resolved = $null }
}

# Ejecuta un binario nativo capturando stdout+stderr SIN que 5.1 convierta el
# stderr en error terminante (con ErrorActionPreference=Stop lo haria).
function Invoke-NativeCapture([string]$Exe, [string[]]$Arguments) {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = & $Exe @Arguments 2>&1 | ForEach-Object { "$_" }
        $script:NativeExit = $LASTEXITCODE
    } finally { $ErrorActionPreference = $prev }
    return $out
}

function Get-SshArgs($Mac, [string]$HostName) {
    return @('-o', 'BatchMode=yes', '-o', 'ConnectTimeout=8', '-o', 'StrictHostKeyChecking=yes',
             '-o', 'HostKeyAlgorithms=ssh-ed25519',
             '-o', 'IdentitiesOnly=yes', '-i', $Mac.Key, "$($Mac.User)@$HostName")
}

# Prueba nombre y luego IP; se queda con el primero que responde.
function Resolve-MacHost($Mac) {
    if ($Mac.Resolved) { return $Mac.Resolved }
    foreach ($h in $Mac.Hosts) {
        $out = Invoke-NativeCapture $script:SshExe ((Get-SshArgs $Mac $h) + 'harness/mac_build.sh version')
        if ($script:NativeExit -eq 0 -and "$out" -match 'mac_build.sh v') { $Mac.Resolved = $h; return $h }
        Write-Warn2 "Mac no responde en '$h': $("$out".Trim())"
    }
    throw 'No se pudo conectar con el Mac por SSH (ver .harness/deploy/mac/SETUP.md).'
}

# Ejecuta un subcomando de mac_build.sh. Devuelve la salida; el exit code
# queda en $script:NativeExit.
function Invoke-Mac($Mac, [string]$SubCommand) {
    $h = Resolve-MacHost $Mac
    return (Invoke-NativeCapture $script:SshExe ((Get-SshArgs $Mac $h) + "harness/mac_build.sh $SubCommand"))
}

# Envia una carpeta al Mac como tar.gz por stdin. PowerShell 5.1 no pasa
# binarios por el pipe entre ejecutables nativos: se usa cmd /c.
function Send-FolderToMac($Mac, [string]$Folder, [string]$Job) {
    $h = Resolve-MacHost $Mac
    $parent = Split-Path $Folder -Parent
    $leaf = Split-Path $Folder -Leaf
    $sshArgs = (Get-SshArgs $Mac $h | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
    $cmd = "`"$($script:TarExe)`" -czf - -C `"$parent`" `"$leaf`" | `"$($script:SshExe)`" $sshArgs `"harness/mac_build.sh receive $Job`""
    $out = Invoke-NativeCapture 'cmd.exe' @('/c', $cmd)
    $rc = $script:NativeExit
    $out
    if ($rc -ne 0) { throw "Fallo al enviar $leaf al Mac (rc=$rc)." }
}
