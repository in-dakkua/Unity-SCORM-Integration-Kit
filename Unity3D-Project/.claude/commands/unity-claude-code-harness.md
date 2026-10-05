---
description: Inicializa el harness para el proyecto Unity concreto del developer (versión, rutas, tool de automatización, modo de verificación).
---

# /unity-claude-code-harness

Ejecuta este cuestionario **siempre** que `.harness/unity.config.json` no
exista, o cuando el humano pida explícitamente reconfigurar. No asumas
ninguna respuesta — este harness se usa en distintos equipos con distintas
versiones de Unity, distintas herramientas de automatización y distintas
preferencias de proceso (TDD estricto vs. plan+visual).

## Paso 1 — Detectar/confirmar el proyecto Unity y su versión

1. Busca `ProjectSettings/ProjectVersion.txt` en la raíz (o pregunta la ruta
   si el proyecto Unity no está en la raíz del repo).
2. Si existe, lee el campo `m_EditorVersion` y muéstraselo al developer para
   que lo confirme (puede tener varias versiones de Unity instaladas).
3. Si no existe, pide al developer que abra su proyecto en el Unity Hub y
   te diga la versión exacta (`Help > About Unity`, o el selector de
   versión del Hub), y la ruta del proyecto si no coincide con la raíz del
   repo.
4. Pregunta también las rutas reales de código y tests, por ejemplo
   `Assets/Scripts` y `Assets/Tests` — no asumas que coinciden con el
   ejemplo. **Pregunta explícitamente si es una sola carpeta o varias**:
   algunos proyectos tienen el código repartido (p. ej. `Assets/Scripts`
   para el juego y `Assets/Template/Scripts` para una plantilla/paquete
   reutilizable que también quieren proteger con el harness). `scripts_root`
   y `tests_root` en `.harness/unity.config.json` son **siempre arrays**
   (aunque sea una sola ruta, va como `["Assets/Scripts"]`) — con una o
   varias, el harness protege por igual todas las que se listen (el leader
   no edita ninguna, el reviewer las revisa todas). Escribe cada array en
   una sola línea del JSON (ver el comentario en
   `.harness/unity.config.example.json` — `init.sh` no sigue arrays
   partidos en varias líneas).

## Paso 2 — Arquitectura de carpetas: adoptar la existente o partir de la plantilla

`.harness/docs/architecture.md` trae una convención por defecto
(`Core/Gameplay/UI` dentro de `scripts_root`). **No la impongas** sin mirar
primero qué hay ya en el proyecto:

1. Si `scripts_root` ya existe y tiene contenido, lista sus subcarpetas de
   primer y segundo nivel (`Glob`/`ls`) y muéstraselas al developer. Por
   ejemplo, puede que ya tengan `Assets/Scripts/Managers`,
   `Assets/Scripts/Systems`, `Assets/Scripts/Data` en vez de
   `Core/Gameplay/UI`.
2. Pregúntale explícitamente:
   - **Adoptar la estructura existente** → reescribe
     `.harness/docs/architecture.md` para que describa *esa* convención
     real (nombres de carpetas, qué va en cada una, qué depende de qué),
     no la de la plantilla. El objetivo es que el documento describa la
     verdad del proyecto, no un ideal.
   - **Adaptar el proyecto a la plantilla** (Core/Gameplay/UI) → deja
     `.harness/docs/architecture.md` como está, y anota en
     `.harness/progress/current.md`/como feature en
     `.harness/feature_list.json` que hace falta una migración progresiva
     si el proyecto es grande (no la hagas tú de golpe en esta sesión).
   - **Una mezcla / variante propia** → edita
     `.harness/docs/architecture.md` junto con el developer hasta que
     refleje exactamente lo que quiere, capa por capa (qué carpeta es
     "lógica pura testeable", cuál son MonoBehaviours, cuál es UI, qué no
     debe depender de qué).
3. Si `scripts_root` **no existe todavía o está vacío** (proyecto nuevo o
   feature inicial), dile al developer que `.harness/docs/architecture.md`
   describe la arquitectura *plantilla* que se usará por defecto
   (Core/Gameplay/UI), y muéstrale exactamente dónde está el archivo y cómo
   editarlo si prefiere otra convención antes de que el implementer empiece
   a crear carpetas.
