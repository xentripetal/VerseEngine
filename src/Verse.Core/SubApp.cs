using Verse.ECS;
using Verse.ECS.Scheduling;
using Verse.ECS.Scheduling.Configs;

namespace Verse.Core;

public class SubApp 
{
	public delegate void ExtractHandler(World from, World to);

	public readonly string Name;
	public readonly World World;
	public readonly ExecutorKind ExecutorKind;
	readonly List<IPlugin> Plugins;
	readonly HashSet<string> PluginNames;
	uint PluginBuildDepth;
	PluginsState PluginState;
	/// <summary>
	/// The schedule that will be run by <see cref="Update"/>
	/// </summary>
	public readonly string UpdateSchedule;
	/// <summary>
	/// The schedule that systems without an explicit schedule will be added to.
	/// </summary>
	public readonly string DefaultSchedule;
	/// <summary>
	///  A function that gives mutable access to two app worlds. This is primarily intended for copying data from the main world to secondary worlds.
	/// </summary>
	ExtractHandler? ExtractFn;

	public static SubApp Empty()
	{
		return new SubApp("empty", "", "");
	}

	public SubApp(string name, string updateSchedule, string defaultSchedule, ExecutorKind kind = ExecutorKind.SingleThreaded)
	{
		World = new World();
		World.SetRes(new ScheduleContainer());
		Name = name;
		ExecutorKind = kind;
		Plugins = new List<IPlugin>();
		PluginNames = new HashSet<string>();
		PluginBuildDepth = 0;
		PluginState = PluginsState.Adding;
		UpdateSchedule = updateSchedule;
		DefaultSchedule = defaultSchedule;
		ExtractFn = null;
	}

	private void RunAsApp(Action<App> fn)
	{
		var app = App.Empty();
		var oldMain = app.SubApps.Main;
		app.SubApps.Main = this;
		try {
			fn(app);
		}
		finally {
			app.SubApps.Main = oldMain;
		}
	}

	public void RunDefaultSchedule()
	{
		if (IsBuildingPlugins) {
			throw new ApplicationException("Cannot run default schedule while building plugins");
		}
		if (UpdateSchedule != "") {
			World.RunSchedule(UpdateSchedule);
		}
	}

	/// <summary>
	/// Runs the default schedule and updates internal component trackers
	/// </summary>
	public void Update()
	{
		RunDefaultSchedule();
		World.Update();
	}

	/// <summary>
	/// Extracts data from another world into the apps world using the registered <see cref="ExtractFn"/> method.
	///
	/// <remarks>Note: There is no default extract method. Calling <see cref="Extract"/> does nothing if <see cref="SetExtract"/> has not been called.</remarks>
	/// </summary>
	/// <param name="from">World to extract from</param>
	public void Extract(World from)
	{
		ExtractFn?.Invoke(from, World);
	}

	public void SetExtract(ExtractHandler fn)
	{
		ExtractFn = fn;
	}

	/// <summary>
	/// Take the function that will be called by <see cref="ExtractHandler"/> out of the app, if any was set,
	/// and replace it with `None`.
	///
	/// If you use Bevy, `bevy_render` will set a default extract function used to extract data from
	/// the main world into the render world as part of the Extract phase. In that case, you cannot replace
	/// it with your own function. Instead, take the Bevy default function with this, and install your own
	/// instead which calls the Bevy default.
	/// </summary>
	/// <returns>The ExtractHandler originally in this app</returns>
	public ExtractHandler? TakeExtract()
	{
		var fn = ExtractFn;
		ExtractFn = null;
		return fn;
	}


	public bool IsBuildingPlugins => PluginBuildDepth > 0;


	private ScheduleContainer GetSchedules()
	{
		var res = World.MustGetResMut<ScheduleContainer>();
		if (res.Value == null) {
			throw new ArgumentException("ScheduleContainer not found");
		}
		return res.Value;
	}

