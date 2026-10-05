---
name: unity-project-bootstrap
description: Set up a new Unity project from scratch with correct folder structure, .gitignore, Assembly Definition files, and essential packages. Use when starting any new Unity project, when asked to "scaffold a Unity project", "create the Unity folder structure", or "set up a new Unity game project".
---

> **Nota del harness (bundled con `unity-dev-skills`):** este proyecto usa
> el harness de `.harness/`. Antes de crear una sola carpeta, lee
> `.harness/unity.config.json` (`scripts_root`, `tests_root`,
> `architecture_status`) y `.harness/docs/architecture.md`. La estructura
> `_Project/Scripts/{Core,Gameplay,UI,Utils}` de la Fase 2 de abajo es el
> default propio de esta skill como paquete standalone — **solo aplica**
> si `architecture_status` es `"template_default"` y `scripts_root` está
> todavía vacío (proyecto nuevo sin decisión tomada). Si
> `architecture_status` es `"adapted_to_existing"` o `"custom"`, la
> convención real de `.harness/docs/architecture.md` gana siempre, aunque
> use nombres de carpeta distintos (p. ej. `Core/Gameplay/UI` planos bajo
> `<scripts_root>`, sin el prefijo `_Project/`). Los nombres de asmdef
> `ProjectName.*` de abajo son ilustrativos — usa el nombre real del
> proyecto. Ver `.harness/docs/unity_dev_skills.md` para el detalle
> completo de precedencia. Esta skill es para crear un proyecto Unity
> **nuevo**: no la invoques desde el rol `leader` (que nunca toca
> `scripts_root`/`tests_root`) — solo `implementer` con un plan aprobado,
> o una sesión dirigida por el humano.

# Unity Project Bootstrap

When bootstrapping a new Unity project, follow this process precisely. The goal is a clean, scalable foundation that avoids common pain points (missing meta files, GC pressure from poor structure, package conflicts).

---

## Phase 1 — Gather Context

Before creating anything, ask the user for:
1. **Unity version** (e.g., 2022.3 LTS, 6000.0.x) — affects available packages and APIs
2. **Project type**: 2D, 3D, Mobile (iOS/Android), PC, WebGL, or Console
3. **Render pipeline**: Built-in, URP, or HDRP
4. **Template used** in Unity Hub (if any)

If already inside an existing project folder, read `ProjectSettings/ProjectVersion.txt` to detect the Unity version automatically.

---

## Phase 2 — Folder Structure

Create the following folder hierarchy inside `Assets/`. Every folder must have a corresponding `.meta` file — Unity generates these on import, but when creating via script/bash, add placeholder `.meta` files to avoid broken references.

```
Assets/
├── _Project/               ← All project-specific content goes here
│   ├── Art/
│   │   ├── Animations/
│   │   ├── Materials/
│   │   ├── Models/
│   │   ├── Shaders/
│   │   ├── Sprites/
│   │   └── Textures/
│   ├── Audio/
│   │   ├── Music/
│   │   └── SFX/
│   ├── Fonts/
│   ├── Prefabs/
│   │   ├── Characters/
│   │   ├── Environment/
│   │   └── UI/
│   ├── Scenes/
│   │   ├── Boot.unity        ← Entry point scene
│   │   └── Main.unity
│   ├── ScriptableObjects/
│   │   └── Data/
│   └── Scripts/
│       ├── Core/
│       ├── Gameplay/
│       ├── UI/
│       └── Utils/
├── Plugins/                  ← Third-party assets and DLLs
│   └── .gitkeep
├── Resources/                ← Only use when addressables aren't available
│   └── .gitkeep
├── StreamingAssets/
│   └── .gitkeep
└── ThirdParty/               ← Asset Store imports go here, isolated
```

**Rules:**
- The `_Project/` prefix with underscore ensures it sorts to the top in the Editor, separating project code from third-party content.
- Never put scripts directly under `Assets/Scripts/` at the root — always under `_Project/Scripts/` or a named subfolder.
- `Resources/` should be nearly empty; prefer Addressables for dynamic loading.

---

## Phase 3 — .gitignore

Create or update `.gitignore` at the project root with this content:

