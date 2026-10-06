# Review — feature F003 `scorm-scenario-demo`

Reviewer, 2026-10-06.

**Veredicto:** `READY_FOR_VISUAL_CHECK` (Ronda 2, 2026-10-06; la Ronda 1 también fue READY_FOR_VISUAL_CHECK, con 2 medios: ver la sección "Ronda 2" al final)

Esto **no es** `APPROVED`. Para el reviewer, a efectos de cierre, equivale a `CHANGES_REQUESTED` mientras falte la firma humana.

- La feature tiene `requires_visual_check: true`.
- `.harness/progress/visual_check_scorm-scenario-demo.md` está **sin firmar**: la línea `Firmado por: ____  Fecha: ____` está en blanco y ninguna casilla de "Resultado" está marcada.

El código cumple la acceptance y el plan. No hay defectos bloqueantes ni altos.

- Conviene corregir los 2 hallazgos medios **antes** del visual check: M1 (textos en inglés en la UI) se vería durante la prueba, y M2 cambia el comportamiento de reanudación.
- El resto puede ir en esta ronda o en una feature posterior.
- Cuando el humano firme, hará falta una última pasada del reviewer para emitir `APPROVED`. Esa pasada solo comprobará la firma, lo anotado en el resultado y las correcciones que se hayan hecho.

Recuento: **0 bloqueantes · 0 altos · 2 medios · 6 bajos · 6 info.**

---

## Verificación realizada (no me he fiado del informe)

- **`.harness/init.sh` re-ejecutado por mí** (Unity cerrado): **exit 0**. Los pasos 1-8 dan OK y el paso 6 ve 1 sola feature `in_progress`.
  - El paso 7 ejecuta `-runTests` sin `-quit`.
  - `.harness/progress/editmode-results.xml` es nuevo (start 12:13:04Z): **96 total, 95 pass, 0 fail, 1 skipped**. El skipped es `ScormManifestBuilderTests`, la XSD 3rd ausente, conocida desde F001.
  - Pasan los 25 tests de `ScenarioTrackerTests` y los 4 de `ScenarioDemoSceneTests`, incluido `PlayMode_SceneStartsTrackerAndRandomPlayCompletesAScenario`.
  - El log no tiene errores de compilación. La única excepción del log (`broken log UI`) es esperada: la provoca a propósito un test de F002 (`ScormScenarioApiTests.cs:607`, con `LogAssert.Expect`).
- **Zip `Builds/SCORM_ScenarioDemo_2004_3rd.zip`** inspeccionado con Python `zipfile`:
  - Tiene 55 entradas, con `imsmanifest.xml` en la raíz.
  - Manifest:
    - `identifier="com.invelon.scormscenariodemo"`, `schemaversion` `2004 3rd Edition`;
    - un único `item`/`resource` `adlcp:scormType="sco"` con `href="index.html"`;
    - 18 `<file>` que coinciden con las entradas reales;
    - LOM `es-ES`;
    - **sin** `imsss:sequencing`, `completionThreshold`, `minNormalizedMeasure` ni `satisfiedByMeasure`.
  - Es correcto: el LMS no recalculará completion/success, y los `cmi.objectives` se crean en runtime sin declararlos en el manifest.
