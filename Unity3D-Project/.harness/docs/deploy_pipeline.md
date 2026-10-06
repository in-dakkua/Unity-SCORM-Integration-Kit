# Despliegue: builds Android / iOS y subida a testing (módulo opcional)

> La elección de cada proyecto vive en `.harness/unity.config.json` →
> claves `deploy_*`, y se decide en `/unity-claude-code-harness` (Paso 5C).
> Este documento es la referencia de ese paso y la que consulta el leader
> antes de ejecutar `/deploy`. Si `deploy_enabled` no es `true`, el módulo
> no existe para ese proyecto: ningún agente lo usa ni lo propone.

## Regla en una frase

**El despliegue lo dispara siempre un humano (`/deploy`), solo lo ejecuta
el leader, el agente nunca ve un secreto, y cada subida a una store
necesita la aprobación explícita de esa subida concreta. Producción no
existe para el agente.**

No hay hooks de VCS, ni CI, ni cron: el módulo no sabe nada de Git ni de
Plastic. `implementer` y `reviewer` no lo ejecutan nunca (igual que con el
control de versiones, ver `.harness/docs/vcs_policy.md`).

## Arquitectura

```
Windows (dev) ─────────────────────────────────────────────────────────────
  /deploy ─► leader ─► .harness/deploy/deploy.ps1
                         │  lee secretos del Windows Credential Manager
                         │  Unity.exe -batchmode -executeMethod HarnessBuild.BuildCli.Build
                         │
   ANDROID ──────────────┤  .aab firmado ─► fastlane (Windows) ─► Google Play · pista internal
                         │
   iOS ──────────────────┘  proyecto Xcode ─► tar | ssh (clave, huella fijada)
                                                   │
Mac de builds (LAN) ───────────────────────────────▼──────────────────────────
  ~/harness/mac_build.sh (lista cerrada de subcomandos)
     receive → ios-build (archive + firma con keychain dedicado) → ios-install (iPhone)
                                                               → ios-upload (TestFlight)
```

- **Android se hace entero en Windows**: compilar, firmar y subir. El Mac
  no toca Android.
- **iOS**: Unity en Windows genera el proyecto Xcode (hace falta el módulo
  *iOS Build Support* en ese Unity). El Mac solo tiene Xcode, CocoaPods y
  fastlane; no necesita Unity.

### Piezas

| Pieza | Dónde | Qué hace |
|---|---|---|
| `.harness/deploy/deploy.ps1` | Windows | Orquestador: `doctor`, `build`, `install`, `upload`, `cleanup` |
| `.harness/deploy/common.ps1` | Windows | Config, Credential Manager (P/Invoke), SSH |
| `.harness/deploy/setup_secrets.ps1` | Windows | Alta de contraseñas del keystore en el Credential Manager. **Lo ejecuta el humano** |
| `.harness/deploy/fastlane/Fastfile` | Windows | Lane `android_internal` (única lane Android) |
| `.harness/deploy/unity_package/com.harness.build/` | → `Packages/` del proyecto | `BuildCli.cs`: build en batchmode (2022.3 y 6000.x) |
| `.harness/deploy/mac/mac_build.sh` | → `~/harness/` del Mac | Receptor y builder iOS, con una lista cerrada de subcomandos |
| `.harness/deploy/mac/fastlane/Fastfile` | → `~/harness/fastlane/` del Mac | Lane `ios_testflight` (única lane iOS) |
| `.harness/deploy/mac/SETUP.md` | — | Configuración del Mac, una vez por Mac y una por dev |
| `.harness/deploy/office_mac.json` | — | IP, cuenta y huella del Mac compartido de la oficina. El Paso 5C los **propone**; `deploy.ps1` no lo lee |
| `.harness/progress/deploy_log.md` | Proyecto | Registro de cada build, instalación y subida. Sin secretos |
| `Builds/harness/<plataforma>/<job>/` | Proyecto | Artefactos y logs de cada job (tiene que estar ignorado en el VCS) |

## Activarlo en un proyecto (cada developer, cada proyecto)

