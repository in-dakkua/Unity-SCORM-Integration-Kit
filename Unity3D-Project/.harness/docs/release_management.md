# Release management — versiones, nombres, ramas y Asana

> Traducción al harness de la guideline corporativa **"[GUIDELINE] Tech
> Release Management Architecture" v3.0.0 (Invelon Technologies, BORRADOR,
> 20/03/2026)**. Si la guideline cambia de versión, este es el único
> documento del harness que hay que actualizar — el resto solo apunta aquí.
>
> Aplica cuando `.harness/unity.config.json` → `release_guideline` es
> `"invelon_rm_v3"` (se decide en `/unity-claude-code-harness`, Paso 5C).
> Con `"none"`, nada de este documento es obligatorio.

## Alcance: qué entra en el harness y qué no

El harness **no es el pipeline de CI/CD** ni lo sustituye. Solo recoge las
partes de la guideline que afectan al trabajo diario de un agente que
escribe código y ejecuta control de versiones:

| Entra (reglas para el agente) | Fuera (lo gestiona CI/CD, DevOps o el PO) |
|---|---|
| Formato de versión y quién la decide | Triggers de CI (build nocturna, auto-trigger en `/release`), TeamCity |
| Nombres de ramas y qué puede hacer el agente en cada una | Entornos (DEV/QA/PRE-PROD/PROD), endpoints y URLs de Aurora Cloud |
| Nomenclatura de builds manuales y artefactos | Subida de artefactos a S3 y a canales de distribución |
| Tarea de release en Asana: nombre, campos, multi-homing | QA Protocol (§10 de la guideline), matriz de aprobación |
| Regla innegociable de no distribución | Deploy, rollback, runbooks por plataforma |

Si en el futuro el harness se conecta al pipeline real, esta tabla es el
punto de partida para decidir qué más incorporar.

## 1. Versionado

### Formato

| Tipo de build | Formato | Ejemplo |
|---|---|---|
| CI/CD | `MAJOR.MINOR.PATCH+BUILD_NUMBER` | `1.4.2+183` |
| Manual / local | `MAJOR.MINOR.PATCH+YYMMDD-HHMM` | `1.4.2+260320-1430` |

- **MAJOR** — cambios incompatibles, rediseños mayores, nueva plataforma.
- **MINOR** — nuevas funcionalidades compatibles con la versión actual.
- **PATCH** — corrección de bugs, mejoras menores.
- **BUILD_NUMBER** — auto-incremental de CI/CD, global al proyecto, nunca
  se resetea.

### Reglas para el agente

- **MUST NOT decidir ni proponer por su cuenta `MAJOR.MINOR.PATCH`.** Lo
  decide el encargado del proyecto como consecuencia del scope de la
  release (qué tareas entran). Si hace falta un número y no está en
  `release_current_version`, el agente pregunta; no lo deduce del volumen
  de cambios.
- **`release_current_version` lo actualiza el leader** con el número que
  le da la persona al planificar cada release (o lo pone a `null` al
  cerrarla). Si nadie lo actualiza, `init.sh` avisará de una discrepancia
  en cada sesión.
- **MUST NOT tocar `BUILD_NUMBER` ni el `versionCode` de Android**
  (`PlayerSettings.Android.bundleVersionCode` /
  `AndroidBundleVersionCode` en `ProjectSettings/ProjectSettings.asset`).
  Son de CI/CD.
