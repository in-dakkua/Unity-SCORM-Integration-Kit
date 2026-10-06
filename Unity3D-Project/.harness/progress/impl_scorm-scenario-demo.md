# Informe de implementación — F003 `scorm-scenario-demo`

Implementer, 2026-10-06. Plan: `.harness/progress/plan_scorm-scenario-demo.md`. El humano eximió el gate del plan.
Rutas relativas a `Unity3D-Project/`.
- No he hecho escrituras de git.
- No he marcado `done` ni firmado el visual check.
- No he tocado `Packages/manifest.json`.
- F003 está en `in_progress`.

## 0. Acceptance y evidencia

| # | Criterio | Estado | Evidencia |
|---|---|---|---|
| 1 | Plantilla separada de la UI. Incluye: escenarios en ScriptableObject; tracker sobre la API de F002 (empezar, decisiones como interacciones, completar con nota, mejor/última, global ponderada en `cmi.score.*`, success/completion, progress, suspend_data JSON versionado, commit, exit suspend/normal) | Cumplido | `Assets/SCORM/Demo/Runtime/` (namespace `Scorm.Scenarios`) no referencia nada de `DemoUI/`. 25 tests en `ScenarioTrackerTests` |
| 2 | Escena generada de forma reproducible (menú + batchmode), primera en Build Settings, TestApp se mantiene | Cumplido | `ScenarioDemoSceneBuilder.BuildFromCommandLine` → `SCENARIO_DEMO_SCENE_OK`. El builder reabre la escena guardada y falla si falta alguna referencia. Tests `BuildSettings_ScenarioDemoFirstAndTestAppKept` y `Scene_HostUnderScormManagerAndAllReferencesWired` |
| 3 | UI uGUI en español: lista de escenarios, jugar aleatorio + nota manual, resumen, Commit/Salir/Finalizar/Reiniciar | Cumplido (visual pendiente) | `DemoUI/`. Smoke de Play Mode `PlayMode_SceneStartsTrackerAndRandomPlayCompletesAScenario`: arranca la escena, comprueba 5 filas, pulsa Jugar → Salir → Relanzar y verifica la reanudación |
| 4 | Log visual de cada llamada (error en rojo), filtrable y limpiable, también a la consola del navegador | Cumplido (visual pendiente) | `ScormLogPanel`. El smoke comprueba que recibe las llamadas de Initialize |
| 5 | Funciona en Play Mode, en `?scormsim=1` y en SCORM Cloud/Moodle, y reanuda al relanzar | Play Mode: cumplido. Navegador, Cloud y Moodle: los firma el humano | Reanudación probada con el backend de Editor (tests) y con el simulador JS (test node, §3). Cloud y Moodle en el visual check |
| 6 | Build WebGL + zip 3rd por CLI, tests EditMode del tracker y README | Cumplido | §3 y `Assets/SCORM/Demo/README.md` |

## 1. Archivos

Todo el código va en `scripts_root[2]` = `Assets/SCORM/Demo`. Unity ha generado los `.meta` de todos los ficheros y carpetas.

**Plantilla — `Runtime/`** (namespace `Scorm.Scenarios`; solo depende de `ScormManager`/`StudentRecord`/`ScormCallInfo` del kit):

- `ScenarioDefinition.cs` (SO):
  - Campos: id estable, título, descripción (corte a 250), peso, min/max, umbral scaled y nº de decisiones del simulador.
  - `Configure()` para las herramientas y los tests.
- `ScenarioCatalog.cs` (SO):
  - Contiene la lista, `ScenarioScorePolicy` (Best/Last), `GlobalScoreMethod`, el umbral global y `requireAllScenariosPassed`.
  - `Validate()` detecta ids vacíos, con espacios o duplicados, rangos min/max inválidos y pesos negativos.
- `ScenarioTracker.cs`: servicio C# puro. Detalle en §2.
- `ScenarioTrackerHost.cs`:
  - MonoBehaviour que va en el GameObject del ScormManager.
  - `Scorm_Initialize_Complete` lleva `[Preserve]`.
  - Lanza el evento `Ready`; si ya está inicializado, recupera el estado en `Start`.
