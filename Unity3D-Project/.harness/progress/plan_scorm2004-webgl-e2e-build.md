# Plan — F001 `scorm2004-webgl-e2e-build`

Estado: **pendiente de aprobación humana** (`requires_plan_approval: true`).
No se ha tocado ningún `.cs`, `.js`, `.jslib`, escena ni prefab.

Rutas reales (de `.harness/unity.config.json`):
- `scripts_root` = `Assets/SCORM/_Scripts` (runtime) y `Assets/SCORM/Editor` (editor)
- `tests_root` = `Assets/SCORM/Tests` (todavía no existe)

---

## 0. Lo que se ha comprobado

| Hecho | Evidencia |
|---|---|
| `Assets/` del repo es la versión Web Player: `Application.ExternalCall`, `Thread.Sleep`, `Assets/WebPlayerTemplates/SCORM`, sin jslib. La escena y los prefabs son **binarios de Unity 5.0.2f1**. | `grep ExternalCall`; cabecera de `TestApp.unity` |
| El paquete `Unity 6 LTS-SCORM-Integration-Kit-WEBGL.unitypackage` trae la escena y los prefabs en **YAML**, más `Plugins/scorm.jslib` y `WebGLTemplates/SCORM/**`. | Paquete extraído en el scratchpad |
| GUIDs: los 9 scripts, prefabs y escena **coinciden** entre paquete y repo; las XSD, Ionic y Loading.png son **idénticas byte a byte**. Solo difiere `Assets/SCORM.meta` (carpeta: se conserva la del repo, nadie la referencia). Son nuevos `Assets/Plugins/**` y `Assets/WebGLTemplates/**`. Solo existen en el repo los 8 `.meta` de `WebPlayerTemplates`. | Comparación de GUIDs |
| Todas las GUIDs externas de la escena y prefabs del paquete (Text, Image, Button, Toggle, InputField, Slider, ScrollRect, EventSystem…) resuelven en `Library/PackageCache/com.unity.ugui@e20f1880fa04`. No quedarán scripts perdidos. | `grep` sobre los `.meta` de PackageCache |
| El último compilado en 6.3 solo da avisos CS0618: `Ionic.Zip.Reduced.dll` y `using System.Web` compilan. Hay un shim `System.Web.dll` en NetStandard 2.1. | `%LOCALAPPDATA%/Unity/Editor/Editor.log` |
| `Ionic.Zip.Reduced.dll.meta` tiene `Any: enabled: 1`, así que entra también en el player WebGL. | `.meta` |
| Las XSD de `Assets/SCORM/Plugins/2004` son de **SCORM 2004 4th Edition**. El historial de `adlcp_v1p3.xsd` dice "2008-03-12 Added completedByMeasure, minProgressMeasure, progressWeight" y "2009-30-01 Added data, map, sharedDataGlobalToSystem". En 4th, `completionThresholdType` es simpleContent `xs:string` con atributos. | `adlcp_v1p3.xsd` |
| `xml.xsd` lleva `<!DOCTYPE ... "XMLSchema.dtd">`: el validador necesita `DtdProcessing.Parse`. | `xml.xsd` |
| El módulo WebGL está instalado en 6000.3.21f1. | `PlaybackEngines/WebGLSupport` |
| `Packages/manifest.json` no incluye `com.unity.test-framework`. | `manifest.json` |
| **Unity está abierto ahora mismo con este proyecto.** El batchmode aborta con "another Unity instance is running". | `init.sh` |
| `init.sh` también está en rojo porque `scripts_root`/`tests_root` ocupan varias líneas en `unity.config.json`. Eso es del leader, fuera de esta feature. | `init.sh` |
| `EditorBuildSettings.asset` (binario) solo contiene `Assets/SCORM/_Scenes/TestApp.unity`. | `EditorBuildSettings.asset` |

---

## 1. Decisiones de diseño (propuestas; las abiertas están en §7)

1. **jslib con retorno directo (síncrono).**
   - La API SCORM de JS es síncrona, así que `scorm.jslib` devuelve el valor directamente con `lengthBytesUTF8+1`, `_malloc` y `stringToUTF8`. IL2CPP libera el `char*` que devuelve un `extern string`; es el patrón documentado por Unity.
   - Desaparecen `SendMessage`, la cola, el polling y `Thread.Sleep`. El paquete todavía usa `Thread.Sleep` en `ScormAPIWrapper.WaitForReturn`, lo que incumple la acceptance #1.
   - Desaparece la carrera con `unityInstance` y el timeout de 15 s cuando no hay API.
   - Desaparece el truncado por `|`: el protocolo `result|code|string|key` cortaba cualquier `suspend_data` o `learner_response` que contuviera `|`.
   - Los códigos de error se obtienen con exports aparte: `GetLastError` y `GetErrorString`.
   - Se mantiene la API pública de `ScormManager`, el mensaje `Scorm_Initialize_Complete` y `Scorm_Commit_Complete`.
   - `ScormAPIWrapper.APICallResult`, `ScormAPIWrapper.SetCallbackValue` y `ScormManager.ScormValueCallback` se conservan como `[Obsolete]` sin efecto, para no romper código de terceros.
