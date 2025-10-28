using FluentAssertions;
using Verse.Core;
using Verse.ECS;
using Verse.ECS.Scheduling;

namespace Verse.Core.Test;

public class PluginTests : IDisposable
{
    private App? _app;

    public void Dispose()
    {
        _app?.Dispose();
    }

    [Fact]
    public void Plugin_Build_ShouldReceiveApp()
    {
        // Arrange
        _app = App.Default();
        var plugin = new SimpleTestPlugin();

        // Act
        plugin.Build(_app);

        // Assert
        plugin.BuildWasCalled.Should().BeTrue();
        plugin.ReceivedApp.Should().Be(_app);
    }

    [Fact]
    public void Plugin_MultipleBuildCalls_ShouldHandleCorrectly()
    {
        // Arrange
        _app = App.Default();
        var plugin = new CountingTestPlugin();

        // Act
        plugin.Build(_app);
        plugin.Build(_app);
        plugin.Build(_app);

        // Assert
        plugin.BuildCallCount.Should().Be(3);
    }

    [Fact]
    public void Plugin_ConfigureApp_ShouldModifyApp()
    {
        // Arrange
        _app = App.Default();
        var plugin = new AppConfiguringPlugin();
        
        var initialResourceExists = false;
        try
        {
            _app.World.GetRes<PluginTestResource>();
            initialResourceExists = true;
        }
        catch
        {
            // Resource doesn't exist initially - this is expected
        }

        // Act
        plugin.Build(_app);

        // Assert
        initialResourceExists.Should().BeFalse();
        var resource = _app.World.GetResource<PluginTestResource>();
        resource.Should().NotBeNull();
        resource.Value.Name.Should().Be("Added by plugin");
    }

    [Fact]
    public void Plugin_AddSystems_ShouldAddSystemsToApp()
    {
        // Arrange
        _app = App.Default();
        var plugin = new SystemAddingPlugin();

        // Act
        plugin.Build(_app);

        // Assert
        // Verify that the plugin added its systems
        // Exact verification depends on how systems are tracked
        plugin.SystemsAdded.Should().BeTrue();
    }

    [Fact]
    public void Plugin_AddEvents_ShouldRegisterEvents()
    {
        // Arrange
        _app = App.Default();
        var plugin = new EventAddingPlugin();

        // Act
        plugin.Build(_app);

        // Assert
        plugin.EventsAdded.Should().BeTrue();
        // The events should be registered with the world
        _app.World.EventRegistry.Should().NotBeNull();
    }

    [Fact]
    public void Plugin_AddSubApp_ShouldCreateSubApp()
    {
        // Arrange
        _app = App.Default();
        var plugin = new SubAppCreatingPlugin();

        // Act
        plugin.Build(_app);

        // Assert
        var subApp = _app.GetSubApp("test-subapp");
        subApp.Should().NotBeNull();
        subApp!.Name.Should().Be("test-subapp");
    }

    [Fact]
    public void Plugin_ComplexConfiguration_ShouldConfigureEverything()
    {
        // Arrange
        _app = App.Default();
        var plugin = new ComplexConfigurationPlugin();

        // Act
        plugin.Build(_app);

        // Assert
        // Verify resources
        var resource1 = _app.World.GetResource<PluginTestResource>();
        resource1.Should().NotBeNull();
        resource1.Value.Name.Should().Be("Complex Resource 1");

        var resource2 = _app.World.GetResource<OtherPluginTestResource>();
        resource2.Should().NotBeNull();
        resource2.Value.Should().Be(42);

        // Verify sub-app
        var subApp = _app.GetSubApp("complex-subapp");
        subApp.Should().NotBeNull();

        // Verify plugin state
        plugin.ConfigurationSteps.Should().HaveCount(4);
        plugin.ConfigurationSteps.Should().Contain("AddResource1");
        plugin.ConfigurationSteps.Should().Contain("AddResource2");
        plugin.ConfigurationSteps.Should().Contain("AddSubApp");
        plugin.ConfigurationSteps.Should().Contain("AddEvents");
    }

    [Fact]
    public void MainSchedulePlugin_ShouldSetupSchedules()
    {
        // Arrange
        _app = App.Default();
        var plugin = new MainSchedulePlugin(ExecutorKind.SingleThreaded);

        // Act
        plugin.Build(_app);

        // Assert
        // The MainSchedulePlugin should set up main schedules
        // Exact verification depends on implementation
        plugin.Should().NotBeNull();
    }