- `ScenarioScoring.cs`: aritmética pura (scaled, media ponderada, estados globales, progreso).
- `ScenarioProgress.cs`: vista de solo lectura por escenario.
- `ScenarioSaveData.cs` y `ScenarioSaveEntry.cs`: DTO del JSON.
- `ScenarioTrackerException.cs`, `ScenarioScorePolicy.cs`, `GlobalScoreMethod.cs`, `ScenarioStatus.cs`.

**Demo — `DemoUI/`** (namespace `Scorm.Scenarios.Demo`):

- `ScenarioDemoController.cs`: orquesta los paneles. Gestiona Commit, Salir, Finalizar, Reiniciar, Relanzar (solo en Editor) y la semilla.
- `ScenarioListPanel.cs` y `ScenarioRowView.cs`: una fila por escenario, clonada desde una plantilla inactiva.
- `SummaryPanel.cs`: alumno, nota global, estados, progreso, entrada, origen del estado y conexión (Editor / simulador / LMS real / sin LMS).
- `ScormLogPanel.cs`:
  - Suscripción en `OnEnable`. El handler solo encola y hace `Debug.Log`/`LogWarning`.
  - El render se hace en `LateUpdate` con pool: 250 líneas visibles y 2000 llamadas guardadas.
  - Filtros, botón Limpiar y contadores.
- `ScenarioSimulator.cs`, `SimulatedAttempt.cs` y `SimulatedDecision.cs`:
  - Generan decisiones aleatorias de tipo `true-false`/`choice` (`opt-a..d`) en formato SCORM válido, y la nota derivada.

**Datos — `Data/`** (los crea el builder solo si faltan):

| Asset | Id | Peso | Rango | Umbral |
|---|---|---|---|---|
| `Scenario01_EPI` | `scenario-01` | 1 | 0-100 | 0.70 |
| `Scenario02_LOTO` | `scenario-02` | 2 | 0-10 | 0.80 |
| `Scenario03_Carretillas` | `scenario-03` | 1.5 | 0-100 | 0.70 |
| `Scenario04_Incendio` | `scenario-04` | 2 | 0-50 | 0.60 |
| `Scenario05_EspaciosConfinados` | `scenario-05` | 2.5 | 0-100 | 0.75 |

`ScenarioCatalog.asset`: política Best, media ponderada sobre todos los escenarios, umbral 0.7.

**Editor — `Editor/ScenarioDemoSceneBuilder.cs`:**

- Entradas: menú `SCORM/Demo/Build Scenario Demo Scene` y `BuildFromCommandLine` (sale con 0/1).
- Genera la escena:
  - `NewSceneSetup.EmptyScene`, sin luces.
  - Cámara y `EventSystem` + `StandaloneInputModule`.
  - Instancia de `Resources/ScormManager.prefab` con `ScenarioTrackerHost` añadido como override de instancia; **el prefab no se modifica**.
  - Canvas (Overlay, `CanvasScaler` 1920x1080, match 0.5) **colgando del ScormManager** para que le llegue el `BroadcastMessage`.
  - Fuente `LegacyRuntime.ttf` y sprites de `DefaultControls`; sin TMP.
- Al terminar: verifica las referencias y la fuente tras guardar, y fija Build Settings = [ScenarioDemo, TestApp].

**Escena:** `Assets/SCORM/Demo/ScenarioDemo.unity`.

**README:** `Assets/SCORM/Demo/README.md`. Cubre el uso como plantilla, el comportamiento del tracker, qué ve Moodle, la configuración recomendada de la actividad y cómo construir el zip.

**Tests — `tests_root[0]` = `Assets/SCORM/Tests/Editor/`:**
- `ScenarioTrackerTests.cs`: 25 tests.
- `ScenarioDemoSceneTests.cs`: 4 tests, incluido el smoke de Play Mode con `EnterPlayMode`/`ExitPlayMode`. Su `[UnityTearDown]` deja una escena vacía para que los tests siguientes no hagan `BroadcastMessage` a un ScormManager en Edit Mode.

**Plantilla WebGL — `Assets/WebGLTemplates/SCORM/TemplateData/ScormSimulator.js`:**
- Persistencia opcional en `localStorage`, con clave `ScormSimulator:<pathname>`:
  - `Terminate` con `cmi.exit=suspend` guarda los datos; con otro exit los borra.
  - `Initialize` restaura los datos con `cmi.entry=resume` y quita `exit`/`session_time`.
  - `?scormsimreset=1` olvida lo guardado.
- Solo afecta al simulador, que solo se instala cuando no hay API de LMS.

