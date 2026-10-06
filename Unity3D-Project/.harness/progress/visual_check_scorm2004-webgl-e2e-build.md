# Visual check — feature F001 scorm2004-webgl-e2e-build

> Redactado por el implementer el 2026-10-05. **Sin marcar y sin firmar**: solo una persona puede rellenar
> "Resultado" y firmar (ver `.harness/docs/verification.md`, Nivel 3).
>
> Artefactos (en `Unity3D-Project/`):
> - Build WebGL: `Builds/WebGL/`
> - Paquetes: `Builds/SCORM_TestApp_2004_3rd.zip` y `Builds/SCORM_TestApp_2004_4th.zip`
> - Para regenerarlos, ver los comandos en `.harness/progress/impl_scorm2004-webgl-e2e-build.md`.

## Qué probar

### A. Editor y navegador local

1. Abre `Assets/SCORM/_Scenes/TestApp.unity` y entra en Play Mode.
   - Tras unos 2 s desaparece el panel de carga.
   - Learner Data muestra el learner "Rene Descartes" (id `rdescartes`). Viene del backend de Editor (`ScormEditorBackend`).
   - Console no muestra errores (`EntryPointNotFoundException`, `NullReferenceException`, etc.).
2. Sin salir de Play Mode:
   - Objectives → añade un objetivo.
   - Interactions → añade una interacción con "Correct" marcado.
   - El texto de log de la escena muestra `Set cmi.interactions.N.type to true-false`, `...objectives.0.id`, `...correct_responses.0.pattern` y `Result true`. No aparece ningún `Error:`.
3. Sirve el build por HTTP y abre la página con el simulador:
   - Desde `Unity3D-Project/Builds/WebGL`, ejecuta `python -m http.server 8000`.
   - Abre `http://localhost:8000/index.html?scormsim=1`.
   - El título de la pestaña es el ProductName ("SCORM Testbed"), sin "Unity Web Player".
   - El canvas ocupa toda la ventana y se adapta al redimensionar.
   - La consola del navegador muestra `[SCORM] No LMS API found: using ScormSimulator (?scormsim=1).`
   - Se ven los datos del simulador ("Rene Descartes").
   - Recorre los paneles y añade un objetivo y una interacción. No debe haber errores en la consola del navegador.
4. Abre `http://localhost:8000/index.html` **sin** `?scormsim`:
   - La app arranca sin LMS y sin la espera de 15 s del kit anterior.
   - Los campos salen vacíos y no hay errores JS.

### B. SCORM Cloud (cuenta del humano)

1. Library → Add Content → Import a SCORM package → sube `Builds/SCORM_TestApp_2004_3rd.zip`.
   - Se importa sin errores de manifest. Anota cualquier warning, por ejemplo sobre las XSD 4th dentro del zip 3rd (R9).
2. Launch y, en la app:
   - Learner Data muestra tu nombre (`cmi.learner_name`).
   - Score: escribe Max `100` y Raw `75`; pulsa Enter o sal de cada campo.
   - Objectives: añade un objetivo.
   - Interactions: añade una interacción con "Correct".
   - Location: escribe una ubicación y pulsa Set. Esto hace `exit=suspend`, Commit y Terminate. Cierra la ventana.
3. Relanza el contenido.
   - SCORM Data → Entry = `resume` y Location es la del paso anterior.
   - La interacción y el objetivo persisten.
4. Exit panel: marca Completed + Passed → Exit SCORM (`exit=normal`). Cierra.
5. Registration → View Registration / Launch History → Runtime Data:
   - `cmi.learner_name` es correcto.
   - `cmi.score.raw = 75` y `cmi.score.scaled = 0.75`.
   - `completion_status = completed` y `success_status = passed`.
   - `cmi.interactions.0`: `type = true-false`, `objectives.0.id`, `correct_responses.0.pattern = true`, `latency = P0DT0H0M18.4S` (o equivalente) y `result = correct`.
   - Hay `session_time` y `total_time`.
   - El log de la API no tiene ningún SetValue con error 406/407/408.
6. Prueba de `pagehide`: nuevo lanzamiento, cambia algo (por ejemplo Score) y **cierra la pestaña sin pulsar Exit**. En Runtime Data aparecen `session_time` y `exit = suspend`.
7. Repite el paso B1 con `Builds/SCORM_TestApp_2004_4th.zip`. Debe importarse sin errores; si te da tiempo, haz un lanzamiento rápido.
8. (Recomendado) Sube el zip 3rd al Moodle de staging del cliente y repite los pasos B2-B5.
   - Moodle sirve por `pluginfile.php` sin `Content-Encoding`. Esto valida Gzip + Decompression Fallback.
   - Anota el tamaño del zip frente al límite de subida del Moodle.

## Resultado
- [ ] A1-A4: Editor y navegador local se comportan como se espera arriba
- [ ] B1-B6: SCORM Cloud (3rd) importa, guarda y reanuda como se espera arriba
- [ ] B7: SCORM Cloud (4th) importa sin errores
- [ ] B8: Moodle staging (opcional, anotar resultado)
- [ ] No hay errores/warnings nuevos en la consola de Unity ni en la del navegador
- [ ] El prefab/escena no quedó con cambios sin guardar no intencionados

Observaciones:

Firmado por: _____________  Fecha: _____________

## B-extra (añadido por el leader tras review M2) — solo importar, no lanzar

- [ ] SCORM Cloud: importar `Builds/SCORM_ManifestCheck_2004_3rd.zip` (lleva `<adlcp:completionThreshold>0.8</adlcp:completionThreshold>`, descripción con `Ñ`, `&`, `<>`) → sin errores ni warnings de manifest; el título/descripción muestran los acentos bien.
- [ ] SCORM Cloud: importar `Builds/SCORM_ManifestCheck_2004_4th.zip` (lleva `completedByMeasure="true" minProgressMeasure="0.8"`) → sin errores ni warnings de manifest.
- [ ] (Si hay Moodle staging) importar el `_3rd` de este par → Moodle lo acepta sin errores.