	public SubApp AddSystems(string schedule, IIntoSystemConfigs systems)
	{
		GetSchedules().AddSystems(schedule, systems);
		return this;
	}
	public SubApp AddSystems(IIntoSystemConfigs node) => AddSystems(DefaultSchedule, node);
	public SubApp InitRes<T>()
	{
		World.InitRes<T>();
		return this;
	}
	public SubApp InitRes<T>(T value)
	{
		World.SetRes(value);
		return this;
	}
	public SubApp ConfigureSets(string schedule, IIntoSystemSetConfigs configs)
	{
		GetSchedules().ConfigureSets(schedule, configs);
		return this;
	}
	public SubApp AddSchedule(Schedule schedule)
	{
		var schedules = GetSchedules();
		schedules.Insert(schedule);
		return this;
	}
	public SubApp InitSchedule(string label)
	{
		var schedules = GetSchedules();
		if (!schedules.Contains(label))
			schedules.Insert(new Schedule(label, ExecutorKind));
		return this;
	}
	public Schedule? GetSchedule(string label)
	{
		return GetSchedules().Get(label);
	}
	public SubApp AllowAmbiguousComponent<T>() where T : struct
	{
		World.AllowAmbiguousComponent<T>();
		return this;
	}

	public SubApp AllowAmbiguousResource<T>()
	{
		World.AllowAmbiguousResource<T>();
		return this;
	}
	public SubApp IgnoreAmbiguity(string schedule, IIntoSystemSet a, IIntoSystemSet b)
	{
		GetSchedules().IgnoreAmbiguity(schedule, a, b);
		return this;
	}
	public SubApp AddEvent<T>() where T : notnull
	{
		World.RegisterEvent<T>();
		return this;
	}
	public SubApp AddPlugin(IPlugin plugin)
	{
		RunAsApp(app => AddPluginToApp(app, plugin));
		return this;
	}

	/// <summary>
	/// Adds the given plugin to the app. This App MUST have this SubApp as its main app.
	/// </summary>
	/// <param name="app"></param>
	/// <param name="plugin"></param>
	/// <exception cref="ArgumentException"></exception>
	internal void AddPluginToApp(App app, IPlugin plugin)
	{
		if (app.SubApps.Main != this) {
			throw new ArgumentException("App must be this SubApp's main app");
		}
		if (plugin.IsUnique && PluginNames.Contains(plugin.Name)) {
			throw new ArgumentException("Plugin with name " + plugin.Name + " already added");
		}

		var index = Plugins.Count;
		Plugins.Add(new PlaceholderPlugin());
		PluginBuildDepth++;
		try {
			plugin.Build(app);
		}
		finally {
			PluginNames.Add(plugin.Name);
			PluginBuildDepth--;
		}
		Plugins[index] = plugin;
	}
	
	public bool IsPluginAdded(IPlugin plugin)
	{
		return PluginNames.Contains(plugin.Name);
	}

	public List<IPlugin> GetPlugins()
	{
		return Plugins;
	}

	public PluginsState GetPluginsState()
	{
		switch (PluginState) {
			case PluginsState.Adding:
				var state = PluginsState.Ready;
				foreach (var plugin in Plugins) {
					RunAsApp(app => {
						if (!plugin.Ready(app)) {
							state = PluginsState.Adding;
						}
					});
					if (state != PluginsState.Ready)
						break;
				}
				return state;
			default:
				return PluginState;
		}
	}

	public void Finish()
	{
		foreach (var plugin in Plugins) {
			RunAsApp(app => plugin.Finish(app));
		}
		PluginState = PluginsState.Finished;
	}

	public void Cleanup()
	{
		foreach (var plugin in Plugins) {
			RunAsApp(app => plugin.Cleanup(app));
		}
		PluginState = PluginsState.Cleaned;
	}
}

public enum PluginsState
{
	/// <summary>
	/// Plugins are being added
	/// </summary>
	Adding,
	/// <summary>
	/// All plugins already added are ready
	/// </summary>
	Ready,
	/// <summary>
	/// Finish has been executed for all plugins added.
	/// </summary>
	Finished,
	/// <summary>
	/// Cleanup has been executed for all plugins added.
	/// </summary>
	Cleaned
}