- **El zip no está desfasado**: ningún fichero de `Assets/SCORM/Demo`, `Assets/SCORM/_Scripts` ni `Assets/WebGLTemplates` es más reciente que el zip (14:07). El `TemplateData/ScormSimulator.js` del zip es byte a byte igual al de `Assets/`.
- **`EditorBuildSettings.asset`** (binario, inspeccionado por cadenas): `[Assets/SCORM/Demo/ScenarioDemo.unity, Assets/SCORM/_Scenes/TestApp.unity]`. ScenarioDemo va primera y TestApp se conserva. `com.unity.dt.app-ui` en `m_configObjects` viene de los paquetes del humano, no de F003.
- **Entrada de UI**: `com.unity.inputsystem` no está en `Packages/manifest.json` ni en `packages-lock.json`, así que el `StandaloneInputModule` del builder es válido y no hay riesgo de clics muertos.
- **`.meta`**: todos los ficheros y carpetas de `Assets/SCORM/Demo/**` y los 2 tests nuevos tienen su `.meta`.
- **`ProjectSettings/SceneTemplateSettings.json`** y `LightingData.asset` de TestApp no están en el árbol tras mi ejecución de init.sh.
- **Separación plantilla/demo**: un grep de `Scorm.Scenarios.Demo`, `ScenarioSimulator` y `ScormLogPanel` en `Demo/Runtime/*.cs` no encuentra ninguna referencia. La plantilla no depende de DemoUI.
- **`ScormSimulator.js`**:
  - `scorm.js:561-563` solo lo instala con `?scormsim=1` y sin API de LMS.
  - La persistencia en `localStorage` solo vive dentro de `ScormSimulator.Initialize`/`Terminate` (`:134-156`, `:191-206`), así que con un LMS real no se ejecuta.
  - Con un `pagehide` sin "Salir", `scorm.js:635-650` pone `exit=suspend` y llama a `Terminate`, y el simulador guarda el intento. Coherente.
- **Skill `unity-code-review`** (Fases 1-3; la Fase 4 no se ejecuta):
  - Sin fugas de eventos. `ScormLogPanel` se suscribe en `OnEnable` y se desuscribe en `OnDisable` (`ScormLogPanel.cs:258-266`). El controller quita `Ready`/`Changed` en `OnDestroy` (`ScenarioDemoController.cs:53-59`). El tracker solo se suscribe a `ScormCall` dentro de un scope `using` que se libera también si hay excepción (`ScenarioTracker.cs:530-568`).
  - Sin coroutines.
  - Sin trabajo por frame salvo `LateUpdate` cuando hay cambios (`_dirty`).
  - Sin `Find*` por frame.

## Checkpoints

- C0 (proyecto inicializado): [x]
  - Config completa y `unity_version` = ProjectVersion.
  - `automation_tool: none`.
  - `vcs_verified: false`, pero nada de esta revisión depende de escritura de VCS.
  - `release_guideline: none`.
- C1 (arnés completo, init.sh verde, sin violar `project_rules.md`): [x]. `project_rules.md` no tiene reglas.
- C2 (estado coherente): [x]
  - Una sola feature `in_progress` (F003).
  - F003 no está `done`.
  - `current.md` describe la sesión activa.
- C2 — visual check firmado por humano para cerrar F003: [ ] ← Razón: `.harness/progress/visual_check_scorm-scenario-demo.md` sin firma ni resultados marcados.
- C3 (arquitectura): [x] con observaciones
  - No hay lógica SCORM en la UI: los botones solo llaman al tracker.
  - Sí hay `Debug.Log` intencionados: el log de llamadas y el aviso de 4000 caracteres.
  - Ver B3 (ubicación del código de Editor) y M1.
  - `release_guideline: none`.
- C4 (verificación real): [x]. 29 tests nuevos en verde, ejecutados por mí vía init.sh. Los tests del tracker usan el backend de Editor y no mockean MonoBehaviours.
- C5 (cierre de sesión): [ ] ← Razón:
  - `.harness/progress/history.md` no tiene entrada de F003. Le toca al leader al cerrar la sesión.
  - No hay ficheros sospechosos sin trackear. `ProjectSettings/Packages/` es del humano (com.unity.ai.assistant).
- Visual check humano (A Play Mode, B `?scormsim=1`, C SCORM Cloud, D Moodle): [ ] ← pendiente de firma.

## Hallazgos

### Medios

