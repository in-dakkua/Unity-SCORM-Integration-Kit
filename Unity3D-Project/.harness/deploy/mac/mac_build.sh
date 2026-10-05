#!/bin/bash
# mac_build.sh — Lado Mac del modulo de despliegue del harness.
#
# Vive en el Mac de builds (~/harness/mac_build.sh, cuenta dedicada) y es lo
# UNICO que puede ejecutar la clave SSH de cada dev si en authorized_keys se
# usa command="/Users/<cuenta>/harness/mac_build.sh" (forced command): en ese
# caso los argumentos llegan en $SSH_ORIGINAL_COMMAND. Sin forced command se
# puede invocar igual: ssh <mac> '~/harness/mac_build.sh doctor'.
#
# Subcomandos (lista cerrada, cualquier otra cosa se rechaza):
#   version
#   doctor
#   devices
#   receive       <job>                       (stdin: tar.gz)
#   ios-build     <job> <team_id> <debugging|app-store-connect>
#   ios-install   <job> [udid]
#   ios-upload    <job> <team_id> confirmed
#   log           <job> [lineas]
#   cleanup       <job>
#
# Este Mac es SOLO para iOS: Android se compila, firma y sube desde Windows
# (ver .harness/deploy/deploy.ps1). Nunca hay subcomando para produccion: la
# unica subida es a TestFlight. Promocionar a produccion lo hace un humano
# desde App Store Connect.
#
# Compatible con /bin/bash 3.2 (el de macOS). Ver .harness/deploy/mac/SETUP.md.

set -u
umask 077

SCRIPT_VERSION="1"
HARNESS_DIR="$HOME/harness"          # este script + fastlane/Fastfile
SECRETS_DIR="$HOME/.harness"         # keychain_pass, teams/ (chmod 700)
BASE="$HOME/harness-builds"          # un directorio por job
LOCK_DIR="$BASE/.lock"
KEYCHAIN="$HOME/Library/Keychains/harness-build.keychain-db"
PASSFILE="$SECRETS_DIR/keychain_pass"
MIN_FREE_GB="${HARNESS_MIN_FREE_GB:-15}"

export LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8
export PATH="/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin"
export DEVELOPER_DIR="${HARNESS_DEVELOPER_DIR:-/Applications/Xcode.app/Contents/Developer}"
export FASTLANE_SKIP_UPDATE_CHECK=1 FASTLANE_HIDE_CHANGELOG=1 FASTLANE_DISABLE_COLORS=1 FASTLANE_SKIP_DOCS=1 CI=1

ok()   { echo "HARNESS OK   $*"; }
warn() { echo "HARNESS WARN $*"; }
die()  { echo "HARNESS FAIL $*"; exit 1; }

# ── Argumentos ────────────────────────────────────────────────────────────
if [ -n "${SSH_ORIGINAL_COMMAND:-}" ]; then
  read -r -a ARGS <<< "$SSH_ORIGINAL_COMMAND"
else
  ARGS=("$@")
fi
[ "${#ARGS[@]}" -gt 0 ] || die "sin subcomando (ver cabecera de mac_build.sh)"
# Si el cliente mando "…/mac_build.sh doctor", descarta el primer token.
case "${ARGS[0]}" in */mac_build.sh|mac_build.sh) ARGS=("${ARGS[@]:1}") ;; esac
[ "${#ARGS[@]}" -gt 0 ] || die "sin subcomando"

for a in "${ARGS[@]}"; do
  [[ "$a" =~ ^[A-Za-z0-9._:-]+$ ]] || die "argumento no permitido: solo [A-Za-z0-9._:-]"
done
CMD="${ARGS[0]}"
arg() { local i="$1"; if [ "${#ARGS[@]}" -gt "$i" ]; then echo "${ARGS[$i]}"; fi; }

# Valida el job id y deja la ruta en $J. NO usar dentro de $(...): un die en
# una subshell no aborta el script (y con j vacio, un rm -rf podria apuntar
# a donde no toca).
set_job() {
  local job="$1"
  # Debe empezar por alfanumerico: "." o ".." apuntarian fuera de jobs/.
  [[ "$job" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$ ]] || die "job id invalido"
  J="$BASE/jobs/$job"
}

free_gb() { df -g "$HOME" | awk 'NR==2 {print $4}'; }

