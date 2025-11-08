# ADR 002: Allow Resources to be Structs

## Status
Accepted (Implemented)

## Context
Resources in VerseEngine are global singleton values accessible to all systems. Initially, resources were restricted to reference types (classes).

This caused several issues:
1. **Simple configuration** structs couldn't be used as resources
2. **Value semantics** were sometimes more appropriate for immutable config
3. **Consistency** with component system (which now allows both)
4. **Performance** - unnecessary allocations for simple data

## Decision
Allow resources to be either **structs** or **classes**, mirroring the flexibility provided for components.

## Implementation
- Resource storage system updated to handle both types
- `Res<T>` and `ResMut<T>` work with both structs and classes
- Value type resources are boxed when stored
- FromWorld trait works for both types

## Consequences

### Positive
- **Consistency**: Resources and components follow same pattern
- **Flexibility**: Can use appropriate type for use case
- **Simple configs**: Struct-based configuration is natural
- **Immutability**: Value types encourage immutable resources

### Negative
- **Boxing overhead**: Struct resources are boxed (heap allocation)
- **Value copying**: Accessing struct resources involves copy
- **Mutation complexity**: Modifying struct resources requires ResMut and careful handling

### Guidance
- **Use classes** for:
  - Large, mutable state (asset managers, rendering state)
  - Resources that need reference semantics
  - Resources accessed frequently by many systems

- **Use structs** for:
  - Small, immutable configuration (<64 bytes)
  - Data that should be copied (preventing shared mutation)
  - Simple value containers

## Examples

**Struct Resource** (Good):
```csharp
public struct GameConfig
{
    public float Gravity;
    public int MaxPlayers;
}
```

**Class Resource** (Good):
```csharp
public class AssetManager
{
    private Dictionary<string, Asset> _assets;
    public void LoadAsset(string path) { }
}
```

## References
- Git commit: "Allow resources to be structs"
- Related: ADR 001 (Class Components)
