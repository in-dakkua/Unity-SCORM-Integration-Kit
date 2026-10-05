# Review — feature F001 `scorm2004-webgl-e2e-build`

Reviewer, 2026-10-05.

**Veredicto:** `READY_FOR_VISUAL_CHECK`

Esto **no es** `APPROVED`. El código cumple la acceptance y el plan aprobado (§8). No he encontrado defectos bloqueantes ni mayores. La feature tiene `requires_visual_check: true` y `.harness/progress/visual_check_scorm2004-webgl-e2e-build.md` sigue sin firmar: la línea 72 tiene `Firmado por: ____  Fecha: ____` en blanco. Cuando el humano lo firme, hará falta una última pasada del reviewer, que solo comprobará la firma y lo que se anote en el resultado, para emitir `APPROVED`.

Recuento: **0 bloqueantes · 0 mayores · 5 menores · 8 info/nits.**

Ningún hallazgo menor afecta a TestApp ni al recorrido del visual check, así que pueden ir en una feature posterior. La excepción es M2: conviene hacerlo **antes** del paso B del visual check.

---

## Verificación realizada (no me he fiado del informe)

- **`.harness/init.sh` re-ejecutado por mí:** exit 0. El paso 7 ejecuta los tests de verdad, sin `-quit`. El resultado nuevo está en `.harness/progress/editmode-results.xml` (13:21:34Z): **35 total, 34 pass, 0 fail, 1 skipped**. El skipped es `Build_3rd_IsValidAgainst3rdEditionXsd_WhenAvailable`, un `Assert.Ignore` justificado por la ausencia de XSD 3rd (§8 §7-3).
- **Zips inspeccionados con Python `zipfile`.** `Builds/SCORM_TestApp_2004_3rd.zip` y `_4th.zip` tienen 55 entradas cada uno:
  - `imsmanifest.xml` está en la raíz y las XSD también.
  - Ninguna entrada es `.meta`, usa `\` ni es un fichero oculto.
  - Los manifests están en UTF-8 sin BOM (empiezan por `<?x`).
  - `schemaversion` es `2004 3rd Edition` / `2004 4th Edition`.
  - No hay `limitConditions`, `timeLimitAction`, `completionThreshold` ni LOM, porque no se configuraron.
  - Los 18 `<file href>` coinciden con los ficheros reales del build, y `index.html` va primero.
  - El orden de los hijos de `<item>` es correcto.
- **Zip frente a build:** cada fichero de `Build/`, `TemplateData/` e `index.html` dentro del zip es byte a byte igual a `Builds/WebGL`. Los `scorm.js` y `ScormSimulator.js` del build son iguales a los de `Assets/WebGLTemplates`.
- **El build corresponde al código revisado.** En `WebGL.data.unityweb` descomprimido (metadata IL2CPP) aparecen literales nuevos: `***Terminate***`, `No LMS API found: running without an LMS`, ` (response: ` y `.correct_responses.`. No aparece `pattrern`. El único fuente posterior al build (15:09) es `ControllerMain.cs` (15:15), y el informe dice que ese cambio fue solo de comentario.
- **Compresión.** Gzip + Decompression Fallback: los ficheros son `.unityweb` y el loader incluye el descompresor gzip (10 referencias). `ProjectSettings.asset` contiene `PROJECT:SCORM`; antes era `APPLICATION:Default`.
- **API pública frente al `.unitypackage`**, extraído de nuevo en el scratchpad (`pkgrev/`):
  - `ScormManager` solo añade `IsInitialized` e `IsLmsConnected`.
  - `ScormAPIWrapper` añade `IsApiFound`, `IsInitialized` y `DebugPrint` estático, y `Commit()`/`Terminate()` pasan de `void` a `bool`.
  - `StudentRecord` solo añade `logout` al final de `ExitType`.
  - `ControllerMain`, `AnObjective` y `ScormExport` no cambian sus miembros públicos.
  - Nada se ha renombrado.
- **`[Obsolete]` sin efecto:** `ScormAPIWrapper.APICallResult`, `ScormAPIWrapper.SetCallbackValue` y `ScormManager.ScormValueCallback` compilan y no hacen nada. Es correcto.
- **`.meta`/GUIDs.**
  - Las GUID de los 5 scripts runtime coinciden con las del paquete. `TestApp.unity`, `ScormManager.prefab` y `AnObjective.prefab` son byte a byte iguales al paquete.
  - Todas las GUID de `m_Script` de la escena y de los 2 prefabs resuelven: las de uGUI en `Library/PackageCache/com.unity.ugui@e20f1880fa04` y las demás en `Assets/SCORM/_Scripts`. **No hay scripts perdidos.**
  - Ningún fichero o carpeta de `Assets/SCORM`, `Assets/Plugins` ni `Assets/WebGLTemplates` se ha quedado sin `.meta`, y no hay `.meta` huérfanos en `Assets/` ni GUID duplicadas.
  - `Plugins/2004_4th.meta` conserva la GUID de la antigua `2004` (`f1f9ae82…`).
- **Ionic:** el grep `Ionic|System.Web` en `Assets` (`*.cs`, `*.asmdef`, `*.rsp`) no da resultados. La DLL y su `.meta` están borrados en git.
- **`scorm.jslib.meta`:** Any=0, Editor=0, WebGL=1.

---

## Checkpoints

- **C0 — Proyecto inicializado**
  - [x] `unity.config.json` completo; `unity_version` 6000.3.21f1 coincide con `ProjectVersion.txt`.
  - [x] `feature_list.json` sin `is_example_data`.
  - [x] `automation_tool: none`, así que `automation_tool_verified` no aplica.
  - [x] `editor_write_policy` `not_applicable`. Se respetó: la escena y los prefabs son copia del paquete y no hay ediciones del agente.
  - [x] Backlog `local`.
  - [x] `vcs_verified: false`, pero ninguna acción de este cambio dependía de VCS. El implementer no hizo escrituras de git.
  - [x] `release_guideline: none`.
- **C1 — Arnés completo**
  - [x] Existen los ficheros base y los docs.
  - [x] `init.sh` termina con exit 0 (re-ejecutado).
  - [x] `project_rules.md` no tiene reglas MUST/MUST NOT, así que no hay violaciones.
- **C2 — Estado coherente**
  - [x] Solo F001 está en `in_progress`.
  - [x] Plan aprobado antes de implementar (§8).
  - [ ] ← Razón: `visual_check_scorm2004-webgl-e2e-build.md` **no está firmado por un humano**. Bloquea `done`/`APPROVED`.
  - [x] `current.md` describe la sesión activa.
- **C3 — Arquitectura** (la real, de `architecture.md`; no aplica Core/Gameplay/UI)
  - [x] `ScormAPIWrapper` es el único punto de contacto con JS. No hay `UnityEditor` en `_Scripts` y el Editor está en `Assets/SCORM/Editor`.
  - [x] Runtime compatible con WebGL: sin hilos ni `System.IO` en `_Scripts`.
  - [x] No hay `Debug.Log` sueltos de depuración. Los dos de `ScormAPIWrapper.cs:78,129` son el backend de log intencionado fuera de WebGL. El `TODO` de `ScormManager.cs:104` viene del paquete y tiene contexto (SCORM 1.2 no soportado).
  - [x] `release_guideline` es `none`.
  - Nota: `architecture.md` está desfasado tras esta feature (ver I3).
- **C4 — Verificación real** (`verification_mode: plan_and_visual`; los tests se han añadido aunque el modo no los exige)
  - [x] Hay 35 tests EditMode sobre `ScormFormat`, el builder/packager y `ScormManager` con el backend de Editor. Fuerzan es-ES y restauran la cultura en `TearDown`.
  - [x] No se mockea `MonoBehaviour`.
  - [x] Los tests se ejecutaron en batchmode y están verdes (34/35; 1 ignorado con justificación).
  - [ ] Visual check humano (Nivel 3) ← Razón: pendiente de firma.
- **C5 — Cierre de sesión**
  - [x] No hay ficheros sospechosos sin trackear. `Builds/` está ignorado y `LightingData.asset` fue borrado (ver I4).
  - [ ] `history.md` sin entrada ← Razón: la sesión sigue abierta y la feature no está cerrada. **No es un defecto de la feature**; lo hace el leader al cerrar.
  - [x] F001 sigue en `in_progress`, que es su estado correcto.
  - [x] Sin commits, push ni merge.
  - [x] `release_guideline` es `none`.

---

## Hallazgos

### Menores (no bloquean el visual check)

**M1. `findAPI` prefiere 2004 solo dentro de cada ventana, no en toda la cadena.**
- Dónde: `Assets/WebGLTemplates/SCORM/TemplateData/scorm.js:460-503` (`findAPI`) y `:512-523` (`getAPI`).
- Problema:
  - Si un frame más cercano expone `API` (1.2) y un ancestro expone `API_1484_11`, gana el 1.2: `versionIsSCORM2004=false`.
  - Entonces `ScormManager.Initialize` lanza "SCORM 1.2 not currently supported" (`ScormManager.cs:104`) y `studentRecord` se queda en null, así que cualquier getter da NRE.
  - Ocurre en LMS con wrappers duales anidados. En SCORM Cloud y Moodle, que exponen las dos APIs en la misma ventana, no se da.
- Fix: dos pasadas. Primero buscar `API_1484_11` en la cadena de padres y luego en la del opener; solo si no aparece, una segunda pasada buscando `API`.

**M2. Los zips del visual check no ejercitan la diferencia 3rd/4th.**
- Dónde: los dos zips de `Builds/`. Los comandos están en `impl_…md` §5.
- Problema:
  - Los dos manifests solo difieren en `schemaversion`. No llevan `completionThreshold` (elemento en 3rd frente a atributos en 4th), ni LOM, ni `timeLimitAction`.
  - Esa diferencia de la acceptance #4 solo la cubren los tests unitarios y la validación XSD. SCORM Cloud y Moodle (B1/B7) nunca importan un manifest que la contenga.
- Fix: generar un par extra solo para importar, sin lanzarlo:
  - `ScormBuildCli.Package -scormEdition 3rd -scormCompletionThreshold 0.8 -scormDescription "…"`
  - `ScormBuildCli.Package -scormEdition 4th -scormCompletedByMeasure true -scormMinProgressMeasure 0.8`
- Después, añadir al visual check un paso "importa ambos sin errores ni warnings de manifest". No se usa para el flujo de lanzamiento porque `completedByMeasure` cambia cómo se decide la completion.

**M3. `ScormManager` envía `""` al LMS cuando el valor es `not_set` o null.**
- Dónde, en `Assets/SCORM/_Scripts/ScormManager.cs`:
  - `AddInteraction`: `:523-532` (`learner_response` y `result`) y `:538-540` (`description`).
  - `UpdateInteraction`: `:595-604` y `:610-612`.
  - `AddObjective`: `:882-888` (`success_status` y `completion_status`) y `:894-896`.
  - `UpdateObjective`: `:951-957` y `:963-965`.
  - También los ids de objetivo en los bucles `:500-504` y `:615-619`.
- Problema: `ScormFormat.ToVocabulary(not_set)` devuelve `""`, y `ScormAPIWrapper.SetValue` cambia `null` por `""`. `SetValue("cmi.interactions.N.result", "")` o `success_status = ""` no son vocabulario válido, así que un LMS estricto responde 406/407 y deja errores en su log de API.
- No afecta a TestApp: siempre fija type, response, result y estados, así que el paso B5 del visual check no se ve afectado.
- Fix: no llamar a `SetValue` si el valor del vocabulario o la cadena es vacío o null. Es el patrón de "solo escribir lo que el SCO conoce". Añadir un test con el backend.

**M4. Los ajustes de una edición se pierden en silencio en la otra.**
- Dónde: `Assets/SCORM/Editor/ScormManifestBuilder.cs:113-122`.
- Problema: `-scormCompletionThreshold 0.8` con `-scormEdition 4th` no escribe nada, y `completedByMeasure` con 3rd tampoco. No hay ningún aviso: `ScormPackageSettings.Validate()` (`ScormPackageSettings.cs:111-130`) no lo detecta.
- Fix: elegir una de las dos opciones:
  - (a) En 4th, mapear `completionThreshold` → `completedByMeasure="true" minProgressMeasure="<valor>"`; en 3rd, mapear `completedByMeasure`+`minProgressMeasure` → elemento `completionThreshold`.
  - (b) Como mínimo, añadir un warning a `ScormPackageResult.warnings` cuando un ajuste de la otra edición está configurado y se ignora.

**M5. "Build WebGL + Publish" lanza `BuildPipeline.BuildPlayer` dentro de `OnGUI`.**
- Dónde: `Assets/SCORM/Editor/ScormExport.cs:252-253`, que llama a `BuildAndPublish()` (`:148-165`), que a su vez llama a `ScormBuildCli.BuildWebGLPlayer` (`:156`).
- Problema: un build síncrono (minutos, y con cambio de plataforma la primera vez) dentro del layout de IMGUI deja el layout a medias. Lo normal es que aparezca después `EndLayoutGroup: BeginLayoutGroup must be called first`, y además el `SaveFilePanel` de `Publish()` se abre en el mismo frame. No afecta al CLI, que es lo que pide la acceptance #5.
- Fix:
  ```csharp
  EditorApplication.delayCall += BuildAndPublish;
  GUIUtility.ExitGUI();
  ```
  Haz lo mismo con `Publish()` si quieres el patrón uniforme.

### Info / nits (para el leader; no requieren cambios en esta feature)

- **I1. Comentario de `findAPI` desalineado.** `scorm.js:455-457` dice que un acceso cross-origin "ends the search", pero el código (`:478-492`) captura la excepción y **sigue** hacia `win.parent`. El comportamiento es mejor que lo que dice el comentario; basta con corregir el comentario.
- **I2. `CheckThread()` eliminado frente a HEAD.** `public static bool CheckThread()` (HEAD `ScormManager.cs:130`) ya no existe:
  - Ya faltaba en el `.unitypackage`, la base acordada en el plan §2.1.
  - La acceptance #1 exige quitar `System.Threading`.
  - No queda ninguna referencia en `Assets/`.
  - Es coherente, pero difiere de la política de `[Obsolete]` sin efecto. Si se quiere compatibilidad de código fuente con el kit Web Player, basta un stub `[Obsolete] public static bool CheckThread() { return true; }`, que no usa `System.Threading`. Si no, documentarlo como eliminación intencionada en el changelog.
- **I3. `architecture.md` desfasado.** `.harness/docs/architecture.md` describe todavía `Ionic.Zip.Reduced.dll`, `Plugins/2004/`, `WebPlayerTemplates` y `Application.ExternalCall` (Estructura real y principio 6). Debe reflejar:
  - `Plugins/2004_4th` (+ `2004_3rd` futuro) y `Assets/Plugins/scorm.jslib`
  - `Assets/WebGLTemplates/SCORM`
  - `ScormFormat` y `ScormEditorBackend`
  - el puente síncrono
  - `Assets/SCORM/Tests/Editor`

  Es documentación del harness, no código, así que le corresponde al leader.
- **I4. `LightingData.asset`.** `Assets/SCORM/_Scenes/TestApp/LightingData.asset` se regenera en cada build (informe §7.3). Hay que decidir entre `.gitignore` o desactivar Auto Generate en la escena; esto último es una edición de escena y la hace el humano.
- **I5. Ficheros de progreso sin trackear.** `.harness/progress/editmode-results.xml` y `editmode-tests.log` no están trackeados (los genera `init.sh`). Hay que decidir si ignorarlos.
- **I6. Evidencia JS solo en el scratchpad.** Los tests JS headless (`scorm_js_test.js`), el smoke HTTP y el validador PowerShell solo existen en el scratchpad de la sesión. La evidencia de las acceptance #3 y #6 no se puede reproducir después. Sugerencia: moverlos a `Unity3D-Project/Tools/` o `.harness/tools/` en una feature aparte.
- **I7. Backend de Editor en players no WebGL.** En builds standalone o móviles, `ScormAPIWrapper` usa `ScormEditorBackend` con los datos sembrados, y `IsLmsConnected` es `true` (`ScormManager.cs:96`, `ScormAPIWrapper.cs:80`). Es el diseño aprobado (plan §1.2), pero conviene documentarlo para que nadie lo tome como un LMS real. El backend también se compila en el player WebGL sin usarse; el coste es despreciable.
- **I8. Compatibilidad binaria de `Commit()`/`Terminate()`.** `ScormAPIWrapper.Commit()`/`Terminate()` pasan de `void` a `bool`. Es compatible en código fuente pero no en binario (DLL precompiladas de terceros). Ya está anotado como desviación 5 del informe. Aceptable.

### Revisado y correcto (sin hallazgo)

- **`scorm.jslib` (memoria y firmas)**
  - `wglGetValue` y `wglGetErrorString` reservan con `lengthBytesUTF8+1` → `_malloc` → `stringToUTF8`. IL2CPP libera el `char*` de un `extern string`, que es el patrón documentado por Unity: no hay fuga ni doble free.
  - Las 10 firmas coinciden con los `[DllImport]` de `ScormAPIWrapper.cs:48-76`: `int()`, `string(string)`, `int(string,string)`, `string(int)` y `void(string)`.
  - Todo va bajo `#if UNITY_WEBGL && !UNITY_EDITOR`, con un `#else` de firmas idénticas.
  - Las funciones de JS se resuelven como `window["…"]` en el momento de la llamada.
- **`scorm.js`**
  - `doInitialize` es idempotente, comprueba `String(result) !== "true"` y no se re-inicializa tras `Terminate`.
  - `doTerminate` tiene guarda `terminated` y no llama a `window.close()`.
  - Sin API responde al instante con `""`/`"false"`.
  - `DebugPrint` cae a `console.log` si no hay `#console`.
  - `visibilitychange→hidden` hace Commit.
  - `pagehide` escribe `session_time` (formato `PT{h}H{m}M{s}S`, válido y nunca `PT` vacío) y `exit="suspend"` solo si Unity no los fijó, y después hace Commit y Terminate.
  - El simulador solo se instala con `?scormsim=1` **y** sin API.
  - Los accesos a `parent`/`opener` están protegidos con try/catch.
- **`ScormManager`/`ScormFormat`**
  - Los bucles internos usan `x++`; se escribe `pattern` y no `pattrern`.
  - `type` se escribe antes de `response` y `pattern` (evita el 408).
  - Timestamp por defecto: `< 1970` pasa a `DateTime.Now` (`:485-486`).
  - Vocabulario: `true-false`, `fill-in`, `long-fill-in`, `time-out`, `logout`, `no-credit`, `not attempted`, `ab-initio`, y `exit,message` etc.
  - Todos los reales pasan por `ToReal` (Invariant, `0.#######`, sin exponente) y los enteros por `ToString(InvariantCulture)`.
  - `SecondsToTimeInterval` siempre da `P{d}DT{h}H{m}M{s}S`.
  - `ParseTimestamp` es tolerante a vacío.
  - `score.scaled` se limita a [-1,1] y se protege la división por 0 en `ControllerMain.cs:436-441` y `AnObjective.cs:161`.
- **Stripping IL2CPP (R5).** Los 4 receptores por nombre tienen `[Preserve]`: `ControllerMain.Scorm_Initialize_Complete`, `Scorm_Commit_Complete` y `Log`, y `ScormManager.LogMessage`. No hay más receptores de `SendMessage`/`BroadcastMessage` en `Assets`. `ScormAPIWrapper.Log` es un método normal, no un receptor.
- **Exportador**
  - `XmlWriter` en UTF-8 sin BOM con escapado automático; el test de escapado cubre `&<>"` y LOM.
  - El `identifier` se sanea como NCName.
  - Orden de los hijos de `<item>`: title → timeLimitAction → dataFromLMS → completionThreshold → sequencing.
  - Los `href` usan `/` y van URI-escaped por segmento.
  - El packager excluye `.meta` y ficheros ocultos, comprueba que no haya `\`, y cae a las XSD 4th con aviso cuando falta 2004_3rd (log: `SCORM 2004 3rd Edition XSD files not found …`).
  - `ValidateZip` tiene un control negativo que falla como debe.
