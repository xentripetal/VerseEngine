---
description: Guide for creating a new ECS system
---

Guide the user through creating a new ECS system.

Ask the user:
1. What should the system be called?
2. What components does it need to access?
3. What resources does it need (if any)?
4. Should it be a method-based or class-based system?
5. Which SystemSet should it belong to?

Then create the system following VerseEngine conventions:
- Use proper naming (e.g., UpdateVelocitySystem or UpdateVelocity)
- Set up correct query with read/write access
- Add necessary resource parameters
- Include XML documentation
- Add to appropriate SystemSet

## Code Generation with [Schedule] Attribute

For method-based systems, use Verse.ECS.Generator:

**Important**:
- The class must be `partial` to allow code generation
- Methods must have the `[Schedule]` attribute to trigger code generation
- The generator will create wrapper code for dependency injection

**Example**:
```csharp
public partial class GameSystems
{
    [Schedule]  // Triggers code generation
    public void UpdateVelocity(
        Query<Data<Transform, Velocity>, Without<Dead>> query,
        Res<Time> time)
    {
        foreach (var (entity, data) in query)
        {
            // System logic
        }
    }

    [Schedule(Schedules.Startup)]  // Can specify schedule
    public void InitializeGame(Commands commands)
    {
        // Initialization logic
    }
}
```

**The [Schedule] attribute**:
- Required for code generation to recognize the method as a system
- Can optionally specify which schedule to run in: `[Schedule(Schedules.Startup)]`
- Default is `Schedules.Update` if not specified
- The generator creates all the boilerplate for system registration

Show the user example code and ask where they want it created.
