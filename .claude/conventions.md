# VerseEngine Coding Conventions

## Naming Conventions

### General C# Standards
- **PascalCase** for types, methods, properties, public fields
- **camelCase** for local variables, parameters, private fields
- **SCREAMING_CASE** for constants (when appropriate)

**Note**: Do NOT use leading underscores for private fields. Use plain camelCase instead.

### ECS-Specific Naming

**Components**:
```csharp
public struct TransformComponent { }  // Suffix with "Component" for clarity
public class WindowComponent { }      // Can be class or struct
```

**Resources**:
```csharp
public class MyAssets { }             // No suffix needed
public struct GameConfig { }          // Can be class or struct
```

**Systems**:
```csharp
public partial class RunSystems { }              // System groups (must be partial)
public void UpdateVelocity(Query<...> query) { } // Method-based systems with [Schedule]
// Or use FuncSystem.Of for lambda-based systems
```

**System Sets**:
```csharp
public enum GameSystemSet               // Enum-based sets
{
    PreUpdate,
    Update,
    PostUpdate
}
```

### Files and Directories

- One type per file (generally)
- File name matches primary type name
- Group related functionality in directories (e.g., `Scheduling/`, `Storage/`)

## Code Organization

### Project Structure

```
src/
├── Verse.Core/           // Foundation - depends on nothing
├── Verse.ECS/            // Core ECS - depends on Core
├── Verse.MoonWorks/      // Graphics layer - depends on Core
├── Verse.Render/         // High-level rendering - depends on ECS + MoonWorks
└── [Feature modules]/    // Specific features - depend on appropriate layers
```

**Dependency Rules**:
- Core modules at bottom (Verse.Core, Verse.ECS)
- Feature modules depend on core, not each other (when possible)
- Circular dependencies are not allowed

### Namespace Conventions

Namespaces match folder structure:
```csharp
namespace Verse.ECS;                    // Root of project
namespace Verse.ECS.Scheduling;         // Subdirectory
namespace Verse.ECS.Systems;            // Another subdirectory
```

