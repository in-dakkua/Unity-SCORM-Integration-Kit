# Visual check — feature F003 scorm-scenario-demo

> Redactado por el implementer el 2026-10-06. **Sin marcar y sin firmar**: solo una persona puede rellenar
> "Resultado" y firmar (ver `.harness/docs/verification.md`, Nivel 3).
>
> Artefactos (en `Unity3D-Project/`):
> - Escena: `Assets/SCORM/Demo/ScenarioDemo.unity`. Se regenera con *SCORM > Demo > Build Scenario Demo Scene*.
> - Build WebGL: `Builds/WebGL_ScenarioDemo/`
> - Paquete: `Builds/SCORM_ScenarioDemo_2004_3rd.zip`
> - Comandos: `Assets/SCORM/Demo/README.md` y `.harness/progress/impl_scorm-scenario-demo.md`.

## Qué probar

### A. Play Mode en el Editor

1. Abre `Assets/SCORM/Demo/ScenarioDemo.unity` y entra en Play Mode.
   - Se ven 5 escenarios en "no iniciado", con peso y nota de aprobado.
   - Resumen:
     - "Alumno: Rene Descartes (id: rdescartes)"
     - Nota global "—", success `unknown`, completion `incomplete`, progreso 0/5
     - Entrada ab-initio
     - Conexión "Editor (backend SCORM en memoria)"
     - Aviso amarillo: el `suspend_data` de semilla no es JSON. Es lo esperado.
   - Log de llamadas SCORM: Initialize + ~100 GetValue.
   - **Es esperado ver GetValue en rojo al arrancar** (401 en el Editor/simulador; 401/403 "Not Initialized" en un LMS real) para los elementos que aún no tienen valor (`cmi.location`, `cmi.suspend_data`, `cmi.score.*`, etc.) y el contador de errores no en 0. Solo cuentan como fallo los **SetValue, Commit o Terminate en rojo**.
2. Pulsa **Jugar (aleatorio)** en 3 escenarios distintos:
   - Cada fila pasa a "aprobado" o "suspendido", con última/mejor/LMS e intentos = 1.
   - El log muestra en verde:
     - `SetValue cmi.objectives.N.id = "scenario-0X"` **antes** que el resto de campos del objetivo;
     - `cmi.interactions.M.*` con `objectives.0.id = "scenario-0X"`;
     - `cmi.score.raw`, `cmi.progress_measure`, `cmi.suspend_data` (JSON `{"v":1,...}`), `Commit`.
   - Ningún SetValue en rojo.
   - La nota global sube (los escenarios pendientes cuentan 0).
3. Repite **Jugar** en un escenario ya jugado:
   - Intentos = 2.
   - Con política "mejor intento", si la nota nueva es peor, la columna LMS no cambia y no hay `SetValue` del objetivo.
4. Usa **Nota manual**: mueve el slider de un escenario y pulsa el botón. Se aplica esa nota.
5. Filtros del log:
   - "Solo errores" deja solo las líneas rojas (o ninguna).
   - "Solo SetValue" deja solo las escrituras.
   - "Limpiar" vacía la lista y pone los contadores a 0.
6. Pulsa **Salir (suspend)**:
   - Secuencia en el log: `cmi.session_time`, `cmi.exit = "suspend"`, `Commit`, `Terminate`.
   - Los botones se desactivan y el resumen indica "Sesión SCORM cerrada".
7. Pulsa **Relanzar sesión (Editor)**:
   - Entrada pasa a "resume" y el estado se carga de "suspend_data".
   - Las filas conservan intentos y notas.
   - En el log no aparece ningún `SetValue` hasta que vuelvas a jugar.
8. Juega los escenarios restantes:
   - Al completar el 5º, completion pasa a `completed` y success a `passed` o `failed` según la nota global (umbral 70).
9. Pulsa **Finalizar (normal)** y luego **Relanzar**:
   - Intento nuevo: entrada ab-initio y todo "no iniciado".
10. Pulsa **Reiniciar datos demo** con algo jugado:
    - Las filas vuelven a "no iniciado".
    - El mensaje avisa de que el LMS mantiene los objetivos.
