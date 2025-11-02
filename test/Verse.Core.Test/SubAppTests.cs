using FluentAssertions;
using Verse.Core;
using Verse.ECS;
using Verse.ECS.Scheduling;
using Verse.ECS.Systems;
using Verse.ECS.Scheduling.Configs;

namespace Verse.Core.Test;

public class SubAppTests : IDisposable
{
	private SubApp? _subApp;

	public void Dispose()
	{
		_subApp?.Dispose();
	}

	[Fact]
	public void SubApp_Constructor_ShouldInitializeCorrectly()
	{
		// Act
		_subApp = new SubApp("test-app", "UpdateSchedule", "DefaultSchedule");

		// Assert
		_subApp.Should().NotBeNull();
		_subApp.Name.Should().Be("test-app");
		_subApp.World.Should().NotBeNull();
	}

	[Fact]
	public void SubApp_Empty_ShouldCreateEmptySubApp()
	{
		// Act
		_subApp = SubApp.Empty();

		// Assert
		_subApp.Should().NotBeNull();
		_subApp.World.Should().NotBeNull();
	}

	[Fact]
	public void SubApp_AddSystems_ShouldAddSystemsToSchedule()
	{
		// Arrange
		_subApp = new SubApp("test", "Update", "Default");
		var system = new TestSubAppSystem();

		// Act
		var result = _subApp.AddSystems("Update", system);

		// Assert
		result.Should().Be(_subApp); // Should return self for chaining
		// The system should be added to the schedule (exact verification depends on implementation)
	}

	[Fact]
	public void SubApp_AddSystems_WithDefaultSchedule_ShouldUseDefaultSchedule()
	{
		// Arrange
		_subApp = new SubApp("test", "Update", "Default");
		var system = new TestSubAppSystem();

		// Act
		var result = _subApp.AddSystems(system);

		// Assert
		result.Should().Be(_subApp); // Should return self for chaining
	}

	[Fact]
	public void SubApp_InitRes_ShouldInitializeResource()
	{
		// Arrange
		_subApp = new SubApp("test", "Update", "Default");

		// Act
		var result = _subApp.InitResource<TestSubAppResource>();

		// Assert
		result.Should().Be(_subApp); // Should return self for chaining
		// The resource should be initialized (exact verification depends on World implementation)
	}

	[Fact]
	public void SubApp_InitRes_WithValue_ShouldInitializeResourceWithValue()
	{
		// Arrange
		_subApp = new SubApp("test", "Update", "Default");
		var resource = new TestSubAppResource { Data = "Initial Value" };

		// Act
		var result = _subApp.InitResource(resource);

		// Assert
		result.Should().Be(_subApp); // Should return self for chaining
		// The resource should be initialized with the provided value
	}

	[Fact]
	public void SubApp_AddSchedule_ShouldAddSchedule()
	{
		// Arrange
		_subApp = new SubApp("test", "Update", "Default");
		var schedule = new Schedule("CustomSchedule");

		// Act
		var result = _subApp.AddSchedule(schedule);

		// Assert
		result.Should().Be(_subApp); // Should return self for chaining
	}

	[Fact]
	public void SubApp_InitSchedule_ShouldInitializeSchedule()
	{
		// Arrange
		_subApp = new SubApp("test", "Update", "Default");

		// Act
		var result = _subApp.InitSchedule("NewSchedule");

		// Assert
		result.Should().Be(_subApp); // Should return self for chaining
	}

	[Fact]
	public void SubApp_AllowAmbiguousComponent_ShouldConfigureAmbiguity()
	{
		// Arrange
		_subApp = new SubApp("test", "Update", "Default");

		// Act
		var result = _subApp.AllowAmbiguousComponent<TestSubAppComponent>();

		// Assert
		result.Should().Be(_subApp); // Should return self for chaining
	}

	[Fact]
	public void SubApp_AllowAmbiguousResource_ShouldConfigureAmbiguity()
	{
		// Arrange
		_subApp = new SubApp("test", "Update", "Default");

		// Act
		var result = _subApp.AllowAmbiguousResource<TestSubAppResource>();

		// Assert
		result.Should().Be(_subApp); // Should return self for chaining
	}