- **La versión vive en `PlayerSettings.bundleVersion`** (`bundleVersion:` en
  `ProjectSettings/ProjectSettings.asset`). Paso 6 de la guideline
  ("verificar que la versión en ProjectSettings coincide con vX.Y.Z
  acordado"): el agente **sí puede comprobarlo** y avisar de la
  discrepancia — `.harness/init.sh` ya lo hace si
  `release_current_version` está relleno.
- **El "commit de versión"** (paso 7 de la guideline) lo propone el
  leader, nunca un subagente. Lo preferible es que la persona cambie la
  versión en *Project Settings > Player* desde el Editor. Si se edita a
  mano, solo la línea `bundleVersion:` y con el Editor cerrado (si no,
  Unity puede sobrescribirla al guardar). Después, el commit sigue
  `.harness/docs/vcs_policy.md` como cualquier otro.
- Ninguna feature normal cambia la versión. Si el diff de una feature toca
  `bundleVersion` o `bundleVersionCode` sin que la feature lo pida, el
  reviewer lo rechaza (ver `.harness/CHECKPOINTS.md`, C3).

### Versión visible en la app (§3.5 de la guideline)

Si una feature toca la pantalla de login, el lobby/menú principal o el
menú de opciones, **no puede romper la versión visible**:

- Login y menú de opciones: SemVer completo (`v1.4.2+183`).
- Lobby / menú principal: SemVer corto (`v1.4.2`).
- En VR/XR, la versión debe poder consultarse en todo momento (p. ej.
  desde opciones), sin volver al lobby.

La versión se lee en runtime (`Application.version`, más el build number
que inyecte CI si el proyecto lo hace), **nunca como string hardcodeado**.
Dónde va exactamente en cada producto lo decide diseño — el agente no lo
reubica por su cuenta.

## 2. Nombres de builds y artefactos

```
{AppName}_{PLATFORM}_v{MAJOR.MINOR.PATCH}+{BUILD}_{ENV}.{ext}
Ejemplo: DiputacioLleidaVR_META_v1.1.0+183_QA.apk
```

- `{AppName}` sale de `release_app_name`, y `{PLATFORM}` de
  `release_platforms` (p. ej. `META`, `PICO`, `ANDROID`, `IOS`, `WINDOWS`).
- `{BUILD}` es el `BUILD_NUMBER` de CI, o `YYMMDD-HHMM` en una build
  manual.
- `{ENV}` es `DEV`, `QA`, `PREPROD` o `PROD`.

En la práctica el agente solo necesita esto si lanza una build manual
(p. ej. con Unity CLI en batchmode): el archivo **MUST** seguir este
formato con el sufijo de timestamp. Esa build es solo para debugging y
pruebas internas. No se distribuye (ver §5).

## 3. Modelo de ramas

La guideline está basada en Plastic SCM. Los nombres reales de cada
proyecto viven en `unity.config.json` (`branch_*`) y se confirman en
`/unity-claude-code-harness` — **no los asumas, léelos de la config**.

| Rol de la rama | Plastic (guideline) | Git/GitHub (equivalente del harness) | Qué es |
|---|---|---|---|
| `branch_personal_pattern` | `/dev/<usuario_AD>` | `user/<usuario_AD>` | Rama personal de desarrollo. Donde trabaja el agente por defecto. |
| `branch_feature_pattern` | `/dev/<feature>` | `feature/<feature>` | Solo cuando una feature es tan grande que debe ir en paralelo. La crea el agente solo si la persona lo decide. |
| `branch_dev` | `/dev` | `dev` | Integración. Los developers mergean aquí. |
| `branch_release` | `/release` | `release` | Territorio QA. El merge `/dev → /release` arranca el ciclo de QA. |
| `branch_main` | `/main` | `main` | Territorio PROD. El merge `/release → /main` **es** la aprobación formal. Tags `v1.4.2+183`. |
| `branch_hotfix_pattern` | `/hotfix/X.Y.Z-desc` | `hotfix/X.Y.Z-desc` | Emergencias en producción, desde `/main`. |

**¿Por qué en Git no es `dev/<usuario>`?** Git no permite que existan a la
vez una rama `dev` y otra `dev/arafegas`: las ramas son rutas, y
`refs/heads/dev` no puede ser a la vez archivo y carpeta (`cannot lock
ref`). Plastic sí lo permite. Por eso en Git el harness usa `user/` y
`feature/`. Si el equipo prefiere otro prefijo, se cambia en la config.

`<usuario_AD>` es el nombre de Active Directory de la persona (p. ej.
`arafegas`). No se guarda en `unity.config.json`, que es compartido: el
leader lo pregunta la primera vez que tenga que crear la rama personal.

### Qué puede hacer el agente en cada rama

Con `release_guideline: "invelon_rm_v3"`, estos son los defaults que el
leader aplica **encima** de la tabla de `.harness/docs/vcs_policy.md`:

| Acción | Nivel por defecto | Por qué |
|---|---|---|
| Commit/checkin en la rama personal o de feature | según `commit_local` (`allowed` en la tabla base) | Es el espacio de trabajo del developer. |
| Crear la rama personal o de feature | según `create_branch` (`allowed`) | Siempre con el patrón de nombre configurado. |
| Crear una rama de hotfix | `requires_approval` | El `X.Y.Z` lo da la persona; el agente no lo inventa. |
| Merge a `branch_dev` | según `merge_or_checkin_to_protected_branch` (`requires_approval`) | Integración compartida. |
| Merge a `branch_release` o `branch_main` | **`forbidden`** (`merge_into_release_or_main`) | `/dev → /release` arranca QA y exige que exista la tarea de release; `/release → /main` es la aprobación formal. Los hacen Tech Lead/DevOps. |
| Crear tags de versión | **`forbidden`** (`create_tag`) | Los tags de `/main` son de DevOps (paso 9.7). |

Si el equipo quiere relajar alguno, se hace **explícitamente** en
`vcs_permissions` (p. ej. `"merge_into_release_or_main":
"requires_approval"`), con el motivo anotado en `vcs_provider_notes`.

### Hotfix (§13), lo que toca al agente

1. La persona da el número (`PATCH + 1`, p. ej. `1.4.2 → 1.4.3`) y la
   descripción. El agente crea `hotfix/1.4.3-<desc>` desde `branch_main`
   (con aprobación).
2. Fix mínimo, con el mismo flujo de plan → implementer → reviewer que
   cualquier feature.
3. El merge a `/main`, el tag, la build PROD y los merges de vuelta a
   `/release` y `/dev` los hace DevOps. El agente puede recordárselos,
   pero no ejecutarlos.

## 4. Tarea de release en Asana (§8)

Esto **no es el backlog de features** de `.harness/docs/feature_source.md`:
una release agrupa features ya terminadas. Funciona aunque el backlog del
harness esté en modo `local`, siempre que el conector de Asana esté
disponible y `release_asana_releases_project_gid` esté configurado.

### Cuándo existe

- Se abre en la planificación de sprint (PO + DevOps) o al cierre de
  proyecto. **El número de versión debe estar decidido antes.**
- **Debe existir antes del primer build de QA.** Las builds internas de
  DEV no la necesitan.

### Campos obligatorios

| Campo | Valor |
|---|---|
| Nombre | `[<release_project_code>] Release vX.Y.Z — <descripción corta>` |
| Proyectos | **Releases** (global, `release_asana_releases_project_gid`) **+** el proyecto específico (`release_asana_project_gid`) — multi-homing obligatorio |
| Versión | `X.Y.Z` |
| Tipo | New content / Enhancement / Bugfix |
| Plataforma(s) | Pico / Quest / Android / iOS / Windows / Cloud |
| Responsable | Responsable de la release |
| Fecha objetivo | Fin del sprint |
| Descripción | Lista de features o enlace al sprint board |
| Distribución | Aurora Cloud / MDM / Private Channel / Store Privada / Store Pública / Store Business |

**Los nombres exactos de los campos custom y de sus opciones se resuelven
siempre vía MCP** (p. ej. leyendo los custom fields del proyecto Releases),
nunca escritos de memoria. La propia guideline escribe "Enchancement", y
el campo real de Asana puede usar esa u otra grafía. Si no se puede
resolver un campo, se pregunta a la persona. Es la misma regla que ya
aplica a los `gid` de las menciones en `.harness/docs/asana_context_protocol.md`.

### Qué puede hacer el agente

- **Redactar las release notes** a partir de las features `done` (de
  `.harness/feature_list.json` o de las tareas completadas en Asana),
  en `.harness/progress/release_vX.Y.Z.md`. Qué entra lo decide el PO;
  el agente lista y la persona recorta.
- **Redactar la tarea de release** con todos los campos de arriba en ese
  mismo archivo.
- **Crearla o actualizarla en Asana solo el leader, y solo tras
  aprobación**, con el mismo espíritu que los comentarios: enseña en el
  chat el nombre, los dos proyectos y el valor exacto de cada campo,
  pregunta por ajustes y espera un sí explícito. Solo entonces llama a la
  herramienta de Asana. Cualquier comentario en esa tarea sigue
  `.harness/docs/asana_context_protocol.md` tal cual.
- Reportar el resultado real (URL/`gid` de la tarea creada), no un
  "listo" genérico.

## 5. Regla innegociable (§8.5 y §11)

**MUST NOT** entregar, subir o compartir ningún instalable, código o
modelo 3D con nadie fuera del equipo de producción, ni sacar una build del
entorno controlado de desarrollo sin una tarea de release activa en Asana.
No hay excepción que se pueda dar en el chat: solo el CTO puede aprobarla,
fuera del harness.

Además, cualquier build que llegue a un cliente externo es siempre de
PROD, nunca de QA ni PRE-PROD. El agente nunca distribuye builds: eso es
de DevOps.

## 6. QA de la release ≠ visual check del harness

El visual check de `.harness/docs/verification.md` (Nivel 3) lo firma el
developer en el Editor para cerrar **una feature**. El QA Protocol de la
guideline es otra cosa: un tester que **no** implementó la tarea, sobre un
**binario compilado** y nunca desde el Editor, para aprobar **una
release**. Marcar una feature `done` en el harness no significa que haya
pasado QA.
