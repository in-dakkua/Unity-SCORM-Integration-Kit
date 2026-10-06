# Demo de escenarios SCORM (plantilla de partida)

Esta demo valida la siguiente estrategia para una app de formación con varios escenarios dentro de **un único SCO**
(SCORM 2004 3rd Edition, pensada para Moodle):

| Qué | Dónde va en SCORM |
|---|---|
| Cada escenario | `cmi.objectives.n`. El id es estable (`scenario-01`…) y el objetivo lleva su propia nota (`score.raw/min/max/scaled`), `success_status`, `completion_status`, `progress_measure` y `description` |
| Cada decisión del alumno | `cmi.interactions.m`, enlazada al objetivo de su escenario (`objectives.0.id = scenario-0N`) |
| Nota global | Media ponderada de los escenarios. Va en `cmi.score.raw` (0-100), `cmi.score.min=0`, `cmi.score.max=100` y `cmi.score.scaled` |
| Estado global | `cmi.success_status`, `cmi.completion_status` y `cmi.progress_measure` |
| Estado propio (intentos, mejor/última nota, fechas) | `cmi.suspend_data`, como JSON versionado y compacto. `cmi.location` guarda el último escenario |

## Estructura

```
Assets/SCORM/Demo/
├─ Runtime/   PLANTILLA reutilizable (namespace Scorm.Scenarios). Sin dependencias de la UI de demo.
│   ├─ ScenarioDefinition   ScriptableObject: id, título, descripción, peso, min/max, umbral, nº de decisiones (simulador)
│   ├─ ScenarioCatalog      ScriptableObject: lista ordenada, política mejor/último intento, método de nota global, umbral global
│   ├─ ScenarioTracker      servicio C# puro sobre la API de ScormManager (F002)
│   ├─ ScenarioTrackerHost  MonoBehaviour: crea el tracker al recibir Scorm_Initialize_Complete
│   └─ ScenarioScoring, ScenarioProgress, ScenarioSaveData/Entry, enums y ScenarioTrackerException
├─ DemoUI/    Solo para la demo (namespace Scorm.Scenarios.Demo): uGUI, simulador aleatorio y log visual
├─ Data/      5 escenarios de ejemplo (seguridad en planta) + ScenarioCatalog
├─ Editor/    ScenarioDemoSceneBuilder: genera ScenarioDemo.unity
└─ ScenarioDemo.unity   (generada; no editar a mano: vuelve a generarla)
```

## Usarlo como plantilla en el proyecto final

1. **Crear los escenarios.** Usa *Create > SCORM > Scenarios > Scenario Definition* para cada escenario, y *Scenario Catalog* para el catálogo, con los escenarios en orden.
   - **El `id` no se puede cambiar una vez publicado el paquete**: el LMS guarda la nota de cada objetivo con ese id. Usa ids sin espacios (`scenario-01`, `loto-mantenimiento`…).
   - La descripción se corta a 250 caracteres al enviarla al LMS.
2. **Montar la escena.** Pon `ScenarioTrackerHost` (con el catálogo asignado) en el GameObject de `Assets/SCORM/Resources/ScormManager.prefab`, o en un hijo suyo. `ScormManager` avisa del fin de la inicialización con `BroadcastMessage`, que solo llega a ese objeto y a sus hijos.
3. **Enganchar un escenario real en lugar del simulador aleatorio.** Desde tu gameplay, con `host.Tracker` (o el evento `host.Ready`), las llamadas son:

   ```csharp
   ScenarioTracker t = host.Tracker;
   t.BeginScenario("scenario-03");                         // al entrar en el escenario
   t.RecordDecision("scenario-03", "d1",                   // cada decisión del alumno
       StudentRecord.InteractionType.choice, "opt-b",      // respuesta en formato SCORM del tipo
       StudentRecord.ResultType.correct, "opt-b",          // resultado y patrón correcto
       latencySeconds: 6.2f, description: "¿Por dónde cruzas?");
   t.CompleteScenario("scenario-03", rawScore);            // al terminar: nota en el rango min..max del escenario
   // ...
   t.Suspend();   // botón "Salir": exit=suspend, el LMS reanuda la próxima vez
   t.Finish();    // fin de la formación: exit=normal, cierra el intento
   ```

   `ScenarioSimulator` (en DemoUI) es solo el sustituto de ese gameplay; no hace falta copiarlo.
   Formato de las respuestas: `true-false` → `true`/`false`; `choice` → ids sin espacios separados por `[,]`. Con otro formato, un LMS estricto (SCORM Cloud) responde 406.
