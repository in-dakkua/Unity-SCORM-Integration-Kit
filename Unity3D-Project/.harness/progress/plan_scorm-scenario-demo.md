# Plan F003 — `scorm-scenario-demo`

Implementer, 2026-10-06. Gate de aprobación eximido por el humano (chat 2026-10-06, "dale a todo y construye la demo"); el plan se escribe por trazabilidad y se ejecuta sin esperar.
Rutas relativas a `Unity3D-Project/`. Todas las rutas de código caen en `scripts_root[2]` = `Assets/SCORM/Demo` salvo los tests (`tests_root[0]` = `Assets/SCORM/Tests`) y la plantilla WebGL.

## 1. Estrategia validada

Un único SCO. Cada escenario = un `cmi.objectives.n` con id estable (`scenario-01`…) y su propia nota (`score.raw/min/max/scaled`, `success_status`, `completion_status`, `progress_measure`). Las decisiones del escenario = `cmi.interactions.m` enlazadas al objetivo (`objectives.0.id = scenario-0N`). Nota global ponderada → `cmi.score.raw` (0-100, lo que Moodle lleva al libro de calificaciones), `min=0`, `max=100`, `scaled`. Estado propio (intentos, mejor/última nota, timestamps) en `cmi.suspend_data` como JSON versionado.

## 2. Archivos

