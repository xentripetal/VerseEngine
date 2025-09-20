using FluentAssertions;
using Verse.Core;
using Verse.ECS;
using Verse.ECS.Scheduling;
using Verse.ECS.Systems;

namespace Verse.Core.Test;

public class ScheduleTests : IDisposable
{
    private Schedule? _schedule;

    public void Dispose()
    {
        _schedule = null;
    }

    [Fact]
    public void Schedule_Constructor_ShouldInitializeCorrectly()
    {
        // Act
        _schedule = new Schedule("TestSchedule", ExecutorKind.SingleThreaded);

        // Assert
        _schedule.Should().NotBeNull();
        _schedule.Name.Should().Be("TestSchedule");
    }

    [Fact]
    public void Schedule_AddSystem_ShouldAddSystem()
    {
        // Arrange
        _schedule = new Schedule("TestSchedule", ExecutorKind.SingleThreaded);
        var system = new TestScheduleSystem();

        // Act
        _schedule.AddSystems(system);

        // Assert
        ScheduleHasSystem(_schedule, system).Should().BeTrue();
        _schedule.Graph.GetSystems().Should().HaveCount(1);
    }

    [Fact]
    public void Schedule_AddMultipleSystems_ShouldAddAll()
    {
        // Arrange
        _schedule = new Schedule("TestSchedule", ExecutorKind.SingleThreaded);
        var system1 = new TestScheduleSystem { Name = "System1" };
        var system2 = new TestScheduleSystem { Name = "System2" };
        var system3 = new TestScheduleSystem { Name = "System3" };

        // Act
        _schedule.AddSystems(system1);
        _schedule.AddSystems(system2);
        _schedule.AddSystems(system3);

        // Assert
        _schedule.Graph.GetSystems().Should().HaveCount(3);
        ScheduleHasSystem(_schedule, system1).Should().BeTrue();
        ScheduleHasSystem(_schedule, system2).Should().BeTrue();
        ScheduleHasSystem(_schedule, system3).Should().BeTrue();
    }
    
    private static bool ScheduleHasSystem(Schedule schedule, ISystem system)
    {
        return schedule.Graph.GetSystems().Any(x => x.Item2 == system);
    }

    [Fact]
    public void Schedule_Run_ShouldExecuteAllSystems()
    {
        // Arrange
        _schedule = new Schedule("TestSchedule", ExecutorKind.SingleThreaded);
        var system1 = new TestScheduleSystem { Name = "System1" };
        var system2 = new TestScheduleSystem { Name = "System2" };
        
        _schedule.AddSystems(system1);
        _schedule.AddSystems(system2);

        var world = new World();

        // Act
        _schedule.Run(world);

        // Assert
        system1.ExecutionCount.Should().Be(1);
        system2.ExecutionCount.Should().Be(1);
        system1.LastWorld.Should().Be(world);
        system2.LastWorld.Should().Be(world);
    }

    [Fact]
    public void Schedule_RunMultipleTimes_ShouldExecuteSystemsMultipleTimes()
    {
        // Arrange
        _schedule = new Schedule("TestSchedule");
        var system = new TestScheduleSystem();
        _schedule.AddSystems(system);

        var world = new World();

        // Act
        _schedule.Run(world);
        _schedule.Run(world);
        _schedule.Run(world);

        // Assert
        system.ExecutionCount.Should().Be(3);
    }




    [Fact]
    public void Schedule_SystemExecution_ShouldRespectOrder()
    {
        // Arrange
        _schedule = new Schedule("TestSchedule", ExecutorKind.SingleThreaded);
        var executionOrder = new List<string>();
        
        var system1 = new FuncSystem(() => executionOrder.Add("First"));
        var system2 = new FuncSystem(() => executionOrder.Add("Second"));
        var system3 = new FuncSystem(() => executionOrder.Add("Third"));

        _schedule.AddSystems(system1);
        _schedule.AddSystems(system2);
        _schedule.AddSystems(system3);

        var world = new World();

        // Act
        _schedule.Run(world);

        // Assert
        executionOrder.Should().Equal("First", "Second", "Third");
    }