**`.harness/`:**
- `feature_list.json`: F003 → `in_progress`.
- `progress/current.md`, `plan_scorm-scenario-demo.md`, `visual_check_scorm-scenario-demo.md` (sin firmar) y este informe.

**ProjectSettings:**
- `EditorBuildSettings.asset`: ScenarioDemo primera y TestApp detrás. Es el único cambio que hago a propósito, y es imprescindible.
- `ProjectSettings.asset`:
  - La build aplica `ScormBuildCli.ConfigureWebGL`: plantilla `PROJECT:SCORM` y Gzip + decompression fallback, los mismos valores que en F001.
  - Al terminar, el fichero tiene el mismo tamaño (81196 bytes) que al empezar la sesión.
  - `productName` sigue siendo "SCORM Testbed"; el título del paquete va por `-scormTitle`.
- `ProjectSettings/SceneTemplateSettings.json`: Unity lo crea al llamar a `EditorSceneManager.NewScene`. Lo he borrado; volverá a aparecer cuando alguien ejecute el builder o los tests de escena (decisión del leader: commitearlo o ignorarlo).

## 2. Arquitectura: plantilla frente a demo

**`ScenarioTracker`** (plantilla) usa solo la API pública de F002:
- `UpsertObjective`, `RecordInteraction(…, objectiveId)`, `UpdateScore`, `UpdateStatus`, `UpdateProgressMeasure`;
- `SetLocation`, `SetSuspendData`, `SetSessionTime`, `SetExit`, `Commit`, `Terminate`;
- el evento `ScormCall`, que usa para capturar los rechazos de cada operación pública. Esos rechazos se exponen en `LastOperationFailures`/`LastOperationSucceeded`.

**`Initialize()` no escribe nada.** Carga el estado así:
- Si `cmi.suspend_data` es un JSON válido (`v` ≤ versión actual), lo usa.
- Si no (por ejemplo, el texto semilla del backend o una versión futura), reconstruye el estado desde los `cmi.objectives` cuyos ids están en el catálogo: un objetivo `completed` cuenta como 1 intento con su `score.raw`; uno `incomplete` cuenta como intento en curso.
- `RestoredFrom` y `RestoreWarning` informan del origen y del motivo.

**`BeginScenario`:**
- Hace un upsert del objetivo con `completion_status=incomplete` (si nunca se completó) y la description.
- Pone `cmi.completion_status=incomplete` mientras haya escenarios pendientes.
- Escribe `cmi.location` y `cmi.suspend_data`.

**`RecordDecision`:**
- La interacción lleva el id `<escenario>-a<intento iniciado>-<decisionId>`, `objectives.0.id=<escenario>` y weighting 1.
- El número de intento es el contador de inicios `a` del estado, no de intentos completados. Así, un intento retomado tras reanudar no reutiliza ids (test `RecordDecision_AttemptRestartedAfterResume_DoesNotReuseInteractionIds`). Tras `ResetLocalState` la numeración se reinicia (README).
- Si el intento no se había empezado, lo empieza.

**`CompleteScenario(raw)`:**
- Valida el rango y actualiza intentos, mejor nota, última nota y timestamps.
- Escribe en el objetivo la nota que marca la política: raw/min/max, scaled redondeado a 4 decimales, passed/failed según el umbral del escenario, completed, progress 1 y description.
- Recalcula los valores globales:
  - `cmi.score.raw` en 0..100 con 2 decimales, `min=0`, `max=100`, `scaled=raw/100`.
  - `success_status`: passed/failed solo con todos los escenarios completados; unknown antes.
  - `completion_status` y `progress_measure` = completados/total.
- Guarda el estado y hace Commit (`AutoCommit`).

**`Suspend()` / `Finish()`:** estado, `session_time` (también al suspender), `exit` suspend/normal, Commit y Terminate. Después, el tracker queda cerrado (`IsClosed`) y cualquier operación lanza `ScenarioTrackerException`.

**Sin escrituras vacías ni redundantes:**
- Los valores SCORM pasan por `WriteIfChanged` de F002.
- `location` y `suspend_data` se comparan con el último valor aceptado por el LMS. Si el LMS rechaza uno, se reintenta en la siguiente operación.

**JSON (JsonUtility)**, 90-120 caracteres por escenario (un float como 66.6 se serializa como `66.5999984741211`):

