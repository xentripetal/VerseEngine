using Verse.ECS.Scheduling;

namespace Verse.Core;

/// <summary>
/// Default schedules used by Verse.Core
/// <remarks>Based on bevy_app/src/main_schedules.rs</remarks>
/// </summary>
public static class Schedules
{
	/// <summary>
	/// The schedule that contains the app logic that is evaluated each tick of App.Update().
	///
	/// By default, it will run the following schedules in the given order:
	///
	/// TODO - State Transitions
	/// On the first run of the schedule (and only on the first run), it will run:
	/// * StateTransition [^1]
	///      * This means that OnEnter(MyState.Foo) will be called *before* <see cref="PreStartup"/>
	///        if MyState was added to the app with MyState.Foo as the initial state,
	///        as well as OnEnter(MyComputedState) if it computes to Some(Self) in MyState.Foo.
	///      * If you want to run systems before any state transitions, regardless of which state is the starting state,
	///        for example, for registering required components, you can add your own custom startup schedule
	///        before StateTransition. See <see cref="MainScheduleOrder.InsertStartupBefore"/> for more details.
	/// * <see cref="PreStartup"/>
	/// * <see cref="Startup"/>
	/// * <see cref="PostStartup"/>
	///
	/// Then it will run:
	/// * <see cref="First"/>
	/// * <see cref="PreUpdate"/>
	/// * StateTransition [^1]
	/// * <see cref="RunFixedMainLoop"/>
	///     * This will run <see cref="FixedMain"/> zero to many times, based on how much time has elapsed.
	/// * <see cref="Update"/>
	/// * <see cref="PostUpdate"/>
	/// * <see cref="Last"/>
	/// </summary>
	public const string Main = "Main";
	/// The schedule that runs before <see cref="Startup"/>
	///
	/// See the <see cref="Main"/> schedule for some details about how schedules are run.
	public const string PreStartup = "PreStartup";
	/// The schedule that runs once when the app starts.
	///
	/// See the <see cref="Main"/> schedule for some details about how schedules are run.
	public const string Startup = "Startup";
	/// <summary>
	/// The schedule that runs once after [`Startup`].
	///
	/// See the <see cref="Main"/> schedule for some details about how schedules are run.
	/// </summary>
	public const string PostStartup = "PostStartup";

	/// <summary>
	/// Runs first in the schedule
	///
	/// See the <see cref="Main"/> schedule for some details about how schedules are run.
	/// </summary>
	public const string First = "First";
	/// <summary>
	/// The schedule that contains logic that must run before <see cref="Update"/>. For example, a system that reads raw keyboard
	/// input OS events into an `Events` resource. This enables systems in <see cref="Update"/> to consume the events from the `Events`
	/// resource without actually knowing about (or taking a direct scheduler dependency on) the "os-level keyboard event system".
	///
	/// <see cref="PreUpdate"/> exists to do "engine/plugin preparation work" that ensures the APIs consumed in <see cref="Update"/> are "ready".
	/// <see cref="PreUpdate"/> abstracts out "pre work implementation details".
	///
	/// See the <see cref="Main"/> schedule for some details about how schedules are run.
	/// </summary>
	public const string PreUpdate = "PreUpdate";

	/// <summary>
	/// Runs the <see cref="FixedMain"/> schedule in a loop according until all relevant elapsed time has been "consumed".
	///
	/// If you need to order your variable timestep systems before or after
	/// the fixed update logic, use the RunFixedMainLoopSystems system set.
	///
	/// Note that in contrast to most other Bevy schedules, systems added directly to
	/// RunFixedMainLoop will *not* be parallelized between each other.
	///
	/// See the <see cref="Main"/> schedule for some details about how schedules are run.
	/// </summary>
	public const string RunFixedMainLoop = "RunFixedMainLoop";

	/// <summary>
	/// Runs first in the <see cref="FixedMain"/> schedule.
	///
	/// See the <see cref="FixedMain"/> schedule for details on how fixed updates work.
	/// See the <see cref="Main"/> schedule for some details about how schedules are run.
	/// </summary>
	public const string FixedFirst = "FixedFirst";

	/// <summary>
	/// The schedule that contains logic that must run before <see cref="FixedUpdate"/>.
	///
	/// See the <see cref="FixedMain"/> schedule for details on how fixed updates work.
	/// See the <see cref="Main"/> schedule for some details about how schedules are run.
	/// </summary>
	public const string FixedPreUpdate = "FixedPreUpdate";