2. **Backend en el Editor.**
   - Las llamadas `[DllImport("__Internal")]` van dentro de `#if UNITY_WEBGL && !UNITY_EDITOR`.
   - En el Editor y en otras plataformas se usa un backend C# en memoria (`ScormEditorBackend`) con los mismos datos que `ScormSimulator.js`. Así Play Mode en `TestApp` funciona sin `EntryPointNotFoundException`.
3. **Helper puro `ScormFormat`.**
   - Es una clase estática nueva en runtime con toda la conversión: real con `CultureInfo.InvariantCulture`, parseo tolerante, `timeinterval` en ambos sentidos, timestamp con `RoundtripKind` (vacío → `DateTime.MinValue`) y enum ↔ vocabulario SCORM.
   - `ScormManager` delega en ella. Se puede testear sin las funciones externas.
4. **Vocabulario sin romper la API.**
   - No se renombra ningún enum. `CustomTypeToString` pasa a un mapeo explícito:
     - `true_false` → `true-false`
     - `fill_in` → `fill-in`
     - `long_fill_in` → `long-fill-in`
     - `ExitType.timeout` → `time-out`
     - `CreditType.no_credit` → `no-credit`
     - `TimeLimitActionType` → `exit,message` y los demás
   - Se añade `logout` **al final** de `ExitType`. El resto sigue igual: `not_attempted` → `not attempted` y `not_set` → `""`.
5. **Manifest con `XmlWriter`.** UTF-8 sin BOM, escapado automático y números con InvariantCulture. El edition se elige con un enum `ScormEdition { Scorm2004_3rd (por defecto), Scorm2004_4th }`.
6. **Zip con `System.IO.Compression.ZipArchive`.**
   - Las entradas se crean a mano con separador `/`, y `imsmanifest.xml` y las XSD van en la raíz.
   - Se borra `Ionic.Zip.Reduced.dll`. Hoy entra en el player WebGL y no aporta nada.
   - Se quita `using System.Web`, que no se usa. Es la decisión abierta §7-5.
7. **Build por CLI** en `Assets/SCORM/Editor/ScormBuildCli.cs`, con salida en `Builds/` (fuera de `Assets/`).
   - Plantilla `PROJECT:SCORM`.
   - Compresión según la decisión §7-2.
   - La carpeta de salida es `Builds/WebGL`. Unity nombra `Build/*` como la carpeta, así que no hay espacios en los href.

---

## 2. Archivos

### 2.1 Sincronizar con el paquete (copia byte a byte de `asset` y `asset.meta`)

El origen es `…/scratchpad/pkg/<guid>/`. Si el scratchpad se ha borrado, se vuelve a extraer con `tar -xzf "../Unity 6 LTS-SCORM-Integration-Kit-WEBGL.unitypackage"`.

| Acción | Ruta | Nota |
|---|---|---|
| reemplazar | `Assets/SCORM/_Scripts/{ScormManager,ScormAPIWrapper,StudentRecord,ControllerMain,AnObjective}.cs` (+ `.meta`) | Base para las correcciones de §2.2 |
| reemplazar | `Assets/SCORM/Editor/ScormExport.cs` (+ `.meta`) | Base para §2.3 |
| reemplazar | `Assets/SCORM/_Scenes/TestApp.unity` (+ `.meta`) | Binario U5 → YAML del paquete, **misma GUID**. El cambio visual es el que trae el paquete: misma UI de prueba, pero con uGUI 2.0 |
| reemplazar | `Assets/SCORM/_Prefabs/AnObjective.prefab`, `Assets/SCORM/Resources/ScormManager.prefab` (+ `.meta`) | Ídem |
| crear | `Assets/Plugins.meta`, `Assets/Plugins/scorm.jslib` (+ `.meta`, WebGL only) | Se reescribe en §2.2 |
| crear | `Assets/WebGLTemplates.meta`, `Assets/WebGLTemplates/SCORM/**` (+ `.meta`): `index.html`, `thumbnail.png`, `TemplateData/{scorm.js, ScormSimulator.js, UnityProgress.js, style.css, favicon.ico, fullscreen.png, webgl-logo.png, progress*.png}` | `UnityProgress.js` se queda (no se usa) por fidelidad al paquete |
| borrar | `Assets/WebPlayerTemplates/**` y sus 8 `.meta` (`WebPlayerTemplates.meta`, `SCORM.meta`, `scripts.meta`, `index.html.meta`, `scorm.js.meta`, `ScormSimulator.js.meta`, `thumbnail.png.meta`) | Lo pide la acceptance #1 |
| no tocar | `Assets/SCORM.meta`, XSD, `_Images` | Idénticos o irrelevantes |

