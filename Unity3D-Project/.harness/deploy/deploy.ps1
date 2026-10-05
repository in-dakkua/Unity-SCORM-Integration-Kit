<#
deploy.ps1 - Orquestador del modulo de despliegue del harness (Windows).

Lo lanza el LEADER (via /deploy) o el propio developer. Nunca se dispara
solo: no hay hooks de VCS, CI ni cron.

  .harness\deploy\deploy.ps1 doctor  -Platform android|ios
  .harness\deploy\deploy.ps1 build   -Platform android|ios [-Version 1.2.0] [-BuildNumber 58] [-BundleId x] [-Development]
  .harness\deploy\deploy.ps1 install -Platform ios [-Job <id>] [-Udid <udid>]
  .harness\deploy\deploy.ps1 upload  -Platform android|ios [-Job <id>] -ConfirmUpload [-ReleaseStatus completed|draft]
  .harness\deploy\deploy.ps1 cleanup -Platform ios [-Job <id>]

Reglas estructurales (no solo prosa):
  - upload exige -ConfirmUpload. El leader solo lo pasa tras la aprobacion
    humana explicita de ESA subida concreta.
  - Solo existen TestFlight (iOS) y la pista "internal" (Android). No hay
    ningun camino a produccion.
  - Los secretos se leen del Windows Credential Manager y se pasan a Unity
    solo por variables de entorno del proceso hijo. Nunca a disco ni a logs.

Android: compila, firma y sube desde este Windows. iOS: genera el proyecto
Xcode aqui y el Mac de builds hace archive / firma / TestFlight.
Ver .harness/docs/deploy_pipeline.md.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('doctor', 'build', 'install', 'upload', 'cleanup')]
    [string]$Action,
    [Parameter(Mandatory = $true)]
    [ValidateSet('android', 'ios')]
    [string]$Platform,
    [string]$Version,
    [ValidatePattern('^\d+(\.\d+)*$')]
    [string]$BuildNumber,
    [ValidatePattern('^[A-Za-z0-9.-]+$')]
    [string]$BundleId,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string]$Job,
    [ValidatePattern('^[A-Za-z0-9-]+$')]
    [string]$Udid,
    [switch]$Development,
    [switch]$ConfirmUpload,
    [ValidateSet('completed', 'draft')]
    [string]$ReleaseStatus = 'completed'
)

. "$PSScriptRoot\common.ps1"

$Root = Get-HarnessProjectRoot
$Config = Get-HarnessConfig $Root
$script:Secrets = @()
$script:Problems = 0

if (-not (Get-Cfg $Config 'deploy_enabled' $false)) {
    Write-Fail 'deploy_enabled no es true en .harness/unity.config.json: el modulo de despliegue esta desactivado en este proyecto.'
    exit 1
}
$platforms = @(Get-Cfg $Config 'deploy_platforms' @())
if ($platforms -notcontains $Platform) {
    Write-Fail "La plataforma '$Platform' no esta en deploy_platforms ($($platforms -join ', '))."
    exit 1
}

$ProjectId = Get-Cfg $Config 'deploy_project_id'
if (-not $ProjectId -or $ProjectId -notmatch '^[A-Za-z0-9._-]+$') {
    Write-Fail 'deploy_project_id falta o tiene caracteres no validos ([A-Za-z0-9._-]).'
    exit 1
}
$BuildsRoot = Join-Path $Root "Builds\harness\$Platform"
$DeployLog = Join-Path $Root '.harness\progress\deploy_log.md'

function Add-DeployLog([string]$Act, [string]$JobId, [string]$Detail, [string]$Result) {
    if (-not (Test-Path $DeployLog)) {
        Set-Content -Encoding UTF8 $DeployLog "# Registro de despliegues`r`n`r`nGenerado por .harness/deploy/deploy.ps1. Sin secretos.`r`n`r`n| Fecha | Plataforma | Accion | Job | Detalle | Resultado | Usuario |`r`n|---|---|---|---|---|---|---|"
    }
    $line = "| $(Get-Date -Format 'yyyy-MM-dd HH:mm') | $Platform | $Act | $JobId | $(Protect-Text $Detail $script:Secrets) | $Result | $env:USERNAME |"
    Add-Content -Encoding UTF8 $DeployLog $line
}