**M1. Textos en inglés en la UI de demo (acceptance #3: "textos en español").**
- **Dónde:**
  - Los avisos de `RestoreWarning` se pintan tal cual en el resumen. Se generan en inglés en `ScenarioTracker.cs:450`, `:460`, `:465` y `:470`, y se muestran en `SummaryPanel.cs:191-192`. El "aviso amarillo" que el visual check (A.1) da por esperado saldrá en inglés.
  - Los mensajes de `ScenarioTrackerException`/`ArgumentException` se muestran directamente en `ScenarioDemoController.cs:50`, `:173` y `:177`. Por ejemplo: "Scenario 'x': the score must be between…" o "The SCORM session is closed…".
  - Los tokens `RestoredFrom` (`suspend_data` / `objectives` / `empty`) se muestran crudos en `SummaryPanel.cs:187` y `ScenarioDemoController.cs:72`.
- **Fix:** traducir en **DemoUI**, no en Runtime. La plantilla puede seguir en inglés para logs/excepciones.
  - Exponer un código o enum de motivo (p. ej. `RestoreReason { NotJson, ParseError, NoVersion, NewerVersion }`) además del texto, y mapearlo a español en `SummaryPanel`.
  - En `ScenarioDemoController.Run`, mostrar un mensaje en español por tipo de error, con el detalle técnico solo en `Debug.LogWarning`.
  - Mapear `RestoredFrom` a "datos guardados (suspend_data)" / "objetivos del LMS" / "vacío".

**M2. JSON sin "v" se acepta como versión 1 (agujero en el versionado de `suspend_data`).**
- **Dónde:**
  - `ScenarioSaveData.cs:16` inicializa `public int v = CurrentVersion;`. `JsonUtility.FromJson` respeta los inicializadores de campo para las claves ausentes.
  - Por eso un `cmi.suspend_data` que empiece por `{` pero no sea de este tracker (una versión previa de la app sin `v`, otro formato JSON, `{}`) se parsea como `v=1` con `s` vacío. Se trata como estado válido (`RestoredFrom = "suspend_data"`) **sin reconstruir desde `cmi.objectives`**, y se sobrescribe en la primera escritura.
  - La rama `data.v <= 0` de `ScenarioTracker.cs:463-467` queda en la práctica muerta.
  - Ningún test cubre la ausencia de `v`. `Resume_SuspendDataFromNewerVersion_IsNotTrusted` (`ScenarioTrackerTests.cs:544`) solo cubre `v=99`.
- **Impacto:** el mismo escenario que el diseño quiere proteger (datos de otro formato → reconstruir desde los objetivos) acaba perdiendo el progreso visible, aunque el LMS conserve los objetivos.
- **Fix:**
  - Opción 1: inicializar `v = 0` en el DTO y fijar `v = CurrentVersion` al serializar (en `SuspendDataJson` o en `WriteState`).
  - Opción 2: comprobar la presencia de la clave `"v":` en el texto antes de confiar en el parseo.
  - En ambos casos, añadir el test `Resume_SuspendDataJsonWithoutVersion_RebuildsFromObjectives` con `{}` y `{"foo":1}`.

### Bajos

**B1. `Close` marca la sesión cerrada aunque el LMS rechace `Commit`/`Terminate`** (`ScenarioTracker.cs:325-340`).
- **Problema:**
  - `IsClosed = true` se pone sin mirar `LastOperationFailures`.
  - Si `Commit` falla (sesión del LMS caducada, error de red en Moodle), no hay reintento posible: toda operación lanza `ScenarioTrackerException` y la UI desactiva todos los botones (`ScenarioDemoController.cs:192-199`).
  - Tampoco hay test de rechazo de `Commit`/`Terminate`. Solo se prueba el rechazo de `suspend_data` (`ScenarioTrackerTests.cs:597`).
- **Fix:**
  - Marcar `IsClosed` solo si `Terminate` no falló. Si falló el `Commit`, mantener abierto y devolver `false`, para que la app pueda reintentar.
  - Si se decide cerrar igualmente, documentarlo en el XML doc y en el README.
  - Añadir un test con `ScormEditorBackend` que inyecte un error en Commit.

**B2. Ruta de fallo de escritura: el estado local avanza aunque el LMS rechace el objetivo.**
- **Problema:**
  - `CompleteScenario` muta `entry` (`ScenarioTracker.cs:248-258`) antes de escribir.
  - Ignora el retorno de `ScormManager.UpsertObjective` (`:268`, y `:358` en `StartAttempt`).
  - Un objetivo rechazado (`-1`, id rechazado) o con campos rechazados solo se reintenta la próxima vez que se complete **ese mismo** escenario. `Suspend`/`Finish` reescriben `suspend_data` pero no los objetivos ni `cmi.score.*`.
  - Queda reportado en `LastOperationFailures` (la demo lo muestra), pero el LMS puede quedar con una nota global desfasada si falló un `UpdateScore`.
- **Fix:** en `Close()` (y opcionalmente en `Commit()`), reescribir los objetivos completados y los globales (`WriteGlobals()` más un upsert por escenario completado; `WriteIfChanged` de F002 hace que esto no genere tráfico si ya están). Añadir un test con `InjectSetError("cmi.objectives.0.score.raw", ...)`.

**B3. Separación plantilla/demo solo por convención; la plantilla no es extraíble tal cual.**
- **Problema:**
  - No hay `.asmdef`: el compilador no impide que `Runtime/` acabe usando `Scorm.Scenarios.Demo`.
  - El builder de Editor (`Demo/Editor/ScenarioDemoSceneBuilder.cs:5`) depende de DemoUI. Quien borre `DemoUI/` para quedarse con la plantilla rompe la compilación.
  - La plantilla vive bajo una carpeta llamada `Demo/`.
  - `architecture.md` (principio 2) dice "Código de Editor solo en `Assets/SCORM/Editor/`". El builder está en `Assets/SCORM/Demo/Editor/` y la desviación no aparece en impl §4.
- **Fix:**
  - Elegir una de dos opciones:
    - a) añadir `Scorm.Scenarios.asmdef` (Runtime), `Scorm.Scenarios.Demo.asmdef` (DemoUI, que referencia Runtime) y un asmdef de Editor para el builder;
    - b) mover `Runtime/` a `Assets/SCORM/Scenarios/` y dejar `Demo/` solo para la demo.
  - En cualquier caso, actualizar `architecture.md` con la estructura `Demo/` (Runtime/DemoUI/Editor/Data), o mover el builder.
  - Si se añaden asmdefs, comprobar que los tests (Assembly-CSharp-Editor) siguen viendo los tipos (referencias del asmdef de tests, o `Tests/Editor` también con asmdef).

