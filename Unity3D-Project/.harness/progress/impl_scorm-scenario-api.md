# Informe de implementación — F002 `scorm-scenario-api`

Implementer, 2026-10-06. Plan: `.harness/progress/plan_scorm-scenario-api.md` (gate eximido por el humano). Rutas relativas a `Unity3D-Project/`.
No he hecho escrituras de git y no he marcado nada como done. F001 no se ha tocado.

## 0. Acceptance y evidencia

| # | Criterio | Estado | Evidencia |
|---|---|---|---|
| 1 | Upsert de objetivo por id, con score raw/min/max/scaled, estados, progress y description; también al reanudar | Cumplido | `UpsertObjective_*` y `UpsertObjective_Resume_FindsObjectivesLoadedFromLmsInsteadOfDuplicating` |
| 2 | Interacción enlazable a objetivo por id; id propio opcional | Cumplido | `RecordInteraction_KeepsOwnIdAndLinksObjectivesById`, `RecordInteraction_WithoutIdAndWithUnsetValues_*` |
| 3 | M3: nunca `''`/null; solo campos conocidos y que cambian | Cumplido (ver desviación D1) | `*_SendsNoEmptyValues`, `UpsertObjective_SameDataTwice_SecondCallWritesNothing`, `LegacyAddAndUpdateObjective_*`, `LegacyUpdateInteraction_SkipsEmptyValues`, `UpdateScoreAndStatus_*` |
| 4 | M1: `API_1484_11` en toda la cadena (padres y opener) antes que `API` | Cumplido | Test JS headless, casos 6-9 (§4). El mismo test **falla** contra el `scorm.js` de HEAD (control negativo) |
| 5 | M4: mapeo entre ediciones + warning visible | Cumplido (mapeo **y** warning) | 5 tests nuevos en `ScormManifestBuilderTests`, con validación XSD de los manifests mapeados |
| 6 | M5: `delayCall` + `ExitGUI` | Cumplido, sin test automático (IMGUI) | `ScormExport.cs` |
| 7 | Evento público por cada llamada SCORM | Cumplido | `ScormCall_ReportsEveryOperationWithLmsErrorCodes`, `ScormCall_ThrowingHandler_DoesNotBreakTheApiCall` |
| 8 | Tests EditMode nuevos + init.sh | **Cumplido en ronda 2** (init.sh exit 0, 67/66/0/1). Texto original de la ronda 1: Parcial | Los 22 tests nuevos y los 57 en total están en verde (56 pass, 1 skip). Pero **`init.sh` termina con exit 1**: su paso 6 encuentra F001 y F002 `in_progress` a la vez, y cambiar F001 queda fuera de mis permisos (§3, §6.1) |

## 1. Archivos

`scripts_root[0]` = `Assets/SCORM/_Scripts`:
- **nuevos** (Unity genera sus `.meta` al importar):
  - `ScormCallOperation.cs`
  - `ScormCallInfo.cs`
  - `ScormScoreData.cs`
  - `ScormObjectiveData.cs`
- `ScormAPIWrapper.cs`:
  - evento estático `CallCompleted`
  - `LastErrorCode`
  - Initialize, GetValue, SetValue, Commit y Terminate emiten el evento
  - `GetLastError` solo se llama en un fallo, o en Get (donde es necesario para distinguir `""` de un error)
- `ScormManager.cs`:
  - caché de valores del LMS
  - `LoadValue`, `SetAndCache` y `WriteIfChanged`
  - API nueva (§2)
  - `AddInteraction`, `UpdateInteraction`, `AddObjective` y `UpdateObjective` pasan por el escritor común (M3)
  - `SetCompletionStatus`, `SetSuccessStatus` y `SetExit` no envían `not_set`
  - todos los `scormAPIWrapper.SetValue/GetValue` directos pasan por los helpers
- `ScormEditorBackend.cs`: reglas de un LMS estricto:
  - 351: índice con hueco, id de objetivo cambiado o duplicado
  - 408: campo escrito antes del `.id`
  - 122/123, 132/133 y 142/143: llamadas antes de Initialize o después de Terminate
  - reanudación: tras Terminate con `exit=suspend`, el siguiente Initialize conserva los datos y pone `cmi.entry=resume`; con otro `exit` empieza un intento nuevo desde la semilla
  - `Reset()` sigue siendo el borrado explícito

