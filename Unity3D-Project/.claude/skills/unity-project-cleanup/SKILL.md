---
name: unity-project-cleanup
description: Clean up and reorganize a Unity project. Fixes missing script references, orphaned meta files, empty folders, misplaced assets, and project settings drift. Use when asked to "clean up the Unity project", "fix missing scripts", "remove empty folders", "organize the project", "fix meta files", or "the project is a mess".
---

> **Nota del harness (bundled con `unity-dev-skills`):** el gate humano de
> la Fase 0 de abajo ("¿está todo commiteado?") no es opcional en este
> harness — trátalo igual que el visual check de
> `.harness/docs/verification.md`: nunca lo saltes ni lo des por hecho. Las
> operaciones destructivas (borrar `.meta` huérfanos, carpetas vacías) además
> deben respetar cualquier regla MUST NOT de `.harness/docs/project_rules.md`
> (p. ej. "nunca toques `Assets/Plugins/ThirdParty/`"). No dejes que el rol
> `leader` invoque esta skill sin supervisión como parte de una orquestación
> automática — es una operación de `implementer` con instrucción explícita,
> o dirigida directamente por el humano. Ver
> `.harness/docs/unity_dev_skills.md`.

# Unity Project Cleanup

Unity project entropy is real — over time, assets get misplaced, folders fragment, meta files go missing or become orphaned, and project settings drift. This skill provides a systematic cleanup process.

**⚠️ ALWAYS ensure the project is committed to git (or otherwise backed up) before cleanup. State this to the user and ask for confirmation before proceeding.**

---

## Phase 0 — Backup Verification

Before touching anything, confirm:
```
"Before I start cleanup, please make sure your project is committed to git or backed up. 
This process will move and delete files. Shall I proceed?"
```

Wait for explicit confirmation.

---

## Phase 1 — Audit the Project Structure

### 1.1 Scan for Structural Issues

Run these searches in the project's `Assets/` directory:

```bash
# Find empty folders (Unity leaves ghost .meta files for them)
find Assets/ -type d -empty

# Find .meta files with no corresponding asset
find Assets/ -name "*.meta" | while read meta; do
    asset="${meta%.meta}"
    if [ ! -e "$asset" ]; then
        echo "ORPHANED META: $meta"
    fi
done

# Find assets with no corresponding .meta file
find Assets/ \( -name "*.cs" -o -name "*.prefab" -o -name "*.mat" -o -name "*.unity" -o -name "*.asset" \) | while read asset; do
    if [ ! -f "${asset}.meta" ]; then
        echo "MISSING META: $asset"
    fi
done

# Find scripts not inside an asmdef scope
find Assets/ -name "*.cs" -not -path "*/Editor/*" -not -path "*/Tests/*"
```

### 1.2 Read the Current Folder Structure

List `Assets/` recursively (depth 3) to understand what exists vs. what the target structure should be. Report the findings to the user.

---

## Phase 2 — Fix Missing Script References

Missing script references show as "Missing (Mono Script)" on Prefabs and GameObjects. These happen when:
- A `.cs` file was moved without moving its `.meta` file
- A `.cs` file was deleted but its GUID is still referenced

### Diagnosing via GUID:

Unity uses GUIDs (stored in `.meta` files) to reference assets. To find what's missing:

```bash
# Find all GUIDs referenced in prefabs/scenes
grep -r "m_Script:" Assets/ --include="*.prefab" --include="*.unity" | grep -v "fileID: 0" > /tmp/script_refs.txt

# Cross-reference with existing script GUIDs
find Assets/ -name "*.cs.meta" -exec grep -l "guid:" {} \; > /tmp/existing_guids.txt
```

### Resolution options (present all to user, let them choose):
1. **Re-link via Editor**: Tell the user to select the prefab → find "Missing (Mono Script)" in Inspector → drag the correct script into the slot
2. **GUID transplant**: If the script was renamed, update the `.meta` file of the new script to use the old GUID (this re-links all existing references automatically)
3. **Accept the loss**: If the script no longer exists and a replacement is needed, write the replacement

---

## Phase 3 — Clean Orphaned .meta Files

Orphaned `.meta` files (`.meta` with no matching asset) are safe to delete because they reference nothing. However, always double-check before deleting.

```bash
# Preview orphaned metas (do NOT delete yet — just list)
find Assets/ -name "*.meta" | while read meta; do
    asset="${meta%.meta}"
    if [ ! -e "$asset" ]; then
        echo "$meta"
    fi
done
```

Present the list to the user and ask: "I found N orphaned .meta files. These can be safely deleted. Shall I remove them?"

Only delete after confirmation:
```bash
find Assets/ -name "*.meta" | while read meta; do
    asset="${meta%.meta}"
    if [ ! -e "$asset" ]; then
        rm "$meta"
    fi
done
```

---

## Phase 4 — Remove Empty Folders

Empty folders in Unity create dangling `.meta` files and clutter the Project window.

```bash
# List empty directories
find Assets/ -type d -empty

# Remove empty directories (after user confirmation)
find Assets/ -type d -empty -delete
```

After removing, also remove their corresponding `.meta` files:
```bash
find Assets/ -name "*.meta" | while read meta; do
    asset="${meta%.meta}"
    if [ ! -e "$asset" ]; then rm "$meta"; fi
done
```