**B4. Concepto de demo dentro del SO de la plantilla.** `ScenarioDefinition._simulatedDecisionCount` (`ScenarioDefinition.cs:35-37`, `:46`, y el parámetro de `Configure` en `:49`/`:59`) solo lo usa `ScenarioSimulator`.
- **Fix:** sacarlo a un SO o componente de DemoUI (p. ej. un `ScenarioDemoSettings` con un mapa id → nº de decisiones). Si se deja, documentarlo en el README como "solo demo".

**B5. Ids de interacción y estado tras reanudar con `p=1` sin `BeginScenario`** (`ScenarioTracker.cs:205-213`).
- **Problema:**
  - Si al reanudar hay un intento a medias (`p=1`) y el gameplay llama directamente a `RecordDecision` sin `BeginScenario` (el XML doc dice que está permitido), se reutiliza el mismo `a`.
  - Si los `decisionId` se repiten (`d1`...), se generan ids de interacción iguales a los de la sesión anterior.
  - Además, `d++` (`:210`) se hace antes de que `ScormManager.RecordInteraction` valide el `decisionId`. Un id con espacios lanza `ArgumentException` desde F002 con `d` ya incrementado y el intento ya empezado y escrito.
- **Fix:**
  - Validar `decisionId` (espacios) al principio de `RecordDecision`.
  - Para el intento: tras `Initialize`, tratar los `p=1` restaurados como intento "huérfano" (el primer `RecordDecision` abre un intento nuevo), o documentar en el README que hay que llamar a `BeginScenario` al reentrar.
  - Relacionado (documentado, aceptable): `ResetLocalState` y `RebuildFromObjectives` reinician o aproximan `a`.