	/// <summary>
	/// The schedule that contains most gameplay logic, which runs at a fixed rate rather than every render frame.
	/// For logic that should run once per render frame, use the <see cref="Update"/> schedule instead.
	///
	/// Examples of systems that should run at a fixed rate include (but are not limited to):
	/// - Physics
	/// - AI
	/// - Networking
	/// - Game rules
	///
	/// See the <see cref="Update"/> schedule for examples of systems that *should not* use this schedule.
	/// See the <see cref="FixedMain"/> schedule for details on how fixed updates work.
	/// See the <see cref="Main"/> schedule for some details about how schedules are run.
	/// </summary>
	public const string FixedUpdate = "FixedUpdate";

	/// <summary>
	/// The schedule that runs after the <see cref="FixedUpdate"/> schedule, for reacting
	/// to changes made in the main update logic.
	///
	/// See the <see cref="FixedMain"/> schedule for details on how fixed updates work.
	/// See the <see cref="Main"/> schedule for some details about how schedules are run.
	/// </summary>
	public const string FixedPostUpdate = "FixedPostUpdate";

	/// <summary>
	/// The schedule that runs last in <see cref="FixedMain"/>
	///
	/// See the <see cref="FixedMain"/> schedule for details on how fixed updates work.
	/// See the <see cref="Main"/> schedule for some details about how schedules are run.
	/// </summary>
	public const string FixedLast = "FixedLast";

	/// <summary>
	/// The schedule that contains systems which only run after a fixed period of time has elapsed.
	///
	/// This is run by the <see cref="RunFixedMainLoop"/> schedule. If you need to order your variable timestep systems
	/// before or after the fixed update logic, use the RunFixedMainLoopSystems system set.
	///
	/// Frequency of execution is configured by inserting Time&lt;Fixed&gt; resource, 64 Hz by default.
	///
	/// See the <see cref="Main"/> schedule for some details about how schedules are run.
	/// </summary>
	public const string FixedMain = "FixedMain";

	/// <summary>
	/// The schedule that contains any app logic that must run once per render frame.
	/// For most gameplay logic, consider using <see cref="FixedUpdate"/> instead.
	///
	/// Examples of systems that should run once per render frame include (but are not limited to):
	/// - UI
	/// - Input handling
	/// - Audio control
	///
	/// See the <see cref="FixedUpdate"/> schedule for examples of systems that *should not* use this schedule.
	/// See the <see cref="Main"/> schedule for some details about how schedules are run.
	/// </summary>
	public const string Update = "Update";

	/// <summary>
	/// The schedule that contains scene spawning.
	///
	/// See the <see cref="Main"/> schedule for some details about how schedules are run.
	/// </summary>
	public const string SpawnScene = "SpawnScene";

	/// <summary>
	/// The schedule that contains logic that must run after <see cref="Update"/>. For example, synchronizing "local transforms" in a hierarchy
	/// to "global" absolute transforms. This enables the <see cref="PostUpdate"/> transform-sync system to react to "local transform" changes in
	/// <see cref="Update"/> without the <see cref="Update"/> systems needing to know about (or add scheduler dependencies for) the "global transform sync system".
	///
	/// <see cref="PostUpdate"/> exists to do "engine/plugin response work" to things that happened in <see cref="Update"/>.
	/// <see cref="PostUpdate"/> abstracts out "implementation details" from users defining systems in <see cref="Update"/>.
	///
	/// See the <see cref="Main"/> schedule for some details about how schedules are run.
	/// </summary>
	public const string PostUpdate = "PostUpdate";

	/// Runs last in the schedule.
	///
	/// See the <see cref="Main"/> schedule for some details about how schedules are run.
	public const string Last = "Last";
}

public class MainScheduleOrder
{
	public MainScheduleOrder()
	{
		Labels = new List<string>([
			Schedules.First, Schedules.PreUpdate, Schedules.RunFixedMainLoop, Schedules.Update, Schedules.SpawnScene, Schedules.PostUpdate, Schedules.Last
		]);
		StartupLabels = new List<string>([Schedules.PreStartup, Schedules.Startup, Schedules.PostStartup]);
	}
	public MainScheduleOrder(List<string> labels, List<string> startupLabels)
	{
		Labels = labels;
		StartupLabels = startupLabels;
	}
	public List<string> Labels;
	public List<string> StartupLabels;