---

## Phase 5 — Reorganize Misplaced Assets

### Target Folder Structure (from unity-project-bootstrap):
```
Assets/_Project/
├── Art/          → Sprites, Textures, Materials, Models, Shaders, Animations
├── Audio/        → Music, SFX
├── Fonts/
├── Prefabs/      → Characters, Environment, UI
├── Scenes/
├── ScriptableObjects/
└── Scripts/      → Core, Gameplay, UI, Utils
```

> Este es el default standalone de la skill hermana `unity-project-bootstrap`.
> En este harness, usa la convención real de `.harness/docs/architecture.md`
> (revisa `architecture_status` en `.harness/unity.config.json`) en vez de
> `_Project/*` si el proyecto ya adoptó otra estructura.

### Identifying Misplaced Assets:

```bash
# Scripts outside _Project/Scripts
find Assets/ -name "*.cs" -not -path "*/_Project/Scripts/*" -not -path "*/Editor/*" -not -path "*/Tests/*" -not -path "*/Plugins/*"

# Scenes outside _Project/Scenes
find Assets/ -name "*.unity" -not -path "*/_Project/Scenes/*"

# Materials outside _Project/Art
find Assets/ -name "*.mat" -not -path "*/_Project/Art/*" -not -path "*/Plugins/*" -not -path "*/ThirdParty/*"
```

### Moving Assets Safely:

**CRITICAL:** Never use `mv` from the shell to move Unity assets — this breaks GUID references and leaves orphaned meta files.

Instead:
1. Present the list of misplaced assets to the user
2. Tell the user: "Unity assets must be moved from within the Unity Editor (Project window → drag and drop, or right-click → Move to). If I move them via shell, the GUIDs will break. I'll give you the list so you can move them in the Editor."
3. Generate a clear, actionable checklist for the user

**Exception:** For newly created files that haven't been imported by Unity yet (no `.meta` file), shell `mv` is safe.

---

## Phase 6 — Clean Up Third-Party Folders

Asset Store packages often install in sprawling, poorly named folders. Suggest consolidating them:

```bash
# List all top-level folders in Assets/ that are NOT _Project, Plugins, ThirdParty, Resources, StreamingAssets
ls Assets/ | grep -v "_Project\|Plugins\|ThirdParty\|Resources\|StreamingAssets\|Editor"
```

For each unmoved third-party folder found, recommend moving it to `Assets/ThirdParty/`. (Again, must be done in the Editor — provide the checklist.)

---

## Phase 7 — Fix Assembly Definition Issues

```bash
# Find .cs files not covered by any asmdef
find Assets/_Project/Scripts -name "*.cs" | head -20

# Find all asmdefs
find Assets/ -name "*.asmdef"
```

Check: every script folder under `_Project/Scripts/` should be covered by an asmdef. Identify uncovered scripts and propose the right asmdef to add them to (or create a new one if the folder is a new domain).

---

## Phase 8 — Review Project Settings Drift

Check these common settings that drift over time:

### Player Settings (`ProjectSettings/ProjectSettings.asset`):
```bash
grep -A 2 "companyName\|productName\|bundleVersion\|defaultScreenWidth\|defaultScreenHeight" ProjectSettings/ProjectSettings.asset
```

Report findings to the user. Ask if they want to update company/product name, version number, or screen resolution defaults.

### Quality Settings (`ProjectSettings/QualitySettings.asset`):
```bash
grep "m_QualitySettingNames\|m_CurrentQuality" ProjectSettings/QualitySettings.asset
```

If there are unused Quality levels (e.g., a mobile project with "Ultra" quality), suggest removing them.

### Physics Settings:
```bash
grep "m_Gravity\|m_DefaultSolverIterations" ProjectSettings/DynamicsManager.asset
```

---

## Phase 9 — Generate Cleanup Report

At the end, produce a structured report:

```
## Unity Project Cleanup Report

### ✅ Completed Automatically
- Deleted N orphaned .meta files
- Removed N empty folders

### 📋 Action Required in Unity Editor
(Must be done via drag & drop in the Project window)
- [ ] Move `Assets/Scripts/PlayerController.cs` → `Assets/_Project/Scripts/Gameplay/`
- [ ] Move `Assets/MyAsset/` → `Assets/ThirdParty/MyAsset/`

### ⚠️ Needs Your Decision
- 3 prefabs have Missing Script references:
  - Assets/_Project/Prefabs/Characters/Enemy.prefab
  - Assets/_Project/Prefabs/UI/HealthBar.prefab
  - Assets/_Project/Scenes/Main.unity (2 objects)

### 📊 Before / After
- Files touched: N
- Orphaned metas removed: N
- Empty folders removed: N
- Scripts needing relocation: N
```

---

## What NEVER to Do

- **Never** delete `.meta` files unless you've confirmed they are orphaned
- **Never** use shell `mv` or OS file manager to move Unity assets that have already been imported
- **Never** delete `ProjectSettings/` files
- **Never** modify `packages-lock.json`
- **Never** delete the `Library/` folder — it's regenerated but takes significant time to rebuild on large projects