# ── Lock (un solo build a la vez en el Mac compartido) ────────────────────
acquire_lock() {
  mkdir -p "$BASE"
  if mkdir "$LOCK_DIR" 2>/dev/null; then
    echo "$1 $(date '+%Y-%m-%d %H:%M')" > "$LOCK_DIR/owner"
    trap release_lock EXIT
    return 0
  fi
  local age=0
  if [ -f "$LOCK_DIR/owner" ]; then
    age=$(( $(date +%s) - $(stat -f %m "$LOCK_DIR/owner") ))
  fi
  if [ "$age" -gt 10800 ]; then
    warn "lock huerfano de hace $((age/60)) min — se libera"
    rm -rf "$LOCK_DIR"
    acquire_lock "$1"
    return 0
  fi
  die "Mac ocupado: $(cat "$LOCK_DIR/owner" 2>/dev/null || echo 'otro build en curso'). Reintenta mas tarde."
}
release_lock() { rm -rf "$LOCK_DIR"; }

# ── Keychain dedicado ─────────────────────────────────────────────────────
# No se fia del exit code de unlock-keychain: exige fichero de keychain,
# fichero de contraseña y al menos 1 identidad de firma valida.
unlock_keychain() {
  [ -f "$KEYCHAIN" ] || die "no existe $KEYCHAIN (ver SETUP.md, bloque 'Keychain de builds')"
  [ -f "$PASSFILE" ] || die "no existe $PASSFILE (ver SETUP.md)"
  security unlock-keychain -p "$(cat "$PASSFILE")" "$KEYCHAIN" >/dev/null 2>&1 || die "no se pudo desbloquear el keychain de builds"
  security set-keychain-settings -lut 3600 "$KEYCHAIN" >/dev/null 2>&1
  local n
  n=$(security find-identity -v -p codesigning "$KEYCHAIN" | awk '/valid identities found/ {print $1}')
  [ "${n:-0}" -ge 1 ] || die "el keychain de builds no tiene identidades de firma validas"
}
lock_keychain() { security lock-keychain "$KEYCHAIN" >/dev/null 2>&1 || true; }

team_file() { echo "$SECRETS_DIR/teams/$1/$2"; }

# ── Subcomandos ───────────────────────────────────────────────────────────
cmd_version() { echo "mac_build.sh v$SCRIPT_VERSION"; }