```json
{"v":1,"cur":"scenario-02","s":[{"id":"scenario-02","n":1,"b":7.5,"l":7.5,"p":0,"t0":…,"t1":…,"d":0}]}
```

- Las entradas con ids que no están en el catálogo se conservan.
- Si se superan 64000 caracteres (SPM de 4th), se lanza la excepción antes de escribir.
- Por encima de 4000 (SPM de 2nd/3rd Edition, lo único que un LMS 3rd está obligado a guardar), un `Debug.LogWarning`. Son unos 30-40 escenarios; está documentado en el README.
- El reloj se inyecta en el constructor, para los tests.

**La demo** solo traduce los botones a esas llamadas. Para la versión de producción, se sustituye `ScenarioSimulator` por el gameplay real y se sigue el README §"Usarlo como plantilla".

## 3. Verificación

| Comprobación | Resultado |
|---|---|
| Unity abierto | PID 44888 es otro proyecto (`AytmPalosVR`); no lo he tocado. Todo el batchmode se ha ejecutado sobre este proyecto con el Editor de este proyecto cerrado |
| Compilación + scene builder (`-executeMethod ScenarioDemoSceneBuilder.BuildFromCommandLine`, `Builds/logs/f003-scene.log`) | Exit 0, `SCENARIO_DEMO_SCENE_OK`. Único warning: el CS0414 previo de `ControllerMain`. La verificación tras guardar no encuentra referencias ni fuentes vacías |
| EditMode `-runTests`, sin `-quit` (`Builds/logs/f003-editmode-results.xml`) | **96 tests: 95 pass, 0 fail, 1 skipped.** El skipped es el XSD 3rd, como en F001/F002. Nuevos: 29 (25 de `ScenarioTrackerTests` y 4 de `ScenarioDemoSceneTests`). Los 67 previos siguen en verde |
| `.harness/init.sh` | **Exit 0.** Paso 6 OK (F001 blocked, F002 done, F003 in_progress). Paso 7 OK; `.harness/progress/editmode-results.xml` = 96/95/0/1 |
| Build WebGL + paquete (`ScormBuildCli.BuildAndPackage`, `-scormEdition 3rd`, `-scormBuildDir Builds/WebGL_ScenarioDemo`, `-scormIdentifier com.invelon.scormscenariodemo`, `-scormTitle "Demo Escenarios SCORM"`, `-scormLanguage es-ES`, `-webglCompression gzip-fallback`; log `Builds/logs/f003-build.log`) | Exit 0, `Build result: Succeeded` (4 min 04 s la primera vez; 2 min 31 s la build final, hecha con el código final). Incluye las escenas ScenarioDemo y TestApp. Hubo **1 error** de build step, no bloqueante y heredado: `Component GUI Layer in Main Camera for Scene Assets/SCORM/_Scenes/TestApp.unity is no longer available.` Viene de la escena TestApp de Unity 5; no lo he tocado (fuera de alcance) |
| Zip | **`Builds/SCORM_ScenarioDemo_2004_3rd.zip`, 10 320 998 bytes**: 55 entradas y 18 ficheros de contenido |
| `ScormBuildCli.ValidateZip` (`Builds/logs/f003-validate.log`) | `SCORM_VALIDATE_OK`: schemaversion `2004 3rd Edition`, 18 `<file>` sin ninguno ausente, 0 errores XSD (validado con el set 4th, la limitación conocida de F001) |
| Manifest | identifier `com.invelon.scormscenariodemo` y título "Demo Escenarios SCORM". **Sin `completionThreshold`, `minNormalizedMeasure` ni `satisfiedByMeasure`**, así que el LMS no recalcula completion/success por su cuenta |
| Smoke HTTP | Zip descomprimido en el scratchpad y servido con `python -m http.server`: **18/18 ficheros del manifest con 200**. `index.html` referencia `scorm.js`, `ScormSimulator.js` y los 4 `WebGL_ScenarioDemo.*`. El `ScormSimulator.js` empaquetado incluye la persistencia |
| `ScormSimulator.js` | `node --check` OK. Test headless (`<scratchpad>/f003_sim_test.js`) PASS: suspend → resume tras "recarga"; normal → intento nuevo con la semilla; `?scormsimreset=1` → ab-initio |
| Artefactos | La build de TestApp generó `Assets/SCORM/_Scenes/TestApp/LightingData.asset` (+ carpeta y `.meta`): **borrados**. Ningún otro fichero espurio en `Assets/` |

