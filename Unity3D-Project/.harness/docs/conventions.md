# Convenciones de código (C# / Unity)

> Homogeneidad extrema. La IA predice mejor cuando el repositorio se parece
> a sí mismo en todas partes. Si tu equipo ya tiene un `.editorconfig` o
> guía de estilo propia, esa tiene prioridad — actualiza este archivo para
> que coincida en vez de mantener dos fuentes de verdad.

## Estilo C#

- **Formato:** sigue el `.editorconfig` del proyecto si existe. Si no
  existe, PascalCase para tipos/métodos/propiedades, camelCase para
  variables locales y parámetros.
- **Campos privados:** `_camelCase` con guion bajo. Prefiere
  `[SerializeField] private` sobre campos públicos para exponer algo al
  Inspector.
- **`var`** solo cuando el tipo es obvio por el lado derecho (`var list =
  new List<Note>()`), no cuando oscurece el tipo.
- **Namespaces** que reflejen la carpeta (`Game.Core`, `Game.Gameplay`,
  `Game.UI`), no un único namespace global para todo el proyecto.

## Nombres

| Tipo                       | Convención        | Ejemplo               |
|-----------------------------|-------------------|------------------------|
| Clases / Structs / Enums    | `PascalCase`      | `HealthSystem`        |
| Interfaces                  | `IPascalCase`     | `IDamageable`          |
| Métodos / Propiedades       | `PascalCase`      | `ApplyDamage`          |
| Variables / parámetros      | `camelCase`       | `damageAmount`         |
| Campos privados              | `_camelCase`      | `_currentHealth`       |
| Constantes                  | `UPPER_SNAKE` o `PascalCase` (según convención existente del proyecto) | `MaxHealth` |
| MonoBehaviours              | `PascalCase` + sufijo del rol si ayuda | `PlayerHealthComponent` |

## Estructura de archivo

Un tipo público por archivo, nombre de archivo == nombre del tipo
(`HealthSystem.cs` contiene `class HealthSystem`). Orden dentro de la clase:
campos serializados → campos privados → propiedades → métodos de ciclo de
vida de Unity (`Awake`/`Start`/`Update`, si aplica) → métodos públicos →
métodos privados.

## Tests

- Un archivo de test por tipo relevante de `Core/`:
  `<tests_root>/EditMode/<Tipo>Tests.cs`.
- Usa el Unity Test Framework (NUnit). Nombres de test descriptivos:
  `ApplyDamage_ReducesHealthByAmount`, `ApplyDamage_ClampsAtZero`.
- Lógica en `Core/` → EditMode tests (rápidos, sin GameObjects).
- Comportamiento que depende del ciclo de vida de Unity (coroutines,
  `OnTriggerEnter`, orden de `Update`) → PlayMode tests.
- No mockees `MonoBehaviour`/`GameObject` para probar lógica de `Core/` —
  si necesitas un mock ahí, es una señal de que la lógica debería vivir en
  `Core/` sin dependencia de Unity.

## Manejo de errores

- Excepciones de dominio con nombre propio en `Core/`, no
  `Exception` genérica:

```csharp
public class DamageException : Exception
{
    public DamageException(string message) : base(message) { }
}
```

- El código de `Gameplay/`/`UI/` captura excepciones de dominio y decide
  qué hacer en términos de juego (ignorar, loggear, mostrar feedback) —
  nunca deja que un error de `Core/` reviente el frame sin contexto.
- `Debug.LogError`/`Debug.LogWarning` se usan para diagnóstico, no como
  sustituto de manejar el error donde corresponde.

## Comentarios

Por defecto **no** se escriben. Solo se permiten cuando explican un *por
qué* no obvio (p. ej. un workaround de una versión concreta de Unity, un
orden de ejecución no evidente entre `Update()`s, un valor mágico que viene
de una spec de diseño). Los nombres deben hacer el resto.
