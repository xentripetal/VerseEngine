# ADR 003: Multiple Storage Backends (Table vs SparseSet)

## Status
Accepted (Implemented)

## Context
Different component access patterns have different performance characteristics:
- Some components are iterated frequently (Transform, Velocity)
- Some components are added/removed frequently (Damaged, Selected, Stunned)

A single storage strategy cannot optimize for both patterns.

## Decision
Provide two storage backends that can be selected per-component:

1. **Table Storage** (default)
   - Dense arrays of components
   - Fast iteration (cache-friendly)
   - Slower add/remove (requires moving data)

2. **SparseSet Storage**
   - Sparse lookup structure
   - Fast add/remove (O(1))
   - Slower iteration (indirection)

## Implementation
```csharp
public enum StorageType
{
    Table,      // Default - fast iteration
    SparseSet   // Fast add/remove
}
```

Components can specify their preferred storage type (implementation details TBD).

## Consequences

### Positive
- **Optimized access patterns**: Choose storage for workload
- **Performance tuning**: Fine-grained control over cache behavior
- **Flexibility**: Different components use appropriate storage

### Negative
- **Complexity**: Developers must understand trade-offs
- **Implementation overhead**: Maintaining two storage systems
- **Memory overhead**: Some duplication in sparse set

### Decision Guidelines

**Use Table Storage** (default) for:
- Core game components (Transform, Sprite, Velocity)
- Components present on most entities
- Components accessed in tight loops
- Stable components (rarely added/removed)

**Use SparseSet Storage** for:
- Temporary status effects (Stunned, Burning, Slowed)
- UI selection state (Selected, Hovered)
- Debug markers (DebugDraw, Gizmo)
- Infrequently accessed components

## Performance Characteristics

| Operation | Table | SparseSet |
|-----------|-------|-----------|
| Iteration | O(n) - fast | O(n) - slow |
| Add | O(n) - slow | O(1) - fast |
| Remove | O(n) - slow | O(1) - fast |
| Random access | O(1) | O(1) |
| Cache locality | Excellent | Poor |

## References
- Inspired by: EnTT, Bevy, Unity DOTS
- Implementation: `src/Verse.ECS/Storage/`
