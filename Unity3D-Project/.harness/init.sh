#!/usr/bin/env bash
# init.sh — Verificación e inicialización del entorno (variante Unity)
#
# Este script lo ejecuta el agente al COMENZAR una sesión y antes de
# declarar cualquier tarea como `done`. Si falla, la sesión no debe avanzar.
# Se invoca como `.harness/init.sh` desde la raíz de tu proyecto Unity.
#
# No depende de python3 para lo básico (leer .harness/unity.config.json es
# grep/sed puro) — python3 solo se usa, si está disponible, para una
# validación más profunda de feature_list.json. OJO: en Windows, "python3"
# a veces existe en el PATH pero es solo el stub de Microsoft Store que
# falla al invocarlo (no un intérprete real) — este script lo detecta
# ejecutándolo de verdad, no solo comprobando que el comando existe.

set -u
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
NC='\033[0m'

ok()    { printf "${GREEN}[OK]${NC}    %s\n" "$1"; }
warn()  { printf "${YELLOW}[WARN]${NC}  %s\n" "$1"; }
fail()  { printf "${RED}[FAIL]${NC}  %s\n" "$1"; }

EXIT_CODE=0
CONFIG=".harness/unity.config.json"

# Detección real de python3: command -v no basta, en Windows puede
# "existir" como alias de Microsoft Store que falla al ejecutarse.
HAVE_PY=0
if python3 -c "import sys" >/dev/null 2>&1; then
  HAVE_PY=1
elif python -c "import sys" >/dev/null 2>&1; then
  HAVE_PY=1
  PYBIN="python"
fi
PYBIN="${PYBIN:-python3}"

echo "── 1. Verificando inicialización del proyecto ──────────"

if [ ! -f "$CONFIG" ]; then
  fail "Falta $CONFIG. Este harness no está inicializado todavía."
  fail "Ejecuta /unity-claude-code-harness (ver .claude/commands/unity-claude-code-harness.md) antes de continuar."
  exit 1
fi
ok "Existe $CONFIG"

# Extracción de campos con grep/sed (sin depender de python3) — suficiente
# para un JSON plano como unity.config.json. Campo ausente o null -> "".
json_str_field() {
  local file="$1" key="$2"
  grep -oE "\"$key\"[[:space:]]*:[[:space:]]*(null|\"[^\"]*\")" "$file" 2>/dev/null \
    | head -n1 \
    | sed -E "s/^\"$key\"[[:space:]]*:[[:space:]]*//" \
    | sed -E 's/^"(.*)"$/\1/' \
    | sed 's/^null$//'
}

# scripts_root/tests_root son ahora un ARRAY de rutas (soporta proyectos con
# el código repartido en varias carpetas, p. ej. Assets/Scripts y
# Assets/Template/Scripts). El array debe ir en UNA SOLA LÍNEA del JSON —
# este parser es grep/sed, no un parser JSON real, y no puede seguir un
# array partido en varias líneas.
#
# Salida por stdout: primera línea = estado (OK|LEGACY|MULTILINE), el resto
# son los elementos del array (uno por línea) cuando el estado es OK o
# LEGACY. LEGACY significa que el campo todavía es un string plano (formato
# anterior a esta funcionalidad) — se acepta como único elemento, pero hay
# que avisar de que se migre a array.
json_root_array_field() {
  local file="$1" key="$2"
  local open_line
  open_line="$(grep -nE "\"$key\"[[:space:]]*:[[:space:]]*\[" "$file" 2>/dev/null | head -n1)"
  if [ -n "$open_line" ]; then
    local content
    content="$(grep -oE "\"$key\"[[:space:]]*:[[:space:]]*\[[^]]*\]" "$file" 2>/dev/null | head -n1)"
    if [ -z "$content" ]; then
      echo "MULTILINE"
      return
    fi
    content="$(printf '%s' "$content" | sed -E "s/^\"$key\"[[:space:]]*:[[:space:]]*\[//; s/\]\$//")"
    echo "OK"
    if [ -n "$(printf '%s' "$content" | tr -d '[:space:]')" ]; then
      printf '%s\n' "$content" | tr ',' '\n' | sed -E 's/^[[:space:]]*"//; s/"[[:space:]]*$//'
    fi
    return
  fi

  local plain
  plain="$(json_str_field "$file" "$key")"
  if [ -n "$plain" ]; then
    echo "LEGACY"
    printf '%s\n' "$plain"
  else
    echo "OK"
  fi
}

