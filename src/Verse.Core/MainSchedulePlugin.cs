using Verse.ECS;
using Verse.ECS.Scheduling;
using Verse.ECS.Scheduling.Configs;
using Verse.ECS.Systems;

namespace Verse.Core;

public class MainSchedulePlugin(ExecutorKind executorKind) : IPlugin, IStaticPlugin
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
			InitRes(mainScheduleOrder).
			InitRes(fixedMainScheduleOrder);

		InitSchedules(app, mainScheduleOrder.StartupLabels);
		InitSchedules(app, mainScheduleOrder.Labels);
		InitSchedules(app, fixedMainScheduleOrder.Labels);

		app.
			AddSystems(Schedules.Main, FuncSystem.Of(mainSystem, "RunMainSystems")).
			AddSystems(Schedules.FixedMain, FuncSystem.Of(fixedMainSystem, "RunFixedMainSystems")).
			ConfigureSets(Schedules.RunFixedMainLoop,
				// TODO this sucks. figure out a better ergonomic
				NodeConfigs<ISystemSet>.Of([
					EnumSet<RunFixedMainLoopSystems>.Of(RunFixedMainLoopSystems.BeforeFixedMainLoop),
					EnumSet<RunFixedMainLoopSystems>.Of(RunFixedMainLoopSystems.FixedMainLoop),
					EnumSet<RunFixedMainLoopSystems>.Of(RunFixedMainLoopSystems.AfterFixedMainLoop),
				], chained: Chain.Yes));

	}

	protected void InitSchedules(App app, IEnumerable<string> schedules)
	{
		foreach (var scheduleLabel in schedules) {
			var schedule = new Schedule(scheduleLabel, executorKind);
			app.AddSchedule(schedule);
		}
	}

	private static void RunMainSystems(World world, Local<bool> ranAtLeastOnce)
	{
		var schedules = world.MustGetRes<MainScheduleOrder>();
		if (!ranAtLeastOnce.Value) {
			foreach (var schedule in schedules.Value!.StartupLabels) {
				world.RunSchedule(schedule);
			}
			ranAtLeastOnce.Value = true;
		}
		foreach (var schedule in schedules.Value!.Labels) {
			world.RunSchedule(schedule);
		}
	}

	private static void RunFixedMainSystems(World world)
	{
		var schedules = world.MustGetRes<FixedMainScheduleOrder>();
		foreach (var schedule in schedules.Value!.Labels) {
			world.RunSchedule(schedule);
		}
	}

	public static IPlugin CreatePlugin(App app) => new MainSchedulePlugin(ExecutorKind.SingleThreaded);
}