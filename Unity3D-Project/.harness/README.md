# Harness Unity — cómo funciona este proyecto con IA

Este proyecto usa una plantilla de Harness Engineering pensada para
equipos Unity. Vive casi entera en esta carpeta (`.harness/`) para no
mezclarse con `Assets/`, `ProjectSettings/`, `Packages/` ni el resto de tu
proyecto — solo `.harness/` y `.claude/` (en la raíz del repo, ambos
ocultos por convención) forman parte del harness.

Se diseñó para equipos que:

- Trabajan en C#/.NET pero con Unity, donde parte del resultado (una
  escena, un prefab, un efecto visual) **solo se verifica abriendo el
  Editor**, no con un test unitario.
- Tienen developers con preferencias distintas: algunos quieren TDD
  estricto, otros prefieren **revisar el plan antes de que el agente
  implemente** y no escriben tests para todo.
- Usan distintas herramientas para que un agente de IA controle Unity:
  el MCP oficial, [unity-mcp de CoplayDev](https://github.com/CoplayDev/unity-mcp)
  (open source) o la [Unity CLI](https://docs.unity.com/en-us/unity-cli/use-unity-cli).

> **¿Vas a usar una herramienta de IA que no es Claude Code** (Cursor,
> Windsurf, un copiloto interno, etc.)? `AGENTS.md` y `CLAUDE.md` asumen
> subagentes y slash commands propios de Claude Code. Dale a esa
> herramienta **[`AI_TOOLS.md`](AI_TOOLS.md)** como su punto de entrada —
> traduce todo el protocolo (inicialización, roles, gates humanos) a algo
> que un único agente sin esos mecanismos puede seguir igual.

## Cómo está organizado el arnés

| Pilar | Manifestación en este repo |
|-------|----------------------------|
| **1. El repositorio ES el sistema** | `.harness/AGENTS.md`, `.harness/init.sh`, `.harness/feature_list.json`, `.harness/progress/`, `.harness/docs/`, `.harness/unity.config.json` |
| **2. Orquestación multi-agente**    | `.claude/agents/leader.md`, `implementer.md`, `reviewer.md` |
| **3. Human-in-the-loop**            | Gate de plan (`.harness/progress/plan_<feature>.md`) + visual check firmado (`.harness/progress/visual_check_<feature>.md`), ver `docs/verification.md` |
| **4. Supervisión y mejora**         | `.harness/CHECKPOINTS.md`, hooks en `.claude/settings.json` |
| **5. Configuración por equipo**     | `.claude/commands/unity-claude-code-harness.md`, `.harness/unity.config.json` |
| **6. Skills de Unity/C# bundled**   | `.claude/skills/` (bootstrap, package manager, code review, test writer, project cleanup) — ver `.harness/docs/unity_dev_skills.md` |
| **7. Comentarios de Asana con humano en el loop** | `.harness/docs/asana_context_protocol.md` — borrador enseñado al developer + disclosure de IA antes de publicar cualquier comentario |
| **8. Control de versiones con permisos configurables** | `.harness/docs/vcs_policy.md` — proveedor (GitHub / Unity VCS-Plastic), tabla de permisos por acción (`allowed`/`requires_approval`/`forbidden`) y protocolo de aprobación; solo el leader ejecuta escritura |
| **9. Versiones y releases según la guideline de la empresa** | `.harness/docs/release_management.md` — formato `MAJOR.MINOR.PATCH+BUILD`, nombres de artefactos, ramas `/dev`–`/release`–`/main` y tarea de release en Asana; el agente no decide versiones ni mergea a release/main |

## Para empezar

Si acabas de instalar esto (con `install.sh`/`install.ps1`), no hace falta
que hagas nada más manualmente:

1. Abre Claude Code en la raíz de tu proyecto Unity. El hook `SessionStart`
   de `.claude/settings.json` detecta que falta `.harness/unity.config.json`
   y el propio agente arranca las preguntas de inicialización en su primer
   mensaje. (Si tu herramienta no dispara hooks, o quieres reconfigurar más
   adelante, pide **`/unity-claude-code-harness`** explícitamente en cualquier momento.)
   Te va a preguntar (no asumir):
   - La versión de Unity de tu proyecto (o la detecta de
     `ProjectSettings/ProjectVersion.txt` y te la confirma).
   - Las rutas reales de tu código y tests (`Assets/Scripts`,
     `Assets/Tests`, o las que uses).
   - **Tu arquitectura de carpetas:** si ya tienes una estructura propia
     dentro de `Assets/Scripts` (p. ej. `Managers/Systems/Data` en vez de
     `Core/Gameplay/UI`), la inspecciona y te pregunta si quieres que
     `.harness/docs/architecture.md` describa *esa* convención real en vez
     de la de la plantilla. Si el proyecto es nuevo, te muestra la
     arquitectura por defecto de la plantilla y dónde editarla si prefieres
     otra.
   - Qué herramienta de automatización Unity vas a usar (MCP oficial,
     unity-mcp OSS, Unity CLI, o ninguna todavía).
   - Tu modo de verificación preferido: TDD estricto, plan+visual check, o
     híbrido — y si el plan de cada feature necesita tu aprobación
     explícita antes de que el agente toque código.
   - **Reglas obligatorias propias del equipo** (MUST/MUST NOT) más allá de
     las que ya trae el harness — p. ej. "nunca toques
     `Assets/Plugins/ThirdParty/`". Quedan en `.harness/docs/project_rules.md`
     con prioridad sobre el resto de docs.
   - **De dónde sale el backlog:** local (`.harness/feature_list.json`
     editado a mano, por defecto) o conectado a un proyecto de **Asana**
     real vía MCP (con `feature_list.json` como caché local sincronizada) —
     ver `.harness/docs/feature_source.md`.
   - **Control de versiones:** qué proveedor usa el equipo (GitHub, Unity
     Version Control/Plastic, o ninguno todavía), de dónde salen las
     reglas de permiso (la tabla común del harness, las reglas reales del
     repo leídas en vivo, o una tabla propia), y qué puede hacer el agente
     sin preguntar, qué necesita tu aprobación explícita antes de cada
     ejecución, y qué no debe hacer nunca (por defecto: force-push o
     reescribir historial) — ver `.harness/docs/vcs_policy.md`.
   - **Versionado y releases:** si el proyecto sigue la guideline
     corporativa de release management — nombre de app, código de
     proyecto, versión en curso, nombres de ramas y proyectos de Asana
     para la tarea de release. Ver `.harness/docs/release_management.md`.
   - **Qué hacer con las dos features de ejemplo** que trae
     `.harness/feature_list.json` (`player_health_core`,
     `player_health_component`) — vaciarlas, dejarlas como referencia, o
     sustituirlas ya por las tuyas. Mientras sigan ahí con
     `"is_example_data": true`, no son trabajo real — trátalas solo como
     muestra del formato.
2. Ejecuta `.harness/init.sh` — debe terminar en verde (los `[WARN]` suelen
   pedir una acción manual tuya en el Editor, no son errores). No depende
   de Python para lo esencial; si quieres la validación más profunda de
   `feature_list.json` y no tienes Python, el propio script te dice de
   dónde bajarlo.
3. Abre `.harness/AGENTS.md` y sigue desde ahí.

## El flujo human-in-the-loop en la práctica

```
Leader recibe la tarea
   │
   ├─→ (si falta contexto) lanza 2-3 explorers en paralelo
   │
   └─→ lanza 1 implementer
           │
           ├─ escribe .harness/progress/plan_<feature>.md
           │
           ├─ SI requires_plan_approval: true → PARA y espera tu "apruebo el plan"
           │
           ├─ implementa + escribe tests (según verification_mode)
           │
           ├─ SI requires_visual_check: true → te pide abrir el Editor,
           │   probar los pasos de .harness/progress/visual_check_<feature>.md,
           │   y FIRMARLO tú — el agente no puede hacerlo por ti
           │
           └─→ lanza 1 reviewer, que rechaza si falta el plan aprobado
               o el visual check firmado
```

Nada de esto pasa "por confianza": `.harness/init.sh` y el reviewer
comprueban en disco que el plan y el visual check existen antes de dejar
marcar una feature como `done`.

## Estructura

```
tu-proyecto-unity/
├── Assets/, ProjectSettings/, Packages/   # tu proyecto Unity, intacto
├── CLAUDE.md                  # Stub mínimo — importa .harness/CLAUDE.md
├── .claude/
│   ├── agents/                # leader, implementer, reviewer (Unity-aware)
│   ├── commands/
│   │   └── unity-claude-code-harness.md   # Cuestionario de inicialización (/unity-claude-code-harness)
│   ├── settings.json          # Hooks: SessionStart dispara /unity-claude-code-harness, Stop fuerza init.sh
│   └── skills/                # unity-dev-skills bundled: bootstrap, package manager,
│                               # code review, test writer, project cleanup (auto-invocados)
└── .harness/
    ├── CLAUDE.md               # Cuerpo real de las instrucciones (importado por el CLAUDE.md raíz)
    ├── AGENTS.md               # Mapa para agentes (divulgación progresiva)
    ├── AI_TOOLS.md             # Punto de entrada para IAs que no son Claude Code
    ├── CHECKPOINTS.md          # Criterios de "estado final correcto"
    ├── feature_list.json       # Alcance: una feature a la vez (trae 2 de ejemplo, is_example_data: true)
    ├── feature_source.json     # local (por defecto) o asana — de dónde sale el backlog real
    ├── init.sh                 # Verificación: config, proyecto Unity real, tests
    ├── unity.config.json       # Config real del proyecto (generado por /unity-claude-code-harness)
    ├── unity.config.example.json
    ├── progress/
    │   ├── current.md          # Sesión activa (estado vivo)
    │   ├── history.md          # Bitácora append-only
    │   ├── plan_<feature>.md      # Plan del implementer, pendiente/aprobado
    │   └── visual_check_<feature>.md  # Checklist firmado por un humano
    └── docs/
        ├── architecture.md     # Capas Core/Gameplay/UI (o la convención real del equipo, ver architecture_status)
        ├── project_rules.md    # Reglas MUST/MUST NOT propias del equipo — prioridad sobre el resto
        ├── conventions.md      # Estilo C#/Unity
        ├── verification.md     # Niveles de verificación + gate visual humano
        ├── mcp_setup.md        # Unity MCP vs unity-mcp OSS vs Unity CLI
        ├── feature_source.md   # local vs Asana — cómo se sincroniza el backlog
        ├── unity_dev_skills.md # Procedencia y reglas de los skills en .claude/skills/
        ├── asana_context_protocol.md # Protocolo obligatorio para comentarios en Asana
        ├── vcs_policy.md       # Proveedor VCS, tabla de permisos por acción y protocolo de aprobación
        └── release_management.md # Versiones, nombres, ramas y tarea de release en Asana (guideline corporativa)
```

## Diferencias con el harness genérico (Python/CLI)

- No asume un lenguaje ni un test runner fijo — las rutas y el modo de
  verificación se preguntan y se guardan en `.harness/unity.config.json`.
- Añade un **gate de plan** explícito antes de tocar código, pensado para
  developers que prefieren revisar antes de que el agente ejecute.
- Añade un **nivel de verificación visual** que un agente no puede
  autocompletar — reconoce que Unity es una app visual y que "los tests
  pasan" no es suficiente evidencia para features de UI/escena.
- No corre verificación en cada `Edit`/`Write` (a diferencia del genérico):
  recompilar/testear en Unity es más lento y a veces requiere el Editor
  abierto, así que solo se fuerza al cerrar sesión (`Stop` hook).
- Vive casi entero en `.harness/`, no en la raíz del proyecto — pensado
  para instalarse dentro de un proyecto Unity que ya tiene su propia
  estructura, sin mezclarse con ella.