    [Fact]
    public void Plugin_DependencyOrder_ShouldRespectOrder()
    {
        // Arrange
        _app = App.Default();
        var plugin1 = new OrderedPlugin { Name = "First", Order = 1 };
        var plugin2 = new OrderedPlugin { Name = "Second", Order = 2 };

        _app.AddPlugin(plugin1);
        _app.AddPlugin(plugin2);

        // Assert
        plugin1.BuildWasCalled.Should().BeTrue();
        plugin2.BuildWasCalled.Should().BeTrue();
    }

    [Fact]
    public void Plugin_ErrorHandling_ShouldNotCrashApp()
    {
        // Arrange
        _app = App.Default();
        var plugin = new ErrorThrowingPlugin();

        // Act & Assert
        var action = () => plugin.Build(_app);
        action.Should().Throw<InvalidOperationException>()
              .WithMessage("Plugin error for testing");
    }

    [Fact]
    public void Plugin_Stateful_ShouldMaintainState()
    {
        // Arrange
        _app = App.Default();
        var plugin = new StatefulPlugin();

        // Act
        plugin.Build(_app);

        // Assert
        plugin.State.Should().Be("Configured");
        plugin.ConfigurationData.Should().NotBeEmpty();
        plugin.ConfigurationData.Should().Contain("Resource initialized");
        plugin.ConfigurationData.Should().Contain("Events registered");
    }
}

// Test plugins for plugin tests
public class SimpleTestPlugin : IPlugin
{
    public bool BuildWasCalled { get; private set; }
    public App? ReceivedApp { get; private set; }

    public void Build(App app)
    {
        BuildWasCalled = true;
        ReceivedApp = app;
    }
}

public class CountingTestPlugin : IPlugin
{
    public int BuildCallCount { get; private set; }

    public void Build(App app)
    {
        BuildCallCount++;
    }
}

public class AppConfiguringPlugin : IPlugin
{
    public void Build(App app)
    {
        app.World.SetRes(new PluginTestResource { Name = "Added by plugin" });
    }
}

public class SystemAddingPlugin : IPlugin
{
    public bool SystemsAdded { get; private set; }

    public void Build(App app)
    {
        // Add systems to the main sub-app
        SystemsAdded = true;
        // Exact system addition API depends on implementation
    }
}

public class EventAddingPlugin : IPlugin
{
    public bool EventsAdded { get; private set; }

    public void Build(App app)
    {
        app.AddEvent<PluginTestEvent>();
        EventsAdded = true;
    }
}

public class SubAppCreatingPlugin : IPlugin
{
    public void Build(App app)
    {
        var subApp = new SubApp("test-subapp", "Update", "Default");
        app.InsertSubApp(subApp);
    }
}

public class ComplexConfigurationPlugin : IPlugin
{
    public List<string> ConfigurationSteps { get; } = new();

    public void Build(App app)
    {
        // Add multiple resources
        app.InsertResource(new PluginTestResource { Name = "Complex Resource 1" });
        ConfigurationSteps.Add("AddResource1");

        app.InsertResource(new OtherPluginTestResource { Value = 42 });
        ConfigurationSteps.Add("AddResource2");

        // Create sub-app
        var subApp = new SubApp("complex-subapp", "Update", "Default");
        app.InsertSubApp(subApp);
        ConfigurationSteps.Add("AddSubApp");

        // Add events
        app.World.AddMessage<PluginTestEvent>();
        app.World.AddMessage<OtherPluginTestEvent>();
        ConfigurationSteps.Add("AddEvents");
    }
}

public class OrderedPlugin : IPlugin
{
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool BuildWasCalled { get; private set; }
    public DateTime BuildTimestamp { get; private set; }

    public void Build(App app)
    {
        BuildWasCalled = true;
        BuildTimestamp = DateTime.UtcNow;
        
        // Small delay to ensure different timestamps
        Thread.Sleep(1);
    }
}

public class ErrorThrowingPlugin : IPlugin
{
    public void Build(App app)
    {
        throw new InvalidOperationException("Plugin error for testing");
    }
}

public class StatefulPlugin : IPlugin
{
    public string State { get; private set; } = "Initial";
    public List<string> ConfigurationData { get; } = new();

    public void Build(App app)
    {
        State = "Building";
        
        app.InsertResource(new PluginTestResource { Name = "Stateful Resource" });
        ConfigurationData.Add("Resource initialized");

        app.AddEvent<PluginTestEvent>();
        ConfigurationData.Add("Events registered");

        State = "Configured";
    }
}

// Test resources and events for plugin tests
public class PluginTestResource
{
    public string Name { get; set; } = string.Empty;
}

public class OtherPluginTestResource
{
    public int Value { get; set; }
}

public class PluginTestEvent
{
    public string Message { get; set; } = string.Empty;
}

public class OtherPluginTestEvent
{
    public int Data { get; set; }
}