### Plantilla reutilizable — `Assets/SCORM/Demo/Runtime/` (scripts_root[2]), namespace `Scorm.Scenarios`, sin dependencias de la UI de demo
- `ScenarioDefinition.cs` (ScriptableObject): id, título, descripción, peso, min/max, umbral scaled, nº de decisiones para simulación.
- `ScenarioCatalog.cs` (ScriptableObject): lista ordenada, `ScenarioScorePolicy` (Best/Last), umbral global, `GlobalScoreMethod` (media ponderada sobre todos los escenarios —no completados = 0— por defecto, o solo completados), `requireAllScenariosPassed`.
- `ScenarioScorePolicy.cs`, `GlobalScoreMethod.cs`, `ScenarioStatus.cs` (enums).
- `ScenarioProgress.cs` (estado por escenario, solo lectura para la UI) + `ScenarioSaveData.cs` (DTO JSON compacto: `v`, `s[]{id,n,b,l,t0,t1}`, `cur`).
- `ScenarioScoring.cs` (estático puro: normalización, media ponderada, decisiones de estado). Testeable sin SCORM.
- `ScenarioTracker.cs` (servicio C# puro): Initialize (lee objetivos + suspend_data; reconstruye de los objetivos si suspend_data falta o no es JSON válido; no escribe nada), BeginScenario, RecordDecision, CompleteScenario, ResetLocalState, Commit, Suspend, Finish, eventos `Changed`/`ScenarioCompleted`/`SessionClosed`. Reloj inyectable para session_time y tests. Sin escrituras vacías ni redundantes (compara contra el último valor aceptado).
- `ScenarioTrackerException.cs`: excepción de dominio (id desconocido, sesión cerrada, suspend_data > 64000).
- `ScenarioTrackerHost.cs` (MonoBehaviour fino): vive en el GameObject del ScormManager (recibe `Scorm_Initialize_Complete` por BroadcastMessage), crea el tracker con el catálogo y lanza `Ready`.

### Demo — `Assets/SCORM/Demo/DemoUI/` (scripts_root[2]), namespace `Scorm.Scenarios.Demo`
- `ScenarioDemoController.cs`: orquesta paneles, botones Commit/Salir/Finalizar/Reiniciar/Relanzar, semilla aleatoria.
- `ScenarioRowView.cs` + `ScenarioListPanel.cs`: fila por escenario (título, estado, última/mejor, intentos; "Jugar (aleatorio)", slider + "Nota manual").
- `ScenarioSimulator.cs` (C# puro): genera N decisiones aleatorias (tipo choice/true-false, respuesta, resultado, patrón correcto, latencia) y nota derivada (% de aciertos ± ruido).
- `SummaryPanel.cs`: alumno, nota global, success/completion, progreso, entrada, conexión (Editor / simulador `?scormsim=1` / LMS real / sin LMS).
- `ScormLogPanel.cs`: log visual de `ScormManager.ScormCall` (hora, op, elemento, valor truncado, resultado, error en rojo), filtros (solo errores / solo Set), limpiar, contador de errores, máx. 300 líneas con pool, también `Debug.Log`.

### Datos — `Assets/SCORM/Demo/Data/`
- 5 `ScenarioDefinition` (seguridad en planta, pesos 1/1/2/1.5/2.5 —p. ej. EPI, bloqueo LOTO, carretillas, incendio, espacios confinados—) + `ScenarioCatalog`. Los crea el builder si faltan.

### Editor — `Assets/SCORM/Demo/Editor/ScenarioDemoSceneBuilder.cs`
- Menú `SCORM/Demo/Build Scenario Demo Scene` + `ScenarioDemoSceneBuilder.BuildFromCommandLine` (batchmode, Exit 0/1).
- Genera `Assets/SCORM/Demo/ScenarioDemo.unity`: cámara, EventSystem (StandaloneInputModule), instancia del prefab `Assets/SCORM/Resources/ScormManager.prefab` con `ScenarioTrackerHost` añadido y el Canvas (Screen Space Overlay, CanvasScaler 1920x1080 match 0.5) colgando debajo; todos los campos serializados cableados. Fuente `LegacyRuntime.ttf` (sin TMP Essentials). Sin luces/lightmaps (no genera LightingData).
- Build Settings: ScenarioDemo primera y habilitada, TestApp detrás (se conserva).

### Tests — `Assets/SCORM/Tests/Editor/ScenarioTrackerTests.cs` (tests_root[0])
Backend de Editor: ids estables y nota por objetivo; política mejor/última; media ponderada y raw/scaled; success/completion global (passed/failed/unknown); progress_measure; reanudación (suspend → Initialize → estado restaurado sin duplicar objetivos, entry=resume); reconstrucción desde objetivos si suspend_data no es JSON; JSON versionado, versión futura y tamaño < 64000; sin escrituras vacías; Initialize no escribe; segunda llamada idéntica no escribe; interacciones enlazadas al objetivo; Finish/Suspend (exit, session_time, commit, terminate); errores de dominio.

### Plantilla WebGL — `Assets/WebGLTemplates/SCORM/TemplateData/ScormSimulator.js`
- Persistencia opcional en `localStorage` para que `?scormsim=1` reanude al recargar: en Terminate con `cmi.exit=suspend` guarda los datos; en Initialize los restaura con `cmi.entry=resume`; con otro exit los borra. `?scormsimreset=1` borra. Sin efecto con un LMS real (el simulador solo se instala sin API).

### Documentación — `Assets/SCORM/Demo/README.md`

## 3. Escenas/prefabs
- Nueva escena `ScenarioDemo.unity` generada por código. El prefab `ScormManager.prefab` NO se modifica: el componente se añade como override de la instancia en la escena.
- TestApp no se toca.

## 4. ProjectSettings
- `EditorBuildSettings.asset`: ScenarioDemo primera (imprescindible por acceptance).
- `ProjectSettings.asset`: solo lo que ya escribe `ScormBuildCli.ConfigureWebGL` (plantilla SCORM, gzip+fallback) y, durante la build, `productName` NO se toca (el título del paquete va por `-scormTitle`).
- `Packages/manifest.json`: no se toca.

## 5. Riesgos / decisiones
- R1. Global con no completados = 0 (para que el libro de calificaciones no muestre 100 tras un solo escenario). Configurable.
- R2. `cmi.scaled_passing_score` del LMS no se usa: el umbral global sale del catálogo (Moodle no lo rellena salvo manifest con `minNormalizedMeasure`). Documentado.
- R3. Tras Terminate, un LMS real no permite reinicializar en la misma página: "Relanzar" solo se ofrece con el backend de Editor; en navegador se relanza recargando.
- R4. Paquetes `com.unity.ai.*` del humano pueden inflar o romper la build WebGL: se documenta con tamaños/errores; no se quitan.
- R5. Reiniciar datos de demo no puede borrar `cmi.objectives` en el LMS; el tracker escribe un estado vacío válido y avisa.