**B6. `RecordDecision` usa `DateTime.Now`, no el reloj inyectado** (`ScenarioTracker.cs:215`). El resto del tracker usa `_utcNow`, y los tests no pueden fijar el `timestamp` de las interacciones.
- **Fix:** `interaction.timeStamp = _utcNow().ToLocalTime()`, o el formato que espere F002.

### Info / nits (no requieren cambio para cerrar)

- **I1.** `SummaryPanel.cs:203`: `url.Contains("scormsim=1")` etiqueta como "Simulador" un LMS real si el parámetro va en la URL. Además coincide con `xscormsim=10`. Mejor usar la misma regex que `scorm.js:78` o preguntar a JS si la API es el simulador.
- **I2.** `ScormLogPanel.cs:335-336`:
  - Fuerza `verticalNormalizedPosition = 0` y `Canvas.ForceUpdateCanvases()` en cada render. Mientras llegan llamadas, el usuario no puede quedarse leyendo líneas antiguas.
  - Se rehacen hasta 250 `Text` y se reserva una `List` por render, solo cuando hay cambios (`_dirty`), así que es aceptable.
  - Sugerencia: solo autoscroll si el usuario ya estaba abajo.
- **I3.** `ScormLogPanel.cs:339-342`: los GetValue 401/403 esperados del arranque cuentan como errores, y el contador no arranca en 0. El visual check ya lo advierte. Opcional: contador separado para "errores de escritura" (Set/Commit/Terminate).
- **I4.** `ScenarioSaveEntry` guarda floats con toda la precisión de JsonUtility (`66.5999984741211`). Está documentado. Opcional: guardar centésimas enteras para reducir unos 30 caracteres por escenario de cara al SPM de 4000 de la 3rd.
- **I5.** `ScenarioTrackerHost.cs:430-435`: si la inicialización falló (catálogo inválido), `Start` la reintenta y el mismo error sale dos veces en consola. Inofensivo.
- **I6.**
  - `ScormSimulator.js:34-37`: la cabecera queda con la frase cortada ("...to ensure that the / system will sucessfully initialize").
  - El simulador no acumula `cmi.total_time` al reanudar. Irrelevante para la demo.
  - `ScenarioDemoSceneBuilder` está en el namespace global (`conventions.md` pide namespaces por carpeta), igual que los tests previos.
  - La serialización binaria de escena y assets está documentada (D1) y sigue siendo recomendable pasar a Force Text como feature aparte.

## Lo que está bien (comprobado)

- **Mapping SCORM correcto:**
  - Ids de objetivo estables tomados del SO.
  - Upsert por id (sin duplicados tras reanudar: test `SuspendAndRelaunch_RestoresStateWithoutDuplicatingObjectives`).
  - Política Best/Last aplicada a lo que se reporta.
  - Objetivo con raw/min/max en su rango, `scaled` redondeado a 4 decimales, success según el umbral del escenario, completion y progress = 1.
  - Global con `raw` 0-100 a 2 decimales, `min=0`, `max=100` y `scaled = raw/100`, coherentes entre sí y con lo que Moodle lleva al libro (`cmi.score.raw`).
  - `success_status` es `unknown` hasta completar todos los escenarios y después `passed`/`failed`, con `requireAllScenariosPassed`. `completion_status` es `incomplete`/`completed` y `progress_measure` = completados/total.
- **`Initialize` no escribe.** Sin escrituras vacías ni redundantes, gracias a `WriteIfChanged` del tracker y de F002.
- **`suspend_data`**: límite de 64000 comprobado antes de escribir, aviso por encima de 4000 y reintento si el LMS lo rechaza (test).
- **`location`** = último escenario.
- **Cierre de sesión**: `session_time` también al suspender, `exit` suspend/normal, `Commit` y un **único** `Terminate` (`RequireOpen` impide un segundo `Close`). Test de orden de llamadas.
- **Demo:**
  - Log con límite (2000 guardadas, 250 visibles, pool de líneas) y handler que solo encola, sin llamar a la API.
  - Escape de `<` para rich text.
  - "Relanzar" solo en Editor.
  - Seed reproducible.
  - El resto de textos de la UI está en español.