function Get-LastJob {
    $f = Join-Path $BuildsRoot 'last_job.txt'
    if (Test-Path $f) { return (Get-Content $f -TotalCount 1).Trim() }
    return $null
}

function Resolve-Job {
    $j = $Job
    if (-not $j) { $j = Get-LastJob }
    if (-not $j) { throw "No hay job previo para ${Platform}: ejecuta 'build' primero o pasa -Job." }
    return $j
}

# ---------------------------------------------------------------- doctor ---
function Invoke-Doctor {
    Write-Host "-- doctor ($Platform) - $Root"
    $info = Get-UnityProjectInfo $Root
    $unity = Find-UnityEditor $info.UnityVersion $Config
    if ($unity) { Write-Ok "Unity $($info.UnityVersion): $unity" }
    else { Write-Fail "Unity $($info.UnityVersion) no esta instalado (Unity Hub). No se compila con otra version."; $script:Problems++ }
    if ($unity) {
        if (Test-UnityModule $unity $Platform) { Write-Ok "Modulo $Platform instalado en ese Unity" }
        else { Write-Fail "Falta el modulo $Platform en Unity $($info.UnityVersion) (anadelo desde Unity Hub)."; $script:Problems++ }
    }
    if (Test-Path (Join-Path $Root 'Packages\com.harness.build\package.json')) { Write-Ok 'Paquete com.harness.build presente' }
    else { Write-Fail 'Falta Packages/com.harness.build (lo copia el Paso 5C de /unity-claude-code-harness desde .harness/deploy/unity_package/).'; $script:Problems++ }
    if (Test-UnityProjectOpen $Root) { Write-Warn2 'El proyecto esta abierto en el Editor: cierralo antes de compilar en batchmode.' }
    if ($Root.Length -gt 90) { Write-Warn2 "Ruta del proyecto larga ($($Root.Length) caracteres): IL2CPP puede fallar por el limite de 260 de Windows." }

    if ($Platform -eq 'android') {
        $id = Get-Cfg $Config 'deploy_android_package' $info.AndroidId
        Write-Info "App: $id  version $($info.BundleVersion)  versionCode actual $($info.AndroidCode) (sugerido: $([int]$info.AndroidCode + 1))"
        $ks = Expand-UserPath (Get-Cfg $Config 'deploy_android_keystore_path')
        if (-not $ks) { Write-Fail 'deploy_android_keystore_path no configurado.'; $script:Problems++ }
        elseif (Test-PathInside $ks $Root) { Write-Fail 'El keystore esta DENTRO del proyecto: muevelo fuera (nunca debe poder versionarse).'; $script:Problems++ }
        elseif (Test-Path $ks) { Write-Ok "Keystore encontrado (propietario: $(Get-Cfg $Config 'deploy_android_keystore_owner' 'sin anotar'))" }
        else { Write-Fail "No existe el keystore en $ks"; $script:Problems++ }
        if (-not (Get-Cfg $Config 'deploy_android_key_alias')) { Write-Fail 'deploy_android_key_alias no configurado.'; $script:Problems++ }
        $prefix = Get-Cfg $Config 'deploy_android_secret_prefix' "harness/$ProjectId"
        foreach ($kv in (Get-HarnessSecretNames $prefix).GetEnumerator()) {
            if ([HarnessCred]::Exists($kv.Value)) { Write-Ok "Secreto '$($kv.Value)' presente en el Credential Manager" }
            else { Write-Fail "Falta '$($kv.Value)' en el Credential Manager: powershell -NoProfile -ExecutionPolicy Bypass -File .harness\deploy\setup_secrets.ps1 -ProjectId $ProjectId (lo ejecuta el humano)"; $script:Problems++ }
        }
        $json = Expand-UserPath (Get-Cfg $Config 'deploy_android_play_json_path')
        if (-not $json) { Write-Warn2 'deploy_android_play_json_path no configurado: solo se podra compilar, no subir.' }
        elseif (Test-PathInside $json $Root) { Write-Fail 'El JSON de la service account esta DENTRO del proyecto: muevelo fuera.'; $script:Problems++ }
        elseif (Test-Path $json) { Write-Ok 'Service account de Google Play encontrada' }
        else { Write-Warn2 "No existe el JSON de la service account en $json (subida no disponible)." }
        if (Get-Command fastlane -ErrorAction SilentlyContinue) { Write-Ok 'fastlane disponible en Windows' }
        else { Write-Warn2 'fastlane no esta instalado en Windows: solo se podra compilar, no subir (ver deploy_pipeline.md, "fastlane en Windows").' }
    }
    else {
        Write-Info "App: $($info.IosId)  version $($info.BundleVersion)  build actual $($info.IosBuild) (sugerido: $([int]$info.IosBuild + 1))"
        $team = Get-Cfg $Config 'deploy_ios_team_id'
        if ($team -match '^[A-Z0-9]{10}$') { Write-Ok "Team ID: $team" } else { Write-Warn2 'deploy_ios_team_id no configurado o invalido: el build del Mac no podra firmar.' }
        $mac = Get-MacTarget $Config
        if (-not (Test-Path $mac.Key)) { Write-Fail "No existe la clave SSH $($mac.Key) (ver .harness/deploy/mac/SETUP.md)."; $script:Problems++ ; return }
        $fp = Get-Cfg $Config 'deploy_ios_mac_host_fingerprint'
        try {
            $h = Resolve-MacHost $mac
            Write-Ok "Mac alcanzable en '$h' (clave de host verificada contra known_hosts)"
            if ($fp) {
                # ssh (StrictHostKeyChecking=yes, HostKeyAlgorithms=ssh-ed25519) ya comprobo
                # la clave real contra known_hosts; aqui se comprueba que la entrada de
                # known_hosts es la huella fijada en la config. (ssh-keyscan de Windows
                # no devuelve nada, por eso no se usa.)
                $known = Invoke-NativeCapture (Join-Path $env:SystemRoot 'System32\OpenSSH\ssh-keygen.exe') @('-F', $h, '-l')
                $actual = ($known | Where-Object { $_ -match '\sED25519\s' } | ForEach-Object { if ($_ -match '(SHA256:\S+)') { $Matches[1] } } | Select-Object -First 1)
                if ($actual -eq $fp) { Write-Ok 'Huella del Mac coincide con deploy_ios_mac_host_fingerprint' }
                else { Write-Fail "La huella del Mac ($actual) NO coincide con la configurada ($fp). No se continua."; $script:Problems++ }
            }
            Invoke-Mac $mac 'doctor' | ForEach-Object {
                if ($_ -match '^HARNESS FAIL') { Write-Fail ($_ -replace '^HARNESS FAIL\s*', 'Mac: '); $script:Problems++ }
                elseif ($_ -match '^HARNESS WARN') { Write-Warn2 ($_ -replace '^HARNESS WARN\s*', 'Mac: ') }
                elseif ($_ -match '^HARNESS OK') { Write-Ok ($_ -replace '^HARNESS OK\s*', 'Mac: ') }
                else { Write-Info "Mac: $_" }
            }
        } catch { Write-Fail $_.Exception.Message; $script:Problems++ }
    }
    if ($script:Problems -eq 0) { Write-Ok 'doctor sin errores' } else { Write-Fail "doctor: $($script:Problems) problema(s)" }
}