4. **Escuchar cambios.** `ScenarioTracker` tiene los eventos `Changed`, `ScenarioCompleted` y `SessionClosed`, y las propiedades `GlobalRaw`, `GlobalSuccess`, `GlobalCompletion`, `ProgressMeasure`, `Scenarios[i].Status/Attempts/BestRaw/LastRaw`.
5. **Errores.**
   - Uso incorrecto (id desconocido, sesión cerrada, nota fuera de rango) → `ScenarioTrackerException` / `ArgumentOutOfRangeException`.
   - Rechazos del LMS → `LastOperationFailures` / `LastOperationSucceeded` (también salen en el log de `ScormManager.ScormCall`).

### Comportamiento del tracker

- **Initialize no escribe nada.**
  - Lee `cmi.suspend_data`. Si no es un JSON de este formato (por ejemplo, datos de otro SCO, o una versión futura), reconstruye el estado a partir de los `cmi.objectives` cuyos ids están en el catálogo y lo avisa en `RestoreWarning`.
  - Al reanudar, los objetivos existentes se actualizan por id; nunca se duplican.
- **Sin escrituras vacías ni redundantes**: solo se envía lo que cambia.
- **Commit automático** tras `CompleteScenario` (`AutoCommit`, activado por defecto). Moodle solo persiste en Commit.
- **Nota global** (`GlobalScoreMethod`):
  - Por defecto, media ponderada sobre **todos** los escenarios, con los no completados contando como 0. Así el libro de calificaciones no muestra 100 tras jugar un solo escenario.
  - Alternativa: media solo de los escenarios completados.
- **Política por escenario** (`ScenarioScorePolicy`): `Best` (mejor intento, por defecto) o `Last` (último intento).
- **`cmi.success_status`**:
  - `passed` o `failed` **solo cuando todos los escenarios están completados**, según el umbral global (y, si `requireAllScenariosPassed`, además todos aprobados).
  - Antes de eso, `unknown`.
- **`cmi.completion_status`**: `completed` cuando todos los escenarios están completados; `incomplete` hasta entonces.
- **`cmi.progress_measure`** = escenarios completados / total.
- **`cmi.scaled_passing_score` del LMS no se usa**: el umbral lo pone el catálogo. Si tu LMS lo define, revisa que coincidan.
- **Límites SCORM**:
  - `suspend_data`: entre 90 y 120 caracteres por escenario (los decimales no exactos, como 66.6, se serializan con toda la precisión del float).
    - El tracker lanza una excepción antes de escribir si se superan 64000 (el máximo de 4th Edition).
    - **SCORM 2004 3rd Edition solo garantiza 4000 caracteres**, unos 30-40 escenarios. Por encima, el tracker avisa con un `Debug.LogWarning`. Moodle guarda más, pero si el destino es otro LMS 3rd, compruébalo.
  - Los ids de interacción son `<escenario>-a<intento iniciado>-<decisión>`. Tras "Reiniciar datos demo" la numeración vuelve a empezar y pueden repetirse ids (las interacciones son un diario: el LMS las acepta, pero el informe queda ambiguo).
  - `cmi.interactions`: el mínimo garantizado por SCORM 2004 es de 250. Con muchos reintentos × decisiones algunos LMS pueden cortar; vigílalo si cada escenario genera muchas decisiones.

## La escena de demo

Para generarla, usa el menú **SCORM > Demo > Build Scenario Demo Scene**, o en batchmode:

```
Unity -batchmode -quit -projectPath . -executeMethod ScenarioDemoSceneBuilder.BuildFromCommandLine
```