- **Builder reproducible:**
  - No sobrescribe los datos existentes.
  - No modifica el prefab: usa un override de instancia.
  - Verifica las referencias y las fuentes tras guardar.
  - Fija Build Settings = [ScenarioDemo, TestApp] y sale con 0/1 en batchmode.
- **README:** guía de plantilla clara, con límites (4000/250), configuración de Moodle y aviso de no usar `-scormCompletionThreshold`.

## Cambios requeridos

Antes del visual check:
1. **M1** — Textos de la demo en español: `RestoreWarning`, mensajes de excepción y `RestoredFrom`, mapeados en DemoUI.
2. **M2** — Versionado de `suspend_data`: que un JSON sin `v` no se acepte como v1, con su test.

Recomendados (esta ronda o en una feature posterior, a decidir por el leader):
3. **B1** — No cerrar el tracker si el LMS rechaza `Commit`/`Terminate` (o documentarlo), con su test.
4. **B2** — Reescribir los objetivos y los globales en `Close()` para recuperar las escrituras rechazadas, con su test.
5. **B3** — asmdefs (o mover `Runtime/`) para que la plantilla sea extraíble sin la demo, y actualizar `architecture.md` con la ubicación del código de Editor en `Demo/Editor/`.
6. **B4–B6** — Ver el detalle arriba.

Para cerrar con `APPROVED`:
7. El humano completa y firma `.harness/progress/visual_check_scorm-scenario-demo.md` (A–D) y anota los warnings de SCORM Cloud/Moodle.
8. El leader añade la entrada de F003 a `.harness/progress/history.md`.
9. Una última pasada del reviewer.

---

# Ronda 2 — re-revisión del delta

Reviewer, 2026-10-06. Delta descrito en la sección "Ronda 2" de `.harness/progress/impl_scorm-scenario-demo.md`.

**Veredicto:** `READY_FOR_VISUAL_CHECK`

Esto **no es** `APPROVED`. El código ya no tiene hallazgos medios ni altos abiertos. Sigue bloqueado por la firma humana de `.harness/progress/visual_check_scorm-scenario-demo.md` (sin firmar) y por la entrada de F003 en `history.md` (C5, le toca al leader).

Abiertos: **0 bloqueantes · 0 altos · 0 medios · 1 bajo · 6 info.**

## Verificación

- **`.harness/init.sh` re-ejecutado por mí: exit 0.** `.harness/progress/editmode-results.xml` es nuevo (start 12:31:46Z): **104 total, 103 pass, 0 fail, 1 skipped**. El skipped es `ScormManifestBuilderTests` (XSD 3rd), igual que antes.
- **Zip** `Builds/SCORM_ScenarioDemo_2004_3rd.zip`:
  - Ningún fichero de `Assets/SCORM/Demo`, `_Scripts` ni `WebGLTemplates` es más reciente que el zip.
  - El `ScormSimulator.js` del zip es byte a byte igual al de `Assets/`.
- **`.meta`**: presentes para los 4 ficheros nuevos (`ScenarioTrackerError`, `ScenarioRestoreSource`, `ScenarioRestoreIssue`, `DemoTexts`).
- **Árbol limpio**: el `git status` no cambia respecto a la ronda 1. No reaparecen `SceneTemplateSettings.json` ni `LightingData.asset`.

## Estado de los hallazgos de la ronda 1