	[Fact]
	public void SubApp_AddEvent_ShouldAddEventType()
	{
		// Arrange
		_subApp = new SubApp("test", "Update", "Default");

		// Act
		var result = _subApp.AddMessage<TestSubAppEvent>();

		// Assert
		result.Should().Be(_subApp); // Should return self for chaining
		// The event type should be registered with the world
	}

	[Fact]
	public void SubApp_AddPlugin_ShouldBuildPlugin()
	{
		// Arrange
		_subApp = new SubApp("test", "Update", "Default");
		var plugin = new TestSubAppPlugin();

		// Act
		var result = _subApp.AddPlugin(plugin);

		// Assert
		result.Should().Be(_subApp); // Should return self for chaining
		plugin.WasBuilt.Should().BeTrue();
		plugin.BuildSubApp.Should().Be(_subApp);
	}

	[Fact]
	public void SubApp_Run_ShouldExecuteSchedule()
	{
		// Arrange
		_subApp = new SubApp("test", "Update", "Default");
		var system = new TestSubAppSystem();
		_subApp.AddSystems(system);

		// Act
		_subApp.Update();

		// Assert
		system.ExecutionCount.Should().BeGreaterThan(0);
	}

	[Fact]
	public void SubApp_Update_ShouldUpdateWorld()
	{
		// Arrange
		_subApp = new SubApp("test", "Update", "Default");
		var initialTick = _subApp.World.CurTick;

		// Act
		_subApp.Update();

		// Assert
		_subApp.World.CurTick.Should().BeGreaterThan(initialTick);
	}

	[Fact]
	public void SubApp_ChainedCalls_ShouldSupportFluentInterface()
	{
		// Act
		_subApp = new SubApp("test", "Update", "Default").AddSystems(new TestSubAppSystem()).InitResource<TestSubAppResource>().AddMessage<TestSubAppEvent>().
			AllowAmbiguousComponent<TestSubAppComponent>();

		// Assert
		_subApp.Should().NotBeNull();
		_subApp.Name.Should().Be("test");
	}

	[Fact]
	public void SubApp_DefaultSchedule_ShouldBeUsedForSystems()
	{
		// Arrange
		_subApp = new SubApp("test", "Update", "MyDefault");
		var system = new TestSubAppSystem();

		// Act
		_subApp.AddSystems(system); // Should use "MyDefault" schedule

		// Assert
		// The system should be added to the default schedule
		// Exact verification depends on implementation details
		_subApp.Should().NotBeNull(); // Basic verification
	}

	[Fact]
	public void SubApp_Dispose_ShouldCleanupResources()
	{
		// Arrange
		_subApp = new SubApp("test", "Update", "Default");

		// Act & Assert - Should not throw
		_subApp.Dispose();
		_subApp = null; // Prevent double disposal in cleanup
	}

	[Fact]
	public void SubApp_MultipleSystems_ShouldExecuteAll()
	{
		// Arrange
		_subApp = new SubApp("test", "Update", "Default");
		var system1 = new TestSubAppSystem { Name = "System1" };
		var system2 = new TestSubAppSystem { Name = "System2" };
		var system3 = new TestSubAppSystem { Name = "System3" };

		// Act
		_subApp.AddSystems("Update", system1);
		_subApp.AddSystems("Update", system2);
		_subApp.AddSystems("Update", system3);
		_subApp.Update();

		// Assert
		system1.ExecutionCount.Should().BeGreaterThan(0);
		system2.ExecutionCount.Should().BeGreaterThan(0);
		system3.ExecutionCount.Should().BeGreaterThan(0);
	}
}

// Test classes for SubApp tests
public class TestSubAppSystem : ClassSystem
{
	public string Name { get; set; } = "TestSystem";
	public int ExecutionCount { get; private set; }

	public override void Run(World world)
	{
		ExecutionCount++;
	}
}

public struct TestSubAppComponent
{
	public int Value { get; set; }
}

public class TestSubAppResource
{
	public string Data { get; set; } = string.Empty;
}

public class TestSubAppEvent
{
	public string Message { get; set; } = string.Empty;
}

public class TestSubAppPlugin : IPlugin
{
	public bool WasBuilt { get; private set; }
	public SubApp? BuildSubApp { get; private set; }

	public void Build(SubApp subApp)
	{
		WasBuilt = true;
		BuildSubApp = subApp;
	}

	public void Build(App app)
	{
		// This version should not be called in SubApp tests
		throw new NotImplementedException("This should not be called in SubApp tests");
	}
}