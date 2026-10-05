---
name: unity-package-manager
description: Safely research, install, update, and remove Unity packages. Prevents version conflicts and broken builds. Use when asked to "add a package to Unity", "install a Unity package", "update a package", "remove a package", "check package compatibility", or when there are errors in packages-lock.json or manifest.json.
---

> **Nota del harness (bundled con `unity-dev-skills`):** `Packages/manifest.json`
> vive fuera de `<scripts_root>`/`<tests_root>`, así que la regla dura del
> rol `leader` ("nunca edites scripts_root/tests_root") no cubre este
> archivo literalmente — pero la práctica de este harness es igual: los
> cambios de paquetes van a través de `implementer` como parte de una
> feature planificada (o de una tarea de mantenimiento pedida
> explícitamente por el humano), nunca los hace `leader` por su cuenta sin
> que quede rastro en `.harness/progress/`. Respeta cualquier regla
> MUST/MUST NOT de `.harness/docs/project_rules.md` sobre paquetes de
> terceros antes de tocar `manifest.json`. Ver
> `.harness/docs/unity_dev_skills.md`.

# Unity Package Manager — Safe Package Operations

Managing Unity packages incorrectly is one of the most common causes of broken projects. This skill ensures every package operation is safe, reversible, and well-documented.

---

## Core Principle

**Always research before you modify.** The only files you should ever touch are:
- `Packages/manifest.json` — the source of truth for what packages the project wants
- Scoped registry entries within `manifest.json`

Never manually edit `Packages/packages-lock.json`. Unity regenerates it automatically.

---

## Phase 1 — Read the Current State

Before any operation, read the project's current package state:

```bash
# Read current manifest
cat Packages/manifest.json

# Check Unity version
cat ProjectSettings/ProjectVersion.txt
```

Extract:
1. The **Unity version** (e.g., `2022.3.15f1`) — critical for compatibility lookups
2. The **currently installed packages** and their versions
3. Any **scoped registries** already configured

---

## Phase 2 — Research Package Compatibility

Before adding or updating a package, verify compatibility. Use WebFetch or WebSearch to check:

### For Unity Registry packages (com.unity.*):
- Fetch the package changelog from: `https://docs.unity3d.com/Packages/<package-name>/changelog/CHANGELOG.html`
- Check the "Verified" column in the Unity Package Manager docs for the target Unity version
- Use this URL pattern: `https://docs.unity3d.com/Manual/upm-ui-install.html`

### For third-party packages (OpenUPM, GitHub):
- Check the package's README for the minimum Unity version
- Look for `package.json` in their repository — the `"unity"` field specifies the minimum version
- Check issues/PRs for known conflicts with commonly used packages

### Compatibility Matrix — Common Packages (Unity 2022.3 LTS):

| Package | Safe Version | Notes |
|---|---|---|
| com.unity.inputsystem | 1.7.0 | Stable; disable old input in Player Settings |
| com.unity.addressables | 1.21.21 | Requires TextMeshPro |
| com.unity.textmeshpro | 3.0.9 | Auto-imported with UGUI |
| com.unity.cinemachine | 2.9.7 | v3.x requires Unity 2023+ |
| com.unity.render-pipelines.universal | 14.0.x | Must match Unity 2022.3.x minor |
| com.unity.netcode.gameobjects | 1.7.1 | Requires Unity Transport |
| com.unity.services.authentication | 3.3.3 | Requires Core package |
| com.unity.test-framework | 1.1.33 | Stable for 2022.3 |

---

## Phase 3 — Installing a Package

### Option A: Unity Registry Package (com.unity.*)

Edit `Packages/manifest.json` and add the package to `"dependencies"`:

```json
{
  "dependencies": {
    "com.unity.inputsystem": "1.7.0",
    ... existing packages ...
  }
}
```

**Safety rules:**
- Add one package at a time when possible
- If the package replaces an existing one (e.g., Input System replaces legacy Input), note this to the user
- Check if the package requires configuration after install (e.g., Input System requires enabling in Player Settings)

### Option B: OpenUPM Package

When a package is hosted on OpenUPM (scoped registry), add BOTH the scoped registry AND the dependency:

```json
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.cysharp.unitask",
        "com.neuecc.r3"
      ]
    }
  ],
  "dependencies": {
    "com.cysharp.unitask": "2.3.3",
    ... existing packages ...
  }
}
```

**Important:** The scope must match the package's namespace prefix exactly. If the scope is wrong, Unity will still try to resolve from the Unity registry and fail.

### Option C: Git URL Package

For packages hosted on GitHub:

```json
{
  "dependencies": {
    "com.example.package": "https://github.com/author/repo.git#v1.2.3",
    ... existing packages ...
  }
}
```

**Warnings to communicate to the user:**
- Git packages do NOT auto-update — the exact commit or tag is pinned
- They add network requests every time the project opens
- Not recommended for team projects unless the repo is stable

---

## Phase 4 — Removing a Package

1. Identify if any other package **depends on** the package you're removing
2. Search the codebase for `using` directives referencing the package's namespaces:
   ```bash
   grep -r "using <NamespacePrefix>" Assets/ --include="*.cs"
   ```
3. If found, warn the user about the scripts that will break
4. Remove the entry from `manifest.json`
5. If it was the only package using a scoped registry, remove that registry entry too

---

## Phase 5 — Updating a Package

1. Check the package's changelog for **breaking changes** between the current and target version
2. Pay special attention to major version bumps (e.g., Cinemachine 2.x → 3.x)
3. Search the project for deprecated API usage if updating a major version:
   ```bash
   grep -r "OldApiMethod\|OldClassName" Assets/ --include="*.cs"
   ```
4. Update the version in `manifest.json`
5. Document what changed in a comment above the package entry (as a `//` comment is not valid JSON — instead, tell the user to add this to their project README or CHANGELOG)

---

## Phase 6 — Diagnosing Package Errors

### Common errors and fixes:

**"Package resolution error: Tarball checksum mismatch"**
- Delete `Packages/packages-lock.json` and let Unity regenerate it
- Do NOT edit packages-lock.json manually

**"Error: Assembly 'X' is invalid"**
- A package has a dependency that's missing or version-mismatched
- Check if the package requires another package that's not in manifest.json

**"The type or namespace 'X' could not be found"**
- Check if the relevant asmdef has a reference to the package's assembly
- Go to the .asmdef file → Inspector → Assembly References → add the package assembly

**"Package not compatible with Unity version X"**
- Check the package's `package.json` → `"unity"` field
- Downgrade the package version or upgrade Unity

**Circular dependency error**
- Never happens with Unity Registry packages; can happen with custom asmdefs
- Use the Assembly Definition References window to diagnose

---

## Phase 7 — Post-Install Verification

After modifying `manifest.json`:

1. Tell the user to switch back to Unity (it will auto-resolve packages)
2. Watch for compile errors in the Console
3. Verify the package appears in Window → Package Manager → In Project
4. If the package has a required setup step (e.g., running a setup wizard), tell the user explicitly

---

## Manifest.json Formatting Rules

- Valid JSON only — no comments, no trailing commas
- Use 2-space indentation (Unity's default)
- Keep packages alphabetically sorted within `"dependencies"` for readability
- Always validate JSON before saving: mentally trace braces and brackets

## What NEVER to Do

- **Never** delete `packages-lock.json` unless diagnosing a checksum error
- **Never** add the same package twice with different names
- **Never** mix scoped registry packages and Unity Registry packages with the same namespace
- **Never** install pre-release versions for production projects unless specifically requested
