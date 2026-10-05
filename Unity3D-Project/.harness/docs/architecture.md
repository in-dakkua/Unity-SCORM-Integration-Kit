# Arquitectura — Qué significa "hacer un buen trabajo" (Unity)

> Estándar de calidad contra el que revisan los agentes. Describe la
> estructura **real** del proyecto (Unity-SCORM Integration Kit), no la
> plantilla Core/Gameplay/UI. Rutas en `.harness/unity.config.json`.

## Estructura real

```
Assets/SCORM/
├─ _Scripts/   runtime: ScormManager, ScormAPIWrapper, ControllerMain,
│              StudentRecord, AnObjective
├─ Editor/     ScormExport.cs (exportación de paquete SCORM, solo Editor)
├─ Plugins/    Ionic.Zip.Reduced.dll, 2004/ (terceros — NO editar)
├─ Resources/  _Prefabs/  _Scenes/  _Images/  WebPlayerTemplates/
```

## Principios

1. **ScormAPIWrapper** es el único punto de contacto con la API SCORM
   (JavaScript/LMS). `ScormManager` expone el estado SCORM a la escena.
   `StudentRecord`/`AnObjective` son datos; no deben conocer la UI.
2. **Código de Editor solo en `Assets/SCORM/Editor/`.** Nada de
   `UnityEditor` en `_Scripts` sin `#if UNITY_EDITOR`.
3. **No editar `Plugins/`** ni los `WebPlayerTemplates` sin plan aprobado:
   son piezas de integración con el LMS/navegador.
4. **Compatibilidad WebGL.** El kit se despliega a WebGL; evitar APIs no
   soportadas (hilos, sockets, `System.IO` de disco) en runtime.
5. **Errores explícitos**: sin `Debug.Log` como manejo de errores; fallos
   de comunicación con el LMS se reportan por valor de retorno o excepción
   con contexto.
6. **Migración Unity 5 → Unity 6**: el proyecto conserva APIs antiguas
   (`Application.ExternalCall`, `WebPlayerTemplates`). Cualquier cambio de
   API obsoleta es una feature propia, no un efecto colateral.

## Qué NO hacer

- No mezclar lógica SCORM con UI de escena.
- No `GameObject.Find`/`FindObjectOfType` por frame.
- No añadir frameworks nuevos sin feature acordada en `feature_list.json`.
- No editar escenas/prefabs fuera del plan aprobado
  (`.harness/progress/plan_<feature>.md`). `editor_write_policy` es
  `not_applicable`: el agente solo escribe C# y deja instrucciones.