**Paquetes `com.unity.ai.*` del humano** (no tocados): no rompen la build, pero la **inflan**.

- El build report lista **272 assets de `Packages/com.unity.ai.inference/Runtime/Core/Resources/Sentis/...`** (compute y pixel shaders), unos **9.5 MB sin comprimir**. Por ejemplo, `ConvGeneric.compute` ocupa 6.5 MB. Entran porque están en una carpeta `Resources`.
- También suman ~4 minutos de compilación de shaders en la build.

| Fichero | TestApp F001 (antes de los paquetes) | ScenarioDemo |
|---|---|---|
| `.data.unityweb` | 1 482 042 B | 2 689 895 B (+1.2 MB) |
| `.wasm.unityweb` | 7 644 298 B | 7 679 338 B |
| zip | 9 076 777 B | 10 320 998 B |

`com.unity.ai.assistant` no aporta assets a la build. **Recomendación**: si la app final no usa Sentis en runtime, quitar `com.unity.ai.inference` del proyecto del cliente. Si lo usa, ver si se pueden excluir los Resources.

## 4. Desviaciones respecto al encargo

- **D1. Serialización binaria.** La escena y los `.asset` de Data se guardan en **binario**. El proyecto trae `EditorSettings` en modo binario/mixed desde Unity 5 (`TestApp.unity` es YAML porque es de origen).
  - No he cambiado el modo de serialización: afecta a todo el proyecto.
  - Consecuencia: el reviewer no puede hacer diff de la escena, pero es reproducible con el builder y el test de cableado la cubre.
  - Recomiendo `Asset Serialization = Force Text` como feature aparte.
- **D2. Nota global con los escenarios pendientes = 0.** Es el `GlobalScoreMethod` por defecto, para que el libro de Moodle no muestre 100 tras un solo escenario. La alternativa "solo completados" es configurable.
- **D3. `cmi.scaled_passing_score` no se usa.** El umbral sale del catálogo. Moodle no lo rellena salvo que el manifest tenga `minNormalizedMeasure`.
- **D4. "Relanzar sesión" solo existe en el Editor.** Un LMS real no permite volver a llamar a Initialize tras Terminate en la misma página (scorm.js devuelve `false`). En el navegador se relanza recargando.
- **D5. "Reiniciar datos demo"** escribe un estado vacío **válido**, no `""`, y hace Commit. No puede borrar objetivos, interacciones ni la nota en el LMS; la UI lo avisa.
- **D6. Tests extra no pedidos**: `ScenarioDemoSceneTests`, con el cableado de la escena, Build Settings, el catálogo de ejemplo y el smoke de Play Mode.
- **D7. Plantilla WebGL modificada** (persistencia del simulador). La acceptance #5 pide reanudar con `?scormsim=1` y el simulador original reinicializaba la semilla en cada carga. Aparece en el plan §2; afecta también a TestApp si se prueba con `?scormsim=1`, que pasa a reanudar.

## 5. Abiertos para el leader/humano

1. **Visual check** (`.harness/progress/visual_check_scorm-scenario-demo.md`, sin firmar). Cubre Play Mode, el navegador con el simulador, SCORM Cloud (Runtime Data: objetivos `scenario-0N`, `cmi.score.raw`, interacciones enlazadas) y Moodle (libro de calificaciones e informe de objetivos). Ningún LMS real lo he probado yo.
2. **Capturas no posibles**: no hay MCP ni navegador headless con WebGL. La UI no se ha visto renderizada. El smoke de Play Mode comprueba la lógica y el cableado, no la maquetación; puede hacer falta ajustar tamaños o anclajes tras el visual check.
3. **Error heredado** `GUI Layer` en `TestApp.unity`, que aparece en cada build mientras TestApp esté en Build Settings. Además, construir TestApp genera `LightingData.asset`. Si se quiere una build limpia de la demo, deshabilitar TestApp en Build Settings, o arreglar la escena en una feature aparte.
4. **Peso de `com.unity.ai.inference`** en la build (§3).
5. **Ficheros para el commit** (decisión del leader):
   - `Assets/SCORM/Demo/**` con sus `.meta` (`Demo.meta` incluido).
   - Los 2 tests nuevos con sus `.meta`.
   - `ScormSimulator.js`.
   - `ProjectSettings/EditorBuildSettings.asset`. Ya venía modificado por la reserialización de Unity 6 / paquetes AI, ver F002 §6.2.
   - `ProjectSettings.asset` y `ProjectSettings/Packages/` siguen como los dejó el humano.
   - No commitear `Builds/`.