El builder:
- crea los assets de `Data/` solo si faltan; nunca los sobrescribe;
- genera `ScenarioDemo.unity`: Canvas 1920x1080 con `CanvasScaler`, `EventSystem`, la instancia del prefab `ScormManager` con `ScenarioTrackerHost` y la UI colgando debajo, y todas las referencias cableadas;
- la pone **primera** en Build Settings, con TestApp detrás.

Qué hay en pantalla:
- **Escenarios**: estado (no iniciado / en curso / aprobado / suspendido), última nota, mejor nota y nota enviada al LMS, intentos. Botones **Jugar (aleatorio)** (N decisiones y nota aleatorias) y **Nota manual** (slider).
- **Resumen**: alumno (`learner_name`/`learner_id`), nota global, success/completion, progreso, entrada (ab-initio/resume), de dónde se cargó el estado y tipo de conexión (Editor / simulador `?scormsim=1` / LMS real / sin LMS).
- **Acciones**: Commit, Salir (suspend), Finalizar (normal), Reiniciar datos demo y Relanzar sesión (solo en Editor).
  - Reiniciar datos demo solo borra `suspend_data`: el LMS mantiene objetivos, interacciones y nota.
  - Hay también un campo de semilla aleatoria para partidas reproducibles.
- **Log de llamadas SCORM**: hora, operación, elemento, valor (truncado) y resultado; los errores salen en rojo con su código y texto.
  - Filtros "Solo errores" y "Solo SetValue", botón Limpiar y contadores.
  - Cada llamada va también a `Debug.Log` (en WebGL, a la consola del navegador).

Dónde se puede probar:
- **Play Mode en el Editor**: backend SCORM en memoria. "Salir" y luego "Relanzar sesión" simulan la reanudación.
- **Navegador local con `index.html?scormsim=1`**: el simulador guarda el intento suspendido en `localStorage` y lo reanuda al recargar. `?scormsimreset=1` lo olvida.
- **SCORM Cloud / Moodle**: ver `.harness/progress/visual_check_scorm-scenario-demo.md`.

## Construir el zip SCORM

```
Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod ScormBuildCli.BuildAndPackage ^
  -scormEdition 3rd -scormZip Builds/SCORM_ScenarioDemo_2004_3rd.zip -scormBuildDir Builds/WebGL_ScenarioDemo ^
  -scormIdentifier com.invelon.scormscenariodemo -scormTitle "Demo Escenarios SCORM" -scormLanguage es-ES ^
  -webglCompression gzip-fallback
Unity -batchmode -quit -projectPath . -executeMethod ScormBuildCli.ValidateZip -scormZip Builds/SCORM_ScenarioDemo_2004_3rd.zip
```

- Usa un `-scormBuildDir` propio: el builder borra la carpeta de build anterior.
- No pases `-scormCompletionThreshold`. Con un umbral, el LMS calcularía `completion_status` a partir de `progress_measure` (4/5 = 0.8 ya contaría como "completed" con un escenario pendiente).

## Qué ve Moodle (actividad SCORM)

- **Libro de calificaciones**: con *Método de calificación = Calificación más alta* (o *Puntuación más alta*) y *Calificación máxima = 100*, Moodle toma `cmi.score.raw` (0-100), la nota global ponderada.
  - El método *Objetos de aprendizaje* daría 1/1 con un único SCO.
- **Informe de la actividad > Objetivos** (*Informes > Objetivos* en Moodle 4.x): una fila por `scenario-0N`, con su estado y `score.raw`.
- **Informe de interacciones**: cada decisión con su respuesta, su resultado y el objetivo enlazado.

Configuración recomendada de la actividad (compruébala en vuestra versión de Moodle; los nombres cambian entre versiones):
- *Forzar nuevo intento*: **No**. Si no, la reanudación (`exit=suspend`) no funciona.
- *Intentos*: ilimitados o los que pida la formación. *Calificación de intentos*: *Intento más alto*.
- *Mostrar navegación del curso*: No. *Mostrar en*: ventana actual o nueva, según el campus.
- *Estado de finalización*: *Requiere estado "Completado"* y/o *Requiere calificación mínima*, según el criterio de la formación.
