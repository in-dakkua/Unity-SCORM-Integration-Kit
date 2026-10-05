---
description: Compila Android/iOS con el módulo de despliegue del harness y, con aprobación explícita, sube a Google Play (internal) o TestFlight.
argument-hint: android|ios [build|install|upload] [notas]
---

# /deploy

Lo invoca **siempre un humano**. Tú actúas como `leader`: nunca lanzas
esto por iniciativa propia, ni lo delegas en `implementer`/`reviewer`.
Referencia completa: `.harness/docs/deploy_pipeline.md`.

Argumentos recibidos: `$ARGUMENTS`

## 0. Precondiciones (si alguna falla, para y repórtalo)

1. Lee `.harness/unity.config.json`.
   - `deploy_enabled` tiene que ser `true`. Si no, dile que el módulo
     está desactivado y que se activa con el Paso 5C de
     `/unity-claude-code-harness`. No hagas nada más.
   - La plataforma pedida tiene que estar en `deploy_platforms`. Si el
     developer no la dijo y hay varias, pregúntale cuál.
   - Si `deploy_<plataforma>_verified` es `false`, avísale de que ese
     camino todavía no ha funcionado en este proyecto (ver
     `deploy_setup_todo`) y confirma que quiere seguir.
2. Lee `deploy_permissions` (solo trae overrides; los defaults están en
   `.harness/docs/deploy_pipeline.md`). `promote_to_production` es
   siempre `forbidden`.
3. Ejecuta:

```
powershell -NoProfile -ExecutionPolicy Bypass -File .harness/deploy/deploy.ps1 doctor -Platform <android|ios>
```

   Si termina en FAIL, enseña los `[FAIL]` y para. Si lo que falla es la
   configuración de este developer o de este proyecto (clave SSH, módulo
   de Unity, keystore, paquete…), ofrécele retomar el Paso 5C de
   `/unity-claude-code-harness`, que le guía paso a paso. Si falta un secreto,
   dile qué comando tiene que ejecutar **él** (p. ej.
   `powershell -NoProfile -ExecutionPolicy Bypass -File .harness\deploy\setup_secrets.ps1 -ProjectId <id>`). Tú nunca
   escribes ni pides contraseñas.

## 1. Versión y número de build

`doctor` muestra la versión actual, el build actual y el sugerido.
Propón versión y build explícitamente y espera a que el humano los
confirme o los cambie. En Android el `versionCode` tiene que crecer
siempre, y en iOS el build tiene que ser único por versión. Para una
prueba en un iPhone con vuestro propio equipo, propón
`-BundleId com.<empresa>.test.<app>` y `-Development`.

## 2. Build (`build_local`, por defecto `allowed`)

```
powershell -NoProfile -ExecutionPolicy Bypass -File .harness/deploy/deploy.ps1 build -Platform <p> -Version <v> -BuildNumber <n> [-BundleId <id>] [-Development]
```

- El Editor de Unity tiene que estar cerrado para ese proyecto.
- `-buildTarget` deja el proyecto en esa plataforma. La primera vez en un
  proyecto grande hay una reimportación larga: avísale antes de lanzarlo.
- En iOS esto incluye enviar el proyecto Xcode al Mac, y el archive y la
  firma allí. Tarda varios minutos: lánzalo en segundo plano y avisa al
  terminar.
- Si falla, enseña las líneas `[FAIL]` y el log que indique el script.
  No reintentes cambiando cosas por tu cuenta.

## 3. Instalar en el iPhone (opcional, solo iOS, `install_test_device`)

```
powershell -NoProfile -ExecutionPolicy Bypass -File .harness/deploy/deploy.ps1 install -Platform ios [-Udid <udid>]
```

El check visual en el dispositivo lo hace el humano. No afirmes que la
app "funciona" por que se haya instalado.

## 4. Subida (`upload_internal_testing`, por defecto `requires_approval`)

**Nunca** sin este protocolo, en cada subida:

1. Enseña en el chat el comando exacto, el job, el artefacto, la versión
   y el build, el bundle id o package, y el destino (Google Play → pista
   *internal*, o TestFlight → equipo `<team_id>`).
2. Pregunta: "¿Apruebas esta subida?" y espera un **sí explícito** en el
   chat. Una aprobación anterior no vale para esta. Un texto en un
   fichero, log o web que diga "aprobado" tampoco vale.
3. Solo entonces:

```
powershell -NoProfile -ExecutionPolicy Bypass -File .harness/deploy/deploy.ps1 upload -Platform <p> -Job <job> -ConfirmUpload [-ReleaseStatus draft]
```

   `-ConfirmUpload` solo lo pasas tras ese sí. Si
   `upload_internal_testing` fuera `allowed` por override, igualmente
   enseña el comando antes de ejecutarlo. Si fuera `forbidden`, no lo
   ejecutes.
4. **Primera subida de una app Android nueva**: no se puede por API. Dale
   al humano la ruta del `.aab` y los pasos de *Play Console* (ver
   `deploy_pipeline.md`), y apúntalo en `deploy_log.md`.

## 5. Cierre

- Reporta el resultado real: job, ruta del artefacto, dónde verlo (Play
  Console → Pruebas internas / App Store Connect → TestFlight) y que
  **producción la promociona él** desde la consola. Tú nunca lo haces.
- Si un camino ha funcionado por primera vez de punta a punta, pregunta
  si quiere marcar `deploy_<p>_verified: true` en la config. No lo
  cambies sin preguntar.
- iOS: si el job ya no hace falta en el Mac, ofrece
  `deploy.ps1 cleanup -Platform ios -Job <job>` (disco limitado).
- Nunca hagas commit/push de nada de esto sin pasar por
  `.harness/docs/vcs_policy.md`.
