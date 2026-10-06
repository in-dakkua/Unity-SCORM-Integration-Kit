# Protocolo de comentarios en tareas de Asana

> Este documento define **cómo** se publica cualquier comentario que un
> agente decida dejar en una tarea de Asana — no decide **cuándo** hacerlo.
> Nada aquí obliga a comentar nada; cuando sí se decide comentar (cerrar una
> feature, avisar de un bloqueo, pedir un visual check a alguien concreto,
> etc.), este protocolo es obligatorio sin excepción.
>
> Aplica siempre que `.harness/feature_source.json` tenga `"mode": "asana"`
> (ver `.harness/docs/feature_source.md`) y el agente esté a punto de
> llamar a la herramienta MCP de Asana `add_comment` sobre cualquier tarea.
> Es una regla **nativa del harness** (no un `project_rules.md` por
> equipo): vive en `.claude/agents/leader.md`, `.harness/CLAUDE.md` y
> `.harness/AGENTS.md`.

> Crear o editar la **tarea de release** (proyecto Releases) no es un
> comentario, pero sigue el mismo espíritu: campos exactos enseñados antes
> y un sí explícito. Ver `.harness/docs/release_management.md` §4. Los
> comentarios dentro de esa tarea sí siguen este protocolo tal cual.

## Regla en una frase

**Ningún comentario se publica sin que el developer haya visto antes el
texto exacto que se va a publicar, y haya decidido si quiere añadir/quitar
algo o mencionar a alguien.**

## Quién ejecuta esto

Solo el **leader** llama a `add_comment`. `implementer` y `reviewer` nunca
publican comentarios en Asana directamente — si durante su trabajo surge
algo que vale la pena comunicar en la tarea, se lo devuelven al leader (en
su resultado escrito, como el resto de su comunicación) para que sea él
quien siga este protocolo.

## Los 3 pasos (obligatorios, en orden, cada vez)

1. **Enseña el borrador exacto** en el chat — el texto tal cual se
   publicaría, incluida la línea de disclosure. No resumas "voy a comentar
   que ya terminé la feature": pega el comentario completo.
2. **Pregunta explícitamente** dos cosas — no una:
   - "¿Quieres añadir o quitar algo del texto?"
   - "¿Quieres mencionar a alguien del equipo?"
3. **Ajusta si hace falta y publica solo entonces.** Si hay una mención
   real, resuelve primero el `gid` de esa persona (ver más abajo) antes de
   llamar a `add_comment`.

No hay atajo para "es solo un comentario rápido" — los 3 pasos aplican
igual la primera vez que la enésima.

## Formato del comentario

### Longitud

- **Línea de disclosure** (ver plantilla abajo): siempre la primera línea,
  **no cuenta** dentro del límite de longitud.
- **Cuerpo: 3 a 6 líneas.** Es una síntesis para que un humano la lea en
  10 segundos, no un informe técnico. El detalle completo — decisiones,
  archivos tocados, razonamiento — sigue viviendo en
  `.harness/progress/` (plan, impl, review); el comentario puede
  referenciar ese archivo por nombre, pero no repetir su contenido.

### Línea de disclosure (texto fijo, cópialo tal cual)

```
_Generado por IA (Claude Code) — revisado por el developer antes de publicarse._
```

### Plantilla

```
_Generado por IA (Claude Code) — revisado por el developer antes de publicarse._

<línea 1 de síntesis>
<línea 2 de síntesis>
<línea 3-6 de síntesis, opcional>

(opcional) Detalle completo: .harness/progress/<archivo>.md
```

### Ejemplo de longitud correcta (4 líneas de cuerpo)

```
_Generado por IA (Claude Code) — revisado por el developer antes de publicarse._

Implementado PlayerHealthComponent sobre PlayerHealthCore, siguiendo el plan aprobado.
Tests EditMode en verde; falta el visual check humano sobre la escena Gameplay/Sample.
El reviewer aprobó el código (ver .harness/progress/review_player_health_component.md).
Detalle completo: .harness/progress/impl_player_health_component.md
```

Esto **no** vale como comentario (es un informe, no una síntesis):

```
He implementado la clase PlayerHealthComponent que hereda de MonoBehaviour
y envuelve PlayerHealthCore. Los métodos son TakeDamage(int amount), Heal(int
amount) y los eventos OnDamaged/OnDied se exponen como UnityEvent para que
el diseñador los conecte desde el Inspector. Además añadí un test EditMode
que verifica que la salud no baja de 0 y otro que verifica el evento
OnDied... [sigue 15 líneas más]
```

