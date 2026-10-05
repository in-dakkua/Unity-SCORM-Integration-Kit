---
name: unity-code-review
description: Perform a thorough code review of Unity C# scripts. Catches Unity-specific issues including GC allocations in hot paths, MonoBehaviour lifecycle misuse, missing null checks, performance pitfalls, and architectural anti-patterns. Use when asked to "review this Unity script", "check my C# code", "find bugs in this MonoBehaviour", "optimize this Unity code", or "code review".
---

> **Nota del harness (bundled con `unity-dev-skills`):** este proyecto ya
> tiene un subagente `reviewer` (`.claude/agents/reviewer.md`) cuyo único
> trabajo es aprobar o rechazar contra
> `.harness/docs/architecture.md`/`conventions.md`/`project_rules.md` y
> `.harness/CHECKPOINTS.md`. Cuando el `reviewer` use esta skill, debe
> aplicar el checklist de las Fases 1-3 de abajo como parte de su
> metodología, pero **nunca ejecuta la Fase 4** ("Offer Fixes" — aplicar
> correcciones directamente al archivo): la regla dura del `reviewer` es
> "nunca edites el código del implementador". La Fase 4 solo la ejecuta el
> `implementer` (al corregir tras un veredicto `CHANGES_REQUESTED`) o una
> sesión dirigida por el humano. Los checkpoints C1/C3 de
> `.harness/CHECKPOINTS.md` ya cubren buena parte de S3/S4 de abajo — usa
> ambos documentos juntos, no en conflicto. Ver
> `.harness/docs/unity_dev_skills.md`.

# Unity C# Code Review

A thorough code review for Unity goes beyond standard C# best practices — it must catch Unity-specific pitfalls that are silent in the compiler but catastrophic at runtime.

---

## Phase 1 — Understand the Context

