# Workflow: Debugging ECS Queries

Guide for troubleshooting and debugging ECS queries that aren't returning expected results.

## Common Query Issues

### Issue 1: Query Returns No Results

**Symptoms**: Query iteration doesn't execute, or count is 0

**Possible Causes**:

1. **No matching entities exist**
   ```csharp
   // Check if entities with components exist
   var count = world.Query<Transform, Velocity>().Count();
   Log.Info($"Found {count} entities with Transform and Velocity");
   ```

2. **Component type mismatch**
   ```csharp
   // Make sure you're querying the exact component type
   // Not: PlayerController vs Player
   var query = world.Query<Player>();  // Correct type?
   ```

3. **Entities in wrong archetype**
   - Check entity has ALL required components
   - Verify no conflicting Without<T> terms

**Debug Steps**:

```csharp
// 1. Check entity count
Log.Info($"Total entities: {world.EntityCount()}");

// 2. Check component registration
var component = world.GetComponent<Transform>();
Log.Info($"Transform ID: {component.Id}, Size: {component.Size}");

// 3. List all archetypes
world.ListArchetypes();  // If available

// 4. Check specific entity
var entity = world.Entity(entityId);
var hasTransform = entity.Has<Transform>();
var hasVelocity = entity.Has<Velocity>();
Log.Info($"Entity {entityId}: Transform={hasTransform}, Velocity={hasVelocity}");
```

### Issue 2: Query Returns Too Many Results

**Symptoms**: Iteration processes unexpected entities

**Possible Causes**:

1. **Missing Without<T> filters**
   ```csharp
   // Before: processes all entities
   var query = world.Query<Transform>();

   // After: excludes disabled entities
   var query = new QueryBuilder(world)
       .With<Transform>()
       .Without<Disabled>()
       .Build();
   ```

2. **Shared components on multiple entities**
   - Verify components are properly instantiated per entity

**Debug Steps**:

```csharp
// Log all entities in query
foreach (var (transform, entity) in query.IterWithEntity())
{
    Log.Info($"Entity {entity}: {transform}");
}
```

### Issue 3: Query Results Not Updating

**Symptoms**: Query returns stale data after changes

**Possible Causes**:

1. **Changes not applied yet**
   ```csharp
   // Commands are deferred until ApplyDeferred
   commands.Insert(entity, new Transform());
   // ... query won't see it yet ...
   // ApplyDeferred happens here
   // ... now query can see it
   ```

2. **Query caching**
   - Queries cache matched archetypes
   - Should auto-invalidate, but verify `World.LastArchetypeId` changes

**Debug Steps**:

```csharp
// Force query to rematch archetypes
var archId = world.LastArchetypeId;
Log.Info($"Last archetype ID: {archId}");

// Make structural change
world.Entity(entity).Set(new Transform());

var newArchId = world.LastArchetypeId;
Log.Info($"New archetype ID: {newArchId}");
// Should be different!
```

### Issue 4: Null Reference in Optional Components

**Symptoms**: NullReferenceException when accessing optional components

**Cause**: Optional component is absent

**Fix**:

```csharp
// Bad
.Optional<Weapon>()
foreach (var (transform, weapon) in query.Iter())
{
    weapon.Fire();  // NullReferenceException if no weapon!
}

// Good
.Optional<Weapon>()
foreach (var (transform, weapon) in query.Iter())
{
    if (weapon != null)
    {
        weapon.Fire();
    }
}
```

### Issue 5: Component Data Not Updating

**Symptoms**: Changes to components don't persist

**Possible Causes**:

1. **Read-only access**
   ```csharp
   // Bad - read-only access
   .With<Transform>(TermAccess.Read)
   foreach (var transform in query.Iter())
   {
       transform.X = 10;  // Won't persist!
   }

   // Good - write access
   .With<Transform>(TermAccess.Write)
   foreach (var transform in query.Iter())
   {
       transform.X = 10;  // Persists
   }
   ```

2. **Struct vs Class semantics**
   ```csharp
   // Struct component (value type)
   var transform = world.Get<Transform>(entity);
   transform.X = 10;  // Modifying copy!
   world.Set(entity, transform);  // Must set back

   // Class component (reference type)
   var transform = world.Get<WindowComponent>(entity);
   transform.X = 10;  // Modifies original
   ```