### 2.2 Runtime — `Assets/SCORM/_Scripts` (scripts_root[0]) y plantilla WebGL

| Archivo | Cambios |
|---|---|
| **nuevo** `Assets/SCORM/_Scripts/ScormFormat.cs` | Clase estática pública con estos métodos:<ul><li>`ToReal(float)`: `"R"`/`"0.#######"` con Invariant.</li><li>`ParseReal(string)`: Invariant; vacío o inválido → 0.</li><li>`ParseInt`.</li><li>`ParseUserReal(string)`: Invariant y, si falla, CurrentCulture. Es para lo que escribe el usuario en la UI.</li><li>`SecondsToTimeInterval(float)`: `P{d}DT{h}H{m}M{s.##}S` Invariant, conserva la fracción. El código actual hacía `{3:F}` sobre un `int`, que en es-ES da `18,00`.</li><li>`TimeIntervalToSeconds(string)`: Invariant y tolerante a null.</li><li>`ToTimestamp(DateTime)`: `yyyy-MM-ddTHH:mm:ss`.</li><li>`ParseTimestamp(string)`: `TryParse` Invariant con `RoundtripKind`; vacío o inválido → `DateTime.MinValue`.</li><li>`ToVocabulary(...)`, una sobrecarga por enum.</li><li>`StringTo*`, migrados desde `ScormManager`.</li></ul> |
| **nuevo** `Assets/SCORM/_Scripts/ScormEditorBackend.cs` | Diccionario en memoria con los datos de `ScormSimulator.js` y el error 401 si el elemento no existe. Solo se usa con `!(UNITY_WEBGL && !UNITY_EDITOR)`. |
| `ScormAPIWrapper.cs` | Sustituye las 6 funciones externas por `wglInitialize():int`, `wglIsApiFound():int`, `wglIsScorm2004():int`, `wglGetValue(id):string`, `wglSetValue(id,val):int`, `wglCommit():int`, `wglTerminate():int`, `wglGetLastError():int`, `wglGetErrorString(code):string` y `wgldebugPrint`.<br>`GetValue`/`SetValue` devuelven al instante.<br>Elimina `WaitForReturn`, la cola, `lock` y `Random`.<br>Nueva propiedad pública `IsApiFound`, aditiva.<br>Mantiene `IsScorm2004` y `Log()`. `Log` comprueba null antes de `GameObject.Find`. |
| `ScormManager.cs` | <ul><li>`AddInteraction`: `x++` en los dos bucles internos; `correct_responses.N.pattern`; si `timeStamp == default` usa `DateTime.Now`, porque SCORM exige un año ≥ 1970.</li><li>`UpdateInteraction`: null-check de `objectives` y `correctResponses`, y `pattern`.</li><li>Todo `ToString()` numérico pasa a `ScormFormat.ToReal`.</li><li>`ParseFloat`/`ParseInt`, `float.TryParse` en `StringToResultType` y `timeIntervalToSeconds` delegan en `ScormFormat`.</li><li>Los 3 `DateTime.Parse` de `LoadStudentRecord` pasan a `ScormFormat.ParseTimestamp`.</li><li>`CustomTypeToString` delega en `ScormFormat.ToVocabulary`. Se mantiene la firma `public static string CustomTypeToString(object)`.</li><li>`ScormValueCallback` queda `[Obsolete]` sin efecto.</li><li>Nuevo `public static bool IsLmsConnected`, aditivo.</li><li>`BroadcastMessage("Scorm_Commit_Complete", DontRequireReceiver)`.</li><li>Comprueba null en `GameObject.Find(objectName)`.</li></ul> |
| `StudentRecord.cs` | `ExitType { timeout, suspend, normal, logout }`: solo se añade `logout` al final. |
| `ControllerMain.cs` (escena de prueba) | <ul><li>`float.Parse`/`int.Parse` de la UI pasan a `ScormFormat.ParseUserReal`/TryParse.</li><li>`score.scaled` se limita a [-1,1] y se protege la división por 0.</li><li>**Para que el visual check recorra los bugs corregidos**, `ButtonAddInteractionPressed` añade a la interacción un `objectives[0]` (el primer objetivo existente, si lo hay) y `correctResponses = [{pattern: "true"}]`, con `type = true_false` y `learner_response` `"true"`/`"false"` según el toggle "Correct".</li><li>Solo es código. **No se edita la escena**: se reutilizan los campos existentes.</li></ul> |
| `AnObjective.cs` | `float.Parse` → `ScormFormat.ParseUserReal`; `scaled` limitado a [-1,1] y sin división por 0. |
| `Assets/Plugins/scorm.jslib` | Reescrito con retorno directo. Las funciones de JS se resuelven en tiempo de llamada, porque `scorm.js` se carga en `<head>` antes del loader. |
| `Assets/WebGLTemplates/SCORM/TemplateData/scorm.js` | <ul><li>`doInitialize` devuelve un booleano real y la comprobación es `=== "true"` donde llega como string.</li><li>`findAPI` **prefiere `API_1484_11`** sobre `API` en la misma ventana y protege con try/catch el acceso a `parent` cross-origin.</li><li>`doGetValue`/`doSetValue` devuelven su valor (`""`/`"false"`) al instante si no hay API.</li><li>`doTerminate` sin `window.close()` y con guarda `terminated`, para que nunca se termine dos veces.</li><li>`DebugPrint` usa `#console` si existe y, si no, `console.log`.</li><li>Se elimina el `scorm.foo` muerto y `pausecomp`.</li><li>Registra si Unity ha escrito `cmi.exit` y `cmi.session_time`, y apunta la hora de Initialize.</li><li>`visibilitychange` → `hidden` ⇒ `Commit`. Aquí el XHR síncrono todavía está permitido; en `pagehide` Chrome lo bloquea.</li><li>`pagehide` ⇒ si sigue inicializado y no terminado: fija `cmi.session_time` (calculado) y `cmi.exit` (ver §7-4) si Unity no los fijó, y luego `Commit` + `Terminate`.</li><li>Simulador **solo por opt-in explícito** (§7-4): solo si `?scormsim=1` está en la URL **y** no se encuentra API del LMS. Nunca sombrea un LMS real.</li></ul> |
| `TemplateData/ScormSimulator.js` | Sin auto-activación (se mantiene comentada). Se corrige `GetValue` para que devuelva `""` sin error 401 cuando el valor existe pero está vacío; hoy `if(result)` lo trata como error. |
| `Assets/WebGLTemplates/SCORM/index.html` | <ul><li>`<title>{{{ PRODUCT_NAME }}}</title>` y `#unity-build-title` con `{{{ PRODUCT_NAME }}}`.</li><li>Canvas responsive: el contenedor ocupa el 100% del viewport y se quitan `width=1920 height=1080` y `style.width/height` fijos. Se mantiene `matchWebGLToCanvasSize`.</li><li>Se quita "Unity Web Player |".</li></ul> |

