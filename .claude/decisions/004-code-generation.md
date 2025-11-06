# ADR 004: Code Generation for Systems

## Status
Accepted (Evolved)

## Context
ECS systems need to:
1. Declare component dependencies (for scheduling)
2. Access resources and queries
3. Support dependency injection
4. Minimize boilerplate code

Manual implementation requires verbose system definitions with repetitive setup code.

## Decision
Use **source generators** (Roslyn) to automatically generate system wrapper code from simple method signatures.

## Evolution

### Phase 1: FuncSystems + Res Wrappers
Initial approach used functional wrappers with resource boxing.

### Phase 2: Direct Generation (Current)
Improved to:
- Generate systems without FuncSystem wrappers
- Eliminate Res wrapper overhead
- Support direct parameter injection
- Maintain type safety

## Implementation

Developers write simple methods:
```csharp
[System]
public static void UpdateVelocity(
    Query<Transform, Velocity> query,
    Res<Time> time)
{
    // Implementation
}
```

Generator creates:
- System wrapper class
- Dependency tracking
- Query initialization
- Parameter injection

## Consequences

### Positive
- **Reduced boilerplate**: Simple method signatures
- **Type safety**: Compile-time verification
- **Automatic dependency tracking**: For parallel scheduling
- **Developer experience**: Focus on logic, not infrastructure
- **Performance**: No runtime reflection

### Negative
- **Build time**: Code generation adds to compilation
- **Debugging**: Generated code can be harder to debug
- **IDE support**: May lag behind manual code
- **Magic**: Less explicit than manual implementation

### Future Improvements
- [ ] Code gen for labels (backlog item)
- [ ] Better marking of components that don't need ApplyDeferred
- [ ] Cleanup of generator code (marked as messy in backlog)

## Alternatives Considered

### Manual System Classes
```csharp
public class UpdateVelocity : ClassSystem
{
    public override void Run(World world, ISystemRunner runner) { }
}
```
❌ Too verbose, repetitive boilerplate

### Runtime Reflection
❌ Performance overhead, no compile-time safety

### Method Injection (Current)
✅ Best balance of ergonomics and performance

## References
- Implementation: `src/Verse.ECS.Generator/`, `src/Verse.ECS.Internal.Generator/`
- Backlog: "Rewrite ECS Generator to not be so messy"
- Related: Bevy's system parameters, Unity's ISystem