6. **Interacciones**: el mínimo de SCORM 2004 son 250. Con muchos reintentos × decisiones, algún LMS puede cortar (está en el README). La demo genera 4-6 por partida.
7. **L5 de F002** (completion_threshold en 4th): no aplica a este zip 3rd sin umbral.

---

## Ronda 2 (tras `review_scorm-scenario-demo.md`: READY_FOR_VISUAL_CHECK, 2 medios)

Implementer, 2026-10-06. Sin escrituras de git. No he tocado `Packages/manifest.json`.

### Corregidos

| Hallazgo | Corrección | Test |
|---|---|---|
| **M1** Textos en inglés en la UI | **Plantilla:** expone códigos en lugar de textos. **Demo:** los traduce `DemoUI/DemoTexts.cs`. Detalle abajo | `Initialize_WritesNothing_…` (`RestoreIssue.NotJson`), `Resume_SuspendDataFromNewerVersion_…` (`NewerVersion`), `InvalidCalls_ThrowWithContext` (`UnknownScenario`), `Suspend_…` (`SessionClosed`) |
| **M2** JSON sin `"v"` se aceptaba como v1 | `ScenarioSaveData.v` vale 0 por defecto (JsonUtility respeta los inicializadores para las claves ausentes) y `SuspendDataJson` fija `v = CurrentVersion` al guardar. Un JSON sin versión cae en la rama `NoVersion` y el estado se reconstruye desde `cmi.objectives` | `Resume_SuspendDataJsonWithoutVersion_RebuildsFromObjectives`, con 3 casos (`{}`, `{"foo":1}`, `{"cur":"scenario-01","s":[]}`). `SuspendData_SavedJsonCarriesCurrentVersion` |
| **B1** `Close` cerraba aunque el LMS rechazara Commit/Terminate | Si Commit falla, **no** se llama a Terminate y el tracker sigue abierto (`IsClosed=false`, devuelve `false`); se puede reintentar. Solo se cierra si Terminate tiene éxito. Documentado en el XML doc. La demo muestra "…falló: la sesión sigue abierta, puedes reintentarlo" | `Close_CommitRejected_KeepsSessionOpenWithoutTerminate_AndCanRetry`: backend terminado por fuera → 143 → sin Terminate → reintento OK |
| **B2** Escrituras rechazadas no se recuperaban | `Close()` reescribe los objetivos de los escenarios completados y los globales antes de `suspend_data`/`session_time`. No genera tráfico si el LMS ya tiene los valores (`WriteIfChanged` de F002). Los globales solo se reescriben si hay algún escenario completado, para no enviar `progress_measure=0` en una sesión sin progreso | `Close_RewritesObjectiveAndGlobalScoreTheLmsRejectedBefore`: 406 inyectado en `cmi.objectives.2.score.raw` y en `cmi.score.raw`, recuperados al suspender |
| **B4** Campo de demo en el SO de la plantilla | Opción "documentar": tooltip "DEMO ONLY… Ignored by the tracker". El README ya lo describe como dato del simulador | — |
| **B5** `decisionId` e intentos a medias al reanudar | Se valida que `decisionId` no tenga espacios **antes** de escribir nada o incrementar `d`. Un intento restaurado con `p=1` que no se ha empezado en esta sesión abre un intento nuevo en el primer `RecordDecision`, así que no se reutilizan ids | `RecordDecision_ResumedHalfPlayedAttemptWithoutBegin_StartsNewAttempt`. `InvalidCalls_ThrowWithContext` (`"d 1"`: no escribe nada ni empieza el intento) |
| **B6** Timestamp con `DateTime.Now` | Usa el reloj inyectado: `_utcNow()` pasado a hora local, que es lo que formatea F002 | `RecordDecision_TimestampComesFromInjectedClock` |
| **I1** Detección del simulador | `SummaryPanel.ConnectionLabel` aplica a la query la misma regex que `scorm.js` (`(^|[?&])scormsim=1(&|$)`) | — |
| **I6** Cabecera de `ScormSimulator.js` cortada | Frase corregida. `node --check` OK y el test headless de persistencia sigue en PASS | — |

