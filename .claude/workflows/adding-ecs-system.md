# Workflow: Adding a New ECS System

This guide walks through creating a new system in VerseEngine.

## Step 1: Define System Purpose

Determine:
- What components will it access?
- What resources does it need?
- When should it run? (Update, PreUpdate, PostUpdate, etc.)
- What is its relationship to other systems?

## Step 2: Choose System Style

### Option A: Method-Based System (Recommended)

Simple, declarative approach with code generation:

```csharp
public partial class GameSystems
{
    [System]
    public static void UpdateVelocity(
        Query<Transform, Velocity> query,
        Res<Time> time)
    {
        foreach (var (transform, velocity) in query.Iter())
        {
            transform.X += velocity.X * time.DeltaTime;
            transform.Y += velocity.Y * time.DeltaTime;
        }
    }
}
```

### Option B: Class-Based System

For complex systems needing state:

```csharp
public class PhysicsSystem : ClassSystem
{
    private float _accumulator;

    public override void Run(World world, ISystemRunner runner)
    {
        var query = new QueryBuilder(world)
            .With<Transform>(TermAccess.Write)
            .With<Velocity>(TermAccess.Read)
            .Build();

        // System logic here
    }
}
```

## Step 3: Define Component Access

**Important**: Specify correct access for parallel execution!

### Read-Only Access
```csharp
Query<Transform, Velocity> query  // Both read-only by default
.With<Transform>(TermAccess.Read)
```

Allows system to run in parallel with other readers.

### Write Access
```csharp
.With<Transform>(TermAccess.Write)
```

Exclusive access - no other systems can access Transform while this runs.

### Filtering
```csharp
.With<Player>()          // Must have Player
.Without<Dead>()         // Must NOT have Dead
.Optional<Weapon>()      // May have Weapon (can be null)
```

## Step 4: Access Resources

### Read-Only Resource
```csharp
public static void MySystem(Res<GameConfig> config)
{
    float gravity = config.Value.Gravity;
}
```

### Mutable Resource
```csharp
public static void MySystem(ResMut<GameState> state)
{
    state.Value.Score += 10;
}
```

## Step 5: Handle Structural Changes

**Never modify World structure during iteration!**

Use `Commands` instead:

```csharp
public static void SpawnEnemies(
    Commands commands,
    Res<SpawnTimer> timer)
{
    if (timer.ShouldSpawn())
    {
        var entity = commands.Spawn();
        commands.Insert(entity, new Enemy());
        commands.Insert(entity, new Transform());
    }
}
```

Commands are executed at next `ApplyDeferred` point.

## Step 6: Register System in Schedule

### Add to SystemSet

```csharp
schedule
    .AddSystemSet(GameSystemSet.Update)
    .AddSystem<UpdateVelocity>()
    .AddSystem<ApplyGravity>();
```

### Add Ordering Constraints

```csharp
schedule
    .AddSystem<ApplyGravity>()
    .After<UpdateVelocity>();  // Run after UpdateVelocity
```

or

```csharp
schedule
    .AddSystem<UpdateVelocity>()
    .Before<ApplyPhysics>();  // Run before ApplyPhysics
```

## Step 7: Testing

Create tests for your system:

```csharp
[Fact]
public void UpdateVelocity_MovesEntity()
{
    // Arrange
    var world = new World();
    var entity = world.Spawn()
        .Set(new Transform { X = 0, Y = 0 })
        .Set(new Velocity { X = 10, Y = 5 })
        .Id();

    // Act
    UpdateVelocity(/* query, time */);

    // Assert
    var transform = world.Get<Transform>(entity);
    Assert.Equal(10, transform.X);
}
```

## Common Patterns

### Pattern 1: Transform + Velocity Movement
```csharp
public static void MoveEntities(
    Query<Transform, Velocity> query,
    Res<Time> time)
{
    foreach (var (transform, velocity) in query.Iter())
    {
        transform.X += velocity.X * time.DeltaTime;
        transform.Y += velocity.Y * time.DeltaTime;
    }
}
```

### Pattern 2: Spawn on Event
```csharp
public static void SpawnOnTrigger(
    Commands commands,
    Query<SpawnTrigger> triggers)
{
    foreach (var (trigger, entity) in triggers.IterWithEntity())
    {
        var spawned = commands.Spawn();
        commands.Insert(spawned, trigger.Prefab);
        commands.Remove<SpawnTrigger>(entity);
    }
}
```

### Pattern 3: Cleanup/Despawn
```csharp
public static void DespawnDead(
    Commands commands,
    Query<Dead> query)
{
    foreach (var (_, entity) in query.IterWithEntity())
    {
        commands.Despawn(entity);
    }
}
```

## Performance Tips

1. **Prefer specific queries** - Filter in query, not in loop
2. **Mark read-only access** - Enables parallelization
3. **Batch operations** - Use Commands for multiple changes
4. **Avoid allocations** - Reuse collections when possible
5. **Profile hot paths** - Use Verse.Tracing for measurement

## Troubleshooting

### System not running?
- Check it's registered in schedule
- Check ordering constraints aren't circular
- Verify SystemSet is active

### Performance issues?
- Check query specificity
- Verify correct storage type for components
- Profile with Verse.Tracing
- Consider splitting large systems

### Race conditions?
- Ensure proper read/write access declarations
- Check for shared mutable state
- Use Commands for deferred operations
