# Historial de sesiones (append-only)

> Cada cierre de sesión añade una entrada aquí. No se edita ni se borra
> lo existente.

## 2026-10-05 / 2026-10-06 — sesión leader (dguerra@invelon.com)

- Auditoría del kit vs SCORM 2004 / Moodle. Repo estaba en versión Web Player; la WebGL solo en el .unitypackage.
- F001 scorm2004-webgl-e2e-build: kit WebGL sincronizado, puente jslib síncrono, bugs (bucles, vocabulario, InvariantCulture), exportador 3rd/4th, CLI build/package/validate. Commit 3cbe562. Humano confirmó en chat que funciona en SCORM Cloud; estado `blocked` hasta firmar visual check.
- F002 scorm-scenario-api: UpsertObjective por id, RecordInteraction con objetivos, UpdateScore/Status/Progress, evento ScormCall, sin escrituras vacías, M1/M4/M5. APPROVED en ronda 2. Commit 641addd. `done`.
- F003 scorm-scenario-demo: plantilla Demo/Runtime (ScenarioTracker) + DemoUI con log visual + scene builder + zip Builds/SCORM_ScenarioDemo_2004_3rd.zip. READY_FOR_VISUAL_CHECK en ronda 2; pendiente firma humana.
- Abiertos: XSD SCORM 2004 3rd Edition (Plugins/2004_3rd), F004 asmdefs (B3), paquetes com.unity.ai.* del humano inflan WebGL (~+9.5 MB sin comprimir) y quedan sin commitear, error heredado "GUI Layer" en TestApp, LightingData.asset regenerado en cada build, escenas en serialización binaria.