**Detalle de M1.**
- La plantilla, que sigue en inglés para logs y excepciones, ahora expone:
  - `ScenarioTrackerException.Error` (`ScenarioTrackerError`: `InvalidCatalog`, `ScormNotInitialized`, `NotInitialized`, `SessionClosed`, `UnknownScenario`, `SuspendDataTooLarge`);
  - `ScenarioTracker.RestoreSource` (`ScenarioRestoreSource`) y `RestoreIssue` (`ScenarioRestoreIssue`: `NotJson`, `ParseError`, `NoVersion`, `NewerVersion`);
  - `ScenarioTrackerHost.InitializationErrorKind`.
- `RestoredFrom`/`RestoreWarning` se conservan como tokens/texto técnico.
- `DemoUI/DemoTexts.cs` traduce todo a español.
- `SummaryPanel` y `ScenarioDemoController` ya no muestran `e.Message`, `RestoreWarning` ni `RestoredFrom`. El detalle técnico va a `Debug.LogWarning`.

### No corregidos, y por qué
- **B3** (asmdefs / mover `Runtime/`): añadir asmdefs cambia los ensamblados de los tests (Assembly-CSharp-Editor) y la estructura del kit. Lo propongo como feature aparte.
  - Tampoco he editado `.harness/docs/architecture.md`, que es documentación del harness y le toca al leader. El leader debería añadir la ubicación `Assets/SCORM/Demo/{Runtime,DemoUI,Editor,Data}` y la excepción de "código de Editor en `Demo/Editor/`".
- **I2-I5**: el autoscroll del log, un contador separado para errores de escritura, guardar floats como enteros y el error duplicado en `Host.Start`. Son mejoras opcionales, sin impacto en el visual check; el visual check ya avisa de los GetValue 401/403.

### Archivos de la ronda 2

| Tipo | Archivos |
|---|---|
| Nuevos | `Runtime/ScenarioTrackerError.cs`, `Runtime/ScenarioRestoreSource.cs`, `Runtime/ScenarioRestoreIssue.cs`, `DemoUI/DemoTexts.cs` (con sus `.meta`) |
| Modificados | `Runtime/ScenarioTracker.cs`, `ScenarioTrackerException.cs`, `ScenarioSaveData.cs`, `ScenarioTrackerHost.cs`, `ScenarioDefinition.cs` (tooltip) |
| Modificados (demo) | `DemoUI/SummaryPanel.cs`, `DemoUI/ScenarioDemoController.cs` |
| Modificados (tests y plantilla WebGL) | `Tests/Editor/ScenarioTrackerTests.cs`, `WebGLTemplates/SCORM/TemplateData/ScormSimulator.js` (comentario) |

La escena no cambia: no hay campos serializados nuevos, y los 4 tests de escena pasan.

### Verificación ronda 2

| Comprobación | Resultado |
|---|---|
| EditMode (`Builds/logs/f003-editmode-results.xml`) | **104 tests: 103 pass, 0 fail, 1 skipped** (XSD 3rd). Son 8 tests nuevos, con los 3 casos de M2 contados por separado |
| `.harness/init.sh` | **exit 0**; `.harness/progress/editmode-results.xml` = 104/103/0/1 |
| Build WebGL + zip regenerados con el código de la ronda 2 (mismos argumentos de CLI) | `Build result: Succeeded` en 2 min 06 s. Sigue el error heredado de TestApp "GUI Layer" |
| Zip `Builds/SCORM_ScenarioDemo_2004_3rd.zip` | **10 319 591 B**. Build: `.data` 2 690 864 B, `.wasm` 7 675 428 B |
| `ValidateZip` | `SCORM_VALIDATE_OK`: 55 entradas, 18/18 `<file>`, 0 errores XSD. El manifest sigue sin `completionThreshold`, `minNormalizedMeasure` ni `satisfiedByMeasure` |
| Smoke HTTP | 18/18 con 200. El `ScormSimulator.js` del zip es idéntico al de `Assets/` |
| Limpieza | `Assets/SCORM/_Scenes/TestApp/LightingData.asset` (+ carpeta y `.meta`) y `ProjectSettings/SceneTemplateSettings.json` borrados otra vez. `ProjectSettings.asset`: 81196 B, igual que al empezar |