`scripts_root[1]` = `Assets/SCORM/Editor`:
- `ScormManifestBuilder.cs`: mapeo M4 y `GetEditionWarnings(settings)`.
- `ScormPackager.cs`: añade esos warnings a `ScormPackageResult.warnings`. La CLI ya los escribe con `Debug.LogWarning` y la ventana los muestra en el diálogo final.
- `ScormExport.cs`:
  - M5
  - `HelpBox` de warning en la ventana cuando hay un ajuste de la otra edición

Plantilla: `Assets/WebGLTemplates/SCORM/TemplateData/scorm.js`:
- M1: `findAPIByName(win, name)`; `getAPI` hace dos pasadas (2004 en padres → 2004 en opener → 1.2 en padres → 1.2 en opener).
- `findAPI(win)` se conserva por compatibilidad, también en dos pasadas.
- Corregido el comentario de I1.

`tests_root[0]` = `Assets/SCORM/Tests` (`Editor/`):
- **nuevo** `ScormScenarioApiTests.cs` (17 tests)
- `ScormManifestBuilderTests.cs` (+5 tests M4)

`.harness/`: `feature_list.json` (F002 → `in_progress`), `progress/current.md`, `progress/plan_scorm-scenario-api.md` y este informe.

No se ha tocado ninguna escena ni prefab. TestApp (`ControllerMain`/`AnObjective`) usa el API legado sin cambios.

## 2. API pública nueva (solo añadidos)

```csharp
// ScormAPIWrapper
public static event Action<ScormCallInfo> CallCompleted;
public int LastErrorCode { get; }

// ScormManager
public static event Action<ScormCallInfo> ScormCall;            // add/remove reenviados a ScormAPIWrapper.CallCompleted
public static int  UpsertObjective(ScormObjectiveData data);      // índice, o -1 si el LMS rechaza el id nuevo
public static int  FindObjectiveIndex(string id);                 // -1 si no existe
public static StudentRecord.Objectives GetObjective(string id);   // null si no existe
public static int  RecordInteraction(StudentRecord.LearnerInteractionRecord interaction, params string[] objectiveIds); // índice o -1
public static bool UpdateScore(ScormScoreData score);             // cmi.score.*, solo campos != null y que cambian
public static bool UpdateStatus(StudentRecord.SuccessStatusType successStatus, StudentRecord.CompletionStatusType completionStatus); // not_set = no tocar
public static bool UpdateProgressMeasure(float value);

// Tipos nuevos
public enum ScormCallOperation { Initialize, GetValue, SetValue, Commit, Terminate }
public sealed class ScormCallInfo { Operation; Element; Value; Result; Succeeded; ErrorCode; ErrorDescription; Time; ToString() }
public class ScormScoreData { float? raw, min, max, scaled; static FromRaw(raw, min, max); bool IsEmpty }
public class ScormObjectiveData { string id; ScormScoreData score; SuccessStatusType successStatus = not_set;
                                  CompletionStatusType completionStatus = not_set; float? progressMeasure; string description }
```

Semántica:

- **Orden y validación**
  - `UpsertObjective` valida todo antes de la primera escritura y lanza `ArgumentException`/`ArgumentOutOfRangeException` con contexto si:
    - el id está vacío;
    - `scaled` no está en [-1,1];
    - `min > max`;
    - `raw` no está entre min y max, tomando min/max de la propia actualización o del valor conocido del LMS;
    - `progress` no está en [0,1].
  - Para un id nuevo escribe primero `cmi.objectives.n.id`, con n = `_count`. Si el LMS lo rechaza, no añade nada y devuelve -1.
  - Después escribe min → max → raw → scaled → success → completion → progress → description.
- **Interacciones**
  - `RecordInteraction` es un diario: un id repetido crea una entrada nueva.
  - Respeta `interaction.id` y, si está vacío, genera `urn:STALS:interaction-id-N`.
  - Concatena `objectiveIds`, quitando vacíos y duplicados.
  - Con `type` `not_set` no envía `learner_response`/`correct_responses` (evita el 408).
  - Solo envía `latency` si es > 0.
- **Compatibilidad**
  - `AddObjective`/`AddInteraction` siguen forzando sus ids `urn:STALS:*`.
  - Sin estado inicializado, todos los métodos nuevos lanzan `InvalidOperationException`.
  - El evento lo lanza `ScormAPIWrapper` sin tocar `GetLastError`/`GetErrorString`.
  - Si un handler lanza una excepción, se registra con `Debug.LogException` y no rompe la llamada.

## 3. Verificación