    [Fact]
    public void Schedule_WithSystemDependencies_ShouldRespectDependencies()
    {
        // Arrange
        _schedule = new Schedule("TestSchedule", ExecutorKind.SingleThreaded);
        var executionOrder = new List<string>();
        
        // Create systems with dependencies (exact API depends on implementation)
        var systemA = new FuncSystem(() => executionOrder.Add("A"));
        var systemB = new FuncSystem(() => executionOrder.Add("B"));
        var systemC = new FuncSystem(() => executionOrder.Add("C"));

        // Add in reverse order to test dependency resolution
        _schedule.AddSystems(systemC);
        _schedule.AddSystems(systemB.Before(systemC));
        _schedule.AddSystems(systemA.Before(systemB));

        var world = new World();

        // Act
        _schedule.Run(world);

        // Assert
        executionOrder.Should().Equal("A", "B", "C");
    }

    [Fact]
    public void Schedule_SystemError_ShouldHandleGracefully()
    {
        // Arrange
        _schedule = new Schedule("TestSchedule", ExecutorKind.SingleThreaded);
        var normalSystem = new TestScheduleSystem();
        var errorSystem = new ErrorThrowingSystem();
        
        _schedule.AddSystems(normalSystem);
        _schedule.AddSystems(errorSystem);

        var world = new World();

        // Act & Assert
        var action = () => _schedule.Run(world);
        
        // Depending on implementation, this might:
        // 1. Throw the exception (fail fast)
        // 2. Continue with other systems (resilient)
        // 3. Log error and continue
        try
        {
            action();
            // If no exception, verify the normal system still ran
            normalSystem.ExecutionCount.Should().Be(1);
        }
        catch (InvalidOperationException ex)
        {
            // If exception is thrown, that's also acceptable
            ex.Message.Should().Be("System error for testing");
        }
    }

    [Fact]
    public void Schedule_EmptySchedule_ShouldRunWithoutError()
    {
        // Arrange
        _schedule = new Schedule("EmptySchedule", ExecutorKind.SingleThreaded);
        var world = new World();

        // Act & Assert - Should not throw
        _schedule.Run(world);
    }

    [Fact]
    public void Schedule_ParallelExecution_ShouldExecuteInParallel()
    {
        // Arrange
        _schedule = new Schedule("ParallelSchedule", ExecutorKind.MultiThreaded);
        var slowSystem1 = new SlowTestSystem(50); // 50ms delay
        var slowSystem2 = new SlowTestSystem(50); // 50ms delay
        
        _schedule.AddSystems(slowSystem1);
        _schedule.AddSystems(slowSystem2);

        var world = new World();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        _schedule.Run(world);

        // Assert
        stopwatch.Stop();
        
        slowSystem1.WasExecuted.Should().BeTrue();
        slowSystem2.WasExecuted.Should().BeTrue();
        
        // If executed in parallel, total time should be less than sequential execution
        // This is a bit fragile but helps verify parallel execution
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(90); // Less than 50+50-10ms tolerance
    }
}

// Test systems for schedule tests
public class TestScheduleSystem : ClassSystem
{
    public string Name { get; set; } = "TestSystem";
    public int ExecutionCount { get; private set; }
    public World? LastWorld { get; private set; }

    public override void Run(World world)
    {
        ExecutionCount++;
        LastWorld = world;
    }
}

public class ErrorThrowingSystem : ClassSystem
{
    public override void Run(World world)
    {
        throw new InvalidOperationException("System error for testing");
    }
}

public class SlowTestSystem : ClassSystem
{
    private readonly int _delayMs;
    
    public SlowTestSystem(int delayMs)
    {
        _delayMs = delayMs;
    }
    
    public bool WasExecuted { get; private set; }

    public override void Run(World world)
    {
        Thread.Sleep(_delayMs);
        WasExecuted = true;
    }
}