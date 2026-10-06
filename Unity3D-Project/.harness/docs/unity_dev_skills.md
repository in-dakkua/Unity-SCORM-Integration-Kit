# Skills de Unity/C# bundled (`unity-dev-skills`)

> Este documento explica de dónde salen los 5 skills instalados en
> `.claude/skills/` (raíz del proyecto, hermano de `.claude/agents/` y
> `.claude/commands/`), cómo se relacionan con las reglas de este harness,
> y qué rol puede invocar cada uno.

## Son opcionales — el developer decide, nunca se asume

`install.sh`/`install.ps1` copian `.claude/skills/` junto con el resto del
harness sin preguntar (son solo archivos, no ejecutan nada por sí solos).
La pregunta real — "¿los quieres?" — la hace `/unity-claude-code-harness` en su Paso 4B,
igual que pregunta la herramienta de automatización o el modo de
verificación. La respuesta queda en
`.harness/unity.config.json` → `unity_dev_skills_enabled`:

- `true` (o el campo no existe todavía, en una config anterior a este
  paso) → el pack sigue en `.claude/skills/`, activo.
- `false` → el developer pidió quitarlo; `.claude/skills/` ya no existe en
  este proyecto. Si ves este archivo (`unity_dev_skills.md`) pero
  `.claude/skills/` no está, es exactamente esa situación — no lo
  reinstales sin que te lo pidan explícitamente.

**Nada del harness depende de que este pack exista.** `AGENTS.md`,
`.claude/agents/leader.md`/`implementer.md`/`reviewer.md` y
`.harness/CHECKPOINTS.md` funcionan idénticos con o sin él — todas las
referencias a estos skills en esos archivos están redactadas como "si
existe", nunca como una dependencia dura. Quitarlo no rompe nada del
protocolo del harness (plan, visual check, gates humanos); solo significa
que Claude Code no tendrá esa metodología extra de Unity/C# disponible
para auto-invocar.

**Para recuperarlo más tarde:** copia solo esa carpeta desde el
repositorio de la plantilla (`harness-claude`) a este proyecto, p. ej.
`cp -r <ruta-a-la-plantilla>/.claude/skills .claude/` (o el equivalente
`Copy-Item -Recurse` en PowerShell). **No uses `install.sh --force` /
`install.ps1 -Force`** para esto solo: ese flag sobreescribe *todo* el
árbol del harness, incluidos tus `.harness/docs/architecture.md`,
`project_rules.md` y `unity.config.json` ya personalizados — no está
acotado a `.claude/skills/`.

## Procedencia — léelo antes de asumir que es "oficial de Unity"

Estos 5 skills **no son un plugin de Unity Technologies**. Se bundleron a
partir de un plugin de Claude Code encontrado en
`Desktop/Workspace - Daniel G. - Claude Code/unity-dev-skills.plugin`
(formato zip de plugin de Claude Code: `.claude-plugin/plugin.json` +
`skills/<nombre>/SKILL.md`). Su manifest declara:

```json
{
  "name": "unity-dev-skills",
  "version": "0.1.0",
  "author": { "name": "Daniel G.", "email": "dguerra@invelon.com" }
}
```

Es decir: un plugin de terceros (o propio, a juzgar por el autor), no una
release de Unity Technologies. Si en algún momento aparece un plugin/skill
*realmente* publicado por Unity, trátalo como una fuente distinta y
documenta su procedencia por separado — no mezcles el origen de ambos en
este archivo.

El zip original (`unity-dev-skills.plugin`) quedó guardado en la máquina
de quien integró este pack por primera vez — no forma parte de este
repositorio ni se distribuye con el harness. El bloque `plugin.json` de
arriba es la procedencia durable; si necesitas comparar contra el
original, pide el archivo a quien hizo esta integración.

**Sobre "Unity CLI":** este paquete de skills **no incluye** ningún skill
específico de Unity CLI (build/test en batchmode). Esa pieza ya la cubre
el harness por su cuenta — ver `.harness/docs/mcp_setup.md` (opción 3) y
los campos `automation_tool`/`unity_editor_path` de
`.harness/unity.config.json`. No dupliques esa lógica aquí.

## Qué se cambió al integrarlos

El plugin original usa el formato "Claude plugin" (`.claude-plugin/` +
marketplace), que exige un paso interactivo (`/plugin install` o
`--plugin-dir`) por cada máquina/desarrollador. Eso no encaja con
`install.sh`/`install.ps1`, que solo copian archivos — nadie debería tener
que ejecutar un paso extra después de instalar el harness.

