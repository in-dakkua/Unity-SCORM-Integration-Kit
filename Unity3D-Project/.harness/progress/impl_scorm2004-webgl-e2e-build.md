# Informe de implementación — F001 `scorm2004-webgl-e2e-build`

Implementer, 2026-10-05. Sigue el plan aprobado `.harness/progress/plan_scorm2004-webgl-e2e-build.md` (§8).
Todas las rutas son relativas a `Unity3D-Project/` salvo que se indique otra cosa.

No he hecho ninguna escritura de git: ni commit, ni rama, ni stash, ni checkout. La feature **no** está marcada como done. El visual check está redactado pero **sin marcar ni firmar**.

## 0. Acceptance (`.harness/feature_list.json` F001) y evidencia

| # | Criterio | Estado | Evidencia |
|---|---|---|---|
| 1 | Assets/ en versión WebGL (jslib, WebGLTemplates/SCORM, sin `ExternalCall` ni `System.Threading`); se elimina WebPlayerTemplates | **Cumplido** | §2.1. El grep de restos (abajo) solo encuentra las aserciones de regresión de los tests y un comentario del wrapper que dice que ya no se usa `Thread.Sleep`. También eliminé el `ExternalCall` comentado que traía `ControllerMain.cs` del paquete; `compile4.log` OK. |
| 2 | Bugs bloqueantes: `i++`/`x++`, `pattrern`, vocabulario, null-checks en `UpdateInteraction`, InvariantCulture, `DateTime.Parse` vacío | **Cumplido** | Tests `AddInteraction_WithTwoObjectivesAndTwoCorrectResponses_WritesAllIndexes`, `UpdateInteraction_WritesPatternNotPattrern`, `ToVocabulary_MapsEnumsToScormTokens`, `UpdateInteraction_WithNullCollections_DoesNotThrowAndWritesFields`, `Setters_WriteInvariantNumbersAndVocabulary` y `ToReal_*`/`SecondsToTimeInterval_*` (es-ES), `ParseTimestamp_EmptyReturnsMinValueWithoutException` |
| 3 | `scorm.js`: init contra `"true"`, `DebugPrint` sin `#console`, sin `window.close()`, Commit+Terminate en `pagehide`, sin carrera con `unityInstance`, respuesta inmediata sin API | **Cumplido** (el comportamiento en navegador real lo verifica el humano) | §2.2. Tests JS headless (§5): casos 1, 3, 4 y 5. Ya no se usa `unityInstance.SendMessage` (grep). jslib síncrono enlazado (§5). Visual check A3-A4 y B6 |
| 4 | Exportador: UTF-8 escapado, selector 3rd/4th (elemento en 3rd, atributos en 4th), `schemaversion`, sin `limitConditions`/`timeLimitAction` si no se configuran, ficheros reales del build, sin `.meta` | **Cumplido** | `ScormManifestBuilderTests` (13 tests). Manifest real del zip en §5 (18 `<file>` = ficheros del build). `ValidateZip` comprueba que no hay `.meta` ni `\` |
| 5 | Build WebGL por CLI con plantilla SCORM y compresión desactivada o Decompression Fallback; zip por CLI sin GUI | **Cumplido** | `ScormBuildCli.BuildAndPackage` / `Package` en batchmode (§5). Gzip + `decompressionFallback`, ficheros `.unityweb`. `build_3rd.log` y `package_4th.log` |
| 6 | Zip validado localmente y probado con ScormSimulator; SCORM Cloud en el visual check | **Parcial por diseño** | XSD por dos vías independientes con controles negativos, y smoke HTTP 18/18 (§5). El simulador está probado headless (node) y en Play Mode con el backend equivalente. **El navegador real con `?scormsim=1` y SCORM Cloud quedan para el humano**: visual check A3 y B1-B7 |

Grep de restos (`Assets/`, `*.cs *.js *.jslib *.html`), tras eliminar el `ExternalCall` comentado:

```
$ grep -rn -E "Thread\.Sleep|ExternalCall|unityInstance\.SendMessage|Pointer_stringify|Ionic|System\.Web|pattrern|WebPlayerTemplates|wgldo" Assets ...
Assets/SCORM/Tests/Editor/ScormEditorBackendInteractionTests.cs:78:		Assert.IsNull(Value(p + "correct_responses.0.pattrern"));
Assets/SCORM/Tests/Editor/ScormEditorBackendInteractionTests.cs:106:		Assert.IsNull(Value("cmi.interactions.0.correct_responses.1.pattrern"));
Assets/SCORM/_Scripts/ScormAPIWrapper.cs:40:/// There is no SendMessage round trip, no queue, no polling and no Thread.Sleep.
$ grep -rn "System.Threading\|Thread\." Assets/SCORM/_Scripts Assets/SCORM/Editor
Assets/SCORM/_Scripts/ScormAPIWrapper.cs:40:/// There is no SendMessage round trip, no queue, no polling and no Thread.Sleep.
```

(Los tests usan `System.Threading.Thread.CurrentThread.CurrentCulture` para forzar es-ES. Es código de test, no del runtime.)

Nota: el build WebGL se generó antes de borrar ese comentario del `ControllerMain.cs`. Es un cambio solo de comentario y no altera el binario.

## 1. Resumen

| Comprobación | Resultado |
|---|---|
| Compilación batchmode, tras cada bloque y 4 veces en total | OK. 0 errores. Queda 1 warning preexistente (CS0414 `ControllerMain.completionStatus`) |
| Tests EditMode (batchmode `-runTests`, sin `-quit`) | **35 tests: 34 pasados, 0 fallidos, 1 skipped.** El skipped es intencionado: no hay XSD 3rd en `Plugins/2004_3rd` |
| `.harness/init.sh` | Exit 0, solo con avisos de VCS. **Su paso 7 es un falso verde**: ver §7.1 |
| Build WebGL (CLI) | Succeeded. `Builds/WebGL` ocupa 9.312.297 bytes y comprime con Gzip + Decompression Fallback |
| Zips SCORM | `Builds/SCORM_TestApp_2004_3rd.zip` y `Builds/SCORM_TestApp_2004_4th.zip`: 9.076.777 bytes cada uno, con 55 entradas y 18 `<file>` |
| `ScormBuildCli.ValidateZip` | `SCORM_VALIDATE_OK` en los dos zips (0 errores o avisos XSD). El control negativo da exit 1 |
| PowerShell 5.1 + `XmlSchemaSet` | `PS_VALIDATE_OK` en los dos zips (0 problemas). El control negativo da `PS_VALIDATE_FAILED` |
| Smoke test HTTP (node) | **SMOKE OK: 18/18** ficheros del manifest devuelven 200 con el tamaño exacto. `index.html?scormsim=1` devuelve 200 |
| Tests JS headless (node + vm) de `scorm.js` y `ScormSimulator.js` | Todos pasan (ver §5) |

## 2. Archivos cambiados

### 2.1 Sincronizados desde el `.unitypackage` (paso 1, copia byte a byte de `asset` y `asset.meta`)

- **Reemplazados** (y después editados, ver 2.2):
  - `Assets/SCORM/_Scripts/{ScormManager,ScormAPIWrapper,StudentRecord,ControllerMain,AnObjective}.cs` (+ `.meta`)
  - `Assets/SCORM/Editor/ScormExport.cs` (+ `.meta`)
- **Reemplazados sin cambios** (YAML del paquete, mismas GUID):
  - `Assets/SCORM/_Scenes/TestApp.unity` (+ `.meta`), byte a byte igual al paquete
  - `Assets/SCORM/_Prefabs/AnObjective.prefab` y `Assets/SCORM/Resources/ScormManager.prefab` (+ `.meta`)
- **Metas de carpeta reescritos por el paquete** (misma GUID; solo cambian formato y fin de línea): `Assets/SCORM/{Editor,Plugins,Resources,_Prefabs,_Scenes,_Scripts}.meta`
- **Nuevos**:
  - `Assets/Plugins.meta`, `Assets/Plugins/scorm.jslib` (+ `.meta`, solo WebGL)
  - `Assets/WebGLTemplates.meta`, `Assets/WebGLTemplates/SCORM/**` (+ `.meta`)
- **Borrados**: `Assets/WebPlayerTemplates/**` y sus 8 `.meta`
- `Assets/SCORM.meta` se conserva el del repo, como decía el plan. La comprobación de GUIDs da todo SAME salvo esa carpeta.

### 2.2 Runtime: `Assets/SCORM/_Scripts` y plantilla

| Archivo | Cambio |
|---|---|
| **nuevo** `_Scripts/ScormFormat.cs` | Conversión invariante:<ul><li>`ToReal` usa `0.#######` sin exponente (`0.00001f` → `"0.00001"`).</li><li>`ParseReal`, `ParseInt`, `TryParseUserReal`/`ParseUserReal` (invariante y, si falla, cultura actual).</li><li>`SecondsToTimeInterval` (18.4 → `P0DT0H0M18.4S`) y `TimeIntervalToSeconds` (regex anclada y tolerante).</li><li>`ToTimestamp` y `ParseTimestamp` (`RoundtripKind`; vacío → `MinValue`).</li><li>`ToVocabulary` por enum, con dispatcher `object`.</li><li>`StringTo*` migrados. Se añade `"other"` a `StringToInteractionType`, que faltaba.</li></ul> |
| **nuevo** `_Scripts/ScormEditorBackend.cs` | LMS en memoria con los mismos datos que `ScormSimulator.js`. Error 401 si el elemento no existe. Mantiene `_count` automáticamente, como un LMS real. Expone `Data`, `LastError`, `CommitCount` e `IsTerminated` para los tests. |
| `_Scripts/ScormAPIWrapper.cs` | Reescrito síncrono:<ul><li>Los `[DllImport("__Internal")]` van solo bajo `#if UNITY_WEBGL && !UNITY_EDITOR`. En el `#else` hay métodos con la misma firma que llaman a `ScormEditorBackend`, así que todas las llamadas compilan en el Editor.</li><li>Fuera: cola, `lock`, `Random`, `WaitForReturn` y `Thread.Sleep`.</li><li>`IsApiFound` e `IsInitialized` son aditivos.</li><li>`APICallResult` y `SetCallbackValue` pasan a `[Obsolete]` sin efecto.</li><li>`DebugPrint` es estático.</li><li>`Log` comprueba null antes de usar el GameObject.</li><li>`Commit()` y `Terminate()` devuelven `bool`; antes `void`, compatible en código fuente.</li></ul> |
| `_Scripts/ScormManager.cs` | <ul><li>El `DllImport wgldebugPrint` propio pasa a llamar a `ScormAPIWrapper.DebugPrint`.</li><li>`AddInteraction`: `x++` en los bucles, `.pattern`, timestamp por defecto → `DateTime.Now`. Orden de escritura: id, type, objectives, timestamp, correct_responses, weighting, learner_response, result, latency, description (`type` va antes de `response`/`pattern` para evitar el 408).</li><li>`UpdateInteraction`: null-checks y `.pattern`.</li><li>Todos los reales pasan por `ScormFormat.ToReal` y los enteros por `ToString(InvariantCulture)`.</li><li>Los timestamps pasan por `ToTimestamp`.</li><li>Los 3 `DateTime.Parse` pasan a `ParseTimestamp`.</li><li>`ParseFloat`, `ParseInt`, `StringTo*`, `CustomTypeToString` y las conversiones de tiempo delegan en `ScormFormat`. Firmas públicas intactas.</li><li>`ScormValueCallback` pasa a `[Obsolete]` sin efecto.</li><li>`IsLmsConnected` e `IsInitialized` son nuevos y aditivos.</li><li>`Scorm_Commit_Complete` usa `DontRequireReceiver`.</li><li>`FindManagerObject()` comprueba null.</li><li>`Terminate` envuelto en try/catch.</li><li>`[Preserve]` en `LogMessage`.</li></ul> |
| `_Scripts/StudentRecord.cs` | `ExitType { timeout, suspend, normal, logout }`: `logout` va al final. |
| `_Scripts/ControllerMain.cs` | <ul><li>Los `float.Parse`/`int.Parse` de la UI pasan a `TryParseUserReal`/`TryParse`.</li><li>`score.scaled` limitado a [-1,1] y sin cálculo si max = 0.</li><li>"Add Interaction" crea una interacción `true_false`. `learner_response` es `"true"`/`"false"` según el toggle Correct; el texto escrito se guarda en la descripción. `objectives[0]` es el primer objetivo existente y `correctResponses = [{pattern:"true"}]`.</li><li>`[Preserve]` en `Scorm_Initialize_Complete`, `Scorm_Commit_Complete` y `Log` (R5).</li><li>No se toca la escena.</li></ul> |
| `_Scripts/AnObjective.cs` | `ParseUserReal`. `scaled` limitado a [-1,1] y sin división por 0. |
| `Assets/Plugins/scorm.jslib` | Reescrito con retorno directo: `wglInitialize`, `wglIsApiFound`, `wglIsScorm2004`, `wglGetValue` (`_malloc` + `stringToUTF8`), `wglSetValue`, `wglCommit`, `wglTerminate`, `wglGetLastError`, `wglGetErrorString` (string), `wgldebugPrint`. Las funciones de JS se buscan como `window["doX"]` en el momento de la llamada, así que la minificación no puede romper la búsqueda. |
| `WebGLTemplates/SCORM/TemplateData/scorm.js` | <ul><li>Todos los `do*` son síncronos. Se añade `doIsApiFound`.</li><li>Comprobaciones con `=== "true"` mediante `ensureInitialized()`.</li><li>`findAPI` prefiere `API_1484_11` sobre `API` en cada ventana, con try/catch cross-origin; la búsqueda se hace una sola vez.</li><li>Sin API: respuesta inmediata `""`/`"false"`.</li><li>`doTerminate` no hace `window.close()` y tiene guarda `terminated`; no se puede re-inicializar.</li><li>`DebugPrint` usa `#console` si existe y, si no, `console.log`.</li><li>Fuera `scorm.foo` y `pausecomp`.</li><li>`visibilitychange→hidden` hace Commit.</li><li>`pagehide` con sesión abierta: escribe `session_time` (calculado) y `exit="suspend"` solo si Unity no los escribió; después Commit y Terminate.</li><li>Simulador solo con `?scormsim=1` **y** sin API de LMS.</li></ul> |
| `TemplateData/ScormSimulator.js` | <ul><li>`GetValue` usa `hasOwnProperty`: un valor vacío ya no da 401, y un elemento inexistente devuelve `""` con 401.</li><li>`SetValue` mantiene `_count`.</li><li>`GetLastError` devuelve string y `GetErrorString` da textos básicos.</li><li>La auto-activación sigue comentada.</li></ul> |
| `WebGLTemplates/SCORM/index.html` | <ul><li>`<title>` y `#unity-build-title` usan `{{{ PRODUCT_NAME }}}`; en el build sale "SCORM Testbed".</li><li>Viewport meta para todos.</li><li>Canvas a pantalla completa y responsive, con `<style>` inline: el `style.css` del paquete no define las reglas `#unity-*`.</li><li>Fuera `width=1920 height=1080`, `style.width/height` fijos y "Unity Web Player \|".</li></ul> |

### 2.3 Editor: `Assets/SCORM/Editor`

| Archivo | Cambio |
|---|---|
| **nuevo** `ScormEdition.cs` | `enum ScormEdition { Scorm2004_3rd, Scorm2004_4th }`. Va en su propio fichero por la convención de un tipo por fichero (ver §6). |
| **nuevo** `ScormPackageSettings.cs` | <ul><li>Campos del plan.</li><li>`FromPlayerSettings`, `FromPlayerPrefs` (claves del GUI originales más `Scorm_Edition` y `Completion_Threshold`) y `ApplyCommandLine`.</li><li>`Validate()`; `SanitizeIdentifier` (xs:ID/NCName).</li></ul> |
| **nuevo** `ScormManifestBuilder.cs` | `Build(settings, files)` con `XmlWriter`, UTF-8 sin BOM. <ul><li>`schemaversion` 3rd o 4th.</li><li>3rd: `<adlcp:completionThreshold>0.8</…>` si está configurado. 4th: atributos solo si `completedByMeasure` es true.</li><li>`timeLimitAction`, `dataFromLMS`, `imsss:sequencing/limitConditions` y LOM solo si se configuran.</li><li>`href` con `/` y URI-escaped; `index.html` primero; se excluyen `imsmanifest.xml` y `.meta`.</li></ul> |
| **nuevo** `ScormPackager.cs` | <ul><li>`ResolveXsdDirectory`: si `2004_3rd` falta o no tiene ningún `.xsd`, usa 4th y avisa.</li><li>`Package`: `ZipArchive`, sin `.meta` ni ocultos, comprobación de `\`, y no comprime lo que ya está comprimido.</li><li>`ValidateManifest`: `XmlSchemaSet` + `XmlUrlResolver` + `DtdProcessing.Parse` + `ReportValidationWarnings`. Los avisos de instancia cuentan como error; los avisos de compilación de las propias XSD de ADL no.</li><li>`ValidateZip`.</li></ul> |
| **nuevo** `ScormBuildCli.cs` | <ul><li>`BuildWebGL`, `Package`, `BuildAndPackage` y `ValidateZip`, todos con `EditorApplication.Exit(0/1)` en batchmode.</li><li>Argumentos del plan; además `-scormLanguage`.</li><li>Fija `template=PROJECT:SCORM`, Gzip y `decompressionFallback=true` (o `disabled`).</li><li>Limpia un build WebGL previo en la carpeta de salida solo si parece uno (tiene `index.html` y `Build/`); si la carpeta no está vacía y no lo parece, falla.</li><li>Registra los mensajes de error de `BuildReport.steps`.</li></ul> |
| `ScormExport.cs` | <ul><li>Fuera `System.Web`, `Ionic.Zip`, el diálogo WebPlayer y la lista `.unity3d`.</li><li>Popup de edición 3rd/4th.</li><li>3rd: campo Completion Threshold. 4th: `completedByMeasure`/`minProgressMeasure`.</li><li>"Folder Location" apunta al build WebGL (por defecto `Builds/WebGL`).</li><li>Botones "Publish" (→ `ScormPackager`) y "Build WebGL + Publish".</li><li>Fuera el `{3:F}` y la concatenación de float dependiente de la cultura.</li></ul> |
| **borrado** `Assets/SCORM/Plugins/Ionic.Zip.Reduced.dll` (+ `.meta`) | |
| **movido** `Assets/SCORM/Plugins/2004/` → `Assets/SCORM/Plugins/2004_4th/` (+ `.meta`) | Con `mv`, conservando todas las GUID |

### 2.4 Tests: `Assets/SCORM/Tests/Editor/` (Assembly-CSharp-Editor, sin asmdef)

- `ScormFormatTests.cs` (14 tests, con `es-ES`):
  - `ToReal`, incluido sin exponente
  - parsers
  - `ParseUserReal` con coma
  - `timeinterval` en ambos sentidos y round-trip
  - timestamp vacío y con offset/fracción
  - vocabulario: true-false, fill-in, long-fill-in, time-out, logout, no-credit, not attempted, "", exit,message, ab-initio
  - round-trip de enums
  - estimate numérico
- `ScormEditorBackendInteractionTests.cs` (8 tests, con `es-ES`):
  - carga del StudentRecord
  - `AddInteraction` con 2 objetivos y 2 correct_responses: escribe `objectives.1.id`, `correct_responses.1.pattern`, `_count`, `weighting "0.5"` y `latency P0DT0H0M18.4S`
  - timestamp por defecto
  - `UpdateInteraction` con colecciones null
  - `pattern` y no `pattrern`
  - setters invariantes
  - `suspend_data` con `|`
  - Commit y Terminate
- `ScormManifestBuilderTests.cs` (13 tests):
  - 3rd (elemento decimal) y 4th (atributos)
  - elementos opcionales omitidos o presentes
  - escapado `&<>"`
  - hrefs (`/`, `%20`, sin manifest ni `.meta`)
  - sin BOM
  - **3rd y 4th válidos contra las XSD de `Plugins/2004_4th`**
  - 3rd contra las XSD de `2004_3rd`: `Assert.Ignore` mientras no existan
  - **dos controles negativos**: `scormType` inválido + `timeLimitAction` inválido, y elemento desconocido
  - fallback de `ResolveXsdDirectory`: falta / sin `.xsd` / con `.xsd` / 4th
  - `Package` produce un zip con manifest y XSD en la raíz, sin `\`, `.meta` ni ocultos, y lo valida `ValidateZip`
- `Packages/manifest.json`: `"com.unity.test-framework": "1.6.0"`. Es la versión incluida en la instalación de 6000.3.21f1 (`BuiltInPackages`). Unity actualizó `packages-lock.json`.

### 2.5 Otros

- `../.gitignore` (raíz del repo): `+Unity3D-Project/[Bb]uilds/`
- `ProjectSettings/ProjectSettings.asset` (binario, 80980 → 80976 bytes): el build CLI persiste `template=PROJECT:SCORM`, Gzip y `decompressionFallback`. El build también dejó la plataforma activa en **WebGL**. Es lo aceptado en el plan (R6).
- `.harness/progress/current.md`: progreso anotado.
- `.harness/progress/visual_check_scorm2004-webgl-e2e-build.md`: creado sin marcar ni firmar.

## 3. Compilación

| Log (`Builds/logs/`) | Momento | Resultado |
|---|---|---|
| `compile0.log` | Línea base tras sincronizar el paquete | OK, 2 × CS0414 |
| `compile1.log` | Runtime C# | OK, 1 × CS0414 (preexistente) |
| `compile2.log` | ControllerMain y AnObjective | OK |
| `compile3.log` | Bloque Editor + Ionic borrado + XSD movidas | OK. Assembly-CSharp-Editor compilado |
| `build_3rd.log` | Build WebGL (IL2CPP) | Succeeded |
| `compile4.log` | Tras borrar el `ExternalCall` comentado (plataforma WebGL activa, así que también compila con `UNITY_WEBGL` en el Editor) | OK, 1 × CS0414 |

## 4. Tests y `init.sh`

- Comando: `Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/logs/editmode-results.xml`.
- Resultado final (`Builds/logs/editmode-results.xml`, después de todas las ediciones): total 35, **passed 34, failed 0, skipped 1** (`Build_3rd_IsValidAgainst3rdEditionXsd_WhenAvailable`, Ignore porque no hay XSD 3rd).
- Primera ejecución: 2 fallos. Los avisos de compilación de las XSD LOM de ADL ("Empty choice cannot be satisfied…") se contaban como errores. Se corrigió en `ValidateManifest`: los avisos de compilación de schema se ignoran y los de instancia siguen contando como error.
- `.harness/init.sh`: exit 0. Solo hay avisos de VCS (`vcs_verified` false). Ver §7.1 sobre su paso 7.

## 5. Build, zips y validación

**Build** (en background, 3 min 33 s la primera vez con cambio de plataforma):

```
"C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe" -batchmode -quit -nographics -projectPath . -buildTarget WebGL \
  -executeMethod ScormBuildCli.BuildAndPackage -scormEdition 3rd -scormZip Builds/SCORM_TestApp_2004_3rd.zip \
  -scormIdentifier com.invelon.scormtestapp -scormTitle "SCORM Test App" -scoTitle "Test App" \
  -webglCompression gzip-fallback -logFile Builds/logs/build_3rd.log
… -executeMethod ScormBuildCli.Package -scormEdition 4th -scormZip Builds/SCORM_TestApp_2004_4th.zip (mismos -scorm*) -logFile Builds/logs/package_4th.log
… -executeMethod ScormBuildCli.ValidateZip -scormZip Builds/SCORM_TestApp_2004_<3rd|4th>.zip -logFile Builds/logs/validate_<ed>.log
```

**Contenido de `Builds/WebGL`** (9.312.297 bytes):
- `index.html`: 0 marcadores `{{{` y título "SCORM Testbed".
- `Build/WebGL.{loader.js, data.unityweb, framework.js.unityweb, wasm.unityweb}`. El wasm ocupa 7,6 MB; los `.unityweb` son gzip (`1f 8b`).
- `TemplateData/*` (13 ficheros).

**El jslib está enlazado.** En el `framework.js.unityweb` descomprimido aparecen los 10 símbolos `wgl*` (`_wglGetValue` con `_malloc` + `stringToUTF8`) y ningún `wgldoGetValue` antiguo.

**Zips** (raíz con `imsmanifest.xml` y XSD; 55 entradas; 18 `<file>`):
- `Builds/SCORM_TestApp_2004_3rd.zip`: 9.076.777 bytes. `schemaversion` "2004 3rd Edition". Lleva las XSD **4th**: el fallback avisó con `SCORM 2004 3rd Edition XSD files not found …` (§8, R9).
- `Builds/SCORM_TestApp_2004_4th.zip`: 9.076.777 bytes. "2004 4th Edition". Los dos manifests solo difieren en la línea `schemaversion`.

**Validación**:

| Vía | 3rd | 4th | Control negativo |
|---|---|---|---|
| `ScormBuildCli.ValidateZip` (Unity / .NET) | `SCORM_VALIDATE_OK`, 0 errores XSD, 0 hrefs ausentes | `SCORM_VALIDATE_OK` | Zip con `scormType="bogus"` y `<file href="missing.js">`: **exit 1**, detecta los 2 problemas |
| PowerShell 5.1 (.NET Framework `XmlSchemaSet`, script en el scratchpad `validate_scorm_zip.ps1`) | `PS_VALIDATE_OK`, 0 problemas | `PS_VALIDATE_OK` | `-Corrupt scormType`: `PS_VALIDATE_FAILED` con un error de Enumeration |

Nota: la primera ejecución en PowerShell falló entera por un bug del script. `$null` se pasaba como `""` a `XmlSchemaSet.Add(string, …)`; se corrigió con `[NullString]::Value`.

**Smoke test HTTP** (§3.8 del plan):
- `node smoke_http.js Builds/WebGL <imsmanifest del zip 3rd>` sirve los ficheros **sin Content-Encoding**, como Moodle `pluginfile.php`.
- **18/18** `<file href>` devuelven 200 con el tamaño exacto del disco, y `index.html?scormsim=1` devuelve 200.
- No es posible ejecutar WebGL en un navegador desde el agente; eso queda para el visual check A.

**Tests JS headless** (`node scorm_js_test.js` en el scratchpad; `vm` con `window`/`document` falsos):
- Sin LMS ni `scormsim`: Initialize `"false"`, `""` inmediato y no se instala ningún simulador.
- Con `?scormsim=1`: se instala el simulador. `a|b|c` llega íntegro. Un valor vacío no da 401 y uno inexistente da 401. `_count` crece. Terminate es idempotente y no se puede re-inicializar.
- Con LMS real que expone `API` y `API_1484_11`, y además `scormsim=1`: gana el LMS y se elige 2004. `pagehide` escribe `exit=suspend` y `session_time` y hace Commit y Terminate.
- Si Unity ya fijó `exit=normal` y `session_time`, `pagehide` no los sobrescribe. Si ya está terminado, `pagehide` no hace nada.
- `visibilitychange→hidden` hace Commit.

## 6. Desviaciones respecto al plan

1. **`ScormEdition.cs` va en un fichero aparte** de `ScormPackageSettings.cs`, por la convención de un tipo público por fichero. Además existe la clase `ScormPackageResult` dentro de `ScormPackager.cs`; es un DTO pequeño del packager.
2. **`index.html` lleva un `<style>` inline** para el layout responsive. El `style.css` del paquete es el de la plantilla antigua (`.webgl-content`) y no define `#unity-*`. El plan solo mencionaba `index.html`; no se ha tocado `style.css`.
3. **`ControllerMain` "Add Interaction"**: como el tipo es `true-false`, `learner_response` sale del toggle Correct y el texto escrito por el usuario va a la descripción, `"<desc> (response: <texto>)"`. Si no, el LMS daría 406.
4. **`[Preserve]`** en los 4 receptores de `SendMessage`/`BroadcastMessage`, como mitigación preventiva de R5. No se ha observado stripping.
5. **`ScormAPIWrapper.Commit()`/`Terminate()` devuelven `bool`** en vez de `void`. Es compatible en código fuente, no en binario.
6. **`ScormSimulator.js`**: además de la corrección de `GetValue` del plan, `SetValue` mantiene `_count`, para que en el modo `?scormsim=1` las interacciones y objetivos nuevos se vean, y `GetLastError` devuelve string.
7. **No se ha creado `Plugins/2004_3rd/`** (vacía). Git no versiona carpetas vacías y Unity generaría un `.meta` huérfano. El packager y el test lo tratan como "falta", y el humano la creará al añadir las XSD.
8. **`ValidateManifest` ignora los avisos de compilación de las propias XSD** de ADL (LOM: "Empty choice…"). Los avisos de validación de instancia, como "could not find schema information", siguen contando como error.
9. **La plataforma activa del Editor local ha cambiado a WebGL** por `-buildTarget WebGL`. Es estado local (`Library/`), no un cambio versionado. Al abrir el Editor, el proyecto estará en WebGL.

## 7. Problemas abiertos

1. **`.harness/init.sh` paso 7 es un falso verde.** Lanza `-runTests … -quit`, y con `-quit` Unity sale antes de ejecutar los tests: exit 0 y no se escribe `.harness/progress/editmode-results.xml`. Hay que quitar `-quit` en `init.sh` (línea ~431). Es harness, fuera de esta feature; lo resuelve el leader. Mis resultados reales están en `Builds/logs/editmode-results.xml`.
2. **"1 errors" en el primer build.** El `BuildSummary` del primer build en frío (cambio de plataforma a WebGL) dijo `Succeeded, 1 errors`, pero el log no contiene ningún error de build; solo hay errores de Licensing (`Unsupported protocol version '1.18.1'`) y de `usbmuxd`. Añadí el volcado de los mensajes de `BuildReport.steps` y repetí el build: `Succeeded, 0 errors, 0 warnings`. Fue incremental (3 s), con el mismo resultado de tamaño. Probablemente era el error de Licensing registrado durante el build; a vigilar en el próximo build en frío.
3. **`LightingData.asset` generado por el build.** El build creó `Assets/SCORM/_Scenes/TestApp/LightingData.asset` (+ `.meta`). Lo borré: la escena no lo referencia y sigue idéntica byte a byte a la del paquete. Volverá a aparecer en cada build. Se puede añadir a `.gitignore`, o abrir la escena en el Editor y desactivar Auto Generate lighting (decisión humana).
4. **XSD 3rd Edition pendientes (R9).** El zip 3rd lleva XSD 4th y el packager avisa. Cuando el humano copie el set ADL 3rd en `Assets/SCORM/Plugins/2004_3rd/`, el packager lo usará automáticamente y el test que hoy se ignora pasará a ejecutarse.
5. **Límite de subida del Moodle del cliente sin confirmar.** El zip pesa unos 9,1 MB.
6. **Pruebas que solo puede hacer el humano.** Navegador real, SCORM Cloud, Moodle y `pagehide` en Chrome (R4): ver `visual_check_scorm2004-webgl-e2e-build.md`.
7. **Ficheros sin trackear que necesitan decisión del leader para el commit**: `Assets/Plugins*`, `Assets/WebGLTemplates*`, `Assets/SCORM/Plugins/2004_4th*`, `Assets/SCORM/Tests*` y los `.cs` nuevos con sus `.meta`. Los borrados de `Plugins/2004/**`, Ionic y `WebPlayerTemplates/**` deben ir en el mismo commit que sus sustitutos. `Builds/` queda ignorado.

## 8. Artefactos de verificación (no versionados)

- `Builds/logs/`: `compile0-3.log`, `build_3rd.log`, `package_4th.log`, `validate_3rd.log`, `validate_4th.log`, `validate_negative.log`, `tests*.log`, `editmode-results.xml`.
- Scratchpad de la sesión: `validate_scorm_zip.ps1`, `smoke_http.js`, `scorm_js_test.js`.
