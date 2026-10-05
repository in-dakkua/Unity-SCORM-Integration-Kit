# CHECKPOINTS — Evaluación del estado final (Unity)

> En sistemas multi-agente no se evalúa el camino, se evalúa el destino.
> Estos son los checkpoints objetivos que un juez (humano o IA) puede usar
> para decidir si el proyecto está sano.

## C0 — El proyecto está inicializado

- [ ] Existe `.harness/unity.config.json` con `scripts_root`, `tests_root`
      (arrays — pueden tener una o varias rutas), `unity_version`,
      `automation_tool` y `verification_mode` rellenados (no valores del
      ejemplo sin editar).
- [ ] `ProjectSettings/ProjectVersion.txt` (u otra fuente confirmada por el
      developer) coincide con `unity_version`, o la discrepancia está
      anotada y justificada.
- [ ] `.harness/feature_list.json` **no** tiene `"is_example_data": true`
      si el proyecto ya está trabajando features reales (si sigue en
      `true`, no se está evaluando trabajo real todavía — no falles este
      checkpoint por eso, pero avísalo).
- [ ] Si `automation_tool` no es `none`,
      `automation_tool_verified` es `true` (o, si es `false`, ningún
      cambio bajo revisión dependía de esa herramienta funcionando).
- [ ] Si `automation_tool` es un MCP, `editor_write_policy` no está vacío
      y el trabajo bajo revisión lo respetó (no escribió en escenas/prefabs
      vía MCP si la política es `human_applies_changes`).
- [ ] Si `.harness/feature_source.json` dice `"asana"`, el estado de la
      feature en `.harness/feature_list.json` coincide con el de la tarea
      real en Asana (no se desincronizaron).
- [ ] Si `vcs_provider` no es `none`, `vcs_verified` es `true` (o, si es
      `false`, ninguna acción de escritura de control de versiones bajo
      revisión dependía de que ya estuviera lista).
- [ ] Si `release_guideline` no es `none`, `release_app_name`,
      `release_project_code` y los `branch_*` están rellenos, y
      `release_current_version` (si existe) coincide con `bundleVersion`
      de `ProjectSettings/ProjectSettings.asset` — o la discrepancia está
      explicada (p. ej. el commit de versión todavía está pendiente).

## C1 — El arnés está completo

- [ ] Existen los archivos base: `.harness/AGENTS.md`, `CLAUDE.md`,
      `.harness/AI_TOOLS.md`, `.harness/init.sh`,
      `.harness/feature_list.json`, `.harness/progress/current.md`.
- [ ] Existen los docs: `.harness/docs/architecture.md`,
      `.harness/docs/conventions.md`, `.harness/docs/verification.md`,
      `.harness/docs/mcp_setup.md`, `.harness/docs/vcs_policy.md`,
      `.harness/docs/release_management.md`,
      `.harness/docs/project_rules.md`.
- [ ] `.harness/init.sh` termina con exit code 0.
- [ ] Ninguna regla MUST/MUST NOT de `.harness/docs/project_rules.md` fue
      violada por el cambio bajo revisión.

## C2 — El estado es coherente

- [ ] Como mucho una feature en `in_progress` en
      `.harness/feature_list.json`.
- [ ] Toda feature `done` con `requires_plan_approval: true` tiene un
      `.harness/progress/plan_<feature>.md` que fue aprobado antes de
      implementar.
- [ ] Toda feature `done` con `requires_visual_check: true` tiene un
      `.harness/progress/visual_check_<feature>.md` **firmado por un
      humano** (no generado y auto-marcado por un agente).
- [ ] `.harness/progress/current.md` está vacío o describe la sesión
      activa (no contiene basura de sesiones anteriores).

## C3 — El código respeta la arquitectura

- [ ] El código en cada `scripts_root` configurado respeta la separación
      Core/Gameplay/UI de `.harness/docs/architecture.md` (si hay varias
      rutas, revísalas todas, no solo la primera).
- [ ] No hay lógica de negocio no trivial dentro de `Update()`/callbacks de
      Unity sin delegar a `Core/`.
- [ ] No hay `Debug.Log` sueltos para debug, ni TODOs sin contexto.
- [ ] Si `release_guideline` no es `none`: el cambio no toca
      `bundleVersion`/`bundleVersionCode` salvo que la feature lo pida, y
      no rompe ni hardcodea la versión visible en login, lobby u opciones
      (se lee en runtime, p. ej. `Application.version`). Ver
      `.harness/docs/release_management.md` §1.

## C4 — La verificación es real (según `verification_mode`)

- [ ] Si el modo exige tests: cada `tests_root` configurado tiene al menos
      un test EditMode por tipo relevante de `Core/` bajo su
      `scripts_root` correspondiente, y PlayMode donde aplica.
- [ ] Los tests no mockean `MonoBehaviour`/`GameObject` para probar lógica
      de `Core/`.
- [ ] Los tests corrieron (batchmode o manualmente desde el Test Runner) y
      están en verde — o el motivo de no correrlos está documentado.

## C5 — La sesión se cerró bien

- [ ] No hay archivos sin trackear sospechosos (`*.tmp`, cambios de escena
      no intencionados fuera del `.gitignore`).
- [ ] `.harness/progress/history.md` tiene una entrada por la última
      sesión.
- [ ] La última feature trabajada está reflejada en su estado correcto.
- [ ] Si el leader ejecutó algún commit/push/PR/merge, cada acción
      `requires_approval` de `.harness/docs/vcs_policy.md` tuvo su
      confirmación humana explícita en el chat antes de ejecutarse (no
      basta con que el resultado final se vea bien).
- [ ] Si `release_guideline` no es `none`: toda rama creada por el agente
      sigue un patrón `branch_*`, y el agente no hizo merges a
      `branch_release`/`branch_main` ni creó tags (salvo override
      explícito en `vcs_permissions`). Si se creó o actualizó una tarea de
      release en Asana, la persona aprobó antes los campos exactos.

---

**Cómo usar este archivo:** un agente revisor (`.claude/agents/reviewer.md`)
recorre cada checkbox, marca `[x]` o `[ ]`, y rechaza el cierre de sesión si
quedan boxes vacíos en C0-C5. **C2 y C4 sobre el visual check no las puede
marcar `[x]` el propio agente** basándose en su propia palabra — necesita
el archivo firmado por el humano.