# ----------------------------------------------------------------- build ---
function Invoke-UnityBuild([string]$OutPath, [string]$JobId, [hashtable]$ChildEnv) {
    $info = Get-UnityProjectInfo $Root
    $unity = Find-UnityEditor $info.UnityVersion $Config
    if (-not $unity) { throw "Unity $($info.UnityVersion) no esta instalado." }
    if (-not (Test-UnityModule $unity $Platform)) { throw "Falta el modulo $Platform en Unity $($info.UnityVersion)." }
    if (-not (Test-Path (Join-Path $Root 'Packages\com.harness.build\package.json'))) { throw 'Falta Packages/com.harness.build.' }
    if (Test-UnityProjectOpen $Root) { throw 'El proyecto esta abierto en el Editor. Cierralo y reintenta.' }

    $logFile = Join-Path $BuildsRoot "$JobId\unity.log"
    New-Item -ItemType Directory -Force (Split-Path $logFile) | Out-Null
    $target = if ($Platform -eq 'android') { 'Android' } else { 'iOS' }
    $a = @('-batchmode', '-quit', '-projectPath', $Root, '-buildTarget', $target,
           '-executeMethod', 'HarnessBuild.BuildCli.Build', '-harnessOut', $OutPath, '-logFile', $logFile)
    if ($Version) { $a += @('-harnessVersion', $Version) }
    if ($BuildNumber) { $a += @('-harnessBuildNumber', $BuildNumber) }
    if ($BundleId) { $a += @('-harnessBundleId', $BundleId) }
    if ($Development) { $a += '-harnessDevelopment' }
    if ($Platform -eq 'ios') { $team = Get-Cfg $Config 'deploy_ios_team_id'; if ($team) { $a += @('-harnessTeamId', $team) } }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $unity
    $psi.Arguments = ($a | ForEach-Object { if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ } }) -join ' '
    $psi.UseShellExecute = $false
    foreach ($k in $ChildEnv.Keys) { $psi.EnvironmentVariables[$k] = $ChildEnv[$k] }  # solo en el hijo
    Write-Info "Unity $($info.UnityVersion) batchmode -> $target (log: $logFile)"
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = [Diagnostics.Process]::Start($psi)
    $p.WaitForExit()
    $sw.Stop()
    $lines = @()
    if (Test-Path $logFile) {
        $raw = Get-Content -Raw $logFile
        $masked = Protect-Text $raw $script:Secrets
        if ($masked -ne $raw) { Set-Content -Encoding UTF8 $logFile $masked }
        $lines = $masked -split "`r?`n" | Where-Object { $_ -match '\[HarnessBuild\]|error CS\d+' }
    }
    $lines | Select-Object -First 30 | ForEach-Object { Write-Info $_ }
    if ($p.ExitCode -ne 0) { throw "Unity termino con exit $($p.ExitCode) tras $([int]$sw.Elapsed.TotalSeconds)s. Revisa $logFile" }
    Write-Ok "Unity OK en $([int]$sw.Elapsed.TotalSeconds)s"
}

