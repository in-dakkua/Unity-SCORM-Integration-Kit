# Control de versiones — proveedor, permisos y protocolo de aprobación

> La elección real de cada proyecto vive en `.harness/unity.config.json` →
> `vcs_provider` / `vcs_rules_source` / `vcs_permissions`, y se decide
> interactivamente en `/unity-claude-code-harness` (Paso 5B). Este documento es la
> referencia para esa conversación y la tabla que consulta el leader antes
> de ejecutar cualquier comando de control de versiones — no una decisión
> tomada de antemano ni algo que un agente pueda inventar sobre la marcha.

## Regla en una frase

**El leader es el único rol que ejecuta escritura de control de versiones
(`commit`/`checkin`, ramas, `push`, PR/merge request, merge, borrado de
rama), y solo lo hace consultando primero la tabla de permisos de este
documento — nunca de memoria, nunca "total es solo un commit rápido".**

Leer/inspeccionar (`git status`/`diff`/`log`, `cm status`/`log`) no está
sujeto a esta tabla: cualquier rol (`leader`, `implementer`, `reviewer`)
puede ejecutarlo libremente en cualquier momento, no cambia nada en el
repositorio.

## Quién ejecuta qué

- **`implementer`** y **`reviewer`** tienen `Bash` en su lista de
  herramientas, pero **nunca** ejecutan un comando de control de versiones
  que escriba algo (ni `git commit`, ni `git push`, ni `cm checkin`, ni
  crear/borrar ramas). Si durante su trabajo creen que hace falta un
  commit, un push o abrir un PR, lo devuelven al leader en su resultado
  escrito — igual que ya hacen con lo que valdría la pena comentar en
  Asana (ver `.harness/docs/asana_context_protocol.md`).
- **`leader`** es el único que decide y ejecuta. Sigue el protocolo de
  aprobación de más abajo para cualquier acción marcada
  `requires_approval`, y rechaza sin excepción cualquier acción marcada
  `forbidden` — reportando el bloqueo al humano en vez de buscar un
  rodeo.

Esto es el mismo patrón de un único publicador que ya usa el harness para
Asana (`add_comment` solo lo llama el leader) — aquí aplica a cualquier
comando de escritura del control de versiones.

## Fuentes de las reglas (`vcs_rules_source`)

Tres formas de decidir qué nivel de permiso tiene cada acción. Se elige
una en `/unity-claude-code-harness` y queda anotada en `.harness/unity.config.json` — no
se mezclan sin que el developer lo pida explícitamente.

### 1. `harness_common` — la tabla base de este documento

La tabla de "Permisos por defecto" de más abajo, sin cambios. Es el punto
de partida razonable si el equipo no tiene (o no quiere pensar todavía)
una política propia de ramas protegidas.

### 2. `repo_detected` — leer las reglas reales del repositorio, en vivo

**No se cachea nada de esto en `unity.config.json`** — solo la elección de
la fuente. Las reglas de un repositorio cambian (alguien añade protección
de rama, edita `CODEOWNERS`) y una copia guardada en el momento de
`/unity-claude-code-harness` quedaría obsoleta sin que nadie lo note. El leader relee esto
**cada vez** que va a ejecutar una acción `requires_approval` o va a decidir
si algo está `forbidden`.

Los dos proveedores **no son simétricos** aquí:

- **GitHub** tiene reglas consultables por comando:
  - `gh api repos/{owner}/{repo}/branches/{branch}/protection` — si la
    rama de destino está protegida (requiere PR, requiere reviews, prohíbe
    force-push), trátalo como señal dura: `push` directo a esa rama y
    `force_push_or_rewrite_history` pasan a `forbidden` aunque la tabla
    base diga otra cosa, y `merge_or_checkin_to_protected_branch` exige
    `open_pr_or_merge_request` primero, nunca un merge directo por CLI.
  - Existencia de `.github/CODEOWNERS` (o `CODEOWNERS`/`docs/CODEOWNERS`)
    — si existe, un PR a una ruta con dueño designado probablemente
    necesita su aprobación humana igualmente; no la sustituye el agente.
  - Si `gh` no está autenticado o el comando falla, no asumas "sin
    protección" — trátalo como `custom`/pregunta al humano para esa
    acción concreta.
