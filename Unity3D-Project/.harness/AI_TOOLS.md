# Para otras IAs / herramientas de terceros (no Claude Code)

> Si eres un asistente de IA distinto de Claude Code — Cursor,
> Windsurf, un copiloto interno, o cualquier otra herramienta agentic —
> **este archivo es tu punto de entrada**. `AGENTS.md` describe el mapa del
> repositorio, pero varias piezas del harness (`.claude/agents/*.md`, el
> comando `/unity-claude-code-harness`, "lanza un subagente con la herramienta Agent") son
> mecanismos específicos de Claude Code que tu herramienta probablemente no
> tiene. Este documento traduce ese mismo protocolo a algo que cualquier IA
> puede seguir, tenga o no subagentes, slash commands, o un cuestionario
> interactivo propio.
>
> Todo el harness vive en la carpeta `.harness/` (no en la raíz del repo) —
> cada ruta de este documento asume ese prefijo.

Si tu herramienta soporta "system prompt", "custom instructions",
"knowledge base" o "context files" de proyecto: **apunta esa configuración
a este archivo y a `.harness/AGENTS.md`**. Si tu herramienta solo puede
referenciar una carpeta (no un archivo), apunta a `.harness/` — estos dos
archivos son los primeros que debes leer.

## 1. Qué es este harness, en una frase

Un conjunto de archivos en disco (`.harness/feature_list.json`,
`.harness/progress/`, `.harness/docs/`, `.harness/CHECKPOINTS.md`,
`.harness/unity.config.json`) que define **cómo trabajar de forma
verificable en este proyecto Unity concreto** — qué feature tocar, qué
arquitectura respetar, cómo demostrar que funciona, y qué necesita
confirmación de una persona antes de continuar. El estado del proyecto vive
en estos archivos, no en el historial de chat.

## 2. Los 3 archivos que SIEMPRE debes leer antes de tocar nada

1. **`.harness/unity.config.json`** — si no existe, el proyecto no está
   inicializado para ti todavía. Ve a la sección 3 de este documento antes
   de continuar.
2. **`.harness/docs/project_rules.md`** — reglas MUST/MUST NOT propias de
   este equipo. Tienen prioridad sobre cualquier otra convención de este
   documento o de `.harness/docs/architecture.md`/`conventions.md`.
3. **`.harness/feature_list.json`** — la fuente de verdad de qué hay que
   hacer, **salvo que `.harness/feature_source.json` diga `"mode":
   "asana"`** — en ese caso el backlog real vive en un proyecto de Asana y
   `feature_list.json` es solo una caché local que debes mantener
   sincronizada (ver `.harness/docs/feature_source.md`). Si tu herramienta
   no tiene forma de hablar con Asana, dilo explícitamente al humano en vez
   de inventar features o caer en modo local en silencio. Si además vas a
   **publicar un comentario** en una tarea de Asana (no solo sincronizar su
   `status`), sigue `.harness/docs/asana_context_protocol.md` — es
   obligatorio para cualquier IA, no solo para Claude Code. **Si trae
   `"is_example_data": true`** en la raíz del JSON,
   las features que ves son un ejemplo ilustrativo del formato, no trabajo
   real — confírmalo con la persona antes de tomar ninguna. Cada feature
   real tiene `status` (`pending`/`in_progress`/`done`/`blocked`), y dos
   flags que **no son opcionales para ninguna IA, incluida la tuya**:
   - `requires_plan_approval: true` → no toques código de esa feature
     hasta que una persona apruebe explícitamente tu plan por escrito.
   - `requires_visual_check: true` → no la marques `done` sin que una
     persona abra el proyecto en el Editor de Unity, siga los pasos de
     `.harness/progress/visual_check_<feature>.md` y lo firme. **Ninguna
     IA puede completar o simular esta firma.**

Si `.harness/unity.config.json` existe, también lee
`.harness/docs/architecture.md` (puede describir la convención real del
proyecto, no una plantilla — mira el campo `architecture_status`) y
`.harness/docs/conventions.md` antes de escribir código.

## 3. Si `.harness/unity.config.json` no existe: inicialización

Claude Code resuelve esto con un comando (`/unity-claude-code-harness`) que hace estas
preguntas por ti. Si tu herramienta no tiene un mecanismo equivalente,
**hazle tú mismo estas preguntas a la persona, por texto**, antes de tocar
ningún archivo, y escribe las respuestas en `.harness/unity.config.json`
siguiendo el esquema de `.harness/unity.config.example.json`:

1. Versión de Unity del proyecto (lee `ProjectSettings/ProjectVersion.txt`
   si existe y pide confirmación; si no, pregunta).