```gitignore
# Unity generated
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/

# MemoryCaptures
[Mm]emory[Cc]aptures/

# Asset meta data should only be ignored when the corresponding asset is
# also ignored.
!/[Aa]ssets/**/*.meta

# Unity3D generated meta files
*.pidb.meta
*.pdb.meta
*.mdb.meta

# Unity3D generated file on crash reports
sysinfo.txt

# Builds
*.apk
*.aab
*.unitypackage
*.app

# Crashlytics
crashlytics-build.properties

# Visual Studio / JetBrains Rider
.vs/
.idea/
*.suo
*.user
*.userosscache
*.sln.docstates
*.csproj
*.sln

# VS Code
.vscode/

# OS generated
.DS_Store
.DS_Store?
._*
.Spotlight-V100
.Trashes
ehthumbs.db
Thumbs.db
```

---

## Phase 4 — Assembly Definition Files (.asmdef)

Assembly Definitions speed up compilation dramatically and enforce architectural boundaries. Create one `.asmdef` per major script folder.

### Minimum required asmdefs:

**`Assets/_Project/Scripts/Core/Core.asmdef`**
```json
{
    "name": "ProjectName.Core",
    "rootNamespace": "ProjectName.Core",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

**`Assets/_Project/Scripts/Gameplay/Gameplay.asmdef`**
```json
{
    "name": "ProjectName.Gameplay",
    "rootNamespace": "ProjectName.Gameplay",
    "references": ["ProjectName.Core"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

**`Assets/_Project/Scripts/UI/UI.asmdef`**
```json
{
    "name": "ProjectName.UI",
    "rootNamespace": "ProjectName.UI",
    "references": ["ProjectName.Core", "ProjectName.Gameplay"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Replace `ProjectName` with the actual project name.

**Dependency rule:** Core ← Gameplay ← UI. Core must not reference Gameplay or UI.

---

## Phase 5 — packages/manifest.json

Edit `Packages/manifest.json` to add the essential packages. Always check compatibility with the detected Unity version before adding.

### Essential packages for most projects:
```json
{
  "dependencies": {
    "com.unity.textmeshpro": "3.0.9",
    "com.unity.inputsystem": "1.7.0",
    "com.unity.addressables": "1.21.21",
    "com.unity.cinemachine": "2.9.7",
    "com.unity.2d.sprite": "1.0.0",
    "com.unity.ide.rider": "3.0.28",
    "com.unity.ide.visualstudio": "2.0.22",
    "com.unity.test-framework": "1.1.33"
  }
}
```

**Adjust based on project type:**
- 2D project: add `com.unity.2d.animation`, `com.unity.2d.tilemap`
- URP: ensure `com.unity.render-pipelines.universal` matches the Unity version
- Mobile: add `com.unity.mobile.notifications`
- Remove packages not needed — every unused package adds compile time.

**IMPORTANT:** Never manually edit `packages-lock.json`. Only edit `manifest.json`.

---

## Phase 6 — EditorConfig

Create `.editorconfig` at the project root to enforce consistent code style:

```ini
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.cs]
indent_size = 4
csharp_new_line_before_open_brace = all
csharp_indent_case_contents = true
dotnet_sort_system_directives_first = true
```

---

## Phase 7 — Boot Scene Setup

Create a `Boot` scene script pattern for proper initialization order:

```csharp
// Assets/_Project/Scripts/Core/Bootstrap.cs
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectName.Core
{
    /// <summary>
    /// Entry point. Initializes services before loading the main scene.
    /// Place this script on a GameObject in the Boot scene.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private string _mainSceneName = "Main";

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            // Initialize core services here (e.g., AudioManager, SaveSystem)
        }

        private void Start()
        {
            SceneManager.LoadScene(_mainSceneName);
        }
    }
}
```

---

## Phase 8 — Final Checklist

Before considering the project bootstrapped, verify:

- [ ] `.gitignore` is at the project root (same level as `Assets/`)
- [ ] Folder structure matches the spec above
- [ ] Each script folder has an `.asmdef` file
- [ ] `manifest.json` contains only needed packages
- [ ] `.editorconfig` is at the project root
- [ ] Boot scene exists and is listed first in Build Settings
- [ ] No scripts are placed directly under `Assets/` root

## Common Mistakes to Avoid

- **Never** delete `.meta` files — this breaks GUID references throughout the project
- **Never** put game scripts in `Assets/Plugins/` — that's only for pre-compiled DLLs
- **Avoid** using `Resources.Load()` for anything loaded at runtime in a shipped game — use Addressables instead
- **Avoid** circular `.asmdef` references — they cause hard-to-debug compiler errors