Use file-scoped namespaces (C# 10+):
```csharp
namespace Verse.ECS;

public class World { }
```

## Type Conventions

### Components

**Prefer structs** for simple data:
```csharp
public struct Position
{
    public float X;
    public float Y;
    public float Z;
}
```

**Use classes** when needed (but be aware of performance implications):
```csharp
public class MeshComponent  // Complex data or needs inheritance
{
    public List<Vertex> Vertices { get; }
    public List<uint> Indices { get; }
}
```

**⚠️ Performance Warning**: Class components have performance implications:
- Indirection overhead (pointer chasing)
- Poor cache locality
- GC pressure from allocations
- Slower iteration compared to struct components

Use class components only when absolutely necessary (inheritance, very large data, reference semantics required). For performance-critical components, **always prefer structs**.

**Tag components** (zero-size):
```csharp
public struct Disabled { }  // Just a marker, no data
```

### Resources

Resources can be classes or structs:
```csharp
public class AssetManager { }       // Complex, mutable state
public struct GameConfig { }        // Simple configuration
```

### Systems

**Recommended: Method-based systems with [Schedule] attribute**:
```csharp
public partial class GameSystems  // Must be partial for code generation
{
    [Schedule]  // Required for code generation
    public void UpdatePositions(
        Query<Data<Position>, Writes<Velocity>> query,
        Res<Time> time)
    {
        foreach (var (entity, data) in query)
        {
            data.Mut.X += data.Ref.X * time.Value.DeltaTime;
        }
    }
}
```

**Alternative: FuncSystem.Of for lambdas**:
```csharp
// Register lambda as system
app.AddSystems(Schedules.Update,
    FuncSystem.Of<Res<Time>, ResMut<GameState>>((time, state) =>
    {
        state.Value.ElapsedTime += time.Value.DeltaTime;
    })
);
```

## Code Generation

### System Generation

Use partial classes for generated systems:
```csharp
public partial class GameSystems  // Partial allows code generation
{
    [Schedule]  // Required - triggers code generation
    public void MySystem(Query<Data<Transform>> query)
    {
        // Implementation
    }

    [Schedule(Schedules.Startup)]  // Can specify schedule
    public void Initialize(Commands commands)
    {
        // Initialization logic
    }
}
```

### Generator Attributes

- **`[Schedule]`** - **Required** for code generation to recognize methods as systems
  - Triggers Verse.ECS.Generator to create wrapper code
  - Can optionally specify schedule: `[Schedule(Schedules.Startup)]`
  - Default is `Schedules.Update` if not specified
- Generator creates wrapper code for dependency injection

## ECS Patterns

### Query Patterns

**Read-only access** (default, allows parallelization):
```csharp
.With<Transform>(TermAccess.Read)
```

**Write access** (exclusive):
```csharp
.With<Transform>(TermAccess.Write)
```

**Filtering**:
```csharp
var query = new QueryBuilder(world)
    .With<Transform>(TermAccess.Write)
    .With<Velocity>(TermAccess.Read)
    .Without<Disabled>()  // Exclude disabled entities
    .Build();
```

### Resource Access

**Read-only**:
```csharp
public void MySystem(Res<GameConfig> config)
{
    var value = config.Value;  // Read only
}
```

**Mutable**:
```csharp
public void MySystem(ResMut<GameState> state)
{
    state.Value.Score += 10;  // Can modify
}
```

### Deferred Operations

Use `Commands` for structural changes:
```csharp
public void SpawnEnemies(Commands commands, Res<SpawnConfig> config)
{
    var entity = commands.Spawn();
    commands.Insert(entity, new Enemy());
    commands.Insert(entity, new Transform());
}
```

**Never** modify World structure during iteration!

## Performance Guidelines

### Storage Type Selection

**Table storage** (default):
- Use for components iterated frequently
- Examples: Transform, Velocity, Sprite

**SparseSet storage**:
- Use for components added/removed frequently
- Examples: Damaged, Stunned, Selected

### Component Design

**Keep components small**:
```csharp
// Good - focused, small
public struct Velocity
{
    public Vector3 Value;
}

// Bad - too much data, consider splitting
public struct Character
{
    public Stats Stats;
    public Inventory Inventory;
    public QuestLog Quests;
    // ... too many concerns
}
```

**Split large components** into multiple smaller ones

### System Design

**Prefer specificity** in queries:
```csharp
// Good - specific query
Query<Transform, Velocity, Player>

// Bad - filtering after fetch
Query<Transform, Velocity>  // then check if player
```

## Testing Conventions

### Test Structure

```csharp
[Fact]
public void TestName_Scenario_ExpectedBehavior()
{
    // Arrange
    var world = new World();

    // Act
    var entity = world.Spawn().Id();

    // Assert
    Assert.True(world.IsAlive(entity));
}
```

### Test Naming

- Use descriptive names explaining what is tested
- Follow pattern: `MethodName_Scenario_ExpectedResult`
- Example: `AddComponent_WhenEntityExists_ComponentIsAdded`

## Documentation

### XML Documentation

Document public APIs:
```csharp
/// <summary>
/// Spawns a new entity in the world.
/// </summary>
/// <returns>Entity builder for fluent configuration</returns>
public EntityBuilder Spawn() { }
```

### Code Comments

- Explain **why**, not **what** (code shows what)
- Mark TODOs clearly:
```csharp
// TODO: Optimize for large archetype counts
// TODO: track component removals and clear here
```

## Plugin System

### Plugin Structure

```csharp
public class MyPlugin : IPlugin
{
    public void Apply(App app)
    {
        app.AddSchedulable<MySystems>();
        app.InitResource<MyResource>();
        // Configure app
    }
}
```

### Plugin Registration

```csharp
var app = App.Default();
app.AddPlugin(new MoonWorksPlugin(appInfo));
app.AddPlugin(new RenderPlugin());
app.Run();
```

## Error Handling

### Assertions

Use `EcsAssert.Panic` for contract violations:
```csharp
EcsAssert.Panic(condition, "Descriptive error message");
```

### Null Handling

- Use nullable reference types (C# 8+)
- Check for null when appropriate:
```csharp
if (foundArch == null) {
    // Handle missing archetype
}
```

## Git Practices

### Commit Messages

Follow conventional commits:
```
feat: Add sprite batching system
fix: Resolve archetype traversal bug
refactor: Simplify query matching logic
docs: Update ECS architecture documentation
test: Add tests for component hooks
```

### Branch Naming

- Feature branches: `feature/description`
- Bug fixes: `fix/issue-description`
- Claude branches: `claude/<feature>-<session-id>`

## Don'ts

- ❌ Don't modify World structure during query iteration
- ❌ Don't use public fields for complex mutable state
- ❌ Don't create circular dependencies between modules
- ❌ Don't bypass the ECS for game logic
- ❌ Don't store entity references long-term (entities can be recycled)
- ❌ Don't access components without proper query/term
- ❌ Don't forget to mark write access in queries

## Do's

- ✅ Use Commands for deferred structural changes
- ✅ Mark read-only access when possible (enables parallelization)
- ✅ Keep components focused and small
- ✅ Use meaningful names for systems and components
- ✅ Document public APIs with XML comments
- ✅ Write tests for ECS functionality
- ✅ Follow C# coding standards
- ✅ Use code generation for systems when appropriate
