# Configurar el Mac de builds iOS

Guía de una sola vez por Mac (bloques A–E) y una por dev (bloque F). La
hace un humano: el agente nunca escribe contraseñas. Referencia general:
`.harness/docs/deploy_pipeline.md`.

> Aprendido en la primera instalación (Mac mini, macOS 26, Xcode 27):
> - Comprueba siempre con `whoami` en qué cuenta está la Terminal. Un
>   `su - <admin>` anterior deja la ventana en otra cuenta, y todo lo que
>   hagas se crea allí.
> - Si hay dos Homebrew (`/usr/local` de Intel y `/opt/homebrew` de Apple
>   Silicon), llama siempre a `/opt/homebrew/bin/brew` por su ruta
>   completa.
> - Si `brew reinstall` falla porque ha desaparecido una dependencia
>   (p. ej. `ruby@2.7`), haz `uninstall --ignore-dependencies` e
>   `install` de cero.
> - No aceptes el `chown` que sugiere brew hacia la cuenta de builds:
>   Homebrew es del admin.

## A. Cuenta de builds

Usa una cuenta **estándar** (no admin) dedicada a builds, por ejemplo
`development`. No debe tener Apple IDs personales ni certificados de
otros proyectos en su keychain. Desde esa cuenta:

```bash
whoami
```

## B. Acceso remoto (lo hace un admin)

*Ajustes del Sistema → General → Compartir → Inicio de sesión remoto*:
actívalo, en "Permitir acceso a" deja **solo** la cuenta de builds, y sin
acceso total al disco.

Mantén el Mac despierto: *Ajustes → Energía → Evitar reposo automático*,
o como mínimo "Activar para acceso a la red". Mejor por **cable** y con
**IP reservada** en el DHCP. Los nombres `.local` no cruzan de Wi-Fi a
cable si están en subredes distintas.

## C. Herramientas (lo hace el admin dueño de Homebrew)

Xcode desde la App Store y, con la cuenta admin:

```bash
/opt/homebrew/bin/brew install fastlane cocoapods
```

La cuenta de builds no necesita tocar `xcode-select`: `mac_build.sh` fija
`DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer` en cada
proceso. Para usar otro Xcode en un proyecto concreto, exporta
`HARNESS_DEVELOPER_DIR`.

## D. Scripts del harness (cuenta de builds)

Desde el Windows de un dev, con acceso SSH normal (antes de activar el
forced command del bloque F):

```powershell
ssh development@<IP> "mkdir -p ~/harness/fastlane ~/.harness && chmod 700 ~/harness ~/.harness"
scp .harness\deploy\mac\mac_build.sh development@<IP>:harness/
scp .harness\deploy\mac\fastlane\Fastfile development@<IP>:harness/fastlane/
ssh development@<IP> "chmod 700 ~/harness/mac_build.sh && ~/harness/mac_build.sh doctor"
```

Para **actualizar** el script más adelante hace falta una clave sin forced
command, o hacerlo en persona en el Mac. `deploy.ps1 doctor` muestra la
versión (`mac_build.sh vN`).

## E. Keychain de builds (cuenta de builds, en el Mac, una vez)

Así se puede firmar por SSH. **Primero**, en la Terminal del Mac:
`whoami` tiene que devolver la cuenta de builds.

1. **Exporta el certificado** con el que se va a firmar: *Acceso a
   Llaveros* → llavero *inicio* → *Mis certificados* → clic derecho →
   *Exportar…* → `~/Desktop/dev.p12`, con una contraseña de exportación
   temporal. (Para el equipo de un cliente: importa el `.p12` que te den
   sin pasar por el llavero de inicio.)
2. **Crea la contraseña del keychain**. No se ve al escribirla y queda en
   un fichero `600`:

```bash
mkdir -p ~/.harness && chmod 700 ~/.harness && read -s "KP?Nueva contraseña del keychain de builds: " && print -rn -- "$KP" > ~/.harness/keychain_pass && unset KP && chmod 600 ~/.harness/keychain_pass && echo " OK"
```

3. **Crea el keychain**:

```bash
security create-keychain -p "$(cat ~/.harness/keychain_pass)" harness-build.keychain-db && security set-keychain-settings -lut 3600 harness-build.keychain-db && security unlock-keychain -p "$(cat ~/.harness/keychain_pass)" harness-build.keychain-db && echo OK
```

