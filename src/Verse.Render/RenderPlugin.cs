using System.Threading.Channels;
using Verse.Assets;
using Verse.Core;
using Verse.ECS;
using Verse.ECS.Scheduling;
using Verse.ECS.Scheduling.Configs;
using Verse.ECS.Scheduling.Graph;
using Verse.ECS.Systems;
using Verse.MoonWorks;
using Verse.MoonWorks.Graphics;
using Verse.Render.Graph;

namespace Verse.Render;

public partial class RenderPlugin : IPlugin
{
	public void Build(App app)
	{
		// Add the render app as a sub-app
		var renderApp = new RenderApp();
		var extractSchedule = new Schedule(RenderSchedules.Extract);
		// We skip applying any commands during the ExtractSchedule so commands can be applied on the render thread
		extractSchedule.SetBuildSettings(new ScheduleBuildSettings(autoInsertApplyDeferred: false));
		extractSchedule.SetApplyFinalDeferred(false);

		var startupSchedule = new Schedule(RenderSchedules.Startup);


		renderApp.
			AddSchedule(startupSchedule).
			AddSchedule(extractSchedule).
			AddSchedule(BaseRenderSchedule()).
			InitRes(new RenderGraph()). // 
			InitRes(app.World.GetResMut<AssetServer>().Value!).
			//.add_systems(ExtractSchedule, PipelineCache::extract_shaders)
			AddSystems(RenderSchedules.Render, FuncSystem.Of<World, ResMut<ScheduleContainer>>(ApplyExtractCommands).InSet(RenderSets.ExtractCommands)).
			AddSchedulable(new RenderGraphRunner()).
			AddSchedulable(new EntitySyncSystems());

		var runStartup = true;
		renderApp.SetExtract((World mainWorld, World renderWorld) => {

			if (runStartup) {
				// TODO improve this
				renderWorld.SetRes(mainWorld.GetRes<GraphicsDevice>().Value);
				renderWorld.RunSchedule(RenderSchedules.Startup);
				runStartup = false;
			}


			renderWorld.SetRes(new MainWorld(mainWorld));
			renderWorld.RunSchedule(RenderSchedules.Extract);
			renderWorld.RemoveRes<MainWorld>();
		});


		var timeChannel = Channel.CreateBounded<DateTime>(2);
		renderApp.InitRes(timeChannel.Writer);
		app.InitRes(timeChannel.Reader);
		app.InsertSubApp(renderApp);
	}

	public static void ApplyExtractCommands(World world, ResMut<ScheduleContainer> schedules)
	{
		schedules.Value.Get(RenderSchedules.Extract)!.ApplyDeferred(world);
	}

	public static Schedule BaseRenderSchedule()
	{
		var schedule = new Schedule(RenderSchedules.Render);
		schedule.ConfigureSets(SystemSet.Of(
			RenderSets.ExtractCommands,
			RenderSets.PrepareMeshes,
			RenderSets.ManageViews,
			RenderSets.Queue,
			RenderSets.PhaseSort,
			RenderSets.Prepare,
			RenderSets.Render,
			RenderSets.Cleanup,
			RenderSets.PostCleanup).Chained());

		schedule.ConfigureSets(SystemSet.Of(
			RenderSets.ExtractCommands,
			RenderSets.PrepareAssets,
			RenderSets.PrepareMeshes,
			RenderSets.Prepare
		).Chained());

		schedule.ConfigureSets(SystemSet.Of(
			RenderSets.QueueMeshes,
			RenderSets.QueueSweep
		).Chained().InSet(RenderSets.Queue));
		// TODO We don't have this yet but bevy does it.
		//.After(PrepareAssets<RenderMesh>))

		schedule.ConfigureSets(SystemSet.Of(
			RenderSets.PrepareResources,
			RenderSets.PrepareResourcesCollectPhaseBuffers,
			RenderSets.PrepareResourcesFlush,
			RenderSets.PrepareBindGroups
		).Chained().InSet(RenderSets.Prepare));

		return schedule;
	}
}