## Debugging Tools

### 1. Query Inspection

```csharp
public static void DebugQuery(Query query)
{
    Log.Info($"Query Terms: {string.Join(", ", query.Terms.Select(t => t.Id))}");
    Log.Info($"Entity Count: {query.Count()}");

    // Show matched archetypes (if accessible)
    // foreach (var archetype in query.MatchedArchetypes)
    // {
    //     Log.Info($"Archetype: {archetype}");
    // }
}
```

### 2. Entity Inspector

```csharp
public static void InspectEntity(World world, EcsID entity)
{
    var record = world.GetRecord(entity);
    var archetype = record.Archetype;

    Log.Info($"Entity {entity}:");
    Log.Info($"  Archetype: {archetype.Id}");
    Log.Info($"  Components:");

    foreach (var component in archetype.All)
    {
        Log.Info($"    - {component.Id} (size: {component.Size})");
    }
}
```

### 3. Archetype Visualization

```csharp
public static void VisualizeArchetypes(World world)
{
    // Traverse archetype graph from root
    var visited = new HashSet<Archetype>();
    var queue = new Queue<Archetype>();
    queue.Enqueue(world.Root);

    while (queue.Count > 0)
    {
        var arch = queue.Dequeue();
        if (!visited.Add(arch)) continue;

        Log.Info($"Archetype {arch.Id}: " +
                $"{string.Join(", ", arch.All.Select(c => c.Id))} " +
                $"({arch.Count} entities)");

        // Traverse edges (if accessible)
        // foreach (var edge in arch.Edges)
        //     queue.Enqueue(edge.Target);
    }
}
```

### 4. Change Tracking

```csharp
public static void MonitorChanges(World world)
{
    var tick = world.CurTick;
    Log.Info($"Current tick: {tick}");

    // Check component change tracking
    foreach (var (component, entity) in query.Iter())
    {
        if (component.ChangedSince(tick - 1))
        {
            Log.Info($"Component on {entity} changed");
        }
    }
}
```

## Using Verse.Editor

The built-in ImGui editor provides runtime inspection:

1. **Entity browser**: View all entities and components
2. **Component inspector**: Modify component values
3. **System profiler**: Track system execution times
4. **Query viewer**: See query results in real-time

Enable editor in app:
```csharp
app.AddPlugin(new EditorPlugin());
```

## Performance Profiling

Use `Verse.Tracing` to identify slow queries:

```csharp
using (Tracer.Begin("MyQuery"))
{
    foreach (var (transform, velocity) in query.Iter())
    {
        // Work
    }
}
```

## Common Anti-Patterns

### ❌ Filtering in Loop Instead of Query
```csharp
// Bad - inefficient
foreach (var (entity, player) in allEntities.Iter())
{
    if (player.IsAlive) { }
}

// Good - filter in query
.Without<Dead>()
```

### ❌ Multiple Queries for Same Data
```csharp
// Bad - querying multiple times
var count = query.Count();
foreach (var item in query.Iter()) { }

// Good - reuse iteration
int count = 0;
foreach (var item in query.Iter())
{
    count++;
}
```

### ❌ Structural Changes During Iteration
```csharp
// Bad - modifying World during iteration
foreach (var (entity, dead) in query.IterWithEntity())
{
    world.Despawn(entity);  // DANGER!
}

// Good - use Commands
foreach (var (entity, dead) in query.IterWithEntity())
{
    commands.Despawn(entity);  // Safe, deferred
}
```

## Checklist for Query Issues

- [ ] Verify entities with required components exist
- [ ] Check component types are correct (not similar names)
- [ ] Ensure proper read/write access
- [ ] Verify structural changes are applied (ApplyDeferred)
- [ ] Check for null on optional components
- [ ] Confirm filtering logic (With/Without/Optional)
- [ ] Validate archetype matching
- [ ] Test with simplified query
- [ ] Use logging to inspect results
- [ ] Try runtime editor inspection

## Getting Help

If still stuck:
1. Check `docs/todo/Backlog.md` for known issues
2. Review query implementation in `src/Verse.ECS/Query.cs`
3. Look at test cases in `test/Verse.ECS.Test/`
4. Examine ProjectVerse example usage