function Invoke-Build {
    $jobId = "$ProjectId-$Platform-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    $jobDir = Join-Path $BuildsRoot $jobId
    New-Item -ItemType Directory -Force $jobDir | Out-Null
    $info = Get-UnityProjectInfo $Root
    $detail = "v$(if ($Version) { $Version } else { $info.BundleVersion }) build $(if ($BuildNumber) { $BuildNumber } else { 'sin cambio' })$(if ($BundleId) { " id $BundleId" })$(if ($Development) { ' dev' })"

    if ($Platform -eq 'android') {
        $ks = Expand-UserPath (Get-Cfg $Config 'deploy_android_keystore_path')
        if (-not $ks -or -not (Test-Path $ks)) { throw 'Keystore no configurado o inexistente (ejecuta doctor).' }
        if (Test-PathInside $ks $Root) { throw 'El keystore esta dentro del proyecto: muevelo fuera.' }
        $prefix = Get-Cfg $Config 'deploy_android_secret_prefix' "harness/$ProjectId"
        $names = Get-HarnessSecretNames $prefix
        $ksPass = [HarnessCred]::Read($names.keystore_pass)
        $keyPass = [HarnessCred]::Read($names.key_pass)
        if (-not $ksPass -or -not $keyPass) { throw "Faltan secretos en el Credential Manager (el humano ejecuta: powershell -NoProfile -ExecutionPolicy Bypass -File .harness\deploy\setup_secrets.ps1 -ProjectId $ProjectId)." }
        $script:Secrets += @($ksPass, $keyPass)
        $out = Join-Path $jobDir "$ProjectId.aab"
        $childEnv = @{
            HARNESS_KEYSTORE_PATH = $ks; HARNESS_KEYSTORE_PASS = $ksPass
            HARNESS_KEYALIAS_NAME = (Get-Cfg $Config 'deploy_android_key_alias'); HARNESS_KEYALIAS_PASS = $keyPass
        }
        try { Invoke-UnityBuild $out $jobId $childEnv }
        catch { Add-DeployLog 'build' $jobId $detail 'FAIL'; throw }
        finally { $ksPass = $null; $keyPass = $null; $childEnv = $null }
        if (-not (Test-Path $out)) { Add-DeployLog 'build' $jobId $detail 'FAIL'; throw "Unity no genero $out" }
        Write-Ok ("AAB: {0} ({1:N1} MB)" -f $out, ((Get-Item $out).Length / 1MB))
    }
    else {
        $xcode = Join-Path $jobDir 'xcode'
        try { Invoke-UnityBuild $xcode $jobId @{} }
        catch { Add-DeployLog 'build' $jobId $detail 'FAIL'; throw }
        $team = Get-Cfg $Config 'deploy_ios_team_id'
        if ($team -notmatch '^[A-Z0-9]{10}$') { throw 'deploy_ios_team_id no configurado: el proyecto Xcode se genero pero el Mac no puede firmarlo.' }
        $mac = Get-MacTarget $Config
        Write-Info 'Enviando proyecto Xcode al Mac...'
        Send-FolderToMac $mac $xcode $jobId | ForEach-Object { Write-Info "Mac: $_" }
        $method = if ($Development) { 'debugging' } else { 'app-store-connect' }
        Write-Info "Archive + firma en el Mac ($method)... puede tardar varios minutos."
        $res = Invoke-Mac $mac "ios-build $jobId $team $method"
        $rc = $script:NativeExit
        $res | ForEach-Object { Write-Info "Mac: $_" }
        if ($rc -ne 0) {
            Invoke-Mac $mac "log $jobId 40" | ForEach-Object { Write-Info "Mac log: $_" }
            Add-DeployLog 'build' $jobId $detail 'FAIL'
            throw "Build iOS fallido en el Mac. El job sigue alli para revisarlo; liberalo con: powershell -NoProfile -ExecutionPolicy Bypass -File .harness\deploy\deploy.ps1 cleanup -Platform ios -Job $jobId"
        }
        Remove-Item -Recurse -Force $xcode   # la copia util ya esta en el Mac
    }
    Set-Content -Encoding ASCII (Join-Path $BuildsRoot 'last_job.txt') $jobId
    Add-DeployLog 'build' $jobId $detail 'OK'
    Write-Ok "Job: $jobId"
}

