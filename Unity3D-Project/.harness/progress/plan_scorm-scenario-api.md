# Plan — F002 `scorm-scenario-api`

Implementer, 2026-10-06. `requires_plan_approval: false` (el humano eximió el gate en chat). Este plan es solo para trazabilidad; la implementación sigue sin esperar.
Rutas relativas a `Unity3D-Project/`.

## 1. Alcance (acceptance F002)

| # | Criterio | Cómo |
|---|---|---|
| 1 | Upsert de objetivo por id | `ScormManager.UpsertObjective(ScormObjectiveData)`. Busca el índice por id (ordinal) en `StudentRecord.objectives`, que ya contiene lo cargado del LMS al reanudar. Si no existe, crea en `_count` y escribe `.id` **primero**. Campos parciales (nullable / `not_set` = no escribir). |
| 2 | Interacciones enlazadas a objetivo por id; id propio opcional | `ScormManager.RecordInteraction(LearnerInteractionRecord, params string[] objectiveIds)`: respeta `interaction.id` si no está vacío (si no, genera `urn:STALS:interaction-id-N`), y añade los ids de objetivo. `AddInteraction` legado sigue forzando `urn:STALS:`. |
| 3 | M3: nada de `''`/null en vocabulario/estado/score; solo campos conocidos y que cambian | Caché de valores por elemento (último valor leído con error 0 o escrito con éxito) en `ScormManager`. Todas las escrituras de objetivos/interacciones (nuevas y legadas) pasan por un helper que omite vacíos y valores iguales al caché. Las floats de `StudentRecord` no distinguen "no fijado" de 0, y `AnObjective` muta el mismo objeto que hay en `StudentRecord`. Por eso se compara contra el caché de strings del LMS y no contra los campos del record (ver desviación D1). |
| 4 | M1 `findAPI` | Dos pasadas en `scorm.js`: `API_1484_11` en la cadena de padres y luego en la del opener; solo después, `API`. Se corrige el comentario (I1). Test node con ventanas anidadas. |
| 5 | M4 ajustes de la otra edición | `ScormManifestBuilder`: 3rd sin threshold pero con `completedByMeasure` → elemento `completionThreshold` = `minProgressMeasure`. 4th sin `completedByMeasure` pero con `completionThreshold` → `completedByMeasure="false" minProgressMeasure="X"` (`cmi.completion_threshold` = X, sin que el LMS anule el estado del SCO). Siempre con warning: `ScormManifestBuilder.GetEditionWarnings(settings)` → `ScormPackageResult.warnings` (la CLI y la ventana ya los muestran). |
| 6 | M5 | `ScormExport.OnGUI`: `EditorApplication.delayCall += Publish/BuildAndPublish; GUIUtility.ExitGUI();` fuera de try/catch. |
| 7 | Evento de log | `ScormAPIWrapper.CallCompleted` (static `event Action<ScormCallInfo>`), reenviado por `ScormManager.ScormCall`. Cubre Initialize, GetValue, SetValue, Commit y Terminate, con operación, elemento, valor, resultado, éxito, código y texto de error. Un handler que lance excepción no rompe SCORM. |
| 8 | Tests EditMode | Upsert (nuevo / existente / reanudación), no-escritura de vacíos y redundantes, orden id-primero, validación, enlace interacción-objetivo, evento, M4 (con XSD). |

## 2. Archivos

`scripts_root` = `Assets/SCORM/_Scripts` (runtime) y `Assets/SCORM/Editor` (editor). `tests_root` = `Assets/SCORM/Tests` (subcarpeta `Editor/`, Assembly-CSharp-Editor).

| Archivo | Raíz | Cambio |
|---|---|---|
| `Assets/SCORM/_Scripts/ScormCallInfo.cs` (nuevo) | scripts_root[0] | DTO inmutable del evento |
| `Assets/SCORM/_Scripts/ScormCallOperation.cs` (nuevo) | scripts_root[0] | enum Initialize/GetValue/SetValue/Commit/Terminate |
| `Assets/SCORM/_Scripts/ScormScoreData.cs` (nuevo) | scripts_root[0] | score parcial (nullable) + `FromRaw(raw,min,max)` + `Validate` |
| `Assets/SCORM/_Scripts/ScormObjectiveData.cs` (nuevo) | scripts_root[0] | datos parciales de un objetivo por id |
| `Assets/SCORM/_Scripts/ScormAPIWrapper.cs` | scripts_root[0] | evento estático; `LastErrorCode`; sin cambio de firmas existentes |
| `Assets/SCORM/_Scripts/ScormManager.cs` | scripts_root[0] | caché, helper de escritura, API nueva, M3 en Add/Update legados y setters de vocabulario |
| `Assets/SCORM/_Scripts/ScormEditorBackend.cs` | scripts_root[0] | conserva datos entre Terminate→Initialize (reanudación); `Reset()` sigue siendo el borrado explícito; reglas de LMS real: 351 por índice con hueco o cambio de id, 408 por campo antes de id; `SimulateRelaunch()` |
| `Assets/WebGLTemplates/SCORM/TemplateData/scorm.js` | (plantilla) | M1 |
| `Assets/SCORM/Editor/ScormManifestBuilder.cs` | scripts_root[1] | M4 |
| `Assets/SCORM/Editor/ScormPackager.cs` | scripts_root[1] | añade warnings M4 |
| `Assets/SCORM/Editor/ScormExport.cs` | scripts_root[1] | M5 |
| `Assets/SCORM/Tests/Editor/ScormScenarioApiTests.cs` (nuevo) | tests_root[0] | tests 1-3, 7 |
| `Assets/SCORM/Tests/Editor/ScormManifestBuilderTests.cs` | tests_root[0] | tests M4 |

No se toca ninguna escena ni prefab. `ControllerMain`/`AnObjective` (TestApp) siguen con el API legado y funcionan igual (con menos Sets).

## 3. Riesgos

- R1: `init.sh` paso 6 fallará con 2 features `in_progress` (F001 sigue `in_progress`). No toco F001 (decisión del leader).
- R2: `Packages/manifest.json` tiene paquetes AI añadidos por el usuario sin commitear. No se tocan; si rompen batchmode, se reporta.
- R3: el backend de Editor más estricto (351/408) podría afectar a TestApp en Play Mode. El código legado escribe id primero, y los índices son siempre `_count`, así que no debería.
- R4: M5 no es testeable de forma automática.