# Envuelve json_root_array_field: rellena el array bash de nombre $3 con las
# rutas leídas, y hace FAIL (formato roto) o WARN (formato legado) según
# corresponda. $1=file $2=key $3=nombre de variable array a rellenar.
load_root_array() {
  local file="$1" key="$2" __outvar="$3"
  local out status
  out="$(json_root_array_field "$file" "$key")"
  status="$(printf '%s\n' "$out" | head -n1)"
  local -a __items=()
  while IFS= read -r line; do
    [ -n "$line" ] && __items+=("$line")
  done < <(printf '%s\n' "$out" | tail -n +2)
  case "$status" in
    MULTILINE)
      fail "\"$key\" en $file es un array partido en varias líneas — este harness solo puede leerlo si va en UNA sola línea, p. ej. \"$key\": [\"Assets/Scripts\", \"Assets/Template/Scripts\"]. Corrígelo y vuelve a correr init.sh."
      EXIT_CODE=1
      ;;
    LEGACY)
      warn "\"$key\" en $file todavía es un string plano (formato anterior). Sigue funcionando como una única ruta, pero migra a array cuando puedas: \"$key\": [\"${__items[0]:-}\"]."
      ;;
  esac
  eval "$__outvar=(\"\${__items[@]}\")"
}

# Igual que json_str_field pero para campos booleanos (true/false, sin
# comillas) como automation_tool_verified.
json_bool_field() {
  local file="$1" key="$2"
  grep -oE "\"$key\"[[:space:]]*:[[:space:]]*(true|false)" "$file" 2>/dev/null \
    | head -n1 \
    | sed -E "s/^\"$key\"[[:space:]]*:[[:space:]]*//"
}

