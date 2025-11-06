# VerseEngine ECS Architecture

## Overview

VerseEngine's ECS implementation is inspired by Bevy (Rust) and Unity DOTS, prioritizing cache-friendly data layouts and parallel execution. The system uses **archetype-based storage** where entities with identical component compositions are grouped together for optimal iteration performance.

## Core Concepts

### Entities

Entities are lightweight identifiers (64-bit unsigned integers) representing game objects. They have no behavior or data themselves - only an ID and a reference to their archetype.

**Key Type**: `EcsID` (alias for `ulong`)

**Entity Records**: Stored in `World._entities` (EntitySparseSet<EcsRecord>)
- Each entity has an `EcsRecord` containing its archetype and row position
- Fast O(1) lookup via sparse set

### Components

Components are pure data containers attached to entities. As of recent development, components can be:
- **Structs** (value types) - Traditional ECS approach, stored inline
- **Classes** (reference types) - For complex components needing inheritance

**Key Types**:
- `ComponentId` - Unique identifier for component type (uint)
- `SlimComponent` - Lightweight component descriptor (ID + Size)
- `Component` - Full component metadata with hooks and creation logic

**Component Registry**: `World.Registry` (ComponentRegistry)
- Maps component types to IDs
- Stores component metadata (size, name, hooks)
- Manages component lifecycle

**Storage Backends**:
```csharp
public enum StorageType
{
    Table,      // Fast iteration, slower add/remove (default)
    SparseSet   // Fast add/remove, slower iteration
}
```

**Tag Components**: Zero-size components (Size == 0) used for filtering without data overhead

### Resources

Resources are global singleton values accessible to all systems. Like components, they can now be either structs or classes.

**Key Type**: `Resources` class in World

Resources use:
- Type-based lookup
- FromWorld trait for initialization
- Dependency injection into systems

### Archetypes

Archetypes are the core of the storage system. An archetype represents a unique combination of components.

**Key Features**:
- **Graph Structure**: Archetypes form a directed graph
  - Edges represent adding/removing components
  - Traversal via `TraverseLeft()` (remove) and `TraverseRight()` (add)
  - Root archetype has no components

- **Component Storage**:
  - Each archetype has column arrays for its components
  - Table storage: Components stored in parallel arrays (AoS layout in memory)
  - SparseSet storage: Separate sparse set per component

- **Entity Movement**: When components are added/removed, entities move between archetypes
  - This is an O(1) swap-remove operation
  - Last entity in the archetype fills the gap

**Archetype Matching**:
- Stored by hash for O(1) lookup: `Archetypes.TryGetFromHashId(hash, out archetype)`
- Hash combines all component IDs using `UnorderedSetHasher.Combine()`

**World Tracking**:
- `World.LastArchetypeId` - Generation counter for archetype changes
- Queries cache matched archetypes and invalidate when this changes

### Queries

Queries provide type-safe, filtered access to entities and components.

**Query Terms**:
```csharp
public enum TermAccess { Read, Write }

- WithTerm(id, access)    // Must have component (read or write access)
- WithoutTerm(id)         // Must NOT have component
- OptionalTerm(id)        // May have component (null if absent)
```

**Query Building**:
```csharp
var query = new QueryBuilder(world)
    .With<Transform>(TermAccess.Write)
    .With<Velocity>(TermAccess.Read)
    .Without<Disabled>()
    .Build();
```

**Query Matching**:
1. Queries match archetypes using superset logic
2. An archetype matches if it has all required components and none of the excluded
3. Matched archetypes are cached until `World.LastArchetypeId` changes
4. `Archetype.GetSuperSets()` traverses archetype graph to find matches

**Iteration**:
- `query.Iter(tick)` returns `QueryIterator`
- Iterates over all matched archetypes
- Provides component arrays for parallel processing

### Systems

Systems contain the logic that operates on entities and components. Systems can be:
- **Methods** - Simple functions with dependency injection
- **Classes** - Implementing ISystem interface for complex logic

