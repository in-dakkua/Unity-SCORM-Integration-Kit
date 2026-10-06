# Fuente del backlog — `local` vs `asana`

> Este documento le dice a cualquier agente (líder o implementador) **de
> dónde saca la lista de features pendientes**. La respuesta vive en
> `.harness/feature_source.json`, escrito por `/unity-claude-code-harness` (o por defecto
> en `{"mode": "local"}` si nunca se preguntó nada).

## Cómo saber en qué modo está el proyecto

```bash
cat .harness/feature_source.json
```

```json
{ "mode": "local" }
```
o
```json
{
  "mode": "asana",
  "workspace_gid": "1234567890",
  "project_gid": "9876543210",
  "notes": "Las columnas del board mapean a status: pending / in_progress / done / blocked. El campo custom 'Requires visual check' mapea a requires_visual_check."
}
```

## Modo `local` (por defecto)

- La fuente de verdad es `.harness/feature_list.json`.
- El developer edita ese archivo a mano (o pide a Claude que añada una
  entrada) para crear, reabrir o repriorizar features.
- `.harness/init.sh` valida el JSON y las reglas (`one_feature_at_a_time`,
  estados válidos, `is_example_data`) antes de dejar avanzar a un agente.

**Cómo añadir una feature manualmente:**

```json
{
  "id": 3,
  "name": "nombre_corto",
  "title": "Título legible",
  "description": "Qué hace y por qué.",
  "acceptance": ["Criterio 1", "Criterio 2"],
  "requires_plan_approval": true,
  "requires_visual_check": false,
  "status": "pending"
}
```

## Modo `asana`

- La fuente de verdad es un proyecto de Asana. `.harness/feature_list.json`
  deja de editarse a mano: se convierte en una **caché local** que el
  agente regenera consultando Asana al empezar cada sesión.
- Cada tarea de Asana se mapea a una feature:
  - `gid` de la tarea → `id`
  - nombre de la tarea → `title`/`name`
  - descripción → `description`
  - sección/columna del board, o un campo custom "Status" → `status`
    (`pending` / `in_progress` / `done` / `blocked`)
  - subtareas, o el campo custom "Acceptance criteria" → `acceptance`
  - un campo custom o tag (p. ej. "Requires visual check" /
    "Requires plan approval") → `requires_visual_check` /
    `requires_plan_approval`. Si Asana no tiene esos campos custom
    todavía, usa los valores por defecto de `unity.config.json`
    (`requires_plan_approval_default`, `requires_visual_check_default`) y
    dilo explícitamente en el plan de la feature.

### Cómo conectar Asana

1. En Claude Code, habilita el conector de Asana si no lo está:
   interactivo con `/mcp` → añadir servidor → **Asana** → sigue el flujo
   OAuth, o `claude mcp add asana` si tu organización distribuye ese MCP.
2. Identifica el `workspace_gid` y el `project_gid` del proyecto de Asana
   que vas a usar como backlog (aparecen en la URL del proyecto en Asana).
   Si el conector ya está conectado, puedes pedirle directamente a Claude
   "lista mis proyectos de Asana" en vez de ir a buscarlos a mano.
3. `/unity-claude-code-harness` te pregunta esto durante la inicialización (o vuelve a
   preguntar si pides "conecta el backlog a Asana" más adelante) y rellena
   `.harness/feature_source.json` con `mode: "asana"` y esos dos valores.
4. A partir de aquí, el **leader** empieza cada sesión consultando las
   tareas del proyecto vía las herramientas MCP de Asana en vez de leer
   `.harness/feature_list.json` directamente, y escribe una copia local en
   `.harness/feature_list.json` para que `.harness/init.sh` y el resto del
   harness (que no hablan con Asana) sigan funcionando sin cambios.
5. Al cerrar una feature, el implementer actualiza el estado en **ambos
   sitios**: la tarea de Asana (vía MCP) y la copia local en
   `.harness/feature_list.json`.

**Nota — comentarios vs. estado:** lo anterior es solo la sincronización
del campo `status`. Si además se va a **publicar un comentario** en la
tarea (cerrar con un resumen, avisar de un bloqueo, pedir un visual check
a alguien), eso sigue un protocolo aparte y obligatorio —
`.harness/docs/asana_context_protocol.md` — y solo lo ejecuta el leader,
nunca el implementer.

**Nota — tareas de release:** la tarea `[PROYECTO] Release vX.Y.Z` del
proyecto global **Releases** no es una feature del backlog y no se
sincroniza en `.harness/feature_list.json`. Agrupa features ya terminadas
y tiene sus propias reglas, independientes de este modo `local`/`asana`:
ver `.harness/docs/release_management.md` §4.

### Si el conector de Asana no está disponible

Si el MCP de Asana no está conectado en la sesión actual (o la herramienta
que estés usando no es Claude Code y no tiene ese conector), el agente debe
avisar explícitamente ("Asana no está conectado, no puedo leer el
backlog") en vez de inventar features o caer en silencio al modo local sin
decirlo. Eso incluye a otras IAs que sigan
`AI_TOOLS.md` — si no tienen forma de hablar con Asana, se lo dicen al
humano y proponen quedarse en modo `local` hasta que alguien más lo
sincronice.