### 2.3 Editor — `Assets/SCORM/Editor` (scripts_root[1])

| Archivo | Contenido |
|---|---|
| **nuevo** `ScormPackageSettings.cs` | Clase serializable con estos campos:<ul><li>edition, identifier, courseTitle, courseDescription, scoTitle, launchData</li><li>completionThreshold (3rd, nullable), completedByMeasure, minProgressMeasure (4th)</li><li>timeLimitAction (enum o "" ), timeLimitSecs</li><li>language (por defecto `en-US`)</li></ul>Se carga desde PlayerPrefs (las claves actuales del GUI, para compatibilidad) y desde argumentos de CLI.<br>La validación del identifier como `xs:ID`/NCName la sanea o falla con un mensaje claro. |
| **nuevo** `ScormManifestBuilder.cs` | Clase estática pura `Build(settings, IEnumerable<string> relativeFiles) → string` con `XmlWriter` (UTF-8 sin BOM).<ul><li>`schemaversion`: `2004 3rd Edition` o `2004 4th Edition`.</li><li>3rd: `<adlcp:completionThreshold>0.8</adlcp:completionThreshold>` solo si está configurado.</li><li>4th: `<adlcp:completionThreshold completedByMeasure="…" minProgressMeasure="…"/>` solo si `completedByMeasure` es true.</li><li>`adlcp:timeLimitAction`, `adlcp:dataFromLMS` e `imsss:sequencing/limitConditions` **solo si se configuran**.</li><li>`<file href>` = todos los ficheros del build WebGL (relativos, `/`, URI-escaped) más `imsmanifest.xml` excluido.</li><li>Recurso `href="index.html"`.</li><li>Orden de los hijos de `<item>` según el CAM: `title`, `timeLimitAction`, `dataFromLMS`, `completionThreshold`, `sequencing`.</li></ul> |
| **nuevo** `ScormPackager.cs` | `Package(buildDir, zipPath, settings)`: `ZipArchive` con el contenido de `buildDir` + `imsmanifest.xml` + las XSD **de la edición elegida** en la raíz. Recorre ficheros y excluye `*.meta` y ocultos. Al terminar comprueba que ninguna entrada tiene `\`.<br>`ValidateManifest(manifestPath, xsdDir) → List<string>` con `XmlSchemaSet` + `XmlUrlResolver` + `DtdProcessing.Parse`. |
| **nuevo** `ScormBuildCli.cs` | Puntos de entrada para `-executeMethod`; todos hacen `EditorApplication.Exit(0/1)`.<ul><li>`ScormBuildCli.BuildWebGL`: fija `PlayerSettings.WebGL.template="PROJECT:SCORM"`, la compresión y `decompressionFallback`, y lanza `BuildPipeline.BuildPlayer` con las escenas habilitadas de EditorBuildSettings (si no hay ninguna, `TestApp.unity`) hacia `-scormBuildDir` (por defecto `Builds/WebGL`). Si `report.summary.result != Succeeded` → Exit(1).</li><li>`ScormBuildCli.Package`: empaqueta un build existente.</li><li>`ScormBuildCli.BuildAndPackage`: los dos anteriores.</li><li>`ScormBuildCli.ValidateZip`: extrae a una carpeta temporal y valida el manifest contra las XSD incluidas.</li></ul>Argumentos: `-scormEdition 3rd|4th`, `-scormZip`, `-scormBuildDir`, `-scormIdentifier`, `-scormTitle`, `-scormDescription`, `-scoTitle`, `-scormLaunchData`, `-scormCompletionThreshold`, `-scormCompletedByMeasure`, `-scormMinProgressMeasure`, `-scormTimeLimitAction`, `-scormTimeLimitSecs`, `-webglCompression disabled|gzip-fallback`. |
| `ScormExport.cs` (ventana) | <ul><li>Quita `using System.Web`, `using Ionic.Zip`, el diálogo "export as WebPlayer" y la lista de ficheros `.unity3d`/`scripts/*.js`.</li><li>Añade un popup Edition (3rd/4th) y el campo Completion Threshold (3rd) o los existentes completedByMeasure/minProgressMeasure (4th).</li><li>"Folder Location" apunta al build WebGL.</li><li>Botones nuevos "Publish" (→ `ScormPackager`) y "Build WebGL + Publish" (→ `ScormBuildCli` sin Exit).</li><li>`SecondsToTimeInterval`/`ParseFloat` usan `ScormFormat`/Invariant. Elimina el `{3:F}` y el `PlayerPrefs.GetFloat` concatenado, que en es-ES daba `0,8` en el manifest.</li></ul> |
| **borrar** `Assets/SCORM/Plugins/Ionic.Zip.Reduced.dll` (+ `.meta`) | Si se aprueba §7-5 |

### 2.4 XSD por edición

- **4th** (lo que hay hoy): se mueve `Assets/SCORM/Plugins/2004/*` → `Assets/SCORM/Plugins/2004_4th/` **con sus `.meta`**. Solo es una ruta: no hay referencias por GUID.
- **3rd**: `Assets/SCORM/Plugins/2004_3rd/` con el set oficial de **ADL SCORM 2004 3rd Edition**:
  - `imscp_v1p1.xsd`, `adlcp_v1p3.xsd`, `adlseq_v1p3.xsd`, `adlnav_v1p3.xsd`, `imsss_v1p0*.xsd`, `ims_xml.xsd`, `xml.xsd`, `XMLSchema.dtd`, `datatypes.dtd`, `lom.xsd` y `common/`, `extend/`, `unique/`, `vocab/`.
  - Se distribuye en el "SCORM 2004 3rd Edition Content Packaging" o "Sample/Conformance Test Suite" de ADL (adlnet.gov), y también en scorm.com → "Schema definition files".
  - **Hay que conseguirlo**: no está en el repo. Es la decisión §7-3.
  - Validar un manifest 3rd contra las XSD 4th **no prueba conformidad 3rd**, porque en 4th `completionThreshold` es string y acepta `0.8`.

### 2.5 Tests — `Assets/SCORM/Tests` (tests_root)

Según §7-5:
- Añadir `com.unity.test-framework` a `Packages/manifest.json`.
- Crear tests EditMode **sin asmdef** en `Assets/SCORM/Tests/Editor/`. Así compilan en Assembly-CSharp-Editor y ven Assembly-CSharp; un asmdef de tests no puede referenciar Assembly-CSharp.
- Tests previstos:
  - `ScormFormatTests.cs`, con `CurrentCulture = es-ES` forzado en `SetUp`:
    - `ToReal(0.5f) == "0.5"`
    - `SecondsToTimeInterval(18.4f) == "P0DT0H0M18.4S"`
    - `ParseReal("0.9") == 0.9f`
    - `ParseTimestamp("")` = MinValue, sin excepción
    - `ParseTimestamp("2015-09-07T09:00:00.5+02:00")`
    - mapeos de vocabulario: true-false, fill-in, long-fill-in, time-out, logout, no-credit, not attempted, ""
  - `ScormManifestBuilderTests.cs`:
    - 3rd → `schemaversion` 3rd y `completionThreshold` como elemento decimal
    - 4th → atributos
    - sin limitConditions ni timeLimitAction si no se configuran
    - `&<>"` escapados
    - manifest válido contra las XSD de `Plugins/2004_4th` (y de `2004_3rd` cuando estén)
  - `ScormEditorBackendInteractionTests.cs`: `AddInteraction` con 2 objetivos y 2 correct_responses termina, y escribe `…objectives.1.id` y `…correct_responses.1.pattern`. Usa `ScormManager` con el backend de Editor.
- `.harness/init.sh` corre `-runTests -testPlatform EditMode`. Sin test-framework, ese paso no puede ponerse verde.

### 2.6 Otros

- `D:/Dev/Github/Unity-SCORM-Integration-Kit/.gitignore` (raíz del repo): añadir `Unity3D-Project/[Bb]uilds/`.
- `ProjectSettings/ProjectSettings.asset`: el build CLI fija `template`, `compressionFormat` y `decompressionFallback`, y Unity los **persiste**. Se acepta ese diff para que el build desde GUI también use la plantilla SCORM. Ver el riesgo R6.
- No se toca `.harness/deploy/unity_package/com.harness.build/Editor/BuildCli.cs`, que es solo Android/iOS.
- No se toca `unity.config.json`.

---

## 3. Orden de implementación

0. **El humano cierra Unity.** Si no, el batchmode no arranca y el Editor abierto podría sobrescribir `TestApp.unity` al guardar.
1. Script de sincronización (§2.1) desde el scratchpad: copia `asset` y `asset.meta`, borra `WebPlayerTemplates`. Comprobación: `git status` y la tabla de GUIDs vuelve a salir como SAME.
2. Compilación en batchmode (`-batchmode -quit -projectPath . -logFile Builds/logs/compile.log`) sobre el paquete tal cual. Línea base: debe compilar, aunque sea con avisos.
3. Runtime: `ScormFormat` → `StudentRecord` → `ScormManager` → `ScormEditorBackend` + `ScormAPIWrapper` → `scorm.jslib` → `scorm.js`/`ScormSimulator.js`/`index.html` → `ControllerMain`/`AnObjective`. Compilación tras cada bloque.
4. Editor: `ScormPackageSettings` → `ScormManifestBuilder` → `ScormPackager` → `ScormBuildCli` → `ScormExport`. Se borra Ionic y se mueven las XSD a `2004_4th`.
5. Test framework + tests EditMode → `init.sh` (requiere que el leader arregle los arrays de `unity.config.json`).
6. Build WebGL CLI **en background** (el primer IL2CPP puede pasar de 10 min):
   ```
   "C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe" -batchmode -quit -projectPath . -buildTarget WebGL \
     -executeMethod ScormBuildCli.BuildAndPackage -scormEdition 3rd -scormZip Builds/SCORM_TestApp_2004_3rd.zip \
     -scormIdentifier com.invelon.scormtestapp -scormTitle "SCORM Test App" -scoTitle "Test App" \
     -webglCompression <§7-2> -logFile Builds/logs/build_3rd.log
   ```
   Después, `ScormBuildCli.Package` con `-scormEdition 4th` → `Builds/SCORM_TestApp_2004_4th.zip`.
7. Validación local:
   - `ScormBuildCli.ValidateZip` para cada zip, y una segunda vía independiente con PowerShell 5.1 + `System.Xml.Schema.XmlSchemaSet`, que funciona aunque Unity esté abierto.
   - Inspección del zip: `imsmanifest.xml` en la raíz, sin `.meta` ni `\`, y todos los `<file href>` existen.
8. Smoke test local:
   - Servir `Builds/WebGL` por HTTP (`python -m http.server` o `npx http-server`, si están) y comprobar por HTTP que cada fichero del manifest devuelve 200.
   - El agente no puede ejecutar WebGL en un navegador; la prueba en navegador con `?scormsim=1` la hace el humano (visual check, parte A).
9. Crear `.harness/progress/visual_check_scorm2004-webgl-e2e-build.md` (§5), pedir la firma y luego el reviewer.

---

## 4. Cómo se prueba cada corrección

| Corrección | Prueba |
|---|---|
| `i++`/`x++`, `pattrern` | Test EditMode (backend de Editor) + visual check: "Add Interaction" con objetivo → en SCORM Cloud aparecen `objectives.0.id` y `correct_responses.0.pattern` |
| Vocabulario true-false… | Test EditMode + SCORM Cloud: `type = true-false` sin error 406 |
| `time-out`/`logout` | Solo test EditMode: TestApp no los usa |
| Null-check en `UpdateInteraction` | Test EditMode: nada en TestApp llama a `UpdateInteraction` |
| InvariantCulture | Tests con es-ES + SCORM Cloud: `score.raw` y `latency` aceptados |
| `DateTime.Parse` vacío | Test EditMode; también el simulador real (timestamps vacíos) |
| scorm.js (init, sin API, sin `window.close`, `pagehide`) | Visual check A y B: abrir `index.html` sin `?scormsim` → la app arranca ya sin LMS y sin cuelgue de 15 s; cerrar la pestaña en SCORM Cloud → registro con `session_time` y `exit` |
| Carrera de `unityInstance` | Desaparece por diseño (retorno directo); se ve en el log del navegador |
| Exportador 3rd/4th | Tests EditMode + `ValidateZip` + import en SCORM Cloud (y Moodle) |

---

## 5. Visual check humano (se escribirá en `visual_check_…md`)

**A. Editor y navegador local**
1. Abre `TestApp.unity` y entra en Play Mode. Se cargan los datos del backend de Editor (learner "Rene Descartes") y no hay errores en Console.
2. Abre `http://localhost:8000/index.html?scormsim=1`. Se ve la UI, responsive, y el título de la pestaña es el ProductName. Recorre los paneles y añade una interacción y un objetivo; no deben aparecer errores en la consola del navegador.

**B. SCORM Cloud** (cuenta del humano; ver §7 sobre la cuenta)
1. Library → Add Content → Import a SCORM package → sube `Builds/SCORM_TestApp_2004_3rd.zip`. Debe importarse sin errores de manifest; anota cualquier warning.
2. Launch.
   - Learner Data muestra tu nombre: `cmi.learner_name`.
   - Score: escribe Max 100 y Raw 75.
   - Objectives: añade un objetivo.
   - Interactions: añade una con "Correct".
   - Location: escribe una ubicación y pulsa Set. Eso hace `exit=suspend`, Commit y Terminate. Cierra la ventana.
3. Relanza. SCORM Data → Entry = `resume` y Location es la del paso anterior; la interacción y el objetivo persisten. Si se usó, `suspend_data` también.
4. Exit panel: marca Completed + Passed → Exit SCORM (`exit=normal`). Cierra.
5. Registration → View Registration / Launch History → Runtime Data.
   - Comprueba `cmi.learner_name`, `cmi.score.raw = 75` y `cmi.score.scaled = 0.75`.
   - Comprueba `completion_status = completed` y `success_status = passed`.
   - En `cmi.interactions.0` deben estar `type = true-false`, `objectives.0.id`, `correct_responses.0.pattern`, `latency` y `result`.
   - Debe haber `session_time` y `total_time`, y ningún SetValue con error 406/407 en el log de la API.
6. Prueba de `pagehide`: nuevo lanzamiento, cambia algo y cierra la pestaña sin pulsar Exit. En Runtime Data aparecen `session_time` y `exit` según §7-4.
7. Repite el paso 1 con el zip 4th.
8. (Recomendado) Sube el zip 3rd al Moodle de staging del cliente y repite los pasos 2-5. Moodle sirve por `pluginfile.php` sin `Content-Encoding`, y es lo que valida la elección de compresión.

---

## 6. Riesgos

- **R1 — Unity abierto.** Bloquea compilación, build, tests e `init.sh`. Mitigación: paso 0.
- **R2 — `init.sh` en rojo por causas ajenas.** Hay arrays multilínea en `unity.config.json` (leader) y `-runTests` sin test framework (§7-5). Sin resolver ambas, la feature no puede cerrarse.
- **R3 — Moodle y SCORM 2004.**
  - Moodle soporta 2004 de forma parcial: sin sequencing, y `limitConditions`/`timeLimitAction` se ignoran.
  - Que Moodle lea `completionThreshold` en formato 4th (atributos) no está garantizado; por eso el 3rd es el valor por defecto.
  - Las respuestas de `.wasm` de `pluginfile.php` pueden llegar sin `application/wasm`. El loader de Unity cae a `instantiate(ArrayBuffer)` con un warning, sin romper.
- **R4 — Cierre de ventana.** Chrome bloquea el XHR síncrono en `pagehide`. Si la API del LMS lo usa, el `Terminate` de `pagehide` puede perderse. Mitigación: `Commit` en `visibilitychange→hidden`. Moodle reciente usa `sendBeacon` al descargar; hay que verificarlo en el paso B6/B8.
- **R5 — Stripping de IL2CPP.** `BroadcastMessage`/`SendMessage` por nombre (`Scorm_Initialize_Complete`, `Log`) dependen de métodos de MonoBehaviour. Normalmente se preservan, pero si el nivel de stripping es alto puede hacer falta `[Preserve]`. Se verá en el smoke test A2.
- **R6 — `ProjectSettings.asset` binario.** Los cambios de PlayerSettings que hace el build lo reescriben, y quizá pase a texto según el modo de serialización. Saldrá un diff grande en ProjectSettings.
- **R7 — Escena reemplazada.** `TestApp.unity` pasa de binario U5 a la versión YAML del paquete. Si alguien había modificado la escena del repo respecto al paquete, ese cambio se pierde. Ya se ha comprobado que las GUID coinciden y que todas las referencias uGUI resuelven.
- **R8 — Tamaño del zip.** Con compresión Disabled, un WebGL de TestApp ronda los 10-30 MB. Hay que confirmar el límite de subida del Moodle del cliente.
- **R9 — 3rd Edition validada contra XSD 4th.** Si no se consiguen las XSD 3rd, el zip 3rd lleva XSD 4th: es un superset y en la práctica es inocuo en Moodle y SCORM Cloud, pero no es estrictamente conforme.
- **R10 — API pública.** No se renombra nada. Solo hay añadidos (`logout`, `IsLmsConnected`, `IsApiFound`, `ScormFormat`) y `[Obsolete]` en `ScormValueCallback`, `SetCallbackValue` y `APICallResult`. Código de terceros que dependiera de `SendMessage` hacia `ScormValueCallback` dejaría de recibir datos, pero era un detalle interno.

---

## 7. Decisiones abiertas para el humano

1. **Arquitectura del puente:** ¿se aprueba el jslib con retorno directo (y los `[Obsolete]` sin efecto) en lugar de mantener `SendMessage` + cola? (Recomendado: sí.)
2. **Compresión WebGL:**
   - (a) `Disabled`: lo más simple y compatible; el zip es más grande.
   - (b) `Gzip` + Decompression Fallback: zip más pequeño, Unity descomprime en JS y funciona sin `Content-Encoding`.

   Recomendado: (b), salvo que el límite de subida del Moodle no importe. ¿Cuál es ese límite?
3. **XSD SCORM 2004 3rd Edition:** ¿quién las consigue (ADL/scorm.com) y las deja en `Assets/SCORM/Plugins/2004_3rd/`? ¿O se acepta temporalmente el zip 3rd con XSD 4th (R9)?
4. **Simulador y cierre de ventana:**
   - ¿Simulador activado solo con `?scormsim=1` (recomendado) o con otra forma?
   - En `pagehide` sin exit explícito de la app, ¿se fija `cmi.exit = "suspend"` para permitir reanudar (recomendado) o se deja `""`, que inicia un nuevo intento?
5. **Tests y dependencias:**
   - ¿Se añade `com.unity.test-framework` con tests EditMode en `Assets/SCORM/Tests/Editor` (necesario para que `init.sh -runTests` pueda ponerse verde)?
   - ¿Se borra `Ionic.Zip.Reduced.dll` en favor de `System.IO.Compression`, y se reorganizan las XSD en `Plugins/2004_3rd` y `Plugins/2004_4th`?

   Además hace falta una cuenta de SCORM Cloud (y acceso al Moodle de staging) para el visual check.

---

## 8. Aprobación humana (2026-10-05, dguerra@invelon.com vía chat)

- §7-1: APROBADO jslib con retorno directo + `[Obsolete]` sin efecto.
- §7-2: APROBADO `Gzip` + Decompression Fallback. Límite de subida del Moodle: pendiente de confirmar por el cliente.
- §7-3: APROBADO temporal: zip 3rd con XSD 4th (R9). El humano añadirá las XSD 3rd en `Plugins/2004_3rd` más adelante; el packager debe caer a `2004_4th` si `2004_3rd` está vacío, con aviso.
- §7-4: APROBADO simulador solo con `?scormsim=1` y sin API; `pagehide` sin exit explícito ⇒ `cmi.exit="suspend"`.
- §7-5: APROBADO `com.unity.test-framework` + tests EditMode sin asmdef; borrar `Ionic.Zip.Reduced.dll`; XSD en `Plugins/2004_3rd` y `Plugins/2004_4th`.
- Unity cerrado (confirmado por el humano). `unity.config.json` arrays corregidos por el leader.