Por eso los 5 `SKILL.md` se re-empaquetaron como **skills de proyecto**
planos en `.claude/skills/<nombre>/SKILL.md` (sin `.claude-plugin/`,
sin marketplace). Claude Code descubre e invoca estos skills
automáticamente en cuanto existen en disco, sin ningún paso de
instalación ni entrada en `.claude/settings.json` — sobreviven a un `cp -r`
sencillo, que es exactamente lo que hacen `install.sh`/`install.ps1`.

Los cuerpos de cada skill se mantuvieron casi intactos respecto al
original — solo se añadió, justo después del frontmatter, un bloque de
nota que resuelve los conflictos con las reglas propias del harness (ver
siguiente sección).

## Por qué hicieron falta esas notas — los skills, tal cual venían, chocaban con el harness

| Skill | Conflicto detectado | Cómo se resolvió |
|---|---|---|
| `unity-project-bootstrap` | Impone `Assets/_Project/Scripts/{Core,Gameplay,UI,Utils}` y asmdefs `ProjectName.*` — pero la arquitectura real de cada proyecto vive en `.harness/unity.config.json` (`architecture_status`) y puede ser otra (p. ej. `Core/Gameplay/UI` plano bajo `<scripts_root>`, sin `_Project/`). | Nota al inicio: su estructura solo aplica si `architecture_status` es `"template_default"` y `scripts_root` está vacío; si no, gana `.harness/docs/architecture.md`. |
| `unity-test-writer` | Hardcodea `Assets/_Project/Tests/{EditMode,PlayMode}` — pero la ruta real es `<tests_root>` de `unity.config.json`. | Nota al inicio: usar siempre `<tests_root>` y el estilo de `.harness/docs/conventions.md`. |
| `unity-code-review` | Su Fase 4 ofrece aplicar las correcciones directamente al archivo — pero el rol `reviewer` de este harness tiene la regla dura "nunca edites el código del implementador". | Nota + aviso al final de la Fase 4: el `reviewer` se detiene siempre en la Fase 3; solo `implementer` o el humano llegan a la Fase 4. |
| `unity-package-manager` | Edita `Packages/manifest.json`, que queda **fuera** de `scripts_root`/`tests_root` — no cubierto literalmente por la regla dura del `leader` de "nunca edites scripts_root/tests_root". | Nota: en la práctica de este harness, los cambios de paquetes los hace `implementer` dentro de una feature planificada (o una tarea de mantenimiento explícita), nunca `leader` sin dejar rastro en `.harness/progress/`. |
| `unity-project-cleanup` | Incluye borrados (`rm`, `find -delete`) de `.meta` huérfanos y carpetas vacías — operaciones destructivas dentro de `Assets/`. | Nota: el gate humano de su propia Fase 0 se trata como un gate obligatorio más (igual que el visual check), y se respeta además cualquier regla `MUST NOT` de `.harness/docs/project_rules.md`. No lo invoca `leader` sin supervisión. |

**Regla general de precedencia** (igual que para cualquier otra fuente en
este harness): `.harness/docs/project_rules.md` > `.harness/unity.config.json`
+ `.harness/docs/architecture.md`/`conventions.md` > el contenido de estos
5 `SKILL.md`. Ningún camino/ruta/nombre hardcodeado dentro de un `SKILL.md`
debe ganarle a la config real del proyecto.

## Qué skill usa cada rol

| Rol | Puede invocar | Nunca invoca sin más |
|---|---|---|
| `leader` | Ninguno directamente (nunca toca `scripts_root`/`tests_root`, y delega también el resto) | Todos — su trabajo es orquestar, no ejecutar skills él mismo |
| `implementer` | `unity-test-writer` (al escribir tests de su feature), `unity-package-manager` (si la feature requiere un paquete nuevo, dejando constancia en su plan), `unity-project-bootstrap` (solo si la feature es literalmente crear el proyecto), Fase 4 de `unity-code-review` (al resolver un `CHANGES_REQUESTED`) | `unity-project-cleanup` sin instrucción explícita del humano (es una tarea de mantenimiento, no parte normal de una feature) |
| `reviewer` | Fases 1-3 de `unity-code-review` como metodología de revisión | Fase 4 de `unity-code-review` (aplicar fixes) — regla dura, ver `.claude/agents/reviewer.md` |
| Humano (sesión interactiva libre, sin roles) | Cualquiera de los 5, con los mismos gates (confirmación antes de borrar, etc.) | — |

## Dónde viven los archivos

```
.claude/
└── skills/
    ├── unity-project-bootstrap/SKILL.md
    ├── unity-package-manager/SKILL.md
    ├── unity-code-review/SKILL.md
    ├── unity-test-writer/SKILL.md
    └── unity-project-cleanup/SKILL.md
```

`install.sh`/`install.ps1` copian `.claude/skills/` igual que ya copian
`.claude/agents/` y `.claude/commands/` — sin pasos adicionales para el
developer que instala el harness.