El módulo se activa **por proyecto** y cada developer completa sus pasos
en su máquina. Lo guía el Paso 5C de `/unity-claude-code-harness`
("configura el despliegue"), que va paso a paso y comprueba cada uno:

| Qué | Cada cuánto | Quién |
|---|---|---|
| Plataformas, `deploy_project_id`, permisos, paquete `com.harness.build`, ignores | Por proyecto | El agente, preguntando |
| Módulos Android/iOS en la versión de Unity del proyecto | Por versión de Unity | El developer (Unity Hub) |
| Keystore fuera del proyecto y contraseñas en el Credential Manager (`setup_secrets.ps1`) | Por proyecto | El developer |
| fastlane en Windows (solo si sube a Google Play) | Por máquina | El developer |
| Service account de Google Play / Team ID y API key de Apple | Por app o equipo del cliente | El admin de la cuenta (a menudo el cliente) |
| Clave SSH del developer dada de alta en el Mac (`SETUP.md` bloque F) | Por developer | El developer (y el admin del Mac si hay *forced command*) |
| Keychain de builds y API key por equipo en el Mac (`SETUP.md` bloque E) | Por Mac y equipo de Apple | Alguien en el Mac |

## Modelo de seguridad

| Secreto | Dónde vive | Quién lo lee |
|---|---|---|
| Keystore Android (`.keystore`/`.jks`) | Fuera del proyecto (`deploy_android_keystore_path`) | Unity, durante el build |
| Contraseñas del keystore | Windows Credential Manager: `harness/<project_id>/android_keystore_pass` y `…/android_key_pass` | `deploy.ps1` → Unity, solo en las variables de entorno del proceso hijo |
| JSON de la service account de Google Play | Fuera del proyecto (`deploy_android_play_json_path`) | fastlane en Windows |
| API key de App Store Connect (`.p8`) | Mac: `~/.harness/teams/<TEAM_ID>/AuthKey.p8` (+ `key_id`, `issuer_id`), `chmod 600` | fastlane / xcodebuild en el Mac |
| Certificado de firma iOS | Mac: keychain dedicado `harness-build.keychain-db` | codesign |
| Contraseña de ese keychain | Mac: `~/.harness/keychain_pass`, `chmod 600` | `mac_build.sh` |
| Clave SSH de cada dev | `%USERPROFILE%\.ssh\harness_mac` | ssh |

Reglas que el código impone (no solo la documentación):

- `unity.config.json` solo guarda **referencias**: rutas fuera del repo,
  prefijos de nombres, IDs de equipo y el host. Nunca valores.
  `.harness/init.sh` da FAIL si una ruta de secretos apunta dentro del
  proyecto.
- El agente **nunca** pide, escribe, repite ni lee un secreto. Si falta
  uno, `doctor` dice cuál, y el humano lo da de alta con
  `setup_secrets.ps1` (Windows) o siguiendo `SETUP.md` (Mac).
- `BuildCli.cs` recibe las contraseñas solo por variables de entorno del
  proceso Unity, y **restaura** todos los `PlayerSettings` que toca
  (versión, build, bundle id, team, firma). Lo que **sí** cambia es la
  plataforma activa del proyecto (ver "Unity" más abajo). `deploy.ps1` enmascara
  cualquier secreto conocido en los logs.
- `upload` exige `-ConfirmUpload`, y en el Mac el token `confirmed`. El
  leader solo lo pasa tras la aprobación explícita de **esa** subida.
- **No existe ninguna lane ni subcomando de producción.** Android sube
  siempre a la pista `internal` e iOS a TestFlight. Promocionar a
  producción lo hace el developer en Play Console / App Store Connect.
- SSH solo con clave, `BatchMode`, `StrictHostKeyChecking=yes` y huella
  ED25519 fijada en `deploy_ios_mac_host_fingerprint`. `doctor` falla si
  no coincide.
- El Mac solo acepta la lista cerrada de subcomandos de `mac_build.sh`,
  con argumentos limitados a `[A-Za-z0-9._:-]`. Con *forced command* en
  `authorized_keys` (ver `SETUP.md`), la clave del dev no puede abrir una
  shell.

