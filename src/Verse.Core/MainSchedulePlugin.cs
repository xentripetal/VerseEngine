using Verse.ECS;
using Verse.ECS.Scheduling;
using Verse.ECS.Scheduling.Configs;
using Verse.ECS.Systems;

namespace Verse.Core;

public struct MainSchedulePlugin(ExecutorKind executorKind) : IPlugin
{
	public void Build(App app)
	{
		// simple facilitator schedules benefit from being single threaded
		var mainSchedule = new Schedule(Schedules.Main, ExecutorKind.SingleThreaded);
		var fixedMainSchedule = new Schedule(Schedules.FixedMain, ExecutorKind.SingleThreaded);
		var fixedMainLoopSchedule = new Schedule(Schedules.RunFixedMainLoop, ExecutorKind.SingleThreaded);

		bool ranAtleastOnce = false;
		var mainScheduleOrder = new MainScheduleOrder();
		var fixedMainScheduleOrder = new FixedMainScheduleOrder();
		var mainSystem = RunMainSystems;
		var fixedMainSystem = RunFixedMainSystems;

		app.AddSchedule(mainSchedule).
			AddSchedule(fixedMainSchedule).
			AddSchedule(fixedMainLoopSchedule).
			InsertResource(mainScheduleOrder).
			InsertResource(fixedMainScheduleOrder);

		InitSchedules(app, mainScheduleOrder.StartupLabels);
		InitSchedules(app, mainScheduleOrder.Labels);
		InitSchedules(app, fixedMainScheduleOrder.Labels);

		app.
			AddSystems(Schedules.Main, FuncSystem.Of(mainSystem, "RunMainSystems")).
			AddSystems(Schedules.FixedMain, FuncSystem.Of(fixedMainSystem, "RunFixedMainSystems")).
			ConfigureSets(Schedules.RunFixedMainLoop,
				SystemSet.Of(
						RunFixedMainLoopSystems.BeforeFixedMainLoop,
						RunFixedMainLoopSystems.FixedMainLoop,
						RunFixedMainLoopSystems.AfterFixedMainLoop).
					Chained());

	}

	void InitSchedules(App app, IEnumerable<string> schedules)
	{
		foreach (var scheduleLabel in schedules) {
			var schedule = new Schedule(scheduleLabel, executorKind);
			app.AddSchedule(schedule);
		}
	}

	private static void RunMainSystems(World world, Local<bool> ranAtLeastOnce)
	{
		var schedules = world.Resource<MainScheduleOrder>();
		if (!ranAtLeastOnce.Value) {
			foreach (var schedule in schedules.StartupLabels) {
				world.RunSchedule(schedule);
			}
			ranAtLeastOnce.Value = true;
		}
		foreach (var schedule in schedules.Labels) {
			world.RunSchedule(schedule);
		}
	}

	private static void RunFixedMainSystems(World world)
	{
		var schedules = world.Resource<FixedMainScheduleOrder>();
		foreach (var schedule in schedules.Labels) {
			world.RunSchedule(schedule);
		}
	}

	public static IPlugin CreatePlugin(App app) => new MainSchedulePlugin(ExecutorKind.SingleThreaded);
}