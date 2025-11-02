using System.Collections;
using Verse.ECS;
using Verse.ECS.Scheduling;
using Verse.ECS.Scheduling.Configs;

namespace Verse.Core;

/// <summary>
/// <see cref="App"/> is the primary API for writing user applications. It automates the setup of a
/// [standard lifecycle](Main) and provides interface glue for [plugins](`Plugin`).
///
/// A single [`App`] can contain multiple [`SubApp`] instances, but [`App`] methods only affect
/// the "main" one. To access a particular [`SubApp`], use [`get_sub_app`](App::get_sub_app)
/// or [`get_sub_app_mut`](App::get_sub_app_mut).
/// </summary>
public class App : IDisposable
{
	public static AppExit RunOnce(App app)
	{
		while (app.PluginsState() == Core.PluginsState.Adding) {
			Thread.Sleep(1);
		}
		app.Finish();
		app.Cleanup();
		app.Update();
		return app.ShouldExit() ?? AppExit.Success();
	}

	public static AppExit Run(App app)
	{
		while (app.PluginsState() == Core.PluginsState.Adding) {
			Thread.Sleep(1);
		}
		var exit = app.ShouldExit();
		while (exit == null) {
			app.Update();
			exit = app.ShouldExit();
		}
		app.Finish();
		app.Cleanup();
		return exit.Value;
	}

	internal App(RunnerHandler runFn, SubApp main, params SubApp[] subApps)
	{
		SubApps = new SubApps(main, subApps);
		_runnerFn = runFn;
	}

	public static App Default()
	{
		var main = new SubApp("main", Schedules.Main, Schedules.Update);
		var app = new App(RunOnce, main);
		app.AddMessage<AppExit>();
		app.AddPlugin<MainSchedulePlugin>();
		// TODO add event systems
		return app;
	}

	public static App Empty()
	{
		return new App(RunOnce, SubApp.Empty());
	}

	public delegate AppExit RunnerHandler(App app);
	internal SubApps SubApps;
	private RunnerHandler _runnerFn;

	private bool firstRun = true;

	public void Update()
	{
		if (IsBuildingPlugins()) {
			throw new ApplicationException("Cannot update while building plugins");
		}
		if (firstRun) {
			World.Init();
			firstRun = false;
		}
		SubApps.Update();
	}

	public AppExit Run()
	{
		if (IsBuildingPlugins()) {
			throw new ApplicationException("Cannot run while building plugins");
		}

		return _runnerFn(this);
	}

	public void SetRunner(RunnerHandler runnerFn)
	{
		_runnerFn = runnerFn;
	}

	/// <summary>
	/// Returns the state of all plugins. This is usually called by the event loop, but can be
	/// useful for situations where you want to use <see cref="Update"/>
	/// </summary>
	/// <returns></returns>
	public PluginsState PluginsState()
	{
		var state = SubApps.Main.GetPluginsState();
		foreach (var sub in SubApps.Subs) {
			var subState = sub.Value.GetPluginsState();
			if (subState < state) {
				state = subState;
			}
		}
		return state;
	}

	/// <summary>
	/// Runs <see cref="IPlugin.Finish"/> on all plugins. This is usually called by the event loop once all
	/// plugins are ready.
	/// </summary>
	public void Finish()
	{
		foreach (var subApp in SubApps) {
			subApp.Finish();
		}
	}

	/// <summary>
	/// Runs <see cref="IPlugin.Cleanup"/> on all plugins. This is usually called by the event loop once all
	/// plugins are finished.
	/// </summary>
	public void Cleanup()
	{
		foreach (var subApp in SubApps) {
			subApp.Cleanup();
		}
	}

	protected bool IsBuildingPlugins()
	{
		return SubApps.Any(app => app.IsBuildingPlugins);
	}

	public App AddSystems(string schedule, IIntoSystemConfigs node)
	{
		SubApps.Main.AddSystems(schedule, node);
		return this;
	}
	public App AddSystems(IIntoSystemConfigs node)
	{
		SubApps.Main.AddSystems(node);
		return this;
	}
	public App InitResource<T>() where T : new()
	{
		SubApps.Main.InitResource<T>();
		return this;
	}
	
	public App InitWorldResource<T>() where T : IFromWorld<T>
	{
		SubApps.Main.InitWorldResource<T>();
		return this;
	}
	
	public App InsertResource<T>(T value)
	{
		SubApps.Main.InsertResource(value);
		return this;
	}
	
	public App InitResource<T>(T value)
	{
		SubApps.Main.InitResource(value);
		return this;
	}
	