2. Rutas reales de código y tests (`scripts_root`, `tests_root`) — no
   asumas `Assets/Scripts`/`Assets/Tests` si el proyecto usa otra cosa.
   **Ambos campos son arrays**: pregunta si el código vive en una sola
   carpeta o en varias (p. ej. `Assets/Scripts` y también
   `Assets/Template/Scripts`) y escribe todas las que digan, cada array en
   una sola línea del JSON — un array partido en varias líneas no se puede
   leer con las herramientas grep/sed de `.harness/init.sh`.
3. Arquitectura de carpetas: si algún `scripts_root` ya tiene contenido,
   muéstraselo a la persona y pregunta si `.harness/docs/architecture.md`
   debe describir esa convención real (reescríbelo tú) en vez de la
   plantilla. Si el proyecto es nuevo, dile dónde está la plantilla y cómo
   editarla.
4. Reglas obligatorias propias del equipo (MUST/MUST NOT) — escríbelas en
   `.harness/docs/project_rules.md`.
5. Herramienta de automatización Unity que usa el equipo (ver
   `.harness/docs/mcp_setup.md`) — regístrala en `automation_tool`. **No des por hecho que la
   herramienta elegida ya funciona** — solo pon `automation_tool_verified:
   true` si el requisito concreto de esa herramienta (p. ej.
   `unity_editor_path` para `unity_cli`) ya está confirmado; si el proyecto
   Unity no existía todavía o no se pudo probar, déjalo en `false` y anota
   en `automation_tool_setup_todo` qué falta. Si elegiste un MCP, pregunta
   además si tú (la IA) puedes escribir directamente en escenas/prefabs
   cuando el plan esté aprobado, o si el humano prefiere aplicar esos
   cambios él mismo — guarda la respuesta en `editor_write_policy`
   (`"agent_can_edit_via_tool"` / `"human_applies_changes"`).
6. Modo de verificación: TDD estricto, plan+visual check, o híbrido (ver
   `.harness/docs/verification.md`) y si el gate de plan es obligatorio
   siempre o solo para features con UI/escena.
7. Fuente del backlog: `local` (`.harness/feature_list.json` editado a
   mano) o `asana` (un proyecto de Asana real, con `feature_list.json`
   como caché local) — por defecto es `local`, escribe la elección en
   `.harness/feature_source.json`. Si el humano quiere Asana pero tu
   herramienta no puede conectarse a ella, dilo y queda en `local`.
8. Qué hacer con las features de ejemplo de `.harness/feature_list.json`
   (vaciarlas, dejarlas como referencia, o reemplazarlas por las reales) —
   no dejes esto sin preguntar, es fácil confundirlas con trabajo real.
9. Proveedor de control de versiones (GitHub, Unity Version Control/
   Plastic, o ninguno) y quién puede hacer qué con él — ver
   `.harness/docs/vcs_policy.md`. Pregunta, acción por acción
   (commit local, crear rama, `push`, abrir PR/merge request, fusionar a
   una rama protegida, borrar rama, force-push/reescribir historial), si
   el agente puede hacerlo sin preguntar, necesita tu confirmación
   explícita antes, o nunca debe hacerlo. Guarda la respuesta en
   `vcs_provider`, `vcs_rules_source` y `vcs_permissions` de
   `.harness/unity.config.json`.
10. Si el proyecto sigue la guideline de releases de la empresa
   (`release_guideline`): nombre de app y código de proyecto, plataformas,
   versión en curso (la da la persona, no tú), nombres reales de las
   ramas y los `gid` de Asana del proyecto Releases y del específico — ver
   `.harness/docs/release_management.md` y el Paso 5C de
   `.claude/commands/unity-claude-code-harness.md`.

No inventes valores por defecto sin preguntar — ese es exactamente el
punto de este paso.

## 4. Cómo traducir "leader / implementer / reviewer" si no tienes subagentes

Claude Code divide el trabajo en tres roles (`.claude/agents/leader.md`,
`implementer.md`, `reviewer.md`) que se lanzan como subagentes
independientes. Si tu herramienta es un único agente sin esa capacidad,
**haz tú mismo, en orden, lo que haría cada rol** — no te saltes ningún
paso ni ninguna escritura en disco solo porque eres un solo proceso:

| Rol Claude Code | Qué hacer si eres un único agente |
|---|---|
| **leader** | Lee `.harness/feature_list.json` (confirma primero si `is_example_data` sigue en `true`), elige **una** feature `pending`, cambia su estado a `in_progress`. |
| **implementer — plan** | Escribe `.harness/progress/plan_<feature>.md` con los archivos que vas a tocar. Si `requires_plan_approval: true`, **detente aquí** y pide confirmación explícita a la persona en el chat antes de escribir una sola línea de código. |
| **implementer — código** | Implementa siguiendo `.harness/docs/architecture.md`/`conventions.md`/`project_rules.md`. Escribe los tests que exija `.harness/docs/verification.md` según el `verification_mode` configurado. |
| **implementer — visual check** | Si `requires_visual_check: true`, crea `.harness/progress/visual_check_<feature>.md` con los pasos concretos y pide a la persona que lo pruebe en el Editor y lo firme. No sigas sin esa firma. |
| **reviewer** | Antes de marcar `done`, revisa tu propio trabajo contra `.harness/CHECKPOINTS.md` como si fueras otra persona: ¿corre `.harness/init.sh` en verde?, ¿respetaste `.harness/docs/project_rules.md`?, ¿existe el plan aprobado y el visual check firmado si aplica? Escribe el veredicto en `.harness/progress/review_<feature>.md` aunque seas tú mismo quien lo revisa — la traza en disco es lo que hace esto auditable, no la confianza en el agente. |

La razón de escribir todo esto en archivos (no solo mantenerlo en tu
contexto de conversación) es que **cualquier humano o herramienta puede
auditar después qué pasó**, sin depender de que tu conversación siga viva.

## 5. Lo que ninguna IA puede hacer aquí, sea cual sea la herramienta

- ❌ Marcar `done` una feature con `requires_visual_check: true` sin un
  `.harness/progress/visual_check_<feature>.md` firmado por una persona.
- ❌ Tocar código de una feature con `requires_plan_approval: true` antes
  de que una persona apruebe el plan explícitamente.
- ❌ Ignorar una regla de `.harness/docs/project_rules.md` porque tu
  herramienta no tiene un concepto nativo de "regla obligatoria del
  proyecto" — trátalas como restricciones duras igualmente.
- ❌ Inventar valores de `.harness/unity.config.json` sin preguntar a la
  persona (versión de Unity, rutas, etc.).
- ❌ Tratar las features de `.harness/feature_list.json` como trabajo real
  sin comprobar antes si `is_example_data` sigue en `true`.
- ❌ Publicar un comentario en una tarea de Asana sin enseñar antes el
  borrador exacto al humano y preguntar por ajustes/menciones (ver
  `.harness/docs/asana_context_protocol.md`), o usar "@Nombre" en texto
  plano en vez de una mención real.
- ❌ Ejecutar cualquier escritura de control de versiones (`commit`,
  `push`, crear/borrar ramas, PR/merge request, merge) marcada
  `requires_approval` en `.harness/docs/vcs_policy.md` sin enseñar antes
  el comando exacto y esperar confirmación humana explícita — o marcada
  `forbidden` (por defecto, force-push/reescribir historial) bajo
  cualquier circunstancia, ni aunque la persona lo pida "solo esta vez".
  Leer (`status`/`diff`/`log`) sí es libre en cualquier momento.
- ❌ Decidir o proponer por tu cuenta un número de versión, tocar
  `BUILD_NUMBER`/`bundleVersionCode`, mergear a `/release`/`/main`, crear
  tags, o crear la tarea de release en Asana sin aprobación (ver
  `.harness/docs/release_management.md`).
- ❌ Entregar builds, código o modelos 3D a nadie fuera del equipo de
  producción, bajo ningún concepto.

## 6. Skills de Unity/C# bundled (`.claude/skills/`) — opcionales

Este harness puede traer (**si el developer no los quitó** en `/unity-claude-code-harness`
Paso 4B — mira `unity_dev_skills_enabled` en `.harness/unity.config.json`)
5 skills de Unity/C# en `.claude/skills/` (bootstrap, gestión de paquetes,
code review, escritura de tests, limpieza de proyecto). Son puramente
opcionales: nada de este documento ni del resto del harness depende de que
existan. Ese mecanismo de auto-descubrimiento (`.claude/skills/<nombre>/SKILL.md`)
es específico de Claude Code — tu herramienta probablemente no lo
entiende. Si el directorio existe y necesitas ese conocimiento, lee
directamente los `SKILL.md` de `.claude/skills/*/` como documentación de
referencia (no se auto-invocan para ti), y respeta la misma precedencia
que el resto de este documento: `.harness/docs/project_rules.md` >
`.harness/unity.config.json` + `architecture.md`/`conventions.md` > lo que
diga el `SKILL.md`. Detalle completo (procedencia, por qué no son
"oficiales de Unity", qué rol puede usar cada uno) en
`.harness/docs/unity_dev_skills.md` — si ese archivo tampoco existe, es que
el developer los quitó del todo; no los reinstales sin que te lo pidan.

## 7. Verificación

Ejecuta `.harness/init.sh` (bash) antes de declarar cualquier feature
`done`, y antes de pedir revisión. Si tu entorno no puede correr scripts
bash, pídele a la persona que lo corra y te pegue el resultado — no asumas
que pasa.
