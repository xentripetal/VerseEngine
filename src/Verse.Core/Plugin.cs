namespace Verse.Core;

/// <summary>
/// A collection of Verse app logic and configuration.
/// </summary>
/// <remarks>
/// <para>
/// Plugins configure an <see cref="App"/>. When an <see cref="App"/> registers a plugin,
/// the plugin's <see cref="Build"/> method is run. By default, a plugin
/// can only be added once to an <see cref="App"/>.
/// </para>
/// <para>
/// If the plugin may need to be added twice or more, the property <see cref="IsUnique"/>
/// should be overridden to return false. Plugins are considered duplicate if they have the same
/// <see cref="Name"/>. The default Name implementation returns the type name, which means
/// generic plugins with different type parameters will not be considered duplicates.
/// </para>
/// <para>
/// <b>Adding a plugin to an App</b><br/>
/// When adding a plugin to an <see cref="App"/>:
/// <list type="bullet">
/// <item>the app calls <see cref="Build"/> immediately, and register the plugin</item>
/// <item>once the app started, it will wait for all registered <see cref="Ready"/> to return true</item>
/// <item>it will then call all registered <see cref="Finish"/></item>
/// <item>and call all registered <see cref="Cleanup"/></item>
/// </list>
/// </para>
/// <para>
/// <b>Defining a Plugin</b><br/>
/// The Plugin interface can be implemented for a type to create more advanced plugins.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class AccessibilityPlugin : IPlugin
/// {
///     public bool FlickerDamping { get; set; }
///     
///     public void Build(App app)
///     {
///         if (FlickerDamping)
///         {
///             app.AddSystems(Schedules.PostUpdate, DampFlickering);
///         }
///     }
/// }
/// </code>
/// </example>
public interface IPlugin
{
	/// <summary>
	/// Configures the <see cref="App"/> to which this plugin is added.
	/// </summary>
	/// <param name="app">The app to configure</param>
	public void Build(App app);

	/// <summary>
	/// Has the plugin finished its setup? This can be useful for plugins that need something
	/// asynchronous to happen before they can finish their setup, like the initialization of a renderer.
	/// Once the plugin is ready, <see cref="Finish"/> should be called.
	/// </summary>
	/// <param name="app">The app to check readiness against</param>
	/// <returns>True if the plugin is ready, false otherwise</returns>
	public bool Ready(App app) => true;

	/// <summary>
	/// Finish adding this plugin to the <see cref="App"/>, once all plugins registered are ready. This can
	/// be useful for plugins that depends on another plugin asynchronous setup, like the renderer.
	/// </summary>
	/// <param name="app">The app to finish setup on</param>
	public void Finish(App app) { }

	/// <summary>
	/// Runs after all plugins are built and finished, but before the app schedule is executed.
	/// This can be useful if you have some resource that other plugins need during their build step,
	/// but after build you want to remove it and send it to another thread.
	/// </summary>
	/// <param name="app">The app to cleanup on</param>
	public void Cleanup(App app) { }

	/// <summary>
	/// Configures a name for the Plugin which is primarily used for checking plugin
	/// uniqueness and debugging.
	/// </summary>
	public string Name => GetType().Name;

	/// <summary>
	/// If the plugin can be meaningfully instantiated several times in an <see cref="App"/>,
	/// override this property to return false.
	/// </summary>
	public bool IsUnique => true;
}

public interface IStaticPlugin
{
	public static abstract IPlugin CreatePlugin(App app);
}

/// <summary>
/// A fake plugin that is stubbed in to temporally occupy an entry in an App's plugin list while its building the real plugin.
/// </summary>
internal struct PlaceholderPlugin : IPlugin
{
	public void Build(App app) { }
}