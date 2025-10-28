using Verse.ECS.Scheduling.Configs;
using Verse.ECS.Scheduling.Graph;

namespace Verse.ECS.Scheduling;

/// <summary>
///     Resource that stores <see cref="Schedule" />s mapped to <see cref="ScheduleLabel" />s excluding the current running
///     <see cref="Schedule" />
/// </summary>
/// <remarks>Based on bevy_ecs::schedule:Schedules</remarks>
public class ScheduleContainer : IFromWorld<ScheduleContainer>
{
	public HashSet<ComponentId> IgnoredSchedulingAmbiguities = new ();
	protected Dictionary<string, Schedule> Schedules = new ();

	/// <summary>
	///     Inserts a labeled schedule into the map. Replaces existing schedule if label already exists.
	/// </summary>
	/// <param name="schedule">
	///     If the map already had an entry for the label, this is the old schedule that was replaced. Else
	///     its null.
	/// </param>
	/// <returns></returns>
	public Schedule? Insert(Schedule schedule)
	{
		if (Schedules.ContainsKey(schedule.Name)) {
			var existing = Schedules[schedule.Name];
			Schedules[schedule.Name] = schedule;
			return existing;
		}
		Schedules[schedule.Name] = schedule;
		return null;
	}

	public Schedule? Remove(string scheduleName)
	{
		if (Schedules.ContainsKey(scheduleName)) {
			var schedule = Schedules[scheduleName];
			Schedules.Remove(scheduleName);
			return schedule;
		}
		return null;
	}

	public bool Contains(string scheduleName) => Schedules.ContainsKey(scheduleName);

	public Schedule? Get(string scheduleName)
	{
		Schedules.TryGetValue(scheduleName, out var schedule);
		return schedule;
	}

	public IEnumerable<Schedule> GetAll() => Schedules.Values;

	public void SetBuildSettings(ScheduleBuildSettings settings)
	{
		foreach (var schedule in Schedules.Values) {
			schedule.SetBuildSettings(settings);
		}
	}

	public void AllowAmbiguousComponent(ComponentId id)
	{
		IgnoredSchedulingAmbiguities.Add(id);
	}
	
	public void IgnoreAmbiguity(string schedule, IIntoSystemSet a, IIntoSystemSet b)
	{
		if (!Schedules.TryGetValue(schedule, out var sched)) {
			throw new ArgumentException($"Schedule {schedule} not found when trying to add systems");
		}
		sched.IgnoreAmbiguity(a.IntoSystemSet(), b.IntoSystemSet());
	}

	public void AddSystems(string schedule, IIntoSystemConfigs systems)
	{
		if (!Schedules.TryGetValue(schedule, out var sched)) {
			throw new ArgumentException($"Schedule {schedule} not found when trying to add systems");
		}
		sched.AddSystems(systems.IntoConfigs());
	}

	public void ConfigureSets(string schedule, IIntoSystemSetConfigs configs)
	{
		if (!Schedules.TryGetValue(schedule, out var sched)) {
			throw new ArgumentException($"Schedule {schedule} not found when trying to configure sets");
		}
		sched.ConfigureSets(configs.IntoConfigs());
	}
	public static ScheduleContainer FromWorld(World world) => new ();
}