## Menciones reales — nunca texto plano

- Un "@Nombre" escrito como texto plano **no es una mención** en Asana: no
  genera notificación ni enlace. Está prohibido usarlo como sustituto.
- Toda mención real se construye como `<a data-asana-gid="GID"/>` dentro
  de `html_text` (Asana rellena automáticamente el nombre visible a partir
  del `gid`).
- **El `gid` sale siempre de una llamada real a una herramienta MCP de
  Asana** — nunca se inventa, adivina, ni se reutiliza de memoria de una
  conversación anterior:
  - Preferido: `search_objects` con `resource_type: "user"` (o `"actor"`
    si podría ser un teammate de IA, no una persona) y `query` con el
    nombre que dio el developer.
  - Alternativa: `get_users` y filtrar por nombre/email si `search_objects`
    no da un resultado claro.
- **Si la resolución es ambigua o no da resultados** (varios usuarios con
  nombre parecido, ninguno encontrado): no publiques igual con el primer
  resultado ni caigas a "@Nombre" en texto plano. Pregunta al developer
  cuál es la persona correcta (o su email) antes de continuar.

## Cómo publicar (llamada a `add_comment`)

- **Sin mención** → usa el parámetro `text` (texto plano). Más simple,
  suficiente para la mayoría de comentarios.
- **Con mención real** → usa `html_text`, nunca `text` a la vez. Reglas
  del formato:
  - Todo el contenido va envuelto en una única raíz `<body>...</body>`.
  - Los saltos de línea entre líneas de síntesis van como salto de línea
    literal dentro de `<body>` — **no** uses `<br/>` ni `<p>`, no están en
    la lista de elementos permitidos por la API de Asana.
  - Los únicos elementos permitidos son `<strong>`, `<em>`, `<u>`, `<s>`,
    `<code>`, `<ol>`, `<ul>`, `<li>`, `<a>`, `<blockquote>`, `<pre>` (y el
    `<body>` raíz). Solo `<a>` admite atributos custom (`data-asana-gid`).
  - Ejemplo de `html_text` con mención:

    ```
    <body>_Generado por IA (Claude Code) — revisado por el developer antes de publicarse._

    Visual check pendiente en la escena Gameplay/Sample antes de cerrar la feature.
    <a data-asana-gid="1203456789012345"/> ¿puedes probarlo cuando tengas un hueco?
    Detalle: .harness/progress/impl_player_health_component.md</body>
    ```

## Ejemplo del flujo completo (con mención)

1. Leader termina de orquestar una feature y prepara el borrador (sin
   mención todavía) siguiendo la plantilla de arriba. Lo pega en el chat.
2. Developer: "añade que falta el visual check, y menciona a María para
   que lo revise."
3. Leader ajusta el texto de síntesis, y resuelve el `gid` de María con
   `search_objects(resource_type: "user", query: "María")`. Si hay una
   sola coincidencia clara, la usa; si no, pregunta cuál es.
4. Leader enseña de nuevo el texto final (ahora con la mención ya resuelta
   como se vería, p. ej. "@María ¿puedes probarlo...") y confirma que es
   correcto.
5. Solo entonces llama a `add_comment` con `html_text` y el `<a
   data-asana-gid="...">` real.

## MUST / MUST NOT — resumen

- **MUST** mostrar el borrador exacto antes de publicar, cada vez.
- **MUST** preguntar explícitamente por ajustes y por menciones — las dos
  cosas, no solo una.
- **MUST** empezar el comentario con la línea de disclosure fija.
- **MUST** mantener el cuerpo en 3-6 líneas de síntesis; el detalle vive en
  `.harness/progress/`.
- **MUST** resolver cualquier `gid` de mención con una llamada real a
  `search_objects`/`get_users` antes de construir el `<a
  data-asana-gid="...">`.
- **MUST NOT** publicar un comentario que no haya pasado por los 3 pasos
  en esta misma sesión.
- **MUST NOT** usar "@Nombre" en texto plano como sustituto de una mención
  real.
- **MUST NOT** inventar, adivinar o reutilizar un `gid` de una conversación
  anterior sin volver a resolverlo.
- **MUST NOT** dejar que `implementer` o `reviewer` publiquen un comentario
  directamente — siempre pasa por el leader.
