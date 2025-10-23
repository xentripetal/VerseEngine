using QuikGraph;
using Verse.ECS.Scheduling.Configs;
using Verse.ECS.Scheduling.Executor;
using Verse.ECS.Scheduling.Graph;
using Verse.ECS.Systems;

namespace Verse.ECS.Scheduling;

public enum ExecutorKind
{
	SingleThreaded,
	MultiThreaded,
}

/// <summary>
///     A collection of systems, and the metadata and executor needed to run them
///     in a certain order under certain conditions.
/// </summary>
public class Schedule
{
	internal SystemSchedule Executable;
	internal IExecutor Executor;
	protected bool ExecutorInitialized;
	public readonly SystemGraph Graph;


	public Schedule(string name, ExecutorKind executorKind = ExecutorKind.SingleThreaded)
	{
		Name = name;
		Graph = new SystemGraph();
		Executable = new SystemSchedule();
		Executor = executorKind switch {
			ExecutorKind.SingleThreaded => new SingleThreadedExecutor(),
			ExecutorKind.MultiThreaded  => throw new NotImplementedException(),
			_                           => throw new ArgumentOutOfRangeException(nameof(executorKind), executorKind, null)
		};
	}

	public string Name { get; protected set; }

	public Schedule AddSystems(params IIntoNodeConfigs<ISystem>[] configs)
	{
		foreach (var config in configs) {
			Graph.ProcessConfigs(config.IntoConfigs(), false);
		}
		return this;
	}

	/// <summary>
	///     Suppress warnings and errors that would result from systems in these sets having ambiguities (Conflicting access
	///     but indeterminate order) with systems in set.
	/// </summary>
	/// <param name="a"></param>
	/// <param name="b"></param>
	/// <returns></returns>
	/// <exception cref="ArgumentException"></exception>
	public Schedule IgnoreAmbiguity(ISystemSet a, ISystemSet b)
	{
		var hasA = Graph.SystemSetIds.TryGetValue(a, out var aNode);
		if (!hasA) {
			throw new ArgumentException(
				$"Could not mark system as ambiguous, {a} was not found in the schedule. Did you try to call IgnoreAmbiguity before adding the system to the world?");
		}
		var hasB = Graph.SystemSetIds.TryGetValue(b, out var bNode);
		if (!hasB) {
			throw new ArgumentException(
				$"Could not mark system as ambiguous, {b} was not found in the schedule. Did you try to call IgnoreAmbiguity before adding the system to the world?");
		}
		Graph.AmbiguousWith.AddEdge(new Edge<NodeId>(aNode, bNode));
		return this;
	}

	public Schedule SetBuildSettings(ScheduleBuildSettings settings)
	{
		Graph.Config = settings;
		return this;
	}

	public ScheduleBuildSettings GetBuildSettings() => Graph.Config;

	public Schedule SetExecutor(IExecutor executor)
	{
		Executor = executor;
		return this;
	}

	public IExecutor GetExecutor() => Executor;

	/// <summary>
	///     Set whether the schedule applies deferred system buffers on final time or not. This is a catch-all
	///     in case a system uses commands but was not explicitly ordered before an instance of
	///     [`apply_deferred`]. By default, this setting is true, but may be disabled if needed.
	/// </summary>
	/// <param name="apply"></param>
	/// <returns></returns>
	public Schedule SetApplyFinalDeferred(bool apply)
	{
		Executor.SetApplyFinalDeferred(apply);
		return this;
	}

	public void Run(World scheduleWorld)
	{
		Initialize(scheduleWorld);
		// TODO resource system to get skip systems
		Executor.Run(Executable, scheduleWorld, null);
	}

	public void Initialize(World scheduleWorld)
	{
		if (Graph.Changed) {
			Graph.Initialize(scheduleWorld);
			// TODO - resource system to get Schedules ambiguities
			Executable = Graph.UpdateSchedule(scheduleWorld, Executable, new FixedBitSet(), Name);
			Graph.Changed = false;
			ExecutorInitialized = false;
		}

		if (!ExecutorInitialized) {
			Executor.Init(Executable);
			ExecutorInitialized = true;
		}
	}

	public Schedule ConfigureSets(IIntoNodeConfigs<ISystemSet> sets)
	{
		Graph.ConfigureSets(sets);
		return this;
	}

	public Schedule ConfigureSets(IIntoNodeConfigs<ISystemSet>[] sets)
	{
		foreach (var set in sets) {
			Graph.ConfigureSets(set);
		}
		return this;
	}
	/// <summary>
	/// Directly applies any accumulated system parameters to the world.
	///
	/// This does not need to be called under normal circumstances. It is used in rendering to extract data from the main world,
	/// storing the data in system buffers, before applying their buffers in a different world
	/// </summary>
	public void ApplyDeferred(World world)
	{
		foreach (var system in Executable.Systems) {
			system.ApplyDeferred(world);
		}
	}
}