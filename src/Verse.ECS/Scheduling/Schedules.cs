using Verse.ECS.Scheduling.Graph;

namespace Verse.ECS.Scheduling;

/// <summary>
///     Resource that stores <see cref="Schedule" />s mapped to <see cref="ScheduleLabel" />s excluding the current running
///     <see cref="Schedule" />
/// </summary>
/// <remarks>Based on bevy_ecs::schedule:Schedules</remarks>
public class ScheduleContainer
{
	public HashSet<ulong> IgnoredSchedulingAmbiguities = new ();
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

	public void AllowAmbiguousComponent(ulong componentId)
	{
		IgnoredSchedulingAmbiguities.Add(componentId);
	}

	public void AllowAmbiguousComponent<T>(World world) where T : struct
	{
		var c = world.Registry.GetSlimComponent<T>();
		AllowAmbiguousComponent(c.Id);
	}
}