cmd_doctor() {
  cmd_version
  local v; v=$(xcodebuild -version 2>/dev/null | head -1)
  [ -n "$v" ] && ok "Xcode: $v ($DEVELOPER_DIR)" || warn "xcodebuild no responde con DEVELOPER_DIR=$DEVELOPER_DIR"
  xcodebuild -checkFirstLaunchStatus >/dev/null 2>&1 && ok "Xcode first launch hecho" || warn "Xcode necesita first launch / licencia (lo hace un admin una vez)"
  command -v fastlane >/dev/null && ok "fastlane: $(fastlane --version 2>/dev/null | grep -Eo 'fastlane [0-9.]+' | tail -1)" || warn "fastlane no encontrado en /opt/homebrew/bin"
  command -v pod >/dev/null && ok "CocoaPods: $(pod --version 2>/dev/null)" || warn "CocoaPods no encontrado"
  [ -f "$HARNESS_DIR/fastlane/Fastfile" ] && ok "Fastfile presente" || warn "falta $HARNESS_DIR/fastlane/Fastfile (subidas no disponibles)"
  local f; f=$(free_gb)
  [ "${f:-0}" -ge "$MIN_FREE_GB" ] && ok "Disco libre: ${f} GB" || warn "Disco libre: ${f} GB (< ${MIN_FREE_GB} GB recomendados)"
  if [ -f "$KEYCHAIN" ] && [ -f "$PASSFILE" ]; then
    local perm; perm=$(stat -f %Lp "$PASSFILE")
    [ "$perm" = "600" ] || warn "permisos de keychain_pass = $perm (deberian ser 600)"
    if security unlock-keychain -p "$(cat "$PASSFILE")" "$KEYCHAIN" >/dev/null 2>&1; then
      local n; n=$(security find-identity -v -p codesigning "$KEYCHAIN" | awk '/valid identities found/ {print $1}')
      [ "${n:-0}" -ge 1 ] && ok "Keychain de builds: $n identidad(es) de firma" || warn "Keychain de builds sin identidades de firma"
      lock_keychain
    else
      warn "Keychain de builds no se puede desbloquear"
    fi
  else
    warn "Keychain de builds no configurado (firma por SSH no disponible)"
  fi
  if [ -d "$SECRETS_DIR/teams" ]; then
    for t in "$SECRETS_DIR"/teams/*; do
      [ -d "$t" ] || continue
      local id; id=$(basename "$t")
      if [ -f "$t/AuthKey.p8" ] && [ -f "$t/key_id" ] && [ -f "$t/issuer_id" ]; then ok "Equipo Apple $id: API key presente"; else warn "Equipo Apple $id: API key incompleta"; fi
    done
  fi
  [ -d "$LOCK_DIR" ] && warn "Mac ocupado: $(cat "$LOCK_DIR/owner" 2>/dev/null)" || ok "Mac libre (sin lock)"
}

cmd_devices() { xcrun devicectl list devices 2>&1; }

cmd_receive() {
  set_job "$(arg 1)"; local j="$J"
  [ -e "$j" ] && die "el job ya existe: $(arg 1)"
  local f; f=$(free_gb)
  [ "${f:-0}" -ge "$MIN_FREE_GB" ] || die "disco libre ${f} GB < ${MIN_FREE_GB} GB — limpia jobs antiguos (cleanup) antes de continuar"
  mkdir -p "$j/src" "$j/out"
  tar -xzf - -C "$j/src" || { rm -rf "$j"; die "fallo al descomprimir lo recibido"; }
  ok "recibido en $j/src ($(du -sh "$j/src" | awk '{print $1}'))"
}

find_xcode_container() {
  # Devuelve "-workspace X" o "-project X" dentro de src (maxdepth 2).
  local src="$1" p
  p=$(find "$src" -maxdepth 2 -name 'Unity-iPhone.xcworkspace' -type d | head -1)
  [ -n "$p" ] && { echo "workspace:$p"; return; }
  p=$(find "$src" -maxdepth 2 -name 'Unity-iPhone.xcodeproj' -type d | head -1)
  [ -n "$p" ] && { echo "project:$p"; return; }
}

cmd_ios_build() {
  local job; job=$(arg 1); local team; team=$(arg 2); local method; method=$(arg 3)
  set_job "$job"; local j="$J"
  [ -d "$j/src" ] || die "job sin fuente: $job (usa receive primero)"
  [[ "$team" =~ ^[A-Z0-9]{10}$ ]] || die "team_id invalido (10 caracteres A-Z0-9)"
  case "$method" in debugging|app-store-connect) ;; *) die "metodo invalido: usa debugging o app-store-connect" ;; esac

  acquire_lock "$job"
  local log="$j/build.log"; : > "$log"

  local c; c=$(find_xcode_container "$j/src")
  [ -n "$c" ] || die "no se encontro Unity-iPhone.xcodeproj en el job"
  local dir; dir=$(dirname "${c#*:}")
  if [ -f "$dir/Podfile" ]; then
    echo "pod install..." >> "$log"
    (cd "$dir" && pod install >> "$log" 2>&1) || die "pod install fallo (ver: log $job)"
    c="workspace:$dir/Unity-iPhone.xcworkspace"
  fi
  local container_flag="-project"; [ "${c%%:*}" = "workspace" ] && container_flag="-workspace"

  unlock_keychain
  local auth=()
  if [ -f "$(team_file "$team" AuthKey.p8)" ]; then
    auth=(-authenticationKeyPath "$(team_file "$team" AuthKey.p8)"
          -authenticationKeyID "$(cat "$(team_file "$team" key_id)")"
          -authenticationKeyIssuerID "$(cat "$(team_file "$team" issuer_id)")")
  else
    warn "sin API key para $team: se usan los perfiles ya descargados en este Mac"
  fi

  local start; start=$(date +%s)
  xcodebuild "$container_flag" "${c#*:}" -scheme Unity-iPhone -configuration Release \
    -destination 'generic/platform=iOS' -archivePath "$j/out/App.xcarchive" -derivedDataPath "$j/dd" \
    DEVELOPMENT_TEAM="$team" CODE_SIGN_STYLE=Automatic OTHER_CODE_SIGN_FLAGS="--keychain $KEYCHAIN" \
    -allowProvisioningUpdates ${auth[@]+"${auth[@]}"} archive >> "$log" 2>&1
  local rc=$?
  rm -rf "$j/dd"
  if [ $rc -ne 0 ]; then
    lock_keychain
    grep -E "error:" "$log" | sort | uniq -c | sort -rn | head -15
    die "xcodebuild archive fallo (rc=$rc, $(( $(date +%s) - start ))s). Detalle: log $job"
  fi
  ok "archive en $(( $(date +%s) - start ))s"

  if [ "$method" = "app-store-connect" ]; then
    /usr/libexec/PlistBuddy -c "Add :method string app-store-connect" \
      -c "Add :teamID string $team" -c "Add :signingStyle string automatic" \
      -c "Add :destination string export" "$j/out/ExportOptions.plist" >/dev/null
    xcodebuild -exportArchive -archivePath "$j/out/App.xcarchive" -exportPath "$j/out/ipa" \
      -exportOptionsPlist "$j/out/ExportOptions.plist" -allowProvisioningUpdates \
      ${auth[@]+"${auth[@]}"} OTHER_CODE_SIGN_FLAGS="--keychain $KEYCHAIN" >> "$log" 2>&1 \
      || { lock_keychain; die "exportArchive fallo. Detalle: log $job"; }
    ok "IPA: $(ls "$j/out/ipa"/*.ipa 2>/dev/null | head -1)"
  fi
  lock_keychain
  local app; app=$(ls -d "$j/out/App.xcarchive/Products/Applications/"*.app 2>/dev/null | head -1)
  ok "bundle id: $(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$app/Info.plist" 2>/dev/null) build: $(/usr/libexec/PlistBuddy -c 'Print :CFBundleVersion' "$app/Info.plist" 2>/dev/null)"
}

cmd_ios_install() {
  local job; job=$(arg 1); local udid; udid=$(arg 2)
  set_job "$job"; local j="$J"
  local app; app=$(ls -d "$j/out/App.xcarchive/Products/Applications/"*.app 2>/dev/null | head -1)
  [ -n "$app" ] || die "no hay .app en el job (usa ios-build con metodo debugging)"
  if [ -z "$udid" ]; then
    local found; found=$(xcrun devicectl list devices 2>/dev/null | awk '/connected/ && /physical/' )
    [ "$(printf '%s\n' "$found" | grep -c .)" -eq 1 ] || die "hay 0 o varios iPhone conectados: pasa el UDID (ver: devices)"
    udid=$(printf '%s\n' "$found" | grep -Eo '[0-9A-F]{8}-[0-9A-F]{16}|[0-9A-F-]{36}' | head -1)
  fi
  [ -n "$udid" ] || die "no se pudo determinar el UDID"
  xcrun devicectl device install app --device "$udid" "$app" > "$j/install.log" 2>&1 || { tail -15 "$j/install.log"; die "instalacion fallida"; }
  local bid; bid=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$app/Info.plist")
  ok "instalada $bid en $udid"
  if xcrun devicectl device process launch --device "$udid" "$bid" >> "$j/install.log" 2>&1; then
    ok "app lanzada"
  else
    warn "instalada pero no se pudo lanzar (iPhone bloqueado o sin confiar en el desarrollador)"
  fi
}

run_fastlane() {
  # fastlane busca ./fastlane/Fastfile (comprobado: con el Fastfile suelto en
  # la raiz no encuentra las lanes).
  [ -f "$HARNESS_DIR/fastlane/Fastfile" ] || die "falta $HARNESS_DIR/fastlane/Fastfile"
  (cd "$HARNESS_DIR" && fastlane "$@")
}

cmd_ios_upload() {
  local job; job=$(arg 1); local team; team=$(arg 2)
  [ "$(arg 3)" = "confirmed" ] || die "subida sin confirmacion explicita: se requiere el token 'confirmed'"
  set_job "$job"; local j="$J"
  local ipa; ipa=$(ls "$j/out/ipa"/*.ipa 2>/dev/null | head -1)
  [ -n "$ipa" ] || die "no hay IPA en el job (usa ios-build con metodo app-store-connect)"
  [ -f "$(team_file "$team" AuthKey.p8)" ] || die "falta la API key de App Store Connect del equipo $team (ver SETUP.md)"
  acquire_lock "$job"
  H_IPA="$ipa" H_KEY_ID="$(cat "$(team_file "$team" key_id)")" H_ISSUER_ID="$(cat "$(team_file "$team" issuer_id)")" \
  H_KEY_PATH="$(team_file "$team" AuthKey.p8)" run_fastlane ios_testflight >> "$j/upload.log" 2>&1 \
    || { tail -25 "$j/upload.log"; die "subida a TestFlight fallida"; }
  ok "subido a TestFlight (Apple tarda un rato en procesarlo antes de que aparezca)"
}

cmd_log() {
  set_job "$(arg 1)"; local j="$J"
  local n; n=$(arg 2); [[ "${n:-}" =~ ^[0-9]+$ ]] || n=80
  for f in build.log install.log upload.log; do
    [ -f "$j/$f" ] && { echo "── $f"; tail -n "$n" "$j/$f"; }
  done
}

cmd_cleanup() {
  set_job "$(arg 1)"; local j="$J"
  case "$j" in "$BASE"/jobs/?*) ;; *) die "ruta de job inesperada" ;; esac
  [ -d "$j" ] || die "no existe el job $(arg 1)"
  rm -rf "$j" && ok "job borrado. Disco libre: $(free_gb) GB"
}

case "$CMD" in
  version)        cmd_version ;;
  doctor)         cmd_doctor ;;
  devices)        cmd_devices ;;
  receive)        cmd_receive ;;
  ios-build)      cmd_ios_build ;;
  ios-install)    cmd_ios_install ;;
  ios-upload)     cmd_ios_upload ;;
  log)            cmd_log ;;
  cleanup)        cmd_cleanup ;;
  *)              die "subcomando no permitido: $CMD" ;;
esac