| Comprobación | Resultado |
|---|---|
| Compilación offline previa (Roslyn del Editor, sin Unity abierto) | 0 errores |
| Batchmode `-runTests -testPlatform EditMode`, **sin `-quit`** (`Builds/logs/f002-editmode-results.xml`, `f002-tests.log`). Se lanzó después de que el humano cerrara el Editor (PID 60092) | Compila sin errores. Único warning: el CS0414 preexistente de `ControllerMain`. **57 tests: 56 pass, 0 fail, 1 skipped.** El skipped es `Build_3rd_IsValidAgainst3rdEditionXsd_WhenAvailable`: no hay XSD 3rd, igual que en F001. Los nuevos son 22 = 17 de `ScormScenarioApiTests` y 5 M4, y todos pasan |
| `.harness/init.sh` | Paso 7 (tests reales): `[OK]`; `.harness/progress/editmode-results.xml` da los mismos 57/56/0/1. **Exit 1 por el paso 6**: `[FAIL] Hay 2 features en in_progress (máximo 1)`. Lo causa que F001 sigue `in_progress`, y no lo toco (ver §6.1). Ningún otro FAIL |
| Compilación offline de `_Scripts` con `-define:UNITY_WEBGL` (rama `DllImport` del wrapper, que es la que se publica) | 0 errores |
| Test JS headless (node) | PASS. El control negativo contra el `scorm.js` de HEAD falla, como debe (§4) |
| Artefactos en `Assets/` | Solo los `.cs` nuevos y sus `.meta` legítimos (5). No se ha generado `LightingData.asset` ni nada más |

## 4. Test JS headless (M1)

Script: `<scratchpad>/f002_scorm_js_test.js`. Es el test de F001 más los casos 6-9, y se ejecuta con `cwd` = `Assets/WebGLTemplates/SCORM/TemplateData`.

Casos:
1. El frame hijo expone `API` y el padre `API_1484_11`: gana 2004 y no se llama al 1.2.
2. La cadena de frames solo tiene 1.2 y el opener tiene 2004: gana el opener (2004).
3. Solo hay 1.2: se usa `LMSInitialize` y `doIsScorm2004()` devuelve `false`.
4. Una ventana intermedia lanza excepción (cross-origin): la búsqueda continúa hasta el 2004 de arriba.

Resultados:
- Salida: `ALL JS TESTS PASSED (incl. F002 M1)`. Los casos 1-5 de F001 siguen pasando.
- Control negativo: el mismo script contra el `scorm.js` de HEAD (`git show`) da `AssertionError` en el caso 6.

## 5. Desviaciones

- **D1. Contra qué se compara.** "Que cambian respecto al valor en StudentRecord" se implementa comparando con un **caché por elemento**: el último valor leído del LMS con error 0 y no vacío, o el último aceptado por el LMS. No se compara con los campos de StudentRecord por dos motivos:
  - Los `float` de StudentRecord no distinguen "no fijado" de 0.
  - `AnObjective` modifica in situ el mismo objeto que está en `StudentRecord.objectives` antes de llamar a `UpdateObjective`, así que comparar con StudentRecord habría suprimido escrituras reales.

  Los reales se comparan numéricamente (`"80.0"` == `"80"`). StudentRecord se sigue actualizando con lo escrito.
- **D2. Comportamiento de los métodos legados.** Los Add/Update legados ahora omiten campos vacíos o `not_set` y valores sin cambios:
  - `UpdateObjective` ya no reenvía el `.id` si no cambia.
  - `latency` = 0 ya no se envía (0 = no medido).
  - `weighting` se envía siempre, también 0, tanto en el API legado como en `RecordInteraction`. Es un `float` sin estado "no fijado" y 0 es un peso válido, así que omitirlo cambiaría el significado. El test `RecordInteraction_WithoutIdAndWithUnsetValues_*` fija este comportamiento.
  - Los setters legados de reales (`SetScore*`, `SetProgressMeasure`, …) siguen escribiendo siempre: son escrituras explícitas.
- **D3. `ScormEditorBackend` más estricto y con reanudación** (ver §1). Es lo que permite testear la reanudación y el orden id-primero. Los tests de F001 no dependían del comportamiento anterior.
- **D4. M4: mapeo, no solo aviso.**
  - 3rd → 4th se mapea a `completedByMeasure="false"`, **no** `"true"`, para que el LMS no anule el `completion_status` del SCO.
  - 4th → 3rd se mapea al elemento `completionThreshold`.
  - Si están los dos ajustes, gana el de la edición activa.
  - En todos los casos sale un warning.

## 6. Abiertos