| Id | Estado | Comprobación |
|---|---|---|
| M1 | **Cerrado** | `DemoUI/DemoTexts.cs` traduce `ScenarioRestoreSource`, `ScenarioRestoreIssue`, `ScenarioTrackerError` y las `ArgumentException`. `SummaryPanel.cs:40,44` y `ScenarioDemoController.cs:50,72,177-183` ya no muestran `e.Message`, `RestoreWarning` ni `RestoredFrom`; el detalle técnico va solo a `Debug.LogWarning`. La plantilla sigue en inglés y expone códigos, que es la separación correcta |
| M2 | **Cerrado** | `ScenarioSaveData.cs:19`: `public int v;` vale 0 por defecto. El getter `SuspendDataJson` (`ScenarioTracker.cs:138-145`) fija `v = CurrentVersion` al guardar. La rama `v <= 0` → `NoVersion` (`:535-537`) ya es alcanzable. Test con `{}`, `{"foo":1}` y `{"cur":…,"s":[]}` |
| B1 | **Cerrado** | `Close` (`ScenarioTracker.cs:357-383`): si `Commit` falla no se llama a `Terminate`, y `IsClosed`/`SessionClosed` solo se activan si `Terminate` tiene éxito. Devuelve `false` y se puede reintentar. Tiene test |
| B2 | **Cerrado** | `Close` reescribe primero los objetivos completados (`CompletedObjective`) y `WriteGlobals()`, solo si hay algún escenario completado, antes de `suspend_data`/`session_time`/`exit`. `WriteIfChanged` de F002 evita tráfico redundante. Test con 406 inyectado en `objectives.N.score.raw` y en `cmi.score.raw` |
| B3 | **Abierto (bajo, aplazado)** | Sin asmdefs y `architecture.md` sin actualizar. El implementer lo propone como feature aparte. Acepto el aplazamiento, pero **el leader debe** (a) registrarlo en `feature_list.json` y (b) añadir a `.harness/docs/architecture.md` la estructura `Assets/SCORM/Demo/{Runtime,DemoUI,Editor,Data}` y la excepción del código de Editor en `Demo/Editor/` |
| B4 | **Cerrado (documentado)** | Tooltip "DEMO ONLY… Ignored by the tracker" en el SO, más el README |
| B5 | **Cerrado** | `decisionId` se valida (vacío o con espacios) antes de abrir el scope de escritura o incrementar `d`. `_startedThisSession` hace que un intento restaurado con `p=1` abra un intento nuevo en el primer `RecordDecision`, sin reutilizar ids. Tiene test |
| B6 | **Cerrado** | `interaction.timeStamp = DateTime.SpecifyKind(_utcNow(), Utc).ToLocalTime()`. Tiene test |
| I1 | **Cerrado** | `SummaryPanel.cs:56-62` usa la misma regex que `scorm.js` |
| I6 (cabecera JS) | **Cerrado** | Frase corregida |
| I2–I5 | Abiertos (info) | Opcionales: autoscroll del log, contador de errores de escritura, floats en `suspend_data` y doble log de error en `Host.Start` |

## Observaciones nuevas (info, no bloquean)

- **I7.** El getter `SuspendDataJson` (`ScenarioTracker.cs:138-145`) tiene un efecto lateral: muta `_save.v`. Es inocuo, porque solo hay una versión y el getter es idempotente, pero conviene moverlo a un método `Serialize()` o fijar `v` al construir el estado nuevo o cargado.
- **I8.** Con B1, si un `Finish()` falla en `Commit`, `cmi.exit = "normal"` ya está puesto en el data model. Si el alumno sigue y cierra la pestaña sin reintentar, `scorm.js` (pagehide) respeta el exit que fijó Unity y cierra el intento como `normal`, en vez de `suspend`.
  - Es un caso límite, con un LMS que ya está rechazando el Commit.
  - Opcional: en un `Close` fallido, volver a poner `cmi.exit = "suspend"`, o documentarlo.

## Cambios requeridos

Ninguno de código para pasar al visual check. Para cerrar con `APPROVED`:
1. El humano completa y firma `.harness/progress/visual_check_scorm-scenario-demo.md` (A–D).
   - En A.1, el aviso amarillo ahora sale en español ("cmi.suspend_data no contiene datos de esta app…").
2. El leader añade la entrada de F003 a `.harness/progress/history.md` y gestiona B3: registrarlo como feature y actualizar `architecture.md`.
3. Una última pasada del reviewer (firma + resultados anotados).