## Cómo ejecutar los scripts

Siempre así, desde la raíz del proyecto. `-ExecutionPolicy Bypass` evita
que se bloqueen si el harness vino de un zip descargado (Mark of the Web
con la política `RemoteSigned`):

```
powershell -NoProfile -ExecutionPolicy Bypass -File .harness\deploy\deploy.ps1 doctor -Platform android
```

## Tabla de permisos (`deploy_permissions`)

Mismo formato que `vcs_permissions`: en la config solo van los overrides.

| Acción | Default | Notas |
|---|---|---|
| `build_local` | `allowed` | Compilar sin subir (incluye enviar el proyecto Xcode al Mac y hacer el archive) |
| `install_test_device` | `allowed` | Instalar en un iPhone conectado al Mac |
| `upload_internal_testing` | `requires_approval` | Play (pista internal) / TestFlight. Aprobación por subida, siempre |
| `promote_to_production` | `forbidden` | **Fijo**: `init.sh` da FAIL si se cambia. No existe en el código |

## `/deploy`: flujo del leader

Ver `.claude/commands/deploy.md`. Resumen:

1. `deploy_enabled: true` y la plataforma está en `deploy_platforms`; si
   no, se para.
2. `deploy.ps1 doctor -Platform <p>`. Si da FAIL, se para y se reporta.
   Si `deploy_<p>_verified` es `false`, se avisa de que es un camino sin
   probar en este proyecto.
3. Se proponen versión y número de build (`doctor` muestra el actual y
   el sugerido). El humano los confirma o los cambia.
4. `build` (`build_local: allowed`).
5. Opcional en iOS: `install` en el iPhone conectado al Mac. El check
   visual lo hace el humano.
6. Subida: el leader enseña el comando exacto, el artefacto, la versión,
   el build y el destino, **espera un "sí" explícito** y solo entonces
   ejecuta `upload … -ConfirmUpload`.
7. Reporta el resultado y dónde lo ve el humano. Recuerda que producción
   la promociona él.

## Particularidades de cada plataforma

### Android

- **Play App Signing**: el keystore local es la *upload key*. Si se
  pierde y la app usa Play App Signing, el propietario de la cuenta puede
  pedir el reset en *Play Console → Configuración → Integridad de la
  aplicación*. Sin Play App Signing no hay forma de actualizar la app.
  Anota siempre quién custodia el keystore
  (`deploy_android_keystore_owner`) y guarda una copia de seguridad fuera
  del equipo.
- **Primera subida de una app nueva**: tiene que hacerse a mano en Play
  Console (crear la ficha, aceptar Play App Signing, subir el primer
  `.aab`). `/deploy` hace el build y le da la ruta al humano. Desde la
  segunda versión ya sube fastlane. Si la app sigue en borrador, usa
  `-ReleaseStatus draft`.
- `versionCode` tiene que crecer siempre.
- La service account la invita el admin de la cuenta del cliente en
  *Play Console → Usuarios y permisos*, **solo con permisos de testing**
  para esa app.

### iOS

- **Firma por SSH**: el keychain `login` no se desbloquea en una sesión
  SSH ("User interaction is not allowed"). Por eso se usa un keychain
  dedicado con *partition list* (ver `SETUP.md`). `mac_build.sh` exige
  que exista, que se desbloquee y que tenga al menos una identidad
  válida. No se fía del código de salida de `unlock-keychain`.
- **API key de App Store Connect**: la crea el Account Holder o un Admin
  del equipo del cliente, con rol **App Manager**. Sirve para subir a
  TestFlight y para que xcodebuild gestione perfiles.
- **Primera vez**: registrar el Bundle ID y crear la app en App Store
  Connect lo hace el humano. Después, la primera subida a TestFlight ya
  va por el flujo normal.
- **Bundle ID de pruebas**: para instalar en un iPhone con el equipo
  propio (no el del cliente), usa `-BundleId com.<empresa>.test.<app>`.
  Nunca uses el bundle id del cliente con un equipo que no es el suyo.
