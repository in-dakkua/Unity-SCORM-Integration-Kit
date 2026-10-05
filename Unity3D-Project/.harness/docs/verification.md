# Verificación — Cómo demostrar que el trabajo funciona (Unity)

> Regla de oro: **el agente no dice "funciona", lo demuestra** — y en Unity,
> parte de esa demostración solo la puede dar un humano mirando el Editor.
> El modo de verificación real de este proyecto está en
> `.harness/unity.config.json` → `verification_mode`
> (`strict_tdd` | `plan_and_visual` | `hybrid`).

## Nivel 0 — Plan aprobado (obligatorio si `requires_plan_approval: true`)

Antes de tocar cualquier archivo `.cs`, escena o prefab, el implementer
escribe `.harness/progress/plan_<feature>.md` y **espera aprobación humana
explícita en el chat**. Esto no es un checkbox que el agente pueda marcar
por su cuenta — es, literalmente, el punto de este harness: dar a
developers que prefieren revisar antes de que se ejecute código un punto
de control real.

## Nivel 1 — Tests unitarios EditMode (obligatorio en `strict_tdd` y `hybrid`)

Toda función pública en `Core/` tiene al menos un test EditMode en
`<tests_root>/EditMode/` que:

1. Cubre el camino feliz.
2. Cubre al menos un camino de error si la función puede fallar.

Comando (si Unity está disponible en batchmode — ver `unity_editor_path` en
la config):

```bash
"<UnityEditorPath>" -batchmode -projectPath . -runTests \
  -testPlatform EditMode -testResults ./.harness/progress/editmode-results.xml -quit
```

Si no hay un `unity_editor_path` configurado, corre los tests manualmente
desde **Window > General > Test Runner > EditMode** antes de pedir revisión.

## Nivel 2 — Tests PlayMode (obligatorio para features que dependen del
ciclo de vida de Unity: coroutines, físicas, orden de `Update`, eventos de
colisión)

Igual que el Nivel 1 pero con `-testPlatform PlayMode`. Si la feature no
tiene ningún comportamiento dependiente del motor, no hace falta.

## Nivel 3 — Visual check humano (obligatorio si `requires_visual_check:
true`) — **este nivel no lo puede completar un agente**

El implementer crea `.harness/progress/visual_check_<feature>.md` con esta
plantilla, y pide al humano que lo abra en el Editor y lo firme:

```markdown
# Visual check — feature <id> <name>

## Qué probar
1. Abre la escena `<ruta/a/la/escena>.unity`.
2. Entra en Play Mode.
3. <pasos concretos, p. ej. "reduce la vida del jugador a 0 con la tecla X">
4. Verifica: <qué debería verse/pasar, p. ej. "el UI de vida llega a 0 y se
   dispara la animación de derrota">

## Resultado
- [ ] Se comporta como se espera arriba
- [ ] No hay errores/warnings nuevos en la consola de Unity
- [ ] El prefab/escena no quedó con cambios sin guardar no intencionados

Firmado por: _____________  Fecha: _____________
```

Nadie más que una persona con el Editor abierto puede rellenar la sección
"Resultado" y firmar. Un agente puede *redactar* la plantilla y los pasos,
nunca marcar los checkboxes ni firmar por el humano.

## Modos de verificación (`.harness/unity.config.json` → `verification_mode`)

| Modo             | Nivel 0 (plan) | Nivel 1/2 (tests) | Nivel 3 (visual) |
|-------------------|:--------------:|:------------------:|:-------------------:|
| `strict_tdd`      | según `requires_plan_approval` por feature | Obligatorio | Solo si toca escena/UI |
| `plan_and_visual` | Obligatorio | Opcional — documenta por qué no hay test | Obligatorio |
| `hybrid`          | Obligatorio para features que tocan escena/UI | Obligatorio donde hay lógica pura en `Core/` | Obligatorio donde hay escena/UI/prefab |

## Anti-patrones (no hacer)

- ❌ "He añadido el componente, debería funcionar." → falta evidencia
  ejecutable o el visual check firmado.
- ❌ Un agente marcando el checklist del Nivel 3 o inventando una firma.
- ❌ Test que solo verifica que el método no lanza excepción → tiene que
  comprobar el resultado concreto.
- ❌ Mock de `MonoBehaviour`/`GameObject` para probar lógica de `Core/`.
- ❌ Marcar la feature como `done` sin pasar `.harness/init.sh` y, si
  aplica, sin `.harness/progress/visual_check_<feature>.md` firmado.

## El visual check no es el QA de la release

Si el proyecto sigue la guideline de releases (`release_guideline` en
`.harness/unity.config.json`), no confundas el Nivel 3 con el QA Protocol
de la empresa. El visual check lo firma el developer **en el Editor** para
cerrar **una feature**. El QA Protocol lo ejecuta un tester que **no**
implementó la tarea, sobre un **binario compilado** (nunca desde el
Editor), para aprobar **una release**. Una feature `done` en el harness no
ha pasado QA. Ver `.harness/docs/release_management.md` §6.

## Verificación final antes de cerrar

```bash
.harness/init.sh
```

Si `.harness/init.sh` está rojo, o falta el visual check firmado en una
feature que lo requiere, **no** marques nada como `done`. Anota el bloqueo
en `.harness/progress/current.md` con estado `blocked` en
`.harness/feature_list.json`.
