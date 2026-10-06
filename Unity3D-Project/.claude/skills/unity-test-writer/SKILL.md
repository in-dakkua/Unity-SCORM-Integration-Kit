---
name: unity-test-writer
description: Write Unity tests using NUnit and the Unity Test Framework. Covers EditMode and PlayMode tests, test assembly setup, and testing patterns for MonoBehaviours, ScriptableObjects, and pure C# logic. Use when asked to "write tests for this Unity script", "create unit tests", "add PlayMode tests", "set up the test assembly", or "test this MonoBehaviour".
---

> **Nota del harness (bundled con `unity-dev-skills`):** las rutas
> `Assets/_Project/Tests/{EditMode,PlayMode}` y los namespaces
> `ProjectName.Tests.*` de abajo son el default standalone de esta skill.
> En este proyecto usa siempre `<tests_root>` de
> `.harness/unity.config.json` (revisa también `<scripts_root>` para saber
> qué produccion assembly referenciar) y el estilo de
> `.harness/docs/conventions.md` (sección "Tests") en vez de los nombres
> de abajo. Si el modo de verificación configurado
> (`.harness/unity.config.json` → `verification_mode`) es
> `"plan_and_visual"` sin tests automáticos, no apliques esta skill sin
> confirmar antes con el humano — documenta explícitamente por qué no hay
> test, como pide `.claude/agents/implementer.md`. Ver
> `.harness/docs/unity_dev_skills.md`.

# Unity Test Writer

Unity testing uses the **Unity Test Framework (UTF)** which wraps NUnit. There are two test modes — **EditMode** (no scene, instant, runs in editor) and **PlayMode** (runs in a real scene, frame-accurate). Choosing the right mode is the most important decision in Unity testing.

---

## Phase 1 — Identify What to Test and Which Mode

### EditMode Tests
Use for:
- Pure C# logic (math, data processing, state machines, utilities)
- ScriptableObject logic
- Data validation
- Editor tools and inspectors

**Cannot** test: physics, coroutines (natively), scene hierarchy, Input System.

### PlayMode Tests
Use for:
- MonoBehaviour lifecycle (Awake, Start, OnEnable, OnDisable)
- Coroutines
- Physics interactions
- Scene loading
- Animation events
- Anything that requires a real frame loop

**Rule of thumb:** If it compiles without `using UnityEngine;`, use EditMode. If it touches a MonoBehaviour or needs time to pass, use PlayMode.

---

## Phase 2 — Set Up the Test Assembly

### EditMode Assembly (.asmdef)

