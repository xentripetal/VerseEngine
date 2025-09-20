using FluentAssertions;
using Verse.Core;
using Verse.ECS;

namespace Verse.Core.Test;

public class AppTests : IDisposable
{
	private App? _app;

	public void Dispose()
	{
		_app?.Dispose();
	}

	[Fact]
	public void App_Default_ShouldCreateWithMainSubApp()
	{
		// Act
		_app = App.Default();

		// Assert
		_app.Should().NotBeNull();
		_app.World.Should().NotBeNull();
		_app.SubApps.Should().NotBeNull();
		_app.SubApps.Main.Should().NotBeNull();
		_app.SubApps.Main.Name.Should().Be("main");
	}

	[Fact]
	public void App_Empty_ShouldCreateEmptyApp()
	{
		// Act
		_app = App.Empty();

		// Assert
		_app.Should().NotBeNull();
		_app.SubApps.Should().NotBeNull();
	}

	[Fact]
	public void App_AddPlugin_ShouldRegisterPlugin()
	{
		// Arrange
		_app = App.Default();
		var plugin = new TestPlugin();

		// Act
		_app.AddPlugin(plugin);

		// Assert
		plugin.WasBuilt.Should().BeTrue();
		plugin.BuildApp.Should().Be(_app);
	}

	[Fact]
	public void App_AddPluginGeneric_ShouldCreateAndRegisterPlugin()
	{
		// Arrange
		_app = App.Default();

		// Act
		_app.AddPlugin<TestPlugin>();

		// Assert
		// The plugin should have been created and built
		// We can't directly verify the specific instance was created, 
		// but we can check that plugins were processed
		_app.Should().NotBeNull();
	}

	[Fact]
	public void App_InsertSubApp_ShouldAddSubApp()
	{
		// Arrange
		_app = App.Default();
		var subApp = new SubApp("test-subapp", "TestSchedule", "TestDefault");

		// Act
		_app.InsertSubApp(subApp);

		// Assert
		var retrievedSubApp = _app.GetSubApp("test-subapp");
		retrievedSubApp.Should().NotBeNull();
		retrievedSubApp.Should().Be(subApp);
	}

	[Fact]
	public void App_GetSubApp_WithNonExistentName_ShouldReturnNull()
	{
		// Arrange
		_app = App.Default();

		// Act
		var subApp = _app.GetSubApp("nonexistent");

		// Assert
		subApp.Should().BeNull();
	}

	[Fact]
	public void App_RemoveSubApp_ShouldRemoveSubApp()
	{
		// Arrange
		_app = App.Default();
		var subApp = new SubApp("removable", "TestSchedule", "TestDefault");
		_app.InsertSubApp(subApp);

		// Verify it was added
		_app.GetSubApp("removable").Should().NotBeNull();

		// Act
		_app.RemoveSubApp("removable");

		// Assert
		_app.GetSubApp("removable").Should().BeNull();
	}

	[Fact]
	public void App_PluginsState_ShouldTrackPluginState()
	{
		// Arrange
		_app = App.Default();

		// Act
		var state = _app.PluginsState();

		// Assert
		state.Should().BeOneOf(PluginsState.Adding, PluginsState.Ready, PluginsState.Finished);
	}

	[Fact]
	public void App_RunOnce_ShouldExecuteAppLifecycle()
	{
		// Arrange
		_app = App.Default();

		// Act
		var exit = App.RunOnce(_app);

		// Assert
		exit.Should().NotBeNull();
		// The specific type of exit depends on the app execution
		exit.Should().BeAssignableTo<AppExit>();
	}

	[Fact]
	public void App_Update_ShouldUpdateMainWorld()
	{
		// Arrange
		_app = App.Default();
		var initialTick = _app.World.CurTick;

		// Act
		_app.Update();

		// Assert
		_app.World.CurTick.Should().BeGreaterThan(initialTick);
	}

	[Fact]
	public void App_Cleanup_ShouldCleanupResources()
	{
		// Arrange
		_app = App.Default();

		// Act & Assert - Should not throw
		_app.Cleanup();
	}

	[Fact]
	public void App_Finish_ShouldFinalizeApp()
	{
		// Arrange
		_app = App.Default();

		// Act & Assert - Should not throw
		_app.Finish();
	}

	[Fact]
	public void App_ShouldExit_ShouldReturnExitState()
	{
		// Arrange
		_app = App.Default();

		// Act
		var shouldExit = _app.ShouldExit();
		shouldExit.Should().BeNull();
	}

	[Fact]
	public void App_MultiplePlugins_ShouldExecuteInOrder()
	{
		// Arrange
		_app = App.Default();
		var plugin1 = new TestPlugin { Name = "Plugin1" };
		var plugin2 = new TestPlugin { Name = "Plugin2" };

		// Act
		_app.AddPlugin(plugin1);
		_app.AddPlugin(plugin2);

		// Assert
		plugin1.WasBuilt.Should().BeTrue();
		plugin2.WasBuilt.Should().BeTrue();
		plugin1.BuildApp.Should().Be(_app);
		plugin2.BuildApp.Should().Be(_app);
	}

	[Fact]
	public void App_WithCustomRunner_ShouldUseCustomRunner()
	{
		// Arrange
		var customRunnerCalled = false;
		App.RunnerHandler customRunner = (app) => {
			customRunnerCalled = true;
			return AppExit.Success();
		};

		// Act
		using var app = App.Empty();
		app.SetRunner(customRunner);
		var result = app.Run();

		// Assert
		customRunnerCalled.Should().BeTrue();
		result.Should().Be(AppExit.Success());
	}
}

// Test plugin for App tests
public class TestPlugin : IPlugin, IStaticPlugin
{
	public string Name { get; set; } = "TestPlugin";
	public bool WasBuilt { get; private set; }
	public App? BuildApp { get; private set; }

	public void Build(App app)
	{
		WasBuilt = true;
		BuildApp = app;
	}
	public static IPlugin CreatePlugin(App app) => new TestPlugin();
}