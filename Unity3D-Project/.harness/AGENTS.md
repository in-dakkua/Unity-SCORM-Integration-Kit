# AGENTS.md — Mapa de navegación para agentes de IA (variante Unity)

> Este archivo es el **punto de entrada** para cualquier agente que trabaje en
> este repositorio. NO es una biblia de reglas: es un **mapa**. Lee solo lo
> que necesites cuando lo necesites (divulgación progresiva).
>
> Este harness asume que ya existe un **proyecto Unity real** (con su
> `Assets/`, `ProjectSettings/`, `Packages/`) en este repo o en un repo
> hermano referenciado. No lo crea, se adapta a él. Todo el harness vive en
> `.harness/` (más `.claude/` para lo que Claude Code exige en la raíz) para
> no mezclarse con las carpetas de tu proyecto Unity.
>
> **¿No eres Claude Code?** Este mapa asume subagentes (`.claude/agents/`)
> y el comando `/unity-claude-code-harness`, que son mecanismos propios de Claude Code. Si
> eres Cursor u otra herramienta, lee **[`AI_TOOLS.md`](AI_TOOLS.md)**
> primero — traduce este mismo protocolo a algo que puedas seguir sin esos
> mecanismos.

---

## 0. ¿Está inicializado el proyecto?

Si `.harness/unity.config.json` no existe, **para** y pide ejecutar
`/unity-claude-code-harness` primero (ver `.claude/commands/unity-claude-code-harness.md`). Ese archivo
contiene las rutas reales (`scripts_root`, `tests_root`), la versión de Unity,
la herramienta de automatización elegida y el modo de verificación. Sin él,
cualquier ruta que asumas (`Assets/Scripts/...`) puede ser incorrecta para
este developer.

## 1. Antes de empezar (obligatorio)

1. Lee `.harness/unity.config.json`. Usa sus rutas (`scripts_root`,
   `tests_root`), no rutas hardcodeadas. Fíjate en `architecture_status`:
   si es `"adapted_to_existing"` o `"custom"`, `.harness/docs/architecture.md`
   ya no describe la plantilla genérica, describe la convención real de este
   proyecto — trátalo como la fuente de verdad.
2. Lee `.harness/docs/project_rules.md`. Sus reglas son obligatorias y ganan
   sobre `.harness/docs/architecture.md`/`conventions.md` si hay conflicto.
3. Ejecuta `.harness/init.sh` y verifica que termina sin errores. Si falla,
   **para** y resuelve el entorno antes de tocar código.
4. Lee `.harness/progress/current.md` para entender en qué estado quedó la
   última sesión.
5. Lee `.harness/feature_source.json` para saber de dónde sale el backlog
   (`"local"` → `.harness/feature_list.json` directamente; `"asana"` →
   consulta el proyecto de Asana vía MCP primero, ver
   `.harness/docs/feature_source.md`) y elige **una** tarea con estado
   `pending`. No trabajes en más de una a la vez. **Si el archivo trae
   `"is_example_data": true`**, las features que ves son solo un ejemplo
   ilustrativo del formato — no son trabajo real de este proyecto. Pregunta
   al humano si quiere sustituirlas antes de tomar ninguna (ver
   `.claude/commands/unity-claude-code-harness.md`, Paso 7).

## 2. Mapa del repositorio

| Archivo / carpeta                    | Qué contiene                                                     | Cuándo leerlo |
|---------------------------------------|-------------------------------------------------------------------|---------------|
| `.harness/unity.config.json`          | Config concreta de este proyecto (versión Unity, rutas, tool MCP) | Siempre, al empezar |
| `.harness/feature_source.json`        | De dónde sale el backlog: `local` (feature_list.json) o `asana` (proyecto de Asana vía MCP, con feature_list.json como caché) | Siempre, al empezar |
| `.harness/feature_list.json`          | Lista de tareas con estado (pending / in_progress / done / blocked). Caché local si el modo es `asana`. **Puede traer datos de ejemplo** — mira `is_example_data` | Siempre, al empezar |
| `.harness/progress/current.md`        | Estado de la sesión actual                                        | Siempre, al empezar |
| `.harness/progress/history.md`        | Bitácora append-only de sesiones anteriores                       | Si necesitas contexto histórico |
| `.harness/progress/plan_<feature>.md` | Plan escrito por el implementer, pendiente/aprobado por humano    | Antes de implementar cualquier feature |
| `.harness/progress/visual_check_<feature>.md` | Checklist de verificación visual, **firmado por el humano** | Antes de marcar `done` una feature con `requires_visual_check` |
| `.harness/docs/architecture.md`       | Qué significa "hacer un buen trabajo" en este proyecto Unity — **puede haber sido reescrito para reflejar la convención real del equipo**, no asumas que es la plantilla genérica | Antes de implementar |
| `.harness/docs/project_rules.md`      | Reglas **MUST/MUST NOT** propias del equipo, añadidas en `/unity-claude-code-harness` o después. **Prioridad sobre `architecture.md`/`conventions.md` si hay conflicto.** | Siempre, antes de tocar código |
| `.harness/docs/conventions.md`        | Reglas de estilo C#/Unity, nombres, estructura                   | Antes de escribir código |
| `.harness/docs/verification.md`       | Niveles de verificación: tests, y el gate visual humano           | Antes de declarar una tarea como `done` |
| `.harness/docs/mcp_setup.md`          | Opciones de herramienta de automatización Unity y cómo se eligió  | Si necesitas invocar el Editor/MCP |
| `.harness/docs/vcs_policy.md`         | Proveedor de control de versiones (GitHub / Unity VCS-Plastic), tabla de permisos por acción (`allowed`/`requires_approval`/`forbidden`) y protocolo de aprobación | Antes de que el leader ejecute cualquier commit/push/PR/merge/branch |
| `.harness/docs/release_management.md` | Versionado (`MAJOR.MINOR.PATCH+BUILD`), nombres de artefactos, modelo de ramas `/dev`–`/release`–`/main` y tarea de release en Asana, según la guideline corporativa. Solo aplica si `release_guideline` no es `none` | Antes de crear una rama, tocar la versión o preparar una release |
| `.harness/docs/asana_context_protocol.md` | Protocolo obligatorio para publicar comentarios en tareas de Asana (borrador mostrado al humano + disclosure de IA + menciones reales) | Antes de que el leader publique cualquier comentario en Asana |
| `.harness/CHECKPOINTS.md`             | Criterios objetivos de "estado final correcto"                    | Para auto-evaluarte |
| `.claude/agents/`                     | Definiciones de subagentes (líder, implementador, revisor)        | Si orquestas trabajo |
| `.claude/commands/unity-claude-code-harness.md`      | Cuestionario de inicialización del proyecto                       | Primera vez, o para reconfigurar |
| `.claude/skills/` (opcional)          | 5 skills de Unity/C# bundled (bootstrap, package manager, code review, test writer, project cleanup) — auto-invocados por Claude Code según su `description`, sin paso de instalación. **Puede no existir** si el developer los quitó en `/unity-claude-code-harness` Paso 4B (ver `unity_dev_skills_enabled` en `unity.config.json`) — nada del harness depende de que estén. Ver `.harness/docs/unity_dev_skills.md` para su procedencia y qué rol puede usar cada uno | Antes de invocar cualquiera manualmente, o cuando Claude Code lo dispare solo |
| `<scripts_root>`                      | Código C# del proyecto (definido en unity.config.json)            | Para implementar |
| `<tests_root>`                        | Tests de Unity Test Framework (EditMode/PlayMode)                 | Para verificar |

## 3. Reglas duras (no negociables)

- **Una sola feature a la vez.** No mezcles cambios de varias tareas en la
  misma sesión.
- **No trabajes sobre datos de ejemplo sin avisar.** Si
  `.harness/feature_list.json` tiene `"is_example_data": true`, confirma con
  el humano antes de tomar cualquiera de esas features como trabajo real.
- **Plan antes que código.** Si la feature tiene `requires_plan_approval:
  true`, no se toca ni un archivo `.cs` hasta que el humano apruebe
  `.harness/progress/plan_<feature>.md` explícitamente en el chat.
- **El visual check lo firma un humano, no un agente.** Si la feature tiene
  `requires_visual_check: true`, no se marca `done` sin
  `.harness/progress/visual_check_<feature>.md` completado por una persona.