declare -a SCRIPTS_ROOTS=()
declare -a TESTS_ROOTS=()
load_root_array "$CONFIG" scripts_root SCRIPTS_ROOTS
load_root_array "$CONFIG" tests_root TESTS_ROOTS
[ ${#SCRIPTS_ROOTS[@]} -eq 0 ] && SCRIPTS_ROOTS=("Assets/Scripts")
[ ${#TESTS_ROOTS[@]} -eq 0 ] && TESTS_ROOTS=("Assets/Tests")

UNITY_VERSION="$(json_str_field "$CONFIG" unity_version)"
UNITY_EDITOR_PATH="$(json_str_field "$CONFIG" unity_editor_path)"
AUTOMATION_TOOL="$(json_str_field "$CONFIG" automation_tool)"
AUTOMATION_VERIFIED="$(json_bool_field "$CONFIG" automation_tool_verified)"
AUTOMATION_SETUP_TODO="$(json_str_field "$CONFIG" automation_tool_setup_todo)"
EDITOR_WRITE_POLICY="$(json_str_field "$CONFIG" editor_write_policy)"

SCRIPTS_ROOTS_JOINED="$(IFS=', '; echo "${SCRIPTS_ROOTS[*]}")"
TESTS_ROOTS_JOINED="$(IFS=', '; echo "${TESTS_ROOTS[*]}")"
ok "Config leída: scripts_root=[$SCRIPTS_ROOTS_JOINED] tests_root=[$TESTS_ROOTS_JOINED] unity_version=$UNITY_VERSION"

echo ""
echo "── 2. Verificando la herramienta de automatización ────"

if [ -z "$AUTOMATION_TOOL" ] || [ "$AUTOMATION_TOOL" = "none" ]; then
  ok "Sin herramienta de automatización configurada todavía (automation_tool: none) — nada que verificar."
elif [ "$AUTOMATION_VERIFIED" = "true" ]; then
  ok "Herramienta de automatización '$AUTOMATION_TOOL' verificada (automation_tool_verified: true)."
else
  warn "automation_tool está en '$AUTOMATION_TOOL' pero automation_tool_verified NO es true."
  if [ -n "$AUTOMATION_SETUP_TODO" ]; then
    warn "Pendiente anotado: $AUTOMATION_SETUP_TODO"
  fi
  warn "Ningún agente debería usar '$AUTOMATION_TOOL' para nada real (tests en batchmode,"
  warn "invocar el Editor vía MCP, etc.) hasta confirmar que funciona de verdad. Cuando"
  warn "esté listo, pide 'termina de configurar la herramienta de automatización' para"
  warn "retomar el Paso 4 de /unity-claude-code-harness sin repetir todo el cuestionario."
  if [ "$AUTOMATION_TOOL" = "unity_cli" ] && [ -z "$UNITY_EDITOR_PATH" ] && [ -n "$UNITY_VERSION" ]; then
    CANDIDATES=(
      "/c/Program Files/Unity/Hub/Editor/$UNITY_VERSION/Editor/Unity.exe"
      "$HOME/Unity/Hub/Editor/$UNITY_VERSION/Editor/Unity"
      "/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
    )
    FOUND_ANY=0
    for c in "${CANDIDATES[@]}"; do
      if [ -f "$c" ]; then
        warn "Candidato encontrado para unity_editor_path: $c"
        warn "Confírmalo y añádelo a $CONFIG (unity_editor_path) si es correcto."
        FOUND_ANY=1
      fi
    done
    if [ "$FOUND_ANY" -eq 0 ]; then
      warn "No se encontró automáticamente un ejecutable de Unity $UNITY_VERSION en las rutas"
      warn "típicas de Unity Hub. Ver .harness/docs/mcp_setup.md → 'Cómo localizar el"
      warn "ejecutable de Unity'. Si el proyecto Unity todavía no existe, eso es lo esperado —"
      warn "vuelve a este paso cuando lo tengas creado."
    fi
  fi
fi

case "$AUTOMATION_TOOL" in
  unity_mcp_official|unity_mcp_coplay_oss)
    if [ -z "$EDITOR_WRITE_POLICY" ]; then
      warn "editor_write_policy no está configurado para la herramienta MCP elegida."
      warn "Ningún agente debería escribir en una escena/prefab vía MCP hasta que se"
      warn "decida explícitamente si puede hacerlo directamente o si solo tú aplicas los"
      warn "cambios en el Editor. Pide 'termina de configurar la herramienta de"
      warn "automatización' (Paso 4 de /unity-claude-code-harness) para responder esto."
    else
      ok "editor_write_policy: $EDITOR_WRITE_POLICY"
    fi
    ;;
esac

echo ""
echo "── 2B. Verificando control de versiones ────────────────"

VCS_PROVIDER="$(json_str_field "$CONFIG" vcs_provider)"
VCS_VERIFIED="$(json_bool_field "$CONFIG" vcs_verified)"
VCS_SETUP_TODO="$(json_str_field "$CONFIG" vcs_setup_todo)"
VCS_RULES_SOURCE="$(json_str_field "$CONFIG" vcs_rules_source)"

if [ -z "$VCS_PROVIDER" ] || [ "$VCS_PROVIDER" = "none" ]; then
  ok "Sin proveedor de control de versiones configurado todavía (vcs_provider: none) — nada que verificar."
else
  ok "vcs_provider: $VCS_PROVIDER (fuente de reglas: ${VCS_RULES_SOURCE:-sin configurar})"
  if [ "$VCS_VERIFIED" = "true" ]; then
    ok "Control de versiones '$VCS_PROVIDER' verificado (vcs_verified: true)."
  else
    warn "vcs_provider está en '$VCS_PROVIDER' pero vcs_verified NO es true."
    if [ -n "$VCS_SETUP_TODO" ]; then
      warn "Pendiente anotado: $VCS_SETUP_TODO"
    fi
    warn "Ningún agente debería ejecutar ninguna acción de escritura de control de"
    warn "versiones (ni siquiera commit_local) hasta confirmar que está lista de verdad"
    warn "— ver .harness/docs/vcs_policy.md, sección 'Verificación (vcs_verified)'."
  fi
  if [ -z "$VCS_RULES_SOURCE" ]; then
    warn "vcs_rules_source no está configurado — el leader no sabe qué tabla de permisos"
    warn "seguir. Pide 'termina de configurar el control de versiones' para retomar el"
    warn "Paso 5B de /unity-claude-code-harness."
  fi
  if [ "$HAVE_PY" -eq 1 ]; then
    "$PYBIN" - "$CONFIG" <<'PY'
import json, sys
path = sys.argv[1]
valid_levels = {"allowed", "requires_approval", "forbidden"}
try:
    data = json.load(open(path))
    perms = data.get("vcs_permissions")
    if isinstance(perms, dict):
        for action, level in perms.items():
            if level not in valid_levels:
                print(f"[FAIL]  vcs_permissions.{action} tiene un valor inválido: {level!r} (válidos: {sorted(valid_levels)})")
                sys.exit(1)
        if perms.get("force_push_or_rewrite_history") == "allowed":
            print("[WARN]  vcs_permissions.force_push_or_rewrite_history está en 'allowed' — el default del harness es 'forbidden' (ver .harness/docs/vcs_policy.md). Confirma que es una decisión explícita del equipo, no un descuido.")
        print(f"[OK]    vcs_permissions válido ({len(perms)} overrides sobre la tabla base)")
except Exception:
    pass
PY
    if [ $? -ne 0 ]; then EXIT_CODE=1; fi
  fi
fi

echo ""
echo "── 3. Verificando que hay un proyecto Unity real ───────"

if [ ! -d "Assets" ]; then
  fail "No se encontró la carpeta Assets/ en la raíz."
  fail "Este harness debe vivir dentro de (o correrse desde) un proyecto Unity existente."
  EXIT_CODE=1
else
  ok "Existe Assets/"
fi

if [ ! -f "ProjectSettings/ProjectVersion.txt" ]; then
  warn "No se encontró ProjectSettings/ProjectVersion.txt (¿está el proyecto Unity en otra carpeta?)."
else
  ACTUAL_VERSION=$(grep -m1 'm_EditorVersion:' ProjectSettings/ProjectVersion.txt | awk '{print $2}')
  if [ -n "$UNITY_VERSION" ] && [ -n "$ACTUAL_VERSION" ] && [ "$UNITY_VERSION" != "$ACTUAL_VERSION" ]; then
    warn "unity_version en config ($UNITY_VERSION) no coincide con ProjectVersion.txt ($ACTUAL_VERSION)."
  else
    ok "Versión de Unity coherente: $ACTUAL_VERSION"
  fi
fi

for r in "${SCRIPTS_ROOTS[@]}"; do
  if [ ! -d "$r" ]; then
    warn "No existe $r todavía (¿proyecto nuevo o ruta distinta en config?)."
  else
    ok "Existe $r (scripts_root)"
  fi
done
for r in "${TESTS_ROOTS[@]}"; do
  if [ ! -d "$r" ]; then
    warn "No existe $r todavía (¿proyecto nuevo o ruta distinta en config?)."
  else
    ok "Existe $r (tests_root)"
  fi
done

echo ""
echo "── 3B. Verificando versión y ramas de release ──────────"

# Solo WARN, nunca FAIL: la versión y las ramas las gobiernan personas y
# CI (ver .harness/docs/release_management.md), el harness solo avisa.
RELEASE_GUIDELINE="$(json_str_field "$CONFIG" release_guideline)"
if [ -z "$RELEASE_GUIDELINE" ] || [ "$RELEASE_GUIDELINE" = "none" ]; then
  ok "Sin guideline de releases configurada (release_guideline: ${RELEASE_GUIDELINE:-sin configurar}) — nada que verificar."
else
  ok "release_guideline: $RELEASE_GUIDELINE (ver .harness/docs/release_management.md)"
  RELEASE_CURRENT_VERSION="$(json_str_field "$CONFIG" release_current_version)"
  for key in release_app_name release_project_code branch_main branch_release branch_dev branch_personal_pattern; do
    if [ -z "$(json_str_field "$CONFIG" "$key")" ]; then
      warn "$key está vacío — retoma el Paso 5C de /unity-claude-code-harness ('configura el versionado')."
    fi
  done

  if [ "$(json_str_field "$CONFIG" vcs_provider)" = "github" ]; then
    for key in branch_main branch_release branch_dev branch_personal_pattern branch_feature_pattern branch_hotfix_pattern; do
      case "$(json_str_field "$CONFIG" "$key")" in
        /*) warn "$key empieza por '/' (nombre estilo Plastic), pero vcs_provider es github: Git no admite ramas que empiecen por '/'. Usa p. ej. 'main', 'user/<ad_user>'." ;;
      esac
    done
  fi

  PLAYER_SETTINGS="ProjectSettings/ProjectSettings.asset"
  if [ -f "$PLAYER_SETTINGS" ]; then
    BUNDLE_VERSION="$(grep -m1 -E '^[[:space:]]*bundleVersion:' "$PLAYER_SETTINGS" | sed -E 's/^[[:space:]]*bundleVersion:[[:space:]]*//' | tr -d '\r')"
    BUNDLE_BASE="${BUNDLE_VERSION%%+*}"
    if [ -z "$BUNDLE_VERSION" ]; then
      warn "No se encontró bundleVersion en $PLAYER_SETTINGS."
    elif ! printf '%s' "$BUNDLE_BASE" | grep -qE '^[0-9]+\.[0-9]+\.[0-9]+$'; then
      warn "bundleVersion ('$BUNDLE_VERSION') no sigue MAJOR.MINOR.PATCH. El número lo decide el encargado del proyecto — el agente no lo corrige por su cuenta."
    elif [ -n "$RELEASE_CURRENT_VERSION" ] && [ "$BUNDLE_BASE" != "$RELEASE_CURRENT_VERSION" ]; then
      warn "bundleVersion ($BUNDLE_VERSION) no coincide con release_current_version ($RELEASE_CURRENT_VERSION). Falta el commit de versión, o la config está desactualizada."
    else
      ok "bundleVersion: $BUNDLE_VERSION"
    fi
  else
    warn "No se encontró $PLAYER_SETTINGS — no se puede comprobar la versión del proyecto."
  fi

  # Rama actual (solo Git: en Plastic la rama del workspace no se puede leer
  # de forma fiable sin el cliente cm conectado).
  if [ "$(json_str_field "$CONFIG" vcs_provider)" = "github" ] && git rev-parse --git-dir >/dev/null 2>&1; then
    CURRENT_BRANCH="$(git rev-parse --abbrev-ref HEAD 2>/dev/null)"
    for key in branch_release branch_main; do
      if [ -n "$CURRENT_BRANCH" ] && [ "$CURRENT_BRANCH" = "$(json_str_field "$CONFIG" "$key")" ]; then
        warn "Estás en '$CURRENT_BRANCH' ($key). El agente no trabaja ni commitea aquí: cambia a tu rama personal (branch_personal_pattern)."
      fi
    done
  fi
fi

echo ""
echo "── 4. Verificando archivos base del arnés ──────────────"

for f in .harness/AGENTS.md CLAUDE.md .harness/AI_TOOLS.md .harness/feature_list.json \
         .harness/feature_source.json .harness/progress/current.md \
         .harness/docs/architecture.md .harness/docs/conventions.md \
         .harness/docs/verification.md .harness/docs/mcp_setup.md \
         .harness/docs/project_rules.md .harness/docs/feature_source.md \
         .harness/docs/vcs_policy.md .harness/docs/release_management.md \
         .harness/CHECKPOINTS.md; do
  if [ ! -f "$f" ]; then
    fail "Falta archivo base: $f"
    EXIT_CODE=1
  else
    ok "Existe $f"
  fi
done

echo ""
echo "── 5. Resolviendo fuente del backlog ───────────────────"

FEATURE_SOURCE=".harness/feature_source.json"
BACKLOG_MODE="local"
if [ -f "$FEATURE_SOURCE" ]; then
  BACKLOG_MODE="$(json_str_field "$FEATURE_SOURCE" mode)"
  BACKLOG_MODE="${BACKLOG_MODE:-local}"
fi
ok "Modo de backlog: $BACKLOG_MODE"
if [ "$BACKLOG_MODE" = "asana" ]; then
  warn "Modo Asana: esta validación solo cubre la copia local en"
  warn ".harness/feature_list.json. El agente es responsable de mantenerla"
  warn "sincronizada con el proyecto de Asana real (ver"
  warn ".harness/docs/feature_source.md)."
fi

echo ""
echo "── 6. Validando feature_list.json ──────────────────────"

FEATURES=".harness/feature_list.json"

if [ "$HAVE_PY" -eq 1 ]; then
  "$PYBIN" - "$FEATURES" <<'PY'
import json, sys, os
path = sys.argv[1]
try:
    data = json.load(open(path))
    valid = {"pending", "in_progress", "done", "blocked"}
    if data.get("is_example_data"):
        print("[WARN]  feature_list.json todavía tiene is_example_data: true — son features de ejemplo, no trabajo real. Sustitúyelas o vacíalas antes de usar el harness en serio (ver /unity-claude-code-harness o docs/project_rules.md).")
    in_progress = [f for f in data["features"] if f["status"] == "in_progress"]
    if len(in_progress) > 1:
        print(f"[FAIL]  Hay {len(in_progress)} features en in_progress (máximo 1)")
        sys.exit(1)
    for f in data["features"]:
        if f["status"] not in valid:
            print(f"[FAIL]  Estado inválido en feature {f['id']}: {f['status']}")
            sys.exit(1)
        if f["status"] == "done" and f.get("requires_visual_check"):
            vc_path = os.path.join(".harness", "progress", f"visual_check_{f['name']}.md")
            if not os.path.isfile(vc_path):
                print(f"[FAIL]  Feature {f['id']} ({f['name']}) es done y requiere visual check, pero falta {vc_path}")
                sys.exit(1)
    print(f"[OK]    feature_list.json válido ({len(data['features'])} features)")
except Exception as e:
    print(f"[FAIL]  feature_list.json inválido: {e}")
    sys.exit(1)
PY
  if [ $? -ne 0 ]; then EXIT_CODE=1; fi
else
  warn "No se encontró un intérprete de Python funcional (python3/python): se omite la"
  warn "validación profunda de feature_list.json (solo se comprobó que existe el archivo)."
  warn "Esto NO bloquea el harness — Claude Code no necesita Python para nada más, solo"
  warn "esta comprobación extra la usa. Si la quieres, instala Python 3 desde"
  warn "https://www.python.org/downloads/ (en Windows, marca \"Add python.exe to PATH\""
  warn "durante la instalación; el alias de Microsoft Store que trae Windows por defecto"
  warn "NO sirve, hay que instalar el real)."
  if grep -oE '"status"[[:space:]]*:[[:space:]]*"in_progress"' "$FEATURES" 2>/dev/null | wc -l | grep -qv '^[01]$'; then
    warn "Aviso best-effort sin Python: parece haber más de una feature en in_progress — revísalo a mano."
  fi
fi

echo ""
echo "── 7. Ejecutando tests de Unity ────────────────────────"

if [ -n "$UNITY_EDITOR_PATH" ] && [ -x "$UNITY_EDITOR_PATH" ]; then
  mkdir -p .harness/progress
  if "$UNITY_EDITOR_PATH" -batchmode -projectPath . -runTests \
       -testPlatform EditMode -testResults ./.harness/progress/editmode-results.xml -quit; then
    ok "Tests EditMode ejecutados (ver .harness/progress/editmode-results.xml)"
  else
    fail "Tests EditMode fallaron o Unity batchmode devolvió error"
    EXIT_CODE=1
  fi
else
  warn "No hay unity_editor_path configurado o no es ejecutable."
  warn "Corre los tests manualmente desde Window > General > Test Runner en el Editor"
  warn "antes de pedir revisión o marcar una feature como done."
fi

echo ""
echo "── 8. Resumen ──────────────────────────────────────────"

if [ $EXIT_CODE -eq 0 ]; then
  ok "Entorno listo (revisa los [WARN] arriba: pueden requerir acción manual en el Editor)."
else
  fail "Entorno NO está listo. Resuelve los errores antes de avanzar."
fi

exit $EXIT_CODE