- **Unity VCS (Plastic)** no tiene un equivalente local fiable: los
  permisos reales (quién puede hacer checkin a `main`, bloqueos
  exclusivos de archivos binarios) viven en ACLs del lado del servidor,
  no en un archivo del workspace que el agente pueda leer. `.plasticignore`
  y las convenciones de nombres de rama no son lo mismo que un permiso.
  Con este proveedor, `repo_detected` **degrada a preguntar al humano**
  cada vez que la acción sea sobre una rama compartida (`main`/`development`
  o el nombre que use el equipo) — no finjas que hay paridad con GitHub
  aquí.

### 3. `custom` — reglas que define el developer en `/unity-claude-code-harness`

El developer recorre la tabla de acciones (ver abajo) y decide el nivel de
cada una para su equipo. Las respuestas quedan en
`.harness/unity.config.json` → `vcs_permissions` como *overrides* sobre la
tabla base — solo hace falta escribir las acciones que se apartan del
default de `harness_common`, no las 9 filas completas si la mayoría se
queda igual.

Si además el equipo tiene reglas propias en
`.harness/docs/project_rules.md` (p. ej. "MUST NOT hacer push directo a
`develop`"), esas **ganan** sobre cualquier cosa de este documento — es el
mismo principio de prioridad que ya aplica al resto del harness.

## Taxonomía de acciones y permisos por defecto (`harness_common`)

| Acción | Git / GitHub | Unity VCS (Plastic) — `cm` | Default |
|---|---|---|---|
| `commit_local` | `git add` + `git commit` en una rama local/de feature, sin publicar todavía | `cm checkin` a una rama de tarea propia, no compartida | `allowed` |
| `create_branch` | `git branch` / `git checkout -b` | `cm branch create` | `allowed` |
| `push` | `git push` de una rama a su remoto | `cm checkin` que llega a una rama rastreada/compartida en el servidor (en Plastic, checkin y "publicar" suelen ser el mismo paso) | `requires_approval` |
| `open_pr_or_merge_request` | `gh pr create` | Merge request si el equipo usa Unity DevOps/Cloud; si no, pedir la fusión directamente al humano | `requires_approval` |
| `merge_or_checkin_to_protected_branch` | merge/squash a `main`/`master`/cualquier rama protegida | `cm merge`/checkin directo a `main`/`development`/trunk | `requires_approval` |
| `delete_branch` | `git branch -d/-D`, `git push --delete` | `cm branch delete` | `requires_approval` |
| `create_tag` | `git tag` (+ `git push --tags`) | p. ej. `cm label create` (confirma la sintaxis con `cm help`) | `requires_approval` (`forbidden` con `release_guideline: "invelon_rm_v3"`) |
| `merge_into_release_or_main` | merge/PR hacia `branch_release` o `branch_main` | `cm merge` hacia `branch_release` o `branch_main` | como `merge_or_checkin_to_protected_branch` (`forbidden` con `release_guideline: "invelon_rm_v3"`) |
| `force_push_or_rewrite_history` | `git push --force`(-with-lease), `git rebase` de una rama ya compartida, `git commit --amend` ya publicado | reescritura de historial (rara y desaconsejada en Plastic) | `forbidden` |

`create_tag` y `merge_into_release_or_main` son acciones separadas porque
en el flujo de release de la empresa tienen dueño propio (DevOps/Tech Lead)
— ver la sección siguiente. Sin `release_guideline`, `merge_into_release_or_main`
se trata igual que `merge_or_checkin_to_protected_branch`.

`force_push_or_rewrite_history` en `forbidden` es el único default que
este documento recomienda no relajar salvo un motivo explícito y anotado
por el developer — es exactamente el tipo de acción "difícil de revertir"
que ya cubre la guía general de este agente (ver "Executing actions with
care" en las instrucciones del sistema).

## Modelo de ramas de release (`release_guideline`)

Si `.harness/unity.config.json` → `release_guideline` es `"invelon_rm_v3"`,
el leader trabaja con el modelo de ramas de
`.harness/docs/release_management.md` §3 y sus nombres reales en la config
(`branch_main`, `branch_release`, `branch_dev`, `branch_personal_pattern`,
`branch_feature_pattern`, `branch_hotfix_pattern`):

- Cualquier rama que cree el agente **MUST** seguir uno de esos patrones.
  Por defecto trabaja en la rama personal; la de feature solo si la
  persona lo decide, y la de hotfix siempre con aprobación y con el
  `X.Y.Z` que da la persona.
- `branch_dev` cuenta como rama protegida (`merge_or_checkin_to_protected_branch`).
- `merge_into_release_or_main` y `create_tag` pasan a `forbidden`: el
  merge a `/release` arranca QA, el merge a `/main` es la aprobación
  formal, y los tags son de DevOps.

**Orden de prioridad** cuando varias fuentes dicen cosas distintas (gana
la primera):

1. `.harness/docs/project_rules.md` (MUST/MUST NOT del equipo).
2. `vcs_permissions` en `unity.config.json` (override explícito del
   developer). Por eso `create_tag` y `merge_into_release_or_main` solo se
   escriben ahí cuando el developer decide desviarse del perfil: escribir
   el valor base "para dejarlo documentado" anularía el `forbidden`.
3. Los defaults del perfil de release de arriba.
4. La tabla base `harness_common`.

Con `vcs_rules_source: "repo_detected"`, las reglas leídas en vivo del
repositorio solo pueden **endurecer** este resultado, nunca relajarlo.

## Protocolo de aprobación para `requires_approval` (obligatorio, en orden)

Mismo espíritu que `.harness/docs/asana_context_protocol.md`: nada se
ejecuta sin que el developer haya visto antes exactamente qué se va a
hacer.

1. **Enseña el comando exacto** que vas a ejecutar (o los pasos con
   `gh`/`cm` si son varios), y qué rama/remoto/archivos afecta. No lo
   resumas ("voy a subir los cambios") — pega el comando literal.
2. **Di qué evidencia tienes** de que es seguro hacerlo ahora: reviewer
   `APPROVED`, `.harness/init.sh` en verde, visual check firmado si
   aplica (lo que ya exige `.harness/docs/verification.md` para marcar
   `done` aplica igual antes de publicar ese trabajo).
3. **Espera confirmación explícita** del developer en el chat. Si pide
   cambios (otra rama, otro mensaje de commit, esperar), ajusta y vuelve a
   enseñar antes de ejecutar.
4. **Ejecuta solo entonces**, y reporta el resultado real (hash del
   commit/changeset, URL del PR si se creó) — no un "listo" genérico.

Para `forbidden`: no hay protocolo de aprobación porque no hay excepción
en el chat que lo habilite. Si el developer pide explícitamente saltarse
un `forbidden` (p. ej. "haz force-push de todas formas"), no lo hagas
directamente — dile que para eso hace falta cambiar
`.harness/unity.config.json` → `vcs_permissions` (o pedirlo él mismo desde
su propia terminal), y explica por qué esa acción concreta está marcada
así.

## Notas específicas de Unity (además de lo que ya cubre `project_rules.md`)

- El ejemplo de "no commitear un `.cs`/`.unity`/`.prefab` sin su `.meta`
  correspondiente" ya vive en `.harness/docs/project_rules.md` como
  plantilla de regla — no se repite aquí, pero aplica igual a cualquier
  `commit_local`/`push` que el leader ejecute.
- Si el equipo usa **checkout exclusivo/locks** sobre escenas y prefabs
  (razón habitual por la que un equipo Unity elige Plastic frente a Git:
  los binarios de Unity son hostiles al merge de texto), el leader
  comprueba el estado de lock antes de proponer un `commit_local`/`push`
  sobre esos archivos — un conflicto de escena/prefab no se resuelve
  igual que un conflicto de texto, y no lo intenta un agente sin
  confirmación humana explícita.

## Verificación (`vcs_verified`) — elegir el proveedor no es tenerlo listo

Mismo patrón que `automation_tool_verified` en
`.harness/docs/mcp_setup.md`: **elegir `vcs_provider` no significa que la
herramienta ya funcione**. `vcs_verified` empieza en `false` y solo pasa a
`true` cuando se confirmó de verdad:

| Proveedor | Qué hace falta para `vcs_verified: true` |
|---|---|
| `github` | `gh auth status` confirma sesión autenticada (o el developer confirma que el remoto y las credenciales ya funcionan) |
| `unity_vcs_plastic` | El cliente `cm` está configurado y conectado al servidor del equipo (el developer lo confirma — no hay forma de verificarlo solo con archivos) |
| `none` | No aplica |

Mientras `vcs_verified` sea `false` y `vcs_provider` no sea `"none"`,
`.harness/init.sh` avisa con `[WARN]` y ningún agente debe ejecutar
ninguna acción de escritura (ni siquiera `commit_local`) sin volver a
confirmar con el humano que ya está lista.