**System Scheduling**:
- Systems execute in **SystemSets** which control ordering
- Systems declare their component access (read/write) for parallel execution
- Scheduler automatically parallelizes systems with non-conflicting access

**Code Generation**:
- `Verse.ECS.Generator` generates system wrapper code
- Automatically injects Query, Res<T>, ResMut<T>, Commands parameters
- Handles read/write access tracking for scheduling

**System Parameters**:
- `Query` - Component queries
- `Res<T>` - Read-only resource access
- `ResMut<T>` - Mutable resource access
- `Commands` - Deferred entity/component operations
- `Tick` - Current world tick for change detection

### Change Detection

The engine tracks changes at a tick-granularity level for efficient reactive systems.

**Key Types**:
- `Tick` - Wrapper around uint representing a world update tick
- `World._ticks` - Incremented on each `World.Update()`

**Usage**:
- Components can track when they were last modified
- Systems can query only changed components
- Events use tick-based expiration

### Commands & Deferred Operations

To avoid structural changes during iteration, the engine uses command buffers:

**Commands** class provides:
- `Spawn()` - Create entities
- `Despawn(entity)` - Destroy entities
- `Insert(entity, component)` - Add components
- `Remove(entity, component)` - Remove components

Commands are flushed at **ApplyDeferred** points in the schedule.

## Execution Model

### World Lifecycle

```
World.Init()
  ↓
Loop:
  Schedule.Run(world)     // Execute systems
    → System execution
    → ApplyDeferred points
  World.Update()          // Increment tick, process events
```

### System Execution Flow

```
SystemSet
  ↓
System 1 (parallel) ──┐
System 2 (parallel) ──┤─→ ApplyDeferred
System 3 (parallel) ──┘
  ↓
System 4 (depends on previous)
  ↓
ApplyDeferred
```

### Archetype Operations

**Adding Component**:
1. Get entity's current archetype
2. Find or create target archetype (current + new component)
3. Move entity data to new archetype
4. Update entity record

**Removing Component**:
1. Get entity's current archetype
2. Traverse left in archetype graph
3. Move entity data to new archetype (excluding removed component)
4. Update entity record

## Performance Considerations

### Cache Efficiency
- Archetype storage groups entities with same components
- Iteration is linear over contiguous arrays
- Minimal cache misses during component access

### Parallel Execution
- Systems with non-conflicting component access run in parallel
- Read-only access allows multiple systems simultaneously
- Write access is exclusive per component type

### Memory Layout
- Table storage: AoS (Array of Structs) per archetype
- Components of same type stored contiguously
- Optimal for systems iterating many entities

### Sparse vs Dense
- Table storage: Dense arrays, fast iteration
- SparseSet storage: Sparse lookup, fast add/remove
- Choose based on access patterns

## Key Implementation Files

- `src/Verse.ECS/World.cs` - World management and entity operations
- `src/Verse.ECS/Components.cs` - Component type system
- `src/Verse.ECS/Query.cs` - Query building and iteration
- `src/Verse.ECS/Archetype.cs` - Archetype storage and graph
- `src/Verse.ECS/Scheduling/Schedule.cs` - System scheduling
- `src/Verse.ECS/Systems/SystemSet.cs` - System organization
- `src/Verse.ECS/Storage/` - Storage backend implementations

## Comparison to Other ECS

### vs Bevy (Rust)
- Similar archetype-based approach
- Similar query syntax and system parameters
- VerseEngine adapted for C# language features (no traits, uses interfaces/classes)

### vs Unity DOTS
- Both use archetypes
- VerseEngine focuses on developer ergonomics over raw performance
- Less restrictive component types (allows classes)

### vs EnTT (C++)
- EnTT uses sparse sets exclusively
- VerseEngine offers both table and sparse set storage
- VerseEngine has stronger type safety through C# generics