- **No asumas si puedes escribir en el Editor vía MCP.** Si
  `automation_tool` es un MCP y `editor_write_policy` es
  `"human_applies_changes"`, nunca crees/modifiques escenas o prefabs por
  esa vía — solo código + instrucciones para la persona. Si el campo está
  vacío, para y pregunta antes de escribir nada en el Editor.
- **No uses una herramienta de automatización con
  `automation_tool_verified: false`** para nada real (tests en batchmode,
  MCP) sin volver a confirmar con el humano que ya está lista.
- **No declares una tarea `done` sin pruebas verdes** (cuando el modo de
  verificación lo exija — ver `.harness/docs/verification.md`). Ejecuta
  `.harness/init.sh`.
- **Documenta lo que haces** en `.harness/progress/current.md` mientras
  trabajas, no al final.
- **Los comentarios en Asana siempre pasan por el humano primero.** Si el
  backlog es `"asana"` y vas a publicar un comentario en una tarea, sigue
  `.harness/docs/asana_context_protocol.md` sin excepción: borrador
  enseñado en el chat, pregunta explícita por ajustes/menciones, y solo
  entonces se publica. Ninguna mención vale como "@Nombre" en texto plano
  — solo `<a data-asana-gid="...">` con un `gid` real resuelto por
  herramienta, nunca inventado.
- **El control de versiones tiene su propia tabla de permisos.** Lectura
  (`git status`/`diff`/`log`, `cm status`/`log`) es libre para cualquier
  rol. Cualquier escritura (`commit`/`checkin`, ramas, `push`, PR/merge
  request, merge, borrado de rama) la ejecuta **solo el leader**, y solo
  tras consultar `.harness/unity.config.json` → `vcs_permissions` y
  `.harness/docs/vcs_policy.md`: si la acción es `requires_approval`,
  enseña el comando exacto y espera confirmación humana explícita antes de
  ejecutarlo; si es `forbidden` (p. ej. force-push por defecto), no la
  ejecutes ni buscando un rodeo — repórtalo.
- **Versiones y releases según la guideline.** Si `release_guideline` no
  es `none`, sigue `.harness/docs/release_management.md`. El agente nunca
  decide el número de versión ni toca `BUILD_NUMBER`/`bundleVersionCode`,
  crea ramas solo con los patrones `branch_*`, no mergea a
  `/release`/`/main` ni crea tags (salvo override explícito en
  `vcs_permissions`), y no crea ni edita la tarea de release
  en Asana sin aprobación. **Nunca** entrega builds, código ni modelos a
  nadie fuera del equipo de producción.
- **Deja el repositorio limpio** antes de cerrar la sesión (ver §5).
- **Si no sabes algo, busca en `.harness/docs/`** antes de inventarlo.

## 4. Cómo elegir una tarea

```
1. Abre .harness/feature_list.json
2. Si is_example_data == true, confirma con el humano antes de seguir
3. Filtra por status == "pending"
4. Coge la de menor "id"
5. Cambia su status a "in_progress" y guarda
6. Anota en .harness/progress/current.md: feature, hora de inicio, plan breve
7. Si requires_plan_approval == true: escribe .harness/progress/plan_<feature>.md
   y ESPERA aprobación humana antes de seguir
```

## 5. Cierre de sesión (lifecycle)

Antes de terminar:

1. Ejecuta `.harness/init.sh` — todo verde (o los warnings esperados si el
   modo de verificación no exige tests automáticos, ver
   `.harness/docs/verification.md`).
2. Si la feature requiere visual check: confirma que
   `.harness/progress/visual_check_<feature>.md` está firmado por el
   humano.
3. Si la tarea está acabada: marca `status: "done"` en
   `.harness/feature_list.json`.
4. Mueve el resumen de `.harness/progress/current.md` al final de
   `.harness/progress/history.md`.
5. Vacía `.harness/progress/current.md` dejando solo la plantilla.
6. No dejes archivos temporales, ni `Debug.Log` de debug sin contexto, ni
   TODOs sin contexto.

## 6. Si te bloqueas

- Relee la sección relevante de `.harness/docs/`.
- Si la herramienta de automatización Unity (MCP/CLI) no hace lo que
  esperas, **no inventes un workaround**: documenta el bloqueo en
  `.harness/progress/current.md` y para la sesión. Puede que la config en
  `.harness/unity.config.json` esté desactualizada — pregúntale al humano.