1. **`init.sh` en rojo solo por "2 in_progress"** (F001 + F002). F001 está commiteado (3cbe562), pero en `feature_list.json` sigue `in_progress` a la espera de su visual check. Lo decide el leader: cerrarlo, marcarlo `blocked` o aceptar el FAIL. No hay ningún otro fallo.
2. **`ProjectSettings/EditorBuildSettings.asset`: lo escribió mi ejecución batchmode de tests** (13:05:43, dentro de esa ejecución).
   - Es una re-serialización: pasa de 4148 a 1325 bytes, la versión cambia de `5.0.2f1` a `6000.3.21f1` y añade `m_configObjects` con `com.unity.dt.app-ui`.
   - Por el contenido, parece provocado por los paquetes `com.unity.ai.*` que el humano añadió a `Packages/manifest.json` y por la actualización a Unity 6. No sale del código de F002.
   - No lo he revertido: revertirlo sería una escritura de VCS.
   - El leader decide si entra en algún commit. Lo mismo vale para `ProjectSettings/ProjectSettings.asset` (lo modificó el Editor abierto) y para `ProjectSettings/Packages/` (sin trackear).
3. **M5 sin test automático** (IMGUI). Para verificarlo a mano: abrir SCORM > Export SCORM Package, pulsar "Publish" con un build existente y comprobar que no aparece `EndLayoutGroup` en la consola.
4. **`ScormSimulator.js` (navegador, `?scormsim=1`) no aplica las reglas estrictas** del backend de Editor (351/408) ni la reanudación tras Terminate. Si F003 quiere probar la reanudación en el navegador sin LMS, habrá que ampliarlo; no forma parte del alcance de F002.
5. **Evidencia JS en el scratchpad** (`f002_scorm_js_test.js`), igual que en F001 (I6): no queda versionada.
6. **Ficheros nuevos para el commit** (decisión del leader): los 4 `.cs` de `_Scripts` y `ScormScenarioApiTests.cs`, con sus `.meta`.

---

## Ronda 2 (tras `review_scorm-scenario-api.md`: CHANGES_REQUESTED)

Implementer, 2026-10-06. Sin escrituras de git. No he tocado `EditorBuildSettings.asset` ni `Packages/manifest.json`.

### Corregidos

| Hallazgo | Corrección | Test |
|---|---|---|
| **H3** (bloqueante): el evento no tenía protección contra reentrada | `ScormAPIWrapper.RaiseCall` (método de instancia) usa un flag estático `raising`. Una llamada SCORM hecha desde un handler funciona normal, pero no relanza el evento (sin recursión, sin `StackOverflow`) ni se notifica al resto de handlers. `LastErrorCode` se guarda antes de lanzar el evento y se restaura después, en un `finally`. Documentado en `CallCompleted` y `ScormCall`: los handlers no deben llamar al API, y en un MonoBehaviour hay que suscribirse en `OnEnable` y desuscribirse en `OnDisable` | `H3_HandlerCallingTheApi_DoesNotRecurseAndKeepsOuterErrorCode`. Un handler escribe con `ScormManager.SetLocation` en cada llamada; otro hace un `SetValue` correcto sobre el **mismo** wrapper mientras la llamada externa es un Get con 401, y `LastErrorCode` sigue en 401 |
| **H2** (bloqueante): los fallos de escritura del LMS no se notificaban | Sobrecargas aditivas: `UpsertObjective(ScormObjectiveData, out bool allWritten)` y `RecordInteraction(interaction, out bool allWritten, params string[] objectiveIds)`; las firmas anteriores las llaman. Además, propiedades estáticas `ScormManager.LastWriteFailed`, `LastWriteFailures` y `LastWriteErrorCode` (código del primer rechazo), que se reinician al empezar cada `UpsertObjective`/`RecordInteraction`/`UpdateScore`/`UpdateStatus`/`UpdateProgressMeasure`. Los valores rechazados no entran en caché, así que la siguiente llamada los reintenta | `H2_UpsertObjective_ReportsRejectedFieldAndRetriesItNextTime`: 406 inyectado en `score.raw` → `allWritten=false` y código 406. Al reintentar solo se reenvía ese campo. `H2_RecordInteraction_ReportsRejectedField`. `H2_RecordInteraction_RejectedId_ReturnsMinusOneAndAddsNothing` |
| **H1**: `AddObjective`/`AddInteraction` legados rompían `Count == _count` | El elemento se añade a `StudentRecord` solo si el LMS acepta el id. Hay una excepción explícita y documentada: con `!IsLmsConnected` se añade localmente igualmente, para que la UI sin LMS siga como antes. Si el id se rechaza, no se escribe ningún otro campo | `H1_LegacyAddObjectiveWithRejectedId_*` (351 inyectado; después `UpsertObjective` cae en el índice 2 = `_count`). `H1_LegacyAddInteractionAfterTerminate_*` (133) |
| **L1**: comparación numérica contra un valor no numérico | `WriteIfChanged` solo compara numéricamente si **las dos** cadenas se parsean (`TryParseReal`) | `L1_RealUpdateAgainstVocabularyValue_IsWritten`: un estimate de 0 sobre `"incorrect"` |
| **L2**: NaN/Infinity no se rechazaban | `ValidateScore` rechaza valores no finitos en raw/min/max/scaled con `ArgumentOutOfRangeException` | `L2_NonFiniteScores_AreRejectedBeforeWriting` |
| **L3**: ids de objetivo duplicados dentro de una interacción | `WriteInteraction` deduplica (ordinal) antes de escribir `objectives.m.id` | `L3_DuplicatedObjectiveLinks_AreWrittenOnce` |
| **L4**: ids con espacios o demasiado largos | `ValidateIdentifier`: `ArgumentException` si el id contiene espacios en blanco o pasa de 4000 caracteres. Se aplica al id de objetivo en `UpsertObjective` y, en `RecordInteraction`, al id de la interacción y a los ids de objetivo enlazados. Todo se valida antes de cualquier escritura | `L4_IdsWithWhitespaceOrTooLong_AreRejected` |
| **L6**: el plan anunciaba `SimulateRelaunch()` | Ver D5 | — |
| **L7**: ciclo de vida de los suscriptores del evento estático | Documentado en el XML doc (ver H3) | — |