	public void InsertStartupBefore(string before, string schedule)
	{
		var idx = Labels.IndexOf(schedule);
		if (idx == -1)
			throw new ArgumentException($"Schedule {schedule} not found in main schedule order");
		StartupLabels.Insert(idx, before);
	}

	public void InsertStartupAfter(string after, string schedule)
	{
		var idx = Labels.IndexOf(schedule);
		if (idx == -1)
			throw new ArgumentException($"Schedule {schedule} not found in main schedule order");
		StartupLabels.Insert(idx + 1, after);
	}

	public void InsertBefore(string before, string schedule)
	{
		var idx = Labels.IndexOf(schedule);
		if (idx == -1)
			throw new ArgumentException($"Schedule {schedule} not found in main schedule order");
		Labels.Insert(idx, before);
	}

	public void InsertAfter(string after, string schedule)
	{
		var idx = Labels.IndexOf(schedule);
		if (idx == -1)
			throw new ArgumentException($"Schedule {schedule} not found in main schedule order");
		Labels.Insert(idx + 1, after);
	}
}

public class FixedMainScheduleOrder
{
	public FixedMainScheduleOrder()
	{
		Labels = new List<string>([Schedules.FixedFirst, Schedules.FixedPreUpdate, Schedules.FixedUpdate, Schedules.FixedPostUpdate, Schedules.FixedLast]);
	}

	public FixedMainScheduleOrder(List<string> labels)
	{
		Labels = labels;
	}
	public List<string> Labels;

	public void InsertBefore(string before, string schedule)
	{
		var idx = Labels.IndexOf(schedule);
		if (idx == -1)
			throw new ArgumentException($"Schedule {schedule} not found in main schedule order");
		Labels.Insert(idx, before);
	}

	public void InsertAfter(string after, string schedule)
	{
		var idx = Labels.IndexOf(schedule);
		if (idx == -1)
			throw new ArgumentException($"Schedule {schedule} not found in main schedule order");
		Labels.Insert(idx + 1, after);
	}
}

/// <summary>
/// Set enum for the systems that want to run inside <see cref="Schedules.RunFixedMainLoop"/>
/// but before or after the fixed update logic. Systems in this set
/// will run exactly once per frame, regardless of the number of fixed updates.
/// They will also run under a variable timestep.
/// </summary>
/// 
/// <remarks>
/// <para>
/// This is useful for handling things that need to run every frame, but
/// also need to be read by the fixed update logic. See the individual variants
/// for examples of what kind of systems should be placed in each.
/// </para>
///
/// <para>
/// Note that in contrast to most other Bevy schedules, systems added directly to
/// <see cref="Schedules.RunFixedMainLoop"/> will *not* be parallelized between each other.
/// </para>
/// </remarks>
public enum RunFixedMainLoopSystems
{
	/// <summary>
	/// Runs before the fixed update logic.
	/// </summary>
	///
	/// <remarks>
	/// A good example of a system that fits here
	/// is camera movement, which needs to be updated in a variable timestep,
	/// as you want the camera to move with as much precision and updates as
	/// the frame rate allows. A physics system that needs to read the camera
	/// position and orientation, however, should run in the fixed update logic,
	/// as it needs to be deterministic and run at a fixed rate for better stability.
	/// Note that we are not placing the camera movement system in `Update`, as that
	/// would mean that the physics system already ran at that point.
	/// </remarks>
	/// 
	BeforeFixedMainLoop,
	/// <summary>
	/// Contains the fixed update logic.
	/// Runs [`FixedMain`] zero or more times based on delta of
	/// [`Time<Virtual>`] and [`Time::overstep`].
	/// </summary>
	///
	/// <remarks>
	/// Don't place systems here, use <see cref="Schedules.FixedUpdate"/> and friends instead.
	/// Use this system instead to order your systems to run specifically in between the fixed update logic and all
	/// other systems that run in <see cref="BeforeFixedMainLoop"/> or <see cref="AfterFixedMainLoop"/>
	/// </remarks>
	FixedMainLoop,
	/// <summary>
	///  Runs after the fixed update logic.
	/// </summary>
	///
	/// <remarks>
	/// A good example of a system that fits here
	/// is a system that interpolates the transform of an entity between the last and current fixed update.
	/// </remarks>
	AfterFixedMainLoop,
}