- Apple tarda un rato en procesar cada build en TestFlight. `upload`
  confirma la subida, no la disponibilidad.
- Si el proyecto Unity genera `Podfile`, el Mac ejecuta `pod install` y
  compila el workspace.

### Unity

- Se usa **exactamente** la versión de `ProjectVersion.txt`, y tiene que
  estar instalada con el módulo de la plataforma. No se compila con una
  versión "parecida".
- El Editor tiene que estar **cerrado** para ese proyecto: batchmode no
  puede abrir un proyecto ya abierto.
- **`-buildTarget` cambia la plataforma activa** del proyecto y se queda
  cambiada: al volver a abrir el Editor estará en Android o iOS. La
  primera vez que se cambia en un proyecto grande hay una reimportación
  larga (cambia `Library/`, no los assets). Avisa al developer antes del
  primer `/deploy` de cada plataforma.
- **Addressables**: si el proyecto los usa, `BuildCli` construye el
  contenido explícitamente antes del player. No depende de la preferencia
  "Build Addressables on Player Build", que se guarda por máquina.
- **Rutas largas**: IL2CPP falla si la ruta del proyecto es muy larga (el
  límite de Windows es de 260 caracteres). `doctor` avisa por encima de
  90.
- Escenas: las activas de *Build Settings*. En Unity 6 no se leen todavía
  los Build Profiles.

## fastlane en Windows (solo Android)

Hace falta una vez por máquina de dev que vaya a **subir** Android. Para
compilar no hace falta. La instalación la hace el humano (Ruby con
RubyInstaller y después fastlane):

1. Buscar la versión de RubyInstaller con DevKit disponible en winget e
   instalarla:
   `winget search RubyInstallerTeam.RubyWithDevKit` → `winget install --id RubyInstallerTeam.RubyWithDevKit.<versión> -e`
2. En una terminal nueva: `ridk install` (opción de la toolchain MSYS2 y
   MINGW), necesaria para las gemas nativas.
3. `gem install fastlane -N`
4. Comprobar con `fastlane --version` y
   `powershell -NoProfile -ExecutionPolicy Bypass -File .harness\deploy\deploy.ps1 doctor -Platform android`.

## Verificación (`deploy_android_verified` / `deploy_ios_verified`)

Mismo patrón que `automation_tool_verified` y `vcs_verified`: se ponen en
`true` **solo** cuando ese camino ha funcionado de verdad en este proyecto
(un build firmado, y la subida si el modo es `build_and_upload`). Mientras
sean `false`, `init.sh` avisa y el leader dice en `/deploy` que es un
camino sin probar. Qué falta se apunta en `deploy_setup_todo`.

## Ignorar artefactos en el VCS del proyecto

El Paso 5C propone añadir esto al `.gitignore` / `ignore.conf` del
proyecto, y pregunta antes de tocarlo:

```
Builds/harness/
.harness/deploy/fastlane/report.xml
*.keystore
*.jks
*.p8
*service-account*.json
```

## Solución de problemas

| Síntoma | Causa probable |
|---|---|
| `Mac no responde en 'X.local'` y luego OK por IP | mDNS no cruza la subred (Wi-Fi ≠ cable). Normal: se usa `deploy_ios_mac_ip` |
| `No ED25519 host key is known` | Falta la huella en `known_hosts` para ese nombre o IP (ver `SETUP.md`) |
| `no existe …harness-build.keychain-db` | Falta el bloque "Keychain de builds" de `SETUP.md` |
| `errSecInternalComponent` al firmar | Falta la *partition list* del keychain (ver `SETUP.md`) |
| `Mac ocupado: …` | Otro dev está compilando. Espera, o si el lock tiene más de 3 h se libera solo |
| Unity `Failed to update player build settings` / `DirectoryNotFound` en IL2CPP | Ruta del proyecto demasiado larga |
| `El proyecto esta abierto en el Editor` | Cierra Unity para ese proyecto |