4. Deja constancia de la decisión en `.harness/unity.config.json` →
   `architecture_status` (`"template_default"`, `"adapted_to_existing"` o
   `"custom"`) y `architecture_notes` (una línea explicando qué se decidió
   y por qué).

Esto se puede repetir más adelante (arquitectura viva, no solo en el
arranque) — si el developer cambia de opinión, vuelve a este paso y edita
`.harness/docs/architecture.md` de nuevo.

## Paso 3 — Reglas obligatorias adicionales del proyecto

Pregunta si el developer/equipo quiere añadir reglas **MUST / MUST NOT**
propias para cualquier agente que trabaje en este repo, más allá de las que
ya trae el harness (una feature a la vez, plan antes que código, visual
check humano, etc.). Ejemplos de lo que suele salir aquí:

- "Nunca toques `Assets/Plugins/ThirdParty/`."
- "Todo `ScriptableObject` nuevo debe tener un menú `[CreateAssetMenu]` con
  este prefijo de ruta."
- "Nunca uses `async`/`await` en código que corre en el hilo principal de
  Unity sin pasar por `UniTask` (o el helper que use el equipo)."
- "Nunca commitees cambios de `.meta` sin el archivo que referencian."
- "Los nombres de rama/commit deben seguir `<convención del equipo>`."

Si el developer da alguna, escríbelas (tal cual, sin suavizarlas) en
`.harness/docs/project_rules.md`, bajo el encabezado `## Reglas
obligatorias`, como lista con viñetas, cada una empezando por "MUST" o
"MUST NOT". Si no quiere añadir ninguna ahora, deja
`.harness/docs/project_rules.md` con su plantilla vacía tal cual está — se
puede volver a este paso cuando quiera (no hace falta repetir todo
`/unity-claude-code-harness`, basta con pedir "añade esta regla obligatoria: ...").

## Paso 4 — Herramienta de automatización Unity

> **Puedes saltar directo a este paso** si el humano pide "termina de
> configurar la herramienta de automatización" o similar — no hace falta
> repetir todo `/unity-claude-code-harness` para esto.

Pregunta cuál usa (o quiere empezar a usar) el developer, explicando las
opciones brevemente (ver `.harness/docs/mcp_setup.md` para el detalle):

1. **Unity MCP oficial** (Unity AI / MCP — unity.com/blog/unity-ai-mcp).
2. **unity-mcp open source** (CoplayDev/unity-mcp en GitHub).
3. **Unity CLI** (`docs.unity.com/en-us/unity-cli`) — automatización por
   línea de comandos, sin servidor MCP.
4. **Ninguna todavía / no lo sé** — está bien, se puede dejar `"none"` y
   configurar más tarde con `/unity-claude-code-harness` de nuevo.

No instales ni configures el servidor MCP tú mismo — solo registra la
elección. Si el developer pide ayuda a configurarlo, guíalo con los enlaces
de `.harness/docs/mcp_setup.md`, pero la instalación real (credenciales,
servidores) la hace el humano.

### Elegir el nombre no basta — verifica que la herramienta esté lista

Este es el paso que más fácil se queda a medias: el developer elige una
herramienta, pero en ese momento todavía no existe el proyecto Unity, no
hay Unity instalado, o no se ha probado la conexión MCP. Si dejas
`automation_tool` puesto sin más, el harness queda mintiendo sobre su
propio estado. Así que, para la opción elegida:

- **Unity CLI:** necesitas `unity_editor_path`. Si el developer no te lo da
  directamente, **búscalo tú** en las ubicaciones típicas de Unity Hub
  (Windows: `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe`;
  macOS: `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity`;
  Linux: `~/Unity/Hub/Editor/<version>/Editor/Unity` — ver
  `.harness/docs/mcp_setup.md`) usando Glob/Bash, y pídele que confirme
  cuál es si encuentras candidatos. Si el proyecto Unity **todavía no
  existe** (por eso no hay editor instalado, o no se sabe la ruta), no
  inventes una ruta ni la dejes en blanco sin más: dile explícitamente al
  developer que en cuanto tenga el proyecto creado y Unity instalado, te
  pida "termina de configurar la herramienta de automatización" para
  volver aquí. Mientras tanto, `unity_editor_path` queda en `null` y
  `automation_tool_verified` en `false` — **no lo pongas en `true` sin la
  ruta real**.
