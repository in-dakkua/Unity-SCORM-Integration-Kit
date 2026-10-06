# Arquitectura — Qué significa "hacer un buen trabajo" (Unity)

> Estándar de calidad contra el que revisan los agentes. Describe la
> estructura **real** del proyecto (Unity-SCORM Integration Kit), no la
> plantilla Core/Gameplay/UI. Rutas en `.harness/unity.config.json`.

## Estructura real

```
Assets/
├─ Plugins/scorm.jslib          puente síncrono C# <-> scorm.js (WebGL)
├─ WebGLTemplates/SCORM/        plantilla WebGL: index.html, TemplateData/scorm.js,
│                               ScormSimulator.js (?scormsim=1)
└─ SCORM/
   ├─ _Scripts/   runtime del kit: ScormManager (API pública, UpsertObjective,
   │              RecordInteraction, UpdateScore...), ScormAPIWrapper, ScormFormat,
   │              ScormEditorBackend, StudentRecord, ScormCallInfo..., ControllerMain/
   │              AnObjective (UI de TestApp)
   ├─ Editor/     exportador y CLI: ScormExport, ScormBuildCli, ScormPackager,
   │              ScormManifestBuilder, ScormPackageSettings, ScormEdition
   ├─ Demo/       plantilla multi-escenario + demo (F003)
   │  ├─ Runtime/   PLANTILLA reutilizable: ScenarioDefinition, ScenarioCatalog,
   │  │             ScenarioTracker, host. Sin dependencia de DemoUI.
   │  ├─ DemoUI/    UI de demo (uGUI, textos en español vía DemoTexts), log visual
   │  ├─ Data/      ScenarioDefinition/ScenarioCatalog de ejemplo
   │  ├─ Editor/    ScenarioDemoSceneBuilder (genera ScenarioDemo.unity)
   │  └─ ScenarioDemo.unity, README.md
   ├─ Tests/Editor/  tests EditMode (sin asmdef; ver F004)
   ├─ Plugins/2004_4th/ (2004_3rd/ pendiente)  XSD — terceros, NO editar
   └─ Resources/  _Prefabs/  _Scenes/(TestApp)  _Images/
```

Regla de capas: `DemoUI` -> `Demo/Runtime` -> `ScormManager`. `Demo/Runtime`
no conoce la UI; el proyecto final reutiliza `Demo/Runtime` (o lo mueve a su
propio módulo) y sustituye `DemoUI`.

## Principios

1. **ScormAPIWrapper** es el único punto de contacto con la API SCORM
   (JavaScript/LMS). `ScormManager` expone el estado SCORM a la escena.
   `StudentRecord`/`AnObjective` son datos; no deben conocer la UI.
2. **Código de Editor solo en `Assets/SCORM/Editor/`.** Nada de
   `UnityEditor` en `_Scripts` sin `#if UNITY_EDITOR`.
3. **No editar `Plugins/`** ni los `WebGLTemplates`/`scorm.jslib` sin plan aprobado:
   son piezas de integración con el LMS/navegador.
4. **Compatibilidad WebGL.** El kit se despliega a WebGL; evitar APIs no
   soportadas (hilos, sockets, `System.IO` de disco) en runtime.
5. **Errores explícitos**: sin `Debug.Log` como manejo de errores; fallos
   de comunicación con el LMS se reportan por valor de retorno o excepción
   con contexto.
6. **Puente síncrono**: toda llamada SCORM pasa por `ScormAPIWrapper` (jslib
   en WebGL, `ScormEditorBackend` en Editor). Nada de `Application.ExternalCall`,
   `SendMessage` desde JS ni hilos (migración completada en F001).

## Qué NO hacer

- No mezclar lógica SCORM con UI de escena.
- No `GameObject.Find`/`FindObjectOfType` por frame.
- No añadir frameworks nuevos sin feature acordada en `feature_list.json`.
- No editar escenas/prefabs fuera del plan aprobado
  (`.harness/progress/plan_<feature>.md`). `editor_write_policy` es
  `not_applicable`: el agente solo escribe C# y deja instrucciones.