Before reviewing, determine:
1. **Where is this script used?** (MonoBehaviour on a GameObject, ScriptableObject, pure C# class, Editor script)
2. **How often does it run?** (every frame in Update, on events, once on load)
3. **What's the target platform?** (PC, Mobile, Console) — performance thresholds differ significantly

Read the script completely before commenting on any single issue.

---

## Phase 2 — The Review Checklist

Work through each category below and report all issues found, grouped by severity: **Critical**, **Warning**, **Suggestion**.

---

### 🔴 CRITICAL — Will cause crashes, leaks, or broken builds

#### C1 — Null Reference Errors
```csharp
// ❌ Bad — NullReferenceException if no component found
void Start() {
    GetComponent<Rigidbody>().AddForce(Vector3.up);
}

// ✅ Good
void Start() {
    var rb = GetComponent<Rigidbody>();
    if (rb == null) { Debug.LogError("Missing Rigidbody", this); return; }
    rb.AddForce(Vector3.up);
}
```
Look for: `GetComponent<>()` results used without null check, `FindObjectOfType<>()` results used directly, `[SerializeField]` references used without null guard.

#### C2 — Destroying Objects Still Referenced
```csharp
// ❌ Bad — MissingReferenceException on next frame
Destroy(enemy);
enemy.transform.position = Vector3.zero; // boom

// ✅ Good — null after Destroy
Destroy(enemy);
enemy = null;
```

#### C3 — Coroutine Leaks
```csharp
// ❌ Bad — coroutine outlives the object if it isn't stopped
void OnDisable() { } // forgot to StopAllCoroutines()

// ✅ Good
void OnDisable() { StopAllCoroutines(); }
```
Check: every `StartCoroutine()` must have a corresponding `StopCoroutine()` or `StopAllCoroutines()` in `OnDisable()` or `OnDestroy()`.

#### C4 — Event Subscription Leaks
```csharp
// ❌ Bad — listener never unsubscribed → memory leak + ghost callbacks
void OnEnable() { EventBus.OnPlayerDied += HandleDeath; }

// ✅ Good — symmetric subscribe/unsubscribe
void OnEnable()  { EventBus.OnPlayerDied += HandleDeath; }
void OnDisable() { EventBus.OnPlayerDied -= HandleDeath; }
```
Every `+=` must have a matching `-=` in a disable/destroy lifecycle method.

---

### 🟡 WARNING — Performance problems, especially on mobile

#### W1 — GC Allocations in Hot Paths
These patterns allocate heap memory every frame and trigger the garbage collector:

```csharp
// ❌ Allocates every frame
void Update() {
    var enemies = FindObjectsOfType<Enemy>(); // allocation
    string msg = "Score: " + score;          // string concat allocation
    GetComponents<Collider>();               // returns new array
}

// ✅ Cache references; use StringBuilder or StringFormat for repeated strings
private Enemy[] _enemies;
private StringBuilder _sb = new StringBuilder();

void Start() { _enemies = FindObjectsOfType<Enemy>(); }
void Update() {
    _sb.Clear();
    _sb.Append("Score: ").Append(score);
}
```

**Common GC allocation sources to flag:**
- `FindObjectsOfType<>()`, `FindObjectOfType<>()` in Update/FixedUpdate
- `GetComponents<>()` in Update
- String concatenation with `+` in loops or Update
- `new List<>()` inside Update
- LINQ queries (`Where`, `Select`, etc.) in hot paths — every LINQ call allocates
- `foreach` on Lists (allocates enumerator) — use `for` instead in hot paths

#### W2 — Camera.main in Update
```csharp
// ❌ Camera.main calls FindObjectWithTag internally — O(n) every frame
void Update() { transform.LookAt(Camera.main.transform); }

// ✅ Cache it
private Camera _mainCam;
void Awake() { _mainCam = Camera.main; }
void Update() { transform.LookAt(_mainCam.transform); }
```

#### W3 — Expensive Operations in FixedUpdate
FixedUpdate runs at a fixed timestep (default 50Hz), independent of frame rate. Physics casts (Raycast, OverlapSphere) inside FixedUpdate multiply quickly.
- Prefer event-driven raycasting over polling in FixedUpdate
- Batch physics queries where possible

#### W4 — Update vs. Event-Driven
```csharp
// ❌ Polling every frame when an event would do
void Update() {
    if (Input.GetKeyDown(KeyCode.Space)) Jump();
    if (_health <= 0) Die(); // checked every frame after death
}

// ✅ Use Unity Events, C# events, or Input System callbacks
// Health: call Die() once from the setter, not polled in Update
```

#### W5 — Instantiate/Destroy in Gameplay
Frequent `Instantiate`/`Destroy` causes GC spikes. Flag any `Instantiate`/`Destroy` that happens in Update or in tight loops. Suggest object pooling.

---

### 🔵 SUGGESTION — Maintainability, architecture, clarity

#### S1 — SerializeField vs. Public
```csharp
// ❌ Exposes field unnecessarily to other scripts
public Rigidbody rb;

// ✅ Visible in Inspector, private to other scripts
[SerializeField] private Rigidbody _rb;
```

#### S2 — Naming Conventions
Unity C# standard:
- Private fields: `_camelCase` with underscore prefix
- Public properties: `PascalCase`
- Methods: `PascalCase`
- Constants: `UPPER_SNAKE_CASE` or `PascalCase` (pick one and be consistent)

#### S3 — God MonoBehaviour
If a MonoBehaviour has more than ~200 lines or manages more than one responsibility, suggest splitting it. MonoBehaviours should coordinate; logic should live in plain C# classes that MonoBehaviours call.

#### S4 — Magic Numbers
```csharp
// ❌ What is 0.3f?
if (velocity.magnitude < 0.3f) { ... }

// ✅ Named constant
private const float IDLE_SPEED_THRESHOLD = 0.3f;
if (velocity.magnitude < IDLE_SPEED_THRESHOLD) { ... }
```

#### S5 — RequireComponent
If a MonoBehaviour always needs another component, use `[RequireComponent]` to enforce it:
```csharp
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour { ... }
```

#### S6 — Missing XML Docs on Public API
Any `public` method or property that other scripts will call should have at minimum a one-line `/// <summary>` comment.

---

## Phase 3 — Report Format

Structure the review output as:

```
## Code Review: [FileName].cs

### 🔴 Critical Issues (N)
**[C1] Null reference on line 42**
`GetComponent<Rigidbody>()` result is used without null check. If no Rigidbody is attached, this will throw NullReferenceException at runtime.
Fix: ...

### 🟡 Performance Warnings (N)
**[W1] GC allocation in Update (line 78)**
`FindObjectsOfType<Enemy>()` called every frame allocates a new array. Cache the result in Start().
Fix: ...

### 🔵 Suggestions (N)
**[S2] Naming conventions (lines 12, 23, 45)**
Private fields should use _camelCase prefix: `rb` → `_rb`, `speed` → `_speed`

### ✅ What's Done Well
- Event subscriptions are correctly balanced (OnEnable/OnDisable)
- Coroutines are stopped in OnDisable
- No FindObjectOfType in hot paths
```

Always include a "What's Done Well" section — positive reinforcement matters and tells the developer what patterns to repeat.

---

## Phase 4 — Offer Fixes

After reporting, ask: "¿Quieres que aplique las correcciones directamente al archivo?"

If yes, apply **Critical** fixes first, then **Warnings**, then optionally **Suggestions**. Show a diff-style summary of changes made.

**En este harness:** el rol `reviewer` se detiene siempre en la Fase 3 —
nunca hace esta pregunta ni aplica nada. Solo `implementer` (al resolver un
`CHANGES_REQUESTED`) o una sesión con el humano al mando llegan a la Fase 4.