4. **Importa el certificado**:

```bash
read -s "P12?Contraseña del dev.p12: " && security import ~/Desktop/dev.p12 -k harness-build.keychain-db -P "$P12" -T /usr/bin/codesign -T /usr/bin/security && unset P12 && echo " OK"
```

5. **Permite codesign sin diálogos** (evita `errSecInternalComponent`) y
   añádelo a la lista de búsqueda:

```bash
security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "$(cat ~/.harness/keychain_pass)" harness-build.keychain-db > /dev/null && security list-keychains -d user -s harness-build.keychain-db login.keychain-db && echo OK
```

6. **Borra el `.p12`**: `rm ~/Desktop/dev.p12`.
7. Comprueba desde Windows: `powershell -NoProfile -ExecutionPolicy Bypass -File .harness\deploy\deploy.ps1 doctor -Platform ios`
   debe mostrar `Keychain de builds: N identidad(es) de firma`.

### API key de App Store Connect (por equipo de Apple)

Necesaria para TestFlight, y para que xcodebuild gestione perfiles sin
sesión de Xcode. La crea el Account Holder o un Admin del equipo del
cliente (rol **App Manager**) en *App Store Connect → Usuarios y acceso →
Integraciones → App Store Connect API*. Se descarga una sola vez. En el
Mac, con la cuenta de builds:

```bash
T=<TEAM_ID>; mkdir -p ~/.harness/teams/$T && chmod 700 ~/.harness/teams/$T
mv ~/Downloads/AuthKey_<KEY_ID>.p8 ~/.harness/teams/$T/AuthKey.p8
printf '%s' '<KEY_ID>' > ~/.harness/teams/$T/key_id
printf '%s' '<ISSUER_ID>' > ~/.harness/teams/$T/issuer_id
chmod 600 ~/.harness/teams/$T/*
```

Una carpeta por equipo: un build de un cliente nunca puede firmar con la
clave de otro, porque `mac_build.sh` solo lee la carpeta del `team_id` que
recibe.

### iPhone de pruebas (opcional)

Conéctalo por cable, acepta "Confiar", activa *Ajustes → Privacidad y
seguridad → Modo desarrollador* y haz una vez **▶ Run** desde la
interfaz de Xcode con el equipo y el bundle id de pruebas. Así queda
registrado el dispositivo y descargado el perfil. Después,
`deploy.ps1 install -Platform ios` instala por SSH.

## F. Alta de cada dev (una vez por dev)

En el Windows del dev:

```powershell
ssh-keygen -t ed25519 -f "$env:USERPROFILE\.ssh\harness_mac" -C "<usuario> harness-mac"
type "$env:USERPROFILE\.ssh\harness_mac.pub" | ssh development@<IP> "mkdir -p ~/.ssh && chmod 700 ~/.ssh && cat >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys"
```

La primera conexión pregunta por la huella del Mac. Compárala con
`deploy_ios_mac_host_fingerprint` (`.harness/deploy/office_mac.json` para
el Mac de la oficina) **antes** de aceptar. Si no coincide, no respondas
`yes`: no es el Mac esperado.

**Endurecer** (recomendado, una vez estén dados de alta todos los devs):

1. En `~/.ssh/authorized_keys` del Mac, antepón a cada clave:
   `command="/Users/development/harness/mac_build.sh",no-port-forwarding,no-agent-forwarding,no-X11-forwarding,no-pty `.
   Con eso, esa clave solo puede ejecutar los subcomandos de
   `mac_build.sh`.
2. Un admin desactiva el acceso por contraseña: en
   `/etc/ssh/sshd_config.d/100-harness.conf` pon
   `PasswordAuthentication no` y `KbdInteractiveAuthentication no`, y
   reinicia Remote Login.

## Mantenimiento

- Cada job ocupa unos 7 GB mientras dura. `mac_build.sh` borra la
  DerivedData del job al terminar. Los jobs fallidos se quedan para poder
  revisarlos: `deploy.ps1 cleanup -Platform ios -Job <id>`.
- Si se construye desde la interfaz de Xcode, la DerivedData va a
  `~/Library/Developer/Xcode/DerivedData` y puede crecer mucho. Bórrala de
  vez en cuando (*Xcode → Settings → Locations*).
- `doctor` avisa si quedan menos de 15 GB libres.
