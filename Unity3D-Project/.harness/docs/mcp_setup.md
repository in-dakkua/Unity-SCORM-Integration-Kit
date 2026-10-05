# Herramientas de automatización Unity — cómo elegir y dónde queda registrado

> La elección real de cada proyecto vive en `.harness/unity.config.json` →
> `automation_tool`, y se decide interactivamente con `/unity-claude-code-harness`. Este
> documento es la referencia para esa conversación, no una decisión tomada
> de antemano.

## Opciones

### 1. Unity MCP oficial (Unity AI)
- Referencia: https://unity.com/blog/unity-ai-mcp-how-to-get-started
- Soporte oficial de Unity Technologies, integrado con Unity AI.
- Recomendable si el equipo ya usa Unity 6 / Unity AI y quiere la ruta con
  menos piezas de terceros.
- Configuración real (API keys, habilitar el paquete) la hace el humano
  siguiendo esa guía — el harness solo necesita saber que se eligió esta
  opción para adaptar las instrucciones que da a los subagentes.

### 2. unity-mcp open source (CoplayDev)
- Referencia: https://github.com/CoplayDev/unity-mcp
- Alternativa open source, útil si el equipo quiere control total del
  servidor MCP o no puede depender de la oferta oficial.
- Requiere instalar el paquete Unity + el servidor MCP local siguiendo el
  README de ese repo.

### 3. Unity CLI
- Referencia: https://docs.unity.com/en-us/unity-cli/use-unity-cli
- Automatización por línea de comandos (build, tests en batchmode) sin un
  servidor MCP de por medio.
- Útil si el equipo solo necesita disparar builds/tests desde el agente,
  sin manipular la escena en vivo.
- Es la opción que usa `.harness/init.sh` para intentar correr tests
  automáticos (ver `unity_editor_path` en la config) independientemente de
  qué otra herramienta se elija para el resto del trabajo.

### 4. Ninguna todavía
- Está bien no tener nada configurado al principio. `automation_tool:
  "none"` dejará constancia y se puede volver a `/unity-claude-code-harness` cuando el
  equipo decida.

## Elegir el nombre NO es lo mismo que tenerla lista

Es fácil que en `/unity-claude-code-harness` se elija una herramienta (p. ej. Unity CLI)
en un momento en que todavía no se puede terminar de configurar — por
ejemplo, porque el proyecto Unity aún no existía, o Unity Hub no tenía
ningún editor instalado todavía. Si eso pasa y nadie vuelve a cerrar el
círculo, el harness queda en un estado engañoso: `automation_tool` dice
una cosa, pero en la práctica nadie invoca esa herramienta para nada.

Por eso `.harness/unity.config.json` tiene un campo separado,
`automation_tool_verified` (bool, `false` por defecto), que solo pasa a
`true` cuando el mecanismo concreto de la herramienta elegida **ya
funciona de verdad** — no cuando solo se eligió el nombre. Qué significa
"funciona de verdad" depende de la herramienta:

| Herramienta | Qué hace falta para `automation_tool_verified: true` | Campo relacionado |
|---|---|---|
| `unity_cli` | `unity_editor_path` apunta a un ejecutable de Unity que existe en disco (`.harness/init.sh` lo comprueba) | `unity_editor_path` |
| `unity_mcp_official` / `unity_mcp_coplay_oss` | La persona confirma que probó la conexión MCP y el Editor responde (esto el harness no lo puede verificar solo con archivos — hay que preguntar) | `automation_tool_notes` |
| `none` | No aplica | — |

**Mientras `automation_tool_verified` sea `false` y `automation_tool` no
sea `"none"`:**

- `.harness/init.sh` avisa con un `[WARN]` en cada ejecución, citando
  `automation_tool_setup_todo` si tiene algo escrito.
- Ningún agente (Claude Code u otro) debe usar esa herramienta para
  nada real — correr tests en batchmode, invocar el Editor vía MCP, etc. —
  sin antes volver a preguntar al humano si ya está lista. Seguir
  trabajando en código C# normal no está bloqueado, solo el uso de la
  herramienta de automatización en sí.
- Cuando las circunstancias cambien (se crea el proyecto, se instala
  Unity, se prueba la conexión MCP), el humano puede pedir simplemente
  "termina de configurar la herramienta de automatización" — no hace
  falta repetir todo `/unity-claude-code-harness`, solo esa parte (ver Paso 4 en
  `.claude/commands/unity-claude-code-harness.md`).

### Cómo localizar el ejecutable de Unity (para `unity_editor_path`)

Si el proyecto ya existe pero no se sabe la ruta exacta, un agente con
acceso a shell puede buscarla en las ubicaciones típicas de Unity Hub
antes de preguntar al humano (y luego pedirle que confirme cuál es):

- **Windows:** `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe`
- **macOS:** `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity`
- **Linux:** `~/Unity/Hub/Editor/<version>/Editor/Unity`

`<version>` debe coincidir con `unity_version` en la config (o con
`ProjectSettings/ProjectVersion.txt`). Si no se encuentra en ninguna de
esas rutas, pregunta directamente al developer — puede tener una
instalación fuera de Hub.

## Qué hace y qué NO hace este harness con esa elección

- ✅ Pregunta y registra la elección en `.harness/unity.config.json`.
- ✅ Vuelve a preguntar y verifica antes de asumir que la herramienta
  elegida está realmente lista para usarse (`automation_tool_verified`).
- ✅ Adapta las instrucciones que da a los subagentes (p. ej. qué comando
  usar para correr tests en batchmode) según la elección.
- ❌ No instala paquetes, no crea API keys, no clona repos de terceros por
  su cuenta. La instalación real de MCP/CLI la hace el developer siguiendo
  el enlace correspondiente.

## Nota: esto es distinto de los skills de `.claude/skills/`

Este documento trata de **quién controla el Editor de Unity en vivo**
(MCP) o dispara builds/tests (CLI). El harness también trae, por
separado, 5 *skills* de Claude Code en `.claude/skills/` con conocimiento
de Unity/C# (bootstrap de proyecto, gestión de `manifest.json`, code
review, escritura de tests, limpieza de proyecto) — no sustituyen ni
compiten con la elección de `automation_tool` de aquí arriba, y **no
incluyen** ningún skill de Unity CLI (esa pieza ya la cubre esta página).
Ver `.harness/docs/unity_dev_skills.md`.