- **Unity MCP oficial / unity-mcp OSS:** pregunta explícitamente si el
  developer ya probó la conexión (el Editor responde a través del MCP). Si
  no lo ha probado todavía, deja `automation_tool_verified: false` y
  dile que te avise cuando lo haya probado.
- **Ninguna:** no aplica verificación — deja
  `automation_tool_verified: false` de todas formas (es el valor neutro
  cuando no hay nada que verificar), no es un problema para esta opción.

En `automation_tool_setup_todo` anota, en una frase, qué falta exactamente
y por qué (p. ej. "falta unity_editor_path — el proyecto Unity aún no
existía al configurar esto"), para que `.harness/init.sh` y cualquier
agente futuro sepan qué recordarle al humano sin que tengas que repetir
esta conversación.

### Si eligió un MCP (oficial o CoplayDev): ¿puede el agente escribir en el Editor?

Esto es una pregunta de permisos distinta de "qué herramienta usar", y es
fácil que nadie la haga explícita — no asumas ninguna respuesta por
defecto, ni "el agente nunca toca el Editor" ni lo contrario. Un MCP de
Unity puede crear/modificar GameObjects, escenas y prefabs en vivo; Unity
CLI no puede (solo build/test en batchmode), y sin herramienta esta
pregunta no aplica.

Si `automation_tool` es `unity_mcp_official` o `unity_mcp_coplay_oss`,
pregunta explícitamente:

> "Cuando el plan de una feature esté aprobado, ¿quieres que el agente use
> el MCP para crear o modificar GameObjects/escenas/prefabs directamente
> (con el visual check humano de todas formas obligatorio después), o
> prefieres que el agente **nunca** escriba en el Editor — que solo
> escriba el código C# y te deje instrucciones paso a paso para que tú
> conectes todo a mano?"

Guarda la respuesta en `editor_write_policy`:
- `"agent_can_edit_via_tool"` — el agente puede escribir en el Editor vía
  MCP (siempre dentro del gate de plan aprobado y del visual check).
- `"human_applies_changes"` — el agente jamás escribe en escenas/prefabs,
  ni siquiera vía MCP; solo código + instrucciones para el humano.

Si `automation_tool` es `unity_cli` o `none`, pon
`editor_write_policy: "not_applicable"` sin preguntar nada más.

## Paso 4B — Skills de Unity/C# bundled (`.claude/skills/`)

> **Puedes saltar directo a este paso** si el humano pide "quita los
> skills bundled", "vuelve a añadir los skills", o similar más adelante —
> no hace falta repetir todo `/unity-claude-code-harness` para esto.

`install.sh`/`install.ps1` ya copiaron, sin preguntar, 5 skills de Claude
Code con conocimiento de Unity/C# a `.claude/skills/` (bootstrap de
proyecto, gestión de `manifest.json`, code review, escritura de tests,
limpieza de proyecto — ver `.harness/docs/unity_dev_skills.md` para su
procedencia completa; **no son un plugin oficial de Unity**, pese al
nombre con el que puedan haberte llegado). Como cualquier otra pieza de
este harness, no se asume que el developer los quiere — se pregunta.