# --------------------------------------------------------------- install ---
function Invoke-Install {
    if ($Platform -ne 'ios') { throw 'install solo esta soportado para iOS (iPhone conectado al Mac).' }
    $jobId = Resolve-Job
    $mac = Get-MacTarget $Config
    $sub = "ios-install $jobId"; if ($Udid) { $sub += " $Udid" }
    $res = Invoke-Mac $mac $sub
    $rc = $script:NativeExit
    $res | ForEach-Object { Write-Info "Mac: $_" }
    Add-DeployLog 'install' $jobId "iPhone $(if ($Udid) { $Udid } else { 'unico conectado' })" $(if ($rc -eq 0) { 'OK' } else { 'FAIL' })
    if ($rc -ne 0) { throw 'Instalacion fallida.' }
}

# ---------------------------------------------------------------- upload ---
function Invoke-Upload {
    if (-not $ConfirmUpload) {
        throw 'upload requiere -ConfirmUpload. Solo se pasa tras la aprobacion humana explicita de ESTA subida (ver /deploy).'
    }
    $jobId = Resolve-Job
    if ($Platform -eq 'android') {
        $aab = Join-Path $BuildsRoot "$jobId\$ProjectId.aab"
        if (-not (Test-Path $aab)) { throw "No existe $aab (job $jobId)." }
        $json = Expand-UserPath (Get-Cfg $Config 'deploy_android_play_json_path')
        if (-not $json -or -not (Test-Path $json)) { throw 'Falta el JSON de la service account de Google Play (deploy_android_play_json_path).' }
        if (Test-PathInside $json $Root) { throw 'El JSON de la service account esta dentro del proyecto: muevelo fuera.' }
        if (-not (Get-Command fastlane -ErrorAction SilentlyContinue)) { throw 'fastlane no esta instalado en Windows (ver deploy_pipeline.md, "fastlane en Windows").' }
        $pkg = Get-Cfg $Config 'deploy_android_package' (Get-UnityProjectInfo $Root).AndroidId
        $env:H_AAB = $aab; $env:H_JSON = $json; $env:H_PACKAGE = $pkg; $env:H_STATUS = $ReleaseStatus
        $env:FASTLANE_SKIP_UPDATE_CHECK = '1'; $env:FASTLANE_HIDE_CHANGELOG = '1'; $env:FASTLANE_DISABLE_COLORS = '1'; $env:FASTLANE_SKIP_DOCS = '1'
        $upLog = Join-Path $BuildsRoot "$jobId\upload.log"
        Push-Location $PSScriptRoot   # fastlane busca ./fastlane/Fastfile
        try {
            $fl = (Get-Command fastlane).Source
            $flOut = Invoke-NativeCapture $fl @('android', 'android_internal')
            $rc = $script:NativeExit
            $flOut | Set-Content -Encoding UTF8 $upLog
            $flOut | Select-Object -Last 15 | ForEach-Object { Write-Info $_ }
        } finally {
            Pop-Location
            Remove-Item Env:H_AAB, Env:H_JSON, Env:H_PACKAGE, Env:H_STATUS -ErrorAction SilentlyContinue
        }
        Add-DeployLog 'upload' $jobId "Google Play internal ($ReleaseStatus) $pkg" $(if ($rc -eq 0) { 'OK' } else { 'FAIL' })
        if ($rc -ne 0) { throw "Subida a Google Play fallida. Log: $upLog" }
        Write-Ok 'Subido a Google Play, pista internal. Promocionar a produccion lo hace un humano en Play Console.'
    }
    else {
        $team = Get-Cfg $Config 'deploy_ios_team_id'
        $mac = Get-MacTarget $Config
        $res = Invoke-Mac $mac "ios-upload $jobId $team confirmed"
        $rc = $script:NativeExit
        $res | ForEach-Object { Write-Info "Mac: $_" }
        Add-DeployLog 'upload' $jobId "TestFlight team $team" $(if ($rc -eq 0) { 'OK' } else { 'FAIL' })
        if ($rc -ne 0) { throw 'Subida a TestFlight fallida.' }
        Write-Ok 'Subido a TestFlight. Promocionar a produccion lo hace un humano en App Store Connect.'
    }
}

# --------------------------------------------------------------- cleanup ---
function Invoke-Cleanup {
    if ($Platform -ne 'ios') { throw 'cleanup solo aplica al Mac (iOS). Los builds Android quedan en Builds/harness/android/.' }
    $jobId = Resolve-Job
    Invoke-Mac (Get-MacTarget $Config) "cleanup $jobId" | ForEach-Object { Write-Info "Mac: $_" }
}

try {
    switch ($Action) {
        'doctor'  { Invoke-Doctor; if ($script:Problems -gt 0) { exit 1 } }
        'build'   { Invoke-Build }
        'install' { Invoke-Install }
        'upload'  { Invoke-Upload }
        'cleanup' { Invoke-Cleanup }
    }
    exit 0
} catch {
    Write-Fail (Protect-Text $_.Exception.Message $script:Secrets)
    exit 1
}