	public App ConfigureSets(string schedule, IIntoSystemSetConfigs configs)
	{
		SubApps.Main.ConfigureSets(schedule, configs);
		return this;
	}
	public App AddSchedule(Schedule schedule)
	{
		SubApps.Main.AddSchedule(schedule);
		return this;
	}
	public App InitSchedule(string label)
	{
		SubApps.Main.InitSchedule(label);
		return this;
	}
	public Schedule? GetSchedule(string label)
	{
		return SubApps.Main.GetSchedule(label);
	}
	public App AllowAmbiguousComponent<T>() where T : struct
	{
		SubApps.Main.AllowAmbiguousComponent<T>();
		return this;
	}
	public App AllowAmbiguousResource<T>()
	{
		SubApps.Main.AllowAmbiguousResource<T>();
		return this;
	}
	public App IgnoreAmbiguity(string schedule, IIntoSystemSet a, IIntoSystemSet b)
	{
		SubApps.Main.IgnoreAmbiguity(schedule, a, b);
		return this;
	}

	public App AddMessage<T>() where T : notnull
	{
		// TODO refactor into an actual message vs events system
		SubApps.Main.AddMessage<T>();
		return this;
	}

	public App AddPlugin(IPlugin plugin)
	{
		SubApps.Main.AddPluginToApp(this, plugin);
		return this;
	}

	public App AddSchedulable(ISchedulable schedulable)
	{
		return schedulable.Schedule(this);
	}

	public App AddSchedulable<T>() where T : ISchedulable, new()
	{
		return new T().Schedule(this);
	}

	public App InitScheduleable<T>() where T : ISchedulable, IFromWorld<T>
	{
		return T.FromWorld(World).Schedule(this);
	}
	
	public App AddPlugin<T>() where T : IStaticPlugin => AddPlugin(T.CreatePlugin(this));
	public bool IsPluginAdded(IPlugin plugin)
	{
		return SubApps.Main.IsPluginAdded(plugin);
	}
	public List<IPlugin> GetPlugins()
	{
		return SubApps.Main.GetPlugins();
	}
	public World World => SubApps.Main.World;

	public AppExit? ShouldExit()
	{
		var reader = World.Resource<Messages<AppExit>>().Reader;
		if (reader.IsEmpty) {
			return null;
		}
		var code = 0;
		foreach (var exitEvent in reader) {
			code = Math.Max(code, exitEvent.ExitCode);
		}
		return new AppExit(code);
	}


	public void InsertSubApp(SubApp subApp)
	{
		SubApps.Subs.Add(subApp.Name, subApp);
	}

	public SubApp? GetSubApp(string name)
	{
		return SubApps.Subs.GetValueOrDefault(name);
	}

	public void RemoveSubApp(string name)
	{
		SubApps.Subs.Remove(name);
	}

	private uint _isDisposed = 0;
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 0) {
			if (disposing) {
				SubApps.Dispose();
				SubApps = null!;
			}
		}
	}
}

public record struct AppExit(int ErrCode)
{
	public static AppExit Success() => new AppExit(0);
	public static AppExit Err(int exitCode) => new AppExit(exitCode);
	public bool IsSuccess => ErrCode == 0;
	public bool IsErr => ErrCode != 0;
	public int ExitCode => ErrCode;
	public override string ToString() => IsSuccess ? "Success" : $"Err({ErrCode})";
}

/// <summary>
/// The collection of sub-apps that belong to a <see cref="App"/>
/// </summary>
public sealed class SubApps : IEnumerable<SubApp>, IDisposable
{
	public SubApp Main;
	public Dictionary<string, SubApp> Subs;

	public static SubApps Empty() => new SubApps(SubApp.Empty(), []);

	public SubApps(SubApp main, IEnumerable<SubApp> subs)
	{
		Main = main;
		Subs = new Dictionary<string, SubApp>();
		foreach (var sub in subs) {
			Subs.Add(sub.Name, sub);
		}
	}

	public void Update()
	{
		Main.RunDefaultSchedule();
		foreach (var sub in Subs.Values) {
			sub.Extract(Main.World);
			sub.Update();
		}
		Main.World.Update();
	}

	public IEnumerable<SubApp> GetSubApps()
	{
		yield return Main;
		foreach (var sub in Subs.Values) {
			yield return sub;
		}
	}
	public IEnumerator<SubApp> GetEnumerator() => GetSubApps().GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private uint _isDisposed = 0;
	void Dispose(bool disposing)
	{
		if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 0) {
			if (disposing) {
				Main.Dispose();
				Main = null!;
				foreach (var subApp in Subs) {
					subApp.Value.Dispose();
				}
				Subs = null!;
			}
		}
	}
}