11. Cambia la resolución del Game view (16:9, 4:3, 1280x720…): la UI escala sin solaparse de forma grave.

### B. Navegador local (`?scormsim=1`)

1. Desde `Unity3D-Project/Builds/WebGL_ScenarioDemo`, ejecuta `python -m http.server 8000` y abre `http://localhost:8000/index.html?scormsim=1`.
   - Conexión: "Simulador del navegador (?scormsim=1)".
   - La consola del navegador muestra `[SCORM] No LMS API found: using ScormSimulator` y una línea `[SCORM] ...` por cada llamada.
2. Juega 2 escenarios, pulsa **Salir (suspend)** y recarga la página (F5).
   - Entrada "resume" y los 2 escenarios con sus notas e intentos.
3. Abre `index.html?scormsim=1&scormsimreset=1`: empieza de cero (ab-initio).
4. Abre `index.html` **sin** `?scormsim`:
   - Conexión "Sin LMS" en rojo.
   - Las llamadas salen en rojo en el log y la app no se cuelga.

### C. SCORM Cloud

1. Library → Import → sube `Builds/SCORM_ScenarioDemo_2004_3rd.zip`. Se importa sin errores de manifest; anota cualquier warning.
2. Launch:
   - Alumno = tu nombre.
   - Conexión "LMS real".
   - Juega **3 escenarios** (uno de ellos dos veces).
   - Pulsa **Salir (suspend)** y cierra el player.
3. Vuelve a lanzar:
   - Entrada "resume".
   - Los 3 escenarios con sus intentos y notas.
   - Juega los 2 restantes y pulsa **Finalizar (normal)**.
4. Registration → **View Runtime Data** (o Launch History → Runtime Data):
   - [ ] `cmi.objectives.N.id = scenario-0N` (5 objetivos, sin duplicados), cada uno con `score.raw/min/max/scaled`, `success_status` y `completion_status = completed`.
   - [ ] `cmi.score.raw` = nota global mostrada en la app (0-100), `cmi.score.scaled` = raw/100.
   - [ ] `cmi.success_status` / `cmi.completion_status` iguales a los de la app.
   - [ ] `cmi.interactions.M` con id `scenario-0N-aK-dJ`, `objectives.0.id = scenario-0N`, `learner_response` y `result` sin errores 406.
   - [ ] `cmi.suspend_data` con el JSON `{"v":1,...}`.
   - [ ] `cmi.location` = último escenario.
   - [ ] Total time acumulado (> 0).
5. En el log de la app, ningún SetValue/Commit/Terminate en rojo durante toda la prueba. Los GetValue 401/403 del arranque son esperados. Si hay algún SetValue en rojo, anota elemento y código de error.

### D. Moodle (SCORM 2004 3rd Edition)

1. Crea una actividad SCORM con el zip:
   - Calificación máxima 100.
   - Método de calificación "Calificación más alta".
   - Forzar nuevo intento "No".
2. Como alumno, juega 2 escenarios, pulsa **Salir**, vuelve a entrar y comprueba la reanudación. Termina todos los escenarios y pulsa **Finalizar**.
3. Como profesor:
   - [ ] Libro de calificaciones: la nota del alumno = `cmi.score.raw` global de la app.
   - [ ] Informe de la actividad → Objetivos: una fila por `scenario-0N`, con estado y score.raw.
   - [ ] Informe de interacciones: decisiones con su respuesta, su resultado y el objetivo.
   - [ ] Estado de finalización de la actividad coherente con `completion_status`/`success_status`.

## Resultado
- [ ] A. Play Mode: se comporta como se espera arriba
- [ ] B. Navegador local con simulador: reanuda al recargar
- [ ] C. SCORM Cloud: objetivos por escenario, nota global, interacciones enlazadas y reanudación correctos
- [ ] D. Moodle: libro de calificaciones e informe de objetivos correctos
- [ ] No hay errores/warnings nuevos en la consola de Unity (salvo el aviso esperado del suspend_data de semilla)
- [ ] La escena/prefab no quedó con cambios sin guardar no intencionados

Observaciones (warnings de SCORM Cloud/Moodle, capturas, valores anotados):

Firmado por: _____________  Fecha: _____________