Create `Assets/_Project/Tests/EditMode/EditMode.Tests.asmdef`:
```json
{
    "name": "ProjectName.Tests.EditMode",
    "rootNamespace": "ProjectName.Tests.EditMode",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "ProjectName.Core",
        "ProjectName.Gameplay"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### PlayMode Assembly (.asmdef)

Create `Assets/_Project/Tests/PlayMode/PlayMode.Tests.asmdef`:
```json
{
    "name": "ProjectName.Tests.PlayMode",
    "rootNamespace": "ProjectName.Tests.PlayMode",
    "references": [
        "UnityEngine.TestRunner",
        "ProjectName.Core",
        "ProjectName.Gameplay"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

**Key points:**
- `"autoReferenced": false` prevents test code from being included in production builds
- `"defineConstraints": ["UNITY_INCLUDE_TESTS"]` further isolates test code
- EditMode must include `"UnityEditor.TestRunner"` and set `"includePlatforms": ["Editor"]`

---

## Phase 3 — Writing EditMode Tests

### Template: Pure C# Logic Test
```csharp
using NUnit.Framework;
using ProjectName.Core;

namespace ProjectName.Tests.EditMode
{
    [TestFixture]
    public class HealthSystemTests
    {
        private HealthSystem _sut; // System Under Test

        [SetUp]
        public void SetUp()
        {
            _sut = new HealthSystem(maxHealth: 100);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up if needed
        }

        [Test]
        public void TakeDamage_ReducesHealth_ByDamageAmount()
        {
            // Arrange
            int expectedHealth = 70;

            // Act
            _sut.TakeDamage(30);

            // Assert
            Assert.AreEqual(expectedHealth, _sut.CurrentHealth);
        }

        [Test]
        public void TakeDamage_WhenDamageExceedsHealth_ClampToZero()
        {
            _sut.TakeDamage(150);
            Assert.AreEqual(0, _sut.CurrentHealth);
        }

        [Test]
        public void TakeDamage_FiresOnDied_WhenHealthReachesZero()
        {
            bool diedFired = false;
            _sut.OnDied += () => diedFired = true;

            _sut.TakeDamage(100);

            Assert.IsTrue(diedFired, "OnDied event was not fired");
        }

        [TestCase(0, false)]
        [TestCase(50, false)]
        [TestCase(100, false)]
        [TestCase(101, true)]
        public void TakeDamage_ParameterizedBoundaryTests(int damage, bool expectsDeath)
        {
            _sut.TakeDamage(damage);
            Assert.AreEqual(expectsDeath, _sut.IsDead);
        }
    }
}
```

### Template: ScriptableObject Test
```csharp
using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Tests.EditMode
{
    [TestFixture]
    public class WeaponDataTests
    {
        private WeaponData _weaponData;

        [SetUp]
        public void SetUp()
        {
            _weaponData = ScriptableObject.CreateInstance<WeaponData>();
            _weaponData.damage = 25;
            _weaponData.fireRate = 0.5f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_weaponData);
        }

        [Test]
        public void Damage_IsPositive()
        {
            Assert.Greater(_weaponData.damage, 0);
        }

        [Test]
        public void FireRate_IsPositive()
        {
            Assert.Greater(_weaponData.fireRate, 0f);
        }
    }
}
```

---

## Phase 4 — Writing PlayMode Tests

PlayMode tests use `[UnityTest]` which returns `IEnumerator` and runs inside a real Unity runtime.

### Template: MonoBehaviour Lifecycle Test
```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ProjectName.Gameplay;

namespace ProjectName.Tests.PlayMode
{
    [TestFixture]
    public class PlayerControllerTests
    {
        private GameObject _playerGO;
        private PlayerController _player;

        [SetUp]
        public void SetUp()
        {
            _playerGO = new GameObject("TestPlayer");
            _playerGO.AddComponent<Rigidbody>();
            _player = _playerGO.AddComponent<PlayerController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_playerGO);
        }

        [UnityTest]
        public IEnumerator PlayerController_OnStart_IsAlive()
        {
            // Wait one frame for Start() to run
            yield return null;

            Assert.IsTrue(_player.IsAlive);
        }

        [UnityTest]
        public IEnumerator TakeDamage_AfterTwoFrames_HealthIsReduced()
        {
            yield return null; // wait for Start

            _player.TakeDamage(30);

            yield return null; // wait one more frame

            Assert.AreEqual(70, _player.Health);
        }

        [UnityTest]
        public IEnumerator HealOverTime_RestoresHealth_After3Seconds()
        {
            yield return null; // Start

            _player.TakeDamage(50);
            _player.StartHealing(healPerSecond: 10f);

            // Wait 3 real seconds (or use WaitForSeconds)
            yield return new WaitForSeconds(3.1f);

            Assert.AreEqual(Mathf.Min(100, 50 + 30), _player.Health,
                "Health should have recovered ~30 points after 3 seconds of healing");
        }
    }
}
```

### Template: Coroutine Test
```csharp
[UnityTest]
public IEnumerator SpawnEnemy_AfterDelay_EnemyExistsInScene()
{
    var spawner = new GameObject().AddComponent<EnemySpawner>();
    spawner.spawnDelay = 0.5f;

    spawner.StartSpawning();

    // Before spawn
    Assert.IsNull(GameObject.FindObjectOfType<Enemy>());

    yield return new WaitForSeconds(0.6f);

    // After spawn
    Assert.IsNotNull(GameObject.FindObjectOfType<Enemy>(),
        "Enemy should have been spawned after delay");
}
```

---

## Phase 5 — Testing Patterns for Common Scenarios

### Testing Events / Callbacks
```csharp
[Test]
public void OnHealthChanged_IsFired_WhenDamageIsApplied()
{
    int callCount = 0;
    float capturedHealth = -1;

    _sut.OnHealthChanged += (newHealth) => {
        callCount++;
        capturedHealth = newHealth;
    };

    _sut.TakeDamage(25);

    Assert.AreEqual(1, callCount, "Event should fire exactly once");
    Assert.AreEqual(75f, capturedHealth, 0.001f);
}
```

### Testing with LogAssert (expected Unity log errors)
```csharp
[Test]
public void TakeDamage_WhenNegativeAmount_LogsError()
{
    UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
        "Damage amount cannot be negative");

    _sut.TakeDamage(-10); // Should log an error, not throw
}
```

### Testing Physics (PlayMode only)
```csharp
[UnityTest]
public IEnumerator Rigidbody_WhenForceApplied_MovesInDirection()
{
    var go = new GameObject();
    go.AddComponent<Rigidbody>();
    var startPos = go.transform.position;

    go.GetComponent<Rigidbody>().AddForce(Vector3.right * 100f, ForceMode.Impulse);

    yield return new WaitForFixedUpdate();
    yield return new WaitForFixedUpdate(); // two physics frames

    Assert.Greater(go.transform.position.x, startPos.x,
        "Object should have moved right");

    Object.Destroy(go);
}
```

---

## Phase 6 — Naming Convention for Tests

Every test method name should follow the pattern:
```
MethodName_Condition_ExpectedResult
```

Examples:
- `TakeDamage_WithPositiveDamage_ReducesHealth`
- `Heal_WhenAtMaxHealth_DoesNotExceedMax`
- `OnEnable_WhenCalled_SubscribesToEvents`
- `Awake_WithMissingRigidbody_LogsError`

---

## Phase 7 — What NOT to Test

- **Unity's own systems**: Don't test that `Physics.Raycast` works — test that *your code* responds correctly when a raycast hits.
- **Editor-only behavior** in PlayMode tests (and vice versa)
- **Serialization field default values** — these change in the editor and tests will be brittle
- **Private methods directly** — test them through their public behavior

---

## Phase 8 — Checklist Before Delivering Tests

- [ ] Test file is in the correct folder (`EditMode/` or `PlayMode/`)
- [ ] Test assembly `.asmdef` references the production assembly being tested
- [ ] `[SetUp]` creates all dependencies fresh; `[TearDown]` destroys GameObjects
- [ ] Test names follow `Method_Condition_Result` pattern
- [ ] No test depends on the order of other tests
- [ ] PlayMode tests use `yield return null` before asserting on `Start()` effects
- [ ] ScriptableObject instances are destroyed with `Object.DestroyImmediate` in TearDown