Ayuda de test añadida a `ScormEditorBackend`: `InjectSetError(identifier, errorCode)` / `ClearInjectedErrors()`, que se limpian también con `Reset()`. Simula un LMS que rechaza un valor concreto. Añadidos además los textos de error 406 y 407.

**D5 (L6).** `ScormEditorBackend.SimulateRelaunch()`, previsto en el plan §2, no existe. La reanudación se simula como en un LMS real: `Terminate()` con `cmi.exit=suspend` y después `Initialize()`. Lo hace `Initialize()` (§1, D3).

### No corregidos, y por qué

- **L5** (M4 3rd→4th con `completedByMeasure="false"`): el reviewer lo acepta porque el warning lo explica. Para comprobar si `cmi.completion_threshold` se inicializa en SCORM Cloud hace falta una prueba en el LMS real, que queda para el humano o para F003.
- **L8**: los huecos ya documentados siguen abiertos (§6.3-6.5): test JS en el scratchpad, M5 sin test automático y `ScormSimulator.js` sin las reglas estrictas.

### Verificación ronda 2

| Comprobación | Resultado |
|---|---|
| Compilación offline con Roslyn, runtime + editor + tests, sin defines y con `-define:UNITY_WEBGL` | 0 errores en ambos casos |
| Batchmode `-runTests` EditMode, sin `-quit` (`Builds/logs/f002r2-editmode-results.xml`). El Unity abierto en ese momento (PID 44888) era de **otro** proyecto (`AytmPalosVR`) | **67 tests: 66 pass, 0 fail, 1 skipped** (XSD 3rd, igual que antes). Los 10 tests nuevos de la ronda 2 pasan |
| `.harness/init.sh` | **exit 0**. Paso 6 OK (F001 `blocked`). Paso 7 OK; `.harness/progress/editmode-results.xml` = 67/66/0/1 |
| Artefactos en `Assets/` | Ninguno nuevo. `EditorBuildSettings.asset`/`ProjectSettings.asset`/`Packages/*` siguen como los dejó el humano |

API pública añadida en esta ronda, solo añadidos:

```csharp
public static int  ScormManager.UpsertObjective(ScormObjectiveData data, out bool allWritten);
public static int  ScormManager.RecordInteraction(StudentRecord.LearnerInteractionRecord interaction, out bool allWritten, params string[] objectiveIds);
public static bool ScormManager.LastWriteFailed { get; }
public static int  ScormManager.LastWriteFailures { get; }
public static int  ScormManager.LastWriteErrorCode { get; }
public static void ScormEditorBackend.InjectSetError(string identifier, int errorCode);
public static void ScormEditorBackend.ClearInjectedErrors();
```

Cambio de comportamiento legado (H1): con un LMS conectado, un `AddObjective`/`AddInteraction` cuyo id se rechaza ya no aparece en `GetObjectives()`/`GetInteractions()`. Antes aparecía como entrada fantasma.