Explica brevemente: Claude Code los auto-invoca solo cuando el mensaje
encaja con lo que hacen (p. ej. "revisa este script", "añade un paquete a
Unity") — no se disparan solos sin que se les pida algo así, y ninguno
ejecuta un cambio destructivo sin pedir confirmación explícita primero
(ver el propio `SKILL.md` de `unity-project-cleanup`, Fase 0).

Pregunta explícitamente: **"¿Quieres conservar el pack de 5 skills de
Unity/C# bundled con el harness, o prefieres que lo quite ahora?"**

- **Los quiere conservar** → no toques nada. `unity_dev_skills_enabled: true`.
- **Prefiere quitarlos** → borra tú mismo el directorio `.claude/skills/`
  completo ahora mismo (está fuera de `scripts_root`/`tests_root`, así que
  puedes editarlo directamente). `unity_dev_skills_enabled: false`. Dile
  que puede recuperarlos más tarde copiando solo esa carpeta desde el
  repositorio de la plantilla (`harness-claude`) — **no** con
  `install.sh --force`/`install.ps1 -Force`, que sobreescribiría todo el
  harness (incluidos sus docs y `unity.config.json` ya personalizados),
  no solo `.claude/skills/`.

Guarda la decisión en `.harness/unity.config.json` →
`unity_dev_skills_enabled`. Es un campo puramente informativo: **ningún**
otro archivo del harness (`AGENTS.md`, `.claude/agents/*.md`,
`.harness/CHECKPOINTS.md`) depende de que sea `true` para funcionar — el
protocolo central (plan, visual check, gates humanos) es idéntico con o
sin estos skills.

## Paso 5 — Modo de verificación y human-in-the-loop

Pregunta (no asumas — este es el punto central de por qué esta variante
existe):

1. ¿El equipo quiere **TDD estricto** (tests antes/junto con cada cambio,
   como en el harness genérico), un modo **"plan + visual check"** (para
   developers que no escriben tests pero quieren revisar el plan antes de
   que el agente implemente, y verificar visualmente en el Editor después),
   o un **híbrido** (tests donde la lógica es pura C# testeable, visual
   check donde toca escena/UI/prefabs)?
2. ¿Quieren que el plan del implementer **siempre** requiera aprobación
   humana explícita antes de tocar código, o solo para features que tocan
   escenas/prefabs/UI?
3. ¿El visual check debe ser obligatorio para toda feature que toque una
   escena o prefab? (por defecto: sí)

## Paso 5B — Control de versiones (VCS)

> **Puedes saltar directo a este paso** si el humano pide "configura el
> control de versiones", "conecta el harness a GitHub/Plastic", o similar
> más adelante — no hace falta repetir todo `/unity-claude-code-harness` para esto. Ver
> `.harness/docs/vcs_policy.md` para el detalle completo de todo lo que se
> pregunta aquí (es la referencia que consulta el leader después, este
> paso solo la rellena).

Este paso da al developer control sobre **qué puede hacer un agente con el
control de versiones sin pedir permiso, qué necesita su aprobación
explícita, y qué no debe hacer nunca** — no se asume nada por defecto sin
preguntarlo primero.

1. **Proveedor.** Pregunta cuál usa el equipo:
   - **GitHub** — el harness usará `git`/`gh` (CLI oficial de GitHub) para
     cualquier operación.
   - **Unity Version Control (antes Plastic SCM)** — el harness usará el
     CLI `cm`. Aclara al developer que aquí los permisos reales del
     servidor (quién puede hacer checkin a una rama) no son legibles
     localmente — ver la asimetría explicada en `.harness/docs/vcs_policy.md`.
   - **Ninguno todavía / no lo sé** — está bien, deja `vcs_provider: "none"`
     y el resto de este paso no aplica. Se puede volver más adelante.
2. **Fuente de las reglas de permiso (`vcs_rules_source`).** Si eligió un
   proveedor real, pregunta explícitamente cuál de las tres:
   - **Tabla común del harness (`harness_common`)** — adopta tal cual la
     tabla de `.harness/docs/vcs_policy.md` (recomendado si el equipo no
     tiene todavía una política de ramas protegidas pensada).
   - **Reglas reales del repo, en vivo (`repo_detected`)** — el leader
     relee las reglas del repositorio antes de cada acción sensible en vez
     de fiarse de una copia guardada aquí. Si es GitHub, puedes ayudarte
     tú mismo: comprueba si `gh` está instalado y autenticado
     (`gh auth status`), y si hay `.github/CODEOWNERS`; si el desarrollador
     te da el remoto, puedes mirar
     `gh api repos/{owner}/{repo}/branches/{branch}/protection` para el
     estado real de la rama principal y mostrárselo. Si es Unity VCS,
     explica que esto degrada a "pregunta al humano cada vez" para
     acciones sobre ramas compartidas — no hay ACL local que leer.
   - **Reglas propias (`custom`)** — recorre con el developer la tabla de
     acciones de abajo y anota solo las que se apartan del default.
3. **Recorre la tabla de acciones** (igual en los tres proveedores, lo que
   cambia es el comando concreto — ver `.harness/docs/vcs_policy.md` para
   el mapeo Git/GitHub ↔ Unity VCS de cada una) y confirma o ajusta el
   nivel de cada una: `allowed` (el agente lo hace sin preguntar),
   `requires_approval` (el agente enseña el comando exacto y espera
   confirmación explícita en el chat antes de ejecutarlo), o `forbidden`
   (el agente nunca lo hace, ni aunque se le pida "solo esta vez"):
   - `commit_local` — default `allowed`.
   - `create_branch` — default `allowed`.
   - `push` — default `requires_approval`.
   - `open_pr_or_merge_request` — default `requires_approval`.
   - `merge_or_checkin_to_protected_branch` — default `requires_approval`.
   - `delete_branch` — default `requires_approval`.
   - `create_tag` — default `requires_approval` (`forbidden` si el Paso 5C
     activa la guideline de releases).
   - `merge_into_release_or_main` — default igual que
     `merge_or_checkin_to_protected_branch` (`forbidden` si el Paso 5C
     activa la guideline de releases).
   No escribas `create_tag` ni `merge_into_release_or_main` en
   `vcs_permissions` salvo que el developer quiera desviarse a propósito:
   cualquier valor escrito ahí gana sobre el perfil de releases.
   - `force_push_or_rewrite_history` — default `forbidden`. Si el
     developer pide relajarlo, confírmalo explícitamente (no lo cambies
     sin preguntar "¿estás seguro?") y anota el motivo en
     `vcs_provider_notes`.
   Si eligió `harness_common` y no quiere tocar nada, no hace falta
   recorrer la tabla acción por acción — solo confírmale cuáles son los
   defaults (mostrárselos igual que arriba) para que sepa qué está
   aceptando.
4. **Verificación real, no solo la elección del nombre** — mismo patrón
   que `automation_tool_verified` (Paso 4): pregunta si el CLI elegido ya
   está de verdad listo (`gh auth status` para GitHub; conexión del
   cliente `cm` al servidor para Unity VCS). Si no se ha probado todavía,
   deja `vcs_verified: false` y anota en `vcs_setup_todo` qué falta — no
   lo pongas en `true` sin la confirmación real.
5. Si `.harness/docs/project_rules.md` ya tiene o va a tener reglas
   MUST/MUST NOT sobre ramas o commits (p. ej. "MUST NOT hacer push
   directo a `develop`"), recuérdale al developer que esas reglas ganan
   sobre cualquier default de este paso — no hace falta duplicarlas en
   `vcs_permissions`.

Guarda todo en `.harness/unity.config.json` → `vcs_provider`,
`vcs_provider_notes`, `vcs_verified`, `vcs_setup_todo`, `vcs_rules_source`
y `vcs_permissions` (solo los overrides sobre la tabla base — ver
`.harness/unity.config.example.json` para el formato exacto). Como
`scripts_root`/`tests_root`, cualquier objeto/array de esta sección va en
una línea o pocas líneas legibles, pero recuerda que `vcs_permissions` es
un objeto anidado — `.harness/init.sh` solo lo valida en profundidad si
hay Python disponible (ver el comentario del propio ejemplo), así que no
dejes de revisarlo tú mismo antes de continuar.

## Paso 5C — Release management (guideline de versionado de la empresa)

> **Puedes saltar directo a este paso** si el humano pide "configura el
> versionado", "aplica la guideline de releases", o similar más adelante —
> no hace falta repetir todo `/unity-claude-code-harness`. La referencia completa es
> `.harness/docs/release_management.md`; este paso solo rellena la config.

Este paso decide si el agente sigue la guideline corporativa
"[GUIDELINE] Tech Release Management Architecture" v3.0 en lo que le
afecta: versiones, nombres de ramas y artefactos, y la tarea de release en
Asana. El pipeline de CI/CD, los entornos y el QA Protocol quedan fuera
del harness (ver la tabla de alcance del documento).

1. **¿Aplica la guideline a este proyecto?** Si dice que no (prototipo,
   proyecto sin releases formales), deja `release_guideline: "none"` y
   salta el resto de este paso. Si dice que sí, `"invelon_rm_v3"`.
2. **Nombres.** Pregunta y anota:
   - `release_app_name` — el `{AppName}` de los artefactos, sin espacios
     (p. ej. `DiputacioLleidaVR`).
   - `release_project_code` — el `[PROYECTO]` del nombre de la tarea de
     release en Asana.
   - `release_platforms` — los `{PLATFORM}` de los artefactos (`META`,
     `PICO`, `ANDROID`, `IOS`, `WINDOWS`…), array en una línea.
3. **Versión en curso.** Pregunta si ya hay una release planificada y cuál
   es su `MAJOR.MINOR.PATCH`. **No la propongas tú**: la decide el
   encargado del proyecto. Si existe `ProjectSettings/ProjectSettings.asset`,
   lee su línea `bundleVersion:` y enséñasela para que confirme si coincide.
   Si no hay release todavía, `release_current_version: null`.
4. **CI/CD.** Pregunta si DevOps ya decidió si el proyecto tiene CI/CD
   (`enabled`), si son solo builds manuales con sufijo `+YYMMDD-HHMM`
   (`manual_builds`), o si no lo sabe (`unknown`). Es informativo: el
   harness no se conecta al pipeline.
5. **Ramas.** Propón los nombres por defecto según `vcs_provider` (Paso 5B)
   y pide confirmación o los nombres reales si el repo ya los tiene:
   - Plastic: `/main`, `/release`, `/dev`, `/dev/<ad_user>`,
     `/dev/<feature>`, `/hotfix/<X.Y.Z>-<desc>`.
   - Git/GitHub: `main`, `release`, `dev`, `user/<ad_user>`,
     `feature/<feature>`, `hotfix/<X.Y.Z>-<desc>`. Explica por qué no
     `dev/<usuario>`: Git no puede tener `dev` y `dev/<algo>` a la vez.
   Si el workspace ya existe, puedes listar las ramas reales (`git branch
   -a`, o en Plastic p. ej. `cm find branch`) para comprobarlo en vez de
   preguntar a ciegas.
6. **Asana.** Si el conector de Asana está disponible, ayúdale a encontrar
   el `gid` del proyecto global **Releases** y del proyecto específico
   (buscándolos vía MCP, no de memoria) y guárdalos en
   `release_asana_releases_project_gid` / `release_asana_project_gid`. Es
   independiente del modo del backlog del Paso 6: funciona aunque el
   backlog sea local. Si no hay conector, déjalos en `null` y anótalo.
7. **Confirma los permisos que esto añade** sobre la tabla del Paso 5B:
   `merge_into_release_or_main` y `create_tag` pasan a `forbidden` (el
   merge a `/release` arranca QA, el merge a `/main` es la aprobación
   formal y los tags son de DevOps), y crear una rama de hotfix pide
   aprobación. Si el equipo quiere relajar alguno, anótalo como override en
   `vcs_permissions` con el motivo en `vcs_provider_notes`.

## Paso 6 — Fuente del backlog: local o Asana

> **Puedes saltar directo a este paso** si el humano pide "conecta el
> backlog a Asana" (o "vuelve a local") más adelante — no hace falta
> repetir todo `/unity-claude-code-harness`.

Pregunta explícitamente (por defecto viene en `local`, nunca lo cambies
sin preguntar):

1. **Local** (por defecto) — el backlog vive solo en
   `.harness/feature_list.json`, editado a mano o por el propio agente.
   Deja `.harness/feature_source.json` como `{"mode": "local"}` (ya viene
   así de fábrica, no hace falta tocarlo).
2. **Asana** — el backlog real vive en un proyecto de Asana;
   `.harness/feature_list.json` pasa a ser solo una caché local
   sincronizada por el agente. Si el developer quiere esto:
   - Comprueba si el conector MCP de Asana ya está habilitado en esta
     sesión. Si lo está, puedes listar sus proyectos de Asana directamente
     para ayudarle a identificar el `workspace_gid`/`project_gid` en vez de
     pedírselos en crudo.
   - Si no está conectado, dile cómo habilitarlo (`/mcp` → añadir Asana, o
     `claude mcp add asana`) — no puedes conectarlo tú mismo por él.
   - Pregunta cómo mapea su proyecto de Asana a los campos del harness
     (sección/columna → `status`, algún campo custom o tag →
     `requires_visual_check`/`requires_plan_approval`, ver
     `.harness/docs/feature_source.md`) y anótalo en `notes`.
   - Escribe `.harness/feature_source.json` con
     `{"mode": "asana", "workspace_gid": "...", "project_gid": "...",
     "notes": "..."}`.

Si el developer no tiene Asana o no lo quiere usar, no insistas — `local`
es la opción por defecto y no requiere nada más.

## Paso 7 — feature_list.json: ¿ejemplo, vacío, o features reales ya?

`.harness/feature_list.json` trae, tal cual viene la plantilla, dos
features de **ejemplo** (`player_health_core`, `player_health_component`) y
el flag `"is_example_data": true` en la raíz del archivo. Son solo para que
un developer nuevo vea el formato — **no son trabajo real de tu proyecto**.
Pregunta explícitamente qué prefiere el developer:

1. **Vaciarlo ahora** → deja `"features": []` y quita
   `"is_example_data": true` (o dile que lo haga él si prefiere ir
   añadiendo features a mano primero).
2. **Dejar los ejemplos de momento, como referencia** → no los toques, pero
   dile claramente que son ilustrativos y que `.harness/init.sh` seguirá
   avisando con un `[WARN]` mientras `is_example_data` siga en `true`.
3. **Ya tiene sus propias features listas para pegar** → reemplaza el
   array `features` por las suyas y quita `is_example_data`.

No dejes esto sin preguntar — es fácil que un developer nuevo confunda las
features de ejemplo con tareas reales pendientes si nadie se lo aclara.
(Si en el Paso 6 se eligió modo `asana`, esto es menos relevante — la
primera sincronización real con Asana sustituirá el contenido de todas
formas, pero igual quita `is_example_data` para que no queden dando
vueltas mientras tanto.)

## Paso 8 — Escribir la configuración

Escribe `.harness/unity.config.json` siguiendo el esquema de
`.harness/unity.config.example.json`, con los valores reales que dio el
developer (no copies el ejemplo literal). `scripts_root` y `tests_root`
van **siempre como array** (`["Assets/Scripts"]`, con tantos elementos
como carpetas haya dicho en el Paso 1) y **cada array en una sola línea**
del JSON — `.harness/init.sh` no puede seguir uno partido en varias
líneas y falla explícitamente si lo detecta así. Incluye
`architecture_status` y `architecture_notes` del Paso 2,
`automation_tool_verified`, `automation_tool_setup_todo` y
`editor_write_policy` del Paso 4, `unity_dev_skills_enabled` del Paso 4B, y
`vcs_provider`, `vcs_provider_notes`, `vcs_verified`, `vcs_setup_todo`,
`vcs_rules_source` y `vcs_permissions` del Paso 5B, y `release_guideline`,
`release_app_name`, `release_project_code`, `release_platforms`,
`release_current_version`, `release_ci_cd`, `release_asana_releases_project_gid`,
`release_asana_project_gid` y los `branch_*` del Paso 5C (arrays en una
sola línea, como siempre). El modo de backlog del
Paso 6 va en `.harness/feature_source.json`, no en `unity.config.json`. Incluye también `configured_by` (usa el email del
developer si lo sabes) y `configured_at` (fecha de hoy).

## Paso 9 — Verificar

Ejecuta `.harness/init.sh`. Si falla porque no detecta `Assets/`,
`ProjectSettings/manifest.json` u otras rutas Unity esperadas, avisa al
developer — puede que el proyecto Unity esté en otra carpeta del repo y
haga falta ajustar rutas o correr Claude Code